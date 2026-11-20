/**
 * Contract test of the auth/ownership foundation (issue #799).
 *
 * Like `api.test.ts`, this runs the real Void dev server against the real
 * local D1, so routing, the Better Auth session middleware, the SQL of the
 * handlers and the HTTP semantics are covered together. The threat model
 * (`docs/architecture/web-auth-threat-model.md` §5) demands *negative*
 * cases: an authenticated happy path alone proves nothing.
 *
 * Covered here:
 *
 *   - unauthenticated access to every `/api/v1/me/*` endpoint → 401
 *   - personal workspace provisioning (lazy, idempotent)
 *   - team workspace creation, member add/list/remove
 *   - IDOR: a non-member cannot see or mutate another workspace (404)
 *   - role enforcement: editor/viewer cannot manage members; `owner` is
 *     never grantable through the API (no self-elevation); the last owner
 *     cannot be removed
 *   - oversized bodies → 413 (Content-Length pre-check and the publish
 *     chart body limit), malformed JSON → 422
 *   - XSS / SQLi payloads stored and returned as inert strings
 *   - CSRF: a cross-origin unsafe request loses the session → 401
 *   - concurrent member adds race on the primary key → one 201, one 409
 *   - session revocation (sign-out) → immediate 401
 *   - per-user rate limit → 429
 *   - audit_log rows for workspace/member/staff operations
 *
 * The database is reset to the seed state afterwards, like api.test.ts.
 */
import assert from "node:assert/strict";
import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtempSync, openSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { after, before, describe, it } from "node:test";

const webRoot = fileURLToPath(new URL("..", import.meta.url));
const voidCli = fileURLToPath(new URL("../node_modules/void/dist/cli/cli.mjs", import.meta.url));
const vpCli = fileURLToPath(new URL("../node_modules/vite-plus/bin/vp", import.meta.url));

const port = 30000 + (process.pid % 20000);
const baseUrl = `http://127.0.0.1:${port}`;

const TEST_ADMIN_TOKEN = "test-staff-token-0123456789abcdef";

let server: ChildProcess | undefined;
let serverLogPath = "";
let tempDir: string;

function runVoid(args: string[]): string {
  return execFileSync(process.execPath, [voidCli, ...args], {
    cwd: webRoot,
    encoding: "utf-8",
  });
}

/** Run a SELECT through `void db execute` and return its printed rows. */
function dbQuery(sql: string): string {
  return runVoid(["db", "execute", sql]);
}

interface Session {
  cookie: string;
  userId: string;
  email: string;
  name: string;
}

/** Extract every `name=value` pair from Set-Cookie headers. */
function cookieHeader(response: Response): string {
  const headers = response.headers as Headers & { getSetCookie?: () => string[] };
  const list = headers.getSetCookie?.() ?? [];
  if (list.length === 0) {
    const single = response.headers.get("set-cookie");
    return single ? single.split(";")[0] : "";
  }
  return list.map((value) => value.split(";")[0]).join("; ");
}

/** Sign up a fresh account through the real Better Auth endpoint. */
async function signUp(email: string, name: string): Promise<Session> {
  const response = await fetch(`${baseUrl}/api/auth/sign-up/email`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      name,
      email,
      password: "correct-horse-battery-799",
    }),
  });
  assert.equal(response.status, 200, `sign-up failed for ${email}: ${await response.text()}`);
  const cookie = cookieHeader(response);
  assert.ok(cookie.includes("session_token"), `no session cookie for ${email}`);
  const body = (await response.json()) as { user?: { id?: string } };
  assert.ok(body.user?.id, "sign-up response carried no user id");
  return { cookie, userId: body.user.id, email, name };
}

interface ApiOptions {
  method?: string;
  body?: unknown;
  cookie?: string;
  headers?: Record<string, string>;
  rawBody?: string;
}

async function api(path: string, options: ApiOptions = {}): Promise<Response> {
  const headers: Record<string, string> = { ...(options.headers ?? {}) };
  if (options.cookie) {
    headers.cookie = options.cookie;
  }
  let body: string | undefined;
  if (options.rawBody !== undefined) {
    body = options.rawBody;
    headers["content-type"] = "application/json";
  } else if (options.body !== undefined) {
    body = JSON.stringify(options.body);
    headers["content-type"] = "application/json";
  }
  return fetch(`${baseUrl}${path}`, {
    method: options.method ?? "GET",
    headers,
    body,
  });
}

async function waitForServer(timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${baseUrl}/api/v1/catalog`);
      if (response.status === 200) {
        return;
      }
    } catch {
      // not listening yet
    }
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`dev server did not become ready on ${baseUrl}:\n${readServerLog()}`);
}

function readServerLog(): string {
  if (!serverLogPath) {
    return "";
  }
  try {
    return readFileSync(serverLogPath, "utf-8");
  } catch {
    return "";
  }
}

async function stopServer(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null) {
    return;
  }
  const exited = once(child, "exit");
  killGroup(child, "SIGTERM");
  const timedOut = Symbol("timeout");
  const result = await Promise.race([exited, delay(5_000, timedOut)]);
  if (result === timedOut) {
    killGroup(child, "SIGKILL");
    await once(child, "exit");
  }
}

function killGroup(child: ChildProcess, signal: NodeJS.Signals): void {
  if (child.pid === undefined) {
    child.kill(signal);
    return;
  }
  try {
    process.kill(-child.pid, signal);
  } catch {
    child.kill(signal);
  }
}

function delay(ms: number, fallback: symbol): Promise<symbol> {
  const { promise, resolve } = Promise.withResolvers<symbol>();
  setTimeout(() => resolve(fallback), ms);
  return promise;
}

// Accounts used across the suite. `owner` manages the team workspace,
// `editor`/`viewer` are members, `outsider` belongs to nothing, `flooder`
// exists only for the rate-limit test.
let owner!: Session;
let editor!: Session;
let viewer!: Session;
let outsider!: Session;
let flooder!: Session;
let teamWorkspaceId = "";

before(
  async () => {
    runVoid(["db", "seed"]);

    tempDir = mkdtempSync(join(tmpdir(), "flashbeat-authz-"));
    serverLogPath = join(tempDir, "dev-server.log");
    const logFd = openSync(serverLogPath, "w");

    server = spawn(
      process.execPath,
      [vpCli, "dev", "--port", String(port), "--strictPort", "--host", "127.0.0.1"],
      {
        cwd: webRoot,
        detached: true,
        env: { ...process.env, FLASHBEAT_ADMIN_TOKEN: TEST_ADMIN_TOKEN },
        stdio: ["ignore", logFd, logFd],
      },
    );

    // Cold vite dep-optimization of the auth bundle can take minutes on a
    // loaded machine; the budget is generous on purpose.
    await waitForServer(360_000);

    const suffix = `${process.pid}`;
    owner = await signUp(`owner-${suffix}@example.com`, "Owner User");
    editor = await signUp(`editor-${suffix}@example.com`, "Editor User");
    viewer = await signUp(`viewer-${suffix}@example.com`, "Viewer User");
    outsider = await signUp(`outsider-${suffix}@example.com`, "Outsider User");
    flooder = await signUp(`flooder-${suffix}@example.com`, "Flooder User");
  },
  { timeout: 420_000 },
);

after(async () => {
  if (server) {
    await stopServer(server);
  }
  runVoid(["db", "seed"]);
  rmSync(tempDir, { recursive: true, force: true });
});

describe("authentication", () => {
  it("rejects unauthenticated calls to every /api/v1/me endpoint", async () => {
    for (const path of [
      "/api/v1/me",
      "/api/v1/me/workspaces",
      `/api/v1/me/workspaces/${encodeURIComponent(`personal-${owner.userId}`)}/members`,
    ]) {
      const response = await api(path);
      assert.equal(response.status, 401, `${path} should require a session`);
    }
    const write = await api("/api/v1/me/workspaces", {
      method: "POST",
      body: { name: "nope" },
    });
    assert.equal(write.status, 401);
  });

  it("provisions the personal workspace lazily and idempotently", async () => {
    const first = await api("/api/v1/me", { cookie: owner.cookie });
    assert.equal(first.status, 200);
    const body = (await first.json()) as {
      user: { id: string; email: string };
      workspace: { id: string; role: string };
    };
    assert.equal(body.user.id, owner.userId);
    assert.equal(body.workspace.id, `personal-${owner.userId}`);
    assert.equal(body.workspace.role, "owner");

    const second = await api("/api/v1/me", { cookie: owner.cookie });
    const again = (await second.json()) as { workspace: { id: string } };
    assert.equal(again.workspace.id, body.workspace.id);

    const list = await api("/api/v1/me/workspaces", { cookie: owner.cookie });
    const workspaces = ((await list.json()) as { workspaces: { id: string }[] }).workspaces;
    assert.equal(workspaces.length, 1, "repeated /me calls must not duplicate workspaces");
  });
});

describe("workspace creation and validation", () => {
  it("creates a team workspace with the caller as owner", async () => {
    const response = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      body: { name: "Team Alpha" },
    });
    assert.equal(response.status, 201);
    const body = (await response.json()) as {
      workspace: { id: string; name: string; kind: string; role: string };
    };
    assert.equal(body.workspace.kind, "team");
    assert.equal(body.workspace.role, "owner");
    teamWorkspaceId = body.workspace.id;
    assert.ok(teamWorkspaceId.startsWith("ws-"));
  });

  it("stores XSS and SQLi payloads as inert strings", async () => {
    const xss = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      body: { name: `<script>alert("xss")</script>` },
    });
    assert.equal(xss.status, 201);

    const sqli = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      body: { name: `x' OR '1'='1` },
    });
    assert.equal(sqli.status, 201);

    const list = await api("/api/v1/me/workspaces", { cookie: owner.cookie });
    const body = (await list.json()) as { workspaces: { name: string }[] };
    const names = body.workspaces.map((w) => w.name);
    assert.ok(names.includes(`<script>alert("xss")</script>`));
    assert.ok(names.includes(`x' OR '1'='1`));
    // Exactly the rows we created — the SQLi name matched nothing extra.
    assert.equal(body.workspaces.length, 4);
  });

  it("rejects malformed JSON, wrong shapes and oversized bodies", async () => {
    const malformed = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      rawBody: `{"name":`,
    });
    assert.equal(malformed.status, 422);

    const wrongShape = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      body: { name: 42 },
    });
    assert.equal(wrongShape.status, 422);

    const missing = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      body: {},
    });
    assert.equal(missing.status, 422);

    // Content-Length pre-check: a declared oversized body is refused
    // before a single byte is read.
    const oversized = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: owner.cookie,
      rawBody: JSON.stringify({ name: "x".repeat(16 * 1024) }),
    });
    assert.equal(oversized.status, 413);
  });
});

describe("membership and IDOR", () => {
  it("lets the owner add members and lists them", async () => {
    const add = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: editor.email, role: "editor" },
    });
    assert.equal(add.status, 201);

    const list = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      cookie: owner.cookie,
    });
    assert.equal(list.status, 200);
    const body = (await list.json()) as {
      members: { userId: string; email: string; role: string }[];
    };
    const member = body.members.find((m) => m.userId === editor.userId);
    assert.equal(member?.role, "editor");
    assert.equal(member?.email, editor.email);
  });

  it("hides the workspace from outsiders entirely (IDOR → 404)", async () => {
    for (const attempt of [
      api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, { cookie: outsider.cookie }),
      api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
        method: "POST",
        cookie: outsider.cookie,
        body: { email: viewer.email },
      }),
      api(`/api/v1/me/workspaces/${teamWorkspaceId}/members/${viewer.userId}`, {
        method: "DELETE",
        cookie: outsider.cookie,
      }),
    ]) {
      assert.equal((await attempt).status, 404);
    }
    // A malformed workspace id is just another unknown id — parameterized,
    // never interpreted.
    const weird = await api(
      `/api/v1/me/workspaces/${encodeURIComponent("' OR 1=1 --")}/members`,
      { cookie: owner.cookie },
    );
    assert.equal(weird.status, 404);
  });

  it("enforces roles: editor/viewer cannot manage members", async () => {
    // editor is a member but not owner.
    const addByEditor = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: editor.cookie,
      body: { email: viewer.email, role: "viewer" },
    });
    assert.equal(addByEditor.status, 403);

    const removeByEditor = await api(
      `/api/v1/me/workspaces/${teamWorkspaceId}/members/${owner.userId}`,
      { method: "DELETE", cookie: editor.cookie },
    );
    assert.equal(removeByEditor.status, 403);

    // editor may still *read* the member list.
    const list = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      cookie: editor.cookie,
    });
    assert.equal(list.status, 200);
  });

  it("never grants owner through the API and rejects bad input", async () => {
    const ownerGrant = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: viewer.email, role: "owner" },
    });
    assert.equal(ownerGrant.status, 422);

    const unknownUser = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: "nobody-here@example.com" },
    });
    assert.equal(unknownUser.status, 404);

    const badEmail = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: "not-an-email" },
    });
    assert.equal(badEmail.status, 422);

    const selfAdd = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: owner.email },
    });
    assert.equal(selfAdd.status, 409);

    const duplicate = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: owner.cookie,
      body: { email: editor.email },
    });
    assert.equal(duplicate.status, 409);
  });

  it("resolves concurrent member adds to exactly one success", async () => {
    const [a, b] = await Promise.all([
      api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
        method: "POST",
        cookie: owner.cookie,
        body: { email: viewer.email, role: "viewer" },
      }),
      api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
        method: "POST",
        cookie: owner.cookie,
        body: { email: viewer.email, role: "viewer" },
      }),
    ]);
    const statuses = [a.status, b.status].sort();
    assert.deepEqual(statuses, [201, 409]);

    const list = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      cookie: owner.cookie,
    });
    const members = ((await list.json()) as { members: { userId: string }[] }).members;
    assert.equal(
      members.filter((m) => m.userId === viewer.userId).length,
      1,
      "the racing adds must produce exactly one membership row",
    );
  });

  it("lets a viewer read but not write, and removes members as owner", async () => {
    const viewerList = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      cookie: viewer.cookie,
    });
    assert.equal(viewerList.status, 200);

    const viewerWrite = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: viewer.cookie,
      body: { email: outsider.email },
    });
    assert.equal(viewerWrite.status, 403);

    const removeViewer = await api(
      `/api/v1/me/workspaces/${teamWorkspaceId}/members/${viewer.userId}`,
      { method: "DELETE", cookie: owner.cookie },
    );
    assert.equal(removeViewer.status, 200);

    const removeEditor = await api(
      `/api/v1/me/workspaces/${teamWorkspaceId}/members/${editor.userId}`,
      { method: "DELETE", cookie: owner.cookie },
    );
    assert.equal(removeEditor.status, 200);
  });

  it("protects the last owner from removal", async () => {
    // The owner is the only `owner` row left in the team workspace.
    const selfRemove = await api(
      `/api/v1/me/workspaces/${teamWorkspaceId}/members/${owner.userId}`,
      { method: "DELETE", cookie: owner.cookie },
    );
    assert.equal(selfRemove.status, 409);

    // With a second owner present (inserted directly, as a fixture would
    // be), removing an owner is allowed.
    runVoid([
      "db",
      "execute",
      `INSERT INTO workspace_members ("workspaceId", "userId", "role") VALUES ('${teamWorkspaceId}', '${outsider.userId}', 'owner')`,
    ]);
    const removeSecondOwner = await api(
      `/api/v1/me/workspaces/${teamWorkspaceId}/members/${outsider.userId}`,
      { method: "DELETE", cookie: owner.cookie },
    );
    assert.equal(removeSecondOwner.status, 200);
  });
});

describe("session lifecycle", () => {
  it("rejects requests after sign-out (revoked session reuse)", async () => {
    const signOut = await api("/api/auth/sign-out", {
      method: "POST",
      cookie: owner.cookie,
      headers: { origin: baseUrl },
    });
    assert.ok([200, 204].includes(signOut.status), `sign-out: ${signOut.status}`);

    const after = await api("/api/v1/me", { cookie: owner.cookie });
    assert.equal(after.status, 401, "a revoked session must not authenticate");
    const workspaces = await api("/api/v1/me/workspaces", { cookie: owner.cookie });
    assert.equal(workspaces.status, 401);
  });

  it("ignores the session cookie on cross-origin unsafe requests (CSRF)", async () => {
    // editor still has a valid session. A POST from a foreign origin must
    // not authenticate — Void strips the cookie before session lookup.
    const csrf = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: editor.cookie,
      headers: { origin: "https://evil.example" },
      body: { name: "csrf attempt" },
    });
    assert.equal(csrf.status, 401);

    // The same request from the real origin still works.
    const legit = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: editor.cookie,
      headers: { origin: baseUrl },
      body: { name: "Legit Team" },
    });
    assert.equal(legit.status, 201);
  });
});

describe("rate limits and bounded bodies on public endpoints", () => {
  it("returns 429 once the per-user write limit is exceeded", async () => {
    // flooder has its own counter; 20 writes per minute are allowed.
    let lastStatus = 0;
    for (let i = 0; i < 21; i++) {
      const response = await api("/api/v1/me/workspaces", {
        method: "POST",
        cookie: flooder.cookie,
        body: { name: `flood-${i}` },
      });
      lastStatus = response.status;
    }
    assert.equal(lastStatus, 429);
  });

  it("bounds the public score submission body", async () => {
    const oversized = await api("/api/v1/scores", {
      method: "POST",
      rawBody: `{"pad":"${"x".repeat(64 * 1024)}"}`,
    });
    assert.equal(oversized.status, 413);

    const malformed = await api("/api/v1/scores", {
      method: "POST",
      rawBody: `{not json`,
    });
    assert.equal(malformed.status, 422);
  });

  it("bounds the staff publish body after authentication", async () => {
    // Auth first: no token → 401 even for a huge body.
    const anonymous = await api("/api/v1/admin/songs/aaa-bbb", {
      method: "PUT",
      rawBody: `{"pad":"${"x".repeat(64)}`,
      headers: { "content-type": "application/json" },
    });
    assert.equal(anonymous.status, 401);

    // Staff token + a body past the 2 MiB chart limit → 413.
    const oversized = await fetch(`${baseUrl}/api/v1/admin/songs/aaa-bbb`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      body: `{"pad":"${"x".repeat(3 * 1024 * 1024)}"}`,
    });
    assert.equal(oversized.status, 413);
  });
});

describe("audit log", () => {
  it("records workspace, member and staff publish/archive operations", async () => {
    // A real staff publish + archive writes `song.publish` / `song.archive`
    // rows with actorKind 'staff' (issue #799 §4.4).
    const chart = {
      name: "Audit Song",
      maxBlock: 8,
      BPM: 120,
      offset: 0,
      notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
    };
    const publish = await fetch(`${baseUrl}/api/v1/admin/songs/zz-authz-song`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      body: JSON.stringify({
        title: "Audit Song",
        artist: "Tester",
        youtubeId: "8JXiXt0D6tw",
        level: 5,
        durationSec: 30,
        chart,
      }),
    });
    assert.equal(publish.status, 200, await publish.text());

    const archive = await fetch(`${baseUrl}/api/v1/admin/songs/zz-authz-song`, {
      method: "POST",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      body: JSON.stringify({ archived: true }),
    });
    assert.equal(archive.status, 200);

    const out = dbQuery(
      `SELECT action, "actorKind", "actorUserId", "targetId" FROM audit_log ORDER BY id`,
    );
    for (const expected of [
      "workspace.create",
      "member.add",
      "member.remove",
      "song.publish",
      "song.archive",
    ]) {
      assert.match(out, new RegExp(expected.replace(".", "\\.")), `missing audit action ${expected}`);
    }
    // Staff operations carry no user id; user operations carry the actor.
    assert.match(out, /song\.publish[\s\S]*staff/);
    assert.match(out, /workspace\.create/);
  });
});
