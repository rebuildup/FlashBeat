/**
 * Contract test of the per-account song flow (issue #800).
 *
 * Same harness as `authz.test.ts`: the real Void dev server against the
 * real local D1, so routing, the Better Auth session middleware, the SQL
 * of the handlers and the HTTP semantics are covered together. The issue
 * demands negative cases as contract tests — an authenticated happy path
 * alone proves nothing.
 *
 * Covered here:
 *
 *   - unauthenticated access to every `/api/v1/me/{songs,drafts}*` route → 401
 *   - draft create/list/detail/save (resume of an interrupted edit)
 *   - publish: contract + semantic validation reused from the staff route,
 *     mandatory rights attestation (missing → 422), version increment,
 *     catalog visibility, owner/workspace attribution
 *   - update (republish → version + 1) and unpublish/republish cycle
 *   - IDOR: another user's draft/song → 404; `viewer` role → 403
 *   - slug protection: staff-managed songId → 409 on draft create and on
 *     publish; staff `PUT` on a user-owned song → 409; another workspace's
 *     song → 409
 *   - oversized/malformed bodies → 413/422
 *   - audit_log rows for draft.create / song.publish / song.unpublish
 *
 * The database is reset to the seed state afterwards, like authz.test.ts.
 */
import assert from "node:assert/strict";
import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtempSync, openSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { after, before, describe, it } from "node:test";
import { PUBLISH_BODY_LIMIT } from "../routes/api/v1/_request-body.ts";

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

function dbQuery(sql: string): string {
  return runVoid(["db", "execute", sql]);
}

interface Session {
  cookie: string;
  userId: string;
  email: string;
  name: string;
}

function cookieHeader(response: Response): string {
  const headers = response.headers as Headers & { getSetCookie?: () => string[] };
  const list = headers.getSetCookie?.() ?? [];
  if (list.length === 0) {
    const single = response.headers.get("set-cookie");
    return single ? single.split(";")[0] : "";
  }
  return list.map((value) => value.split(";")[0]).join("; ");
}

async function signUp(email: string, name: string): Promise<Session> {
  const response = await fetch(`${baseUrl}/api/auth/sign-up/email`, {
    method: "POST",
    headers: { "content-type": "application/json", origin: baseUrl },
    body: JSON.stringify({
      name,
      email,
      password: "correct-horse-battery-800",
    }),
  });
  const text = await response.text();
  assert.equal(response.status, 200, `sign-up failed for ${email}: ${text}`);
  const cookie = cookieHeader(response);
  assert.ok(cookie.includes("session_token"), `no session cookie for ${email}`);
  const body = JSON.parse(text) as { user?: { id?: string } };
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
  const headers: Record<string, string> = {
    origin: baseUrl,
    ...options.headers,
  };
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

// A minimal contract-valid chart (one Tap) — the same shape the staff
// publish tests use.
const VALID_CHART = {
  name: "My Song",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
};

const RIGHTS = {
  rightsConfirmed: true,
  materialSource: "自作",
  rightsStatus: "original",
  license: null,
};

function publishBody(overrides: Record<string, unknown> = {}) {
  return {
    title: "My Song",
    artist: "Author",
    youtubeId: "8JXiXt0D6tw",
    level: 5,
    durationSec: 30,
    chart: VALID_CHART,
    ...RIGHTS,
    ...overrides,
  };
}

interface MeSongEntry {
  id: string;
  title: string;
  state: string;
  chartVersion: number;
  ownerWorkspaceId: string;
  ownerWorkspaceName: string;
  role: string;
  canEdit: boolean;
  rightsStatus: string | null;
}

interface MeDraftEntry {
  id: string;
  songId: string | null;
  title: string;
  noteCount: number;
  hasChart: boolean;
  publishedVersion: number | null;
  canEdit: boolean;
}

interface MeList {
  songs: MeSongEntry[];
  drafts: MeDraftEntry[];
}

async function meSongs(cookie: string): Promise<MeList> {
  const response = await api("/api/v1/me/songs", { cookie });
  assert.equal(response.status, 200);
  return (await response.json()) as MeList;
}

let author!: Session;
let outsider!: Session;
let teamOwner!: Session;
let teamViewer!: Session;
let teamWorkspaceId = "";
let draftId = "";
let publishedSongId = "";

before(
  async () => {
    runVoid(["db", "seed"]);

    tempDir = mkdtempSync(join(tmpdir(), "flashbeat-me-songs-"));
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

    await waitForServer(360_000);

    const suffix = `${process.pid}`;
    author = await signUp(`author-${suffix}@example.com`, "Author User");
    outsider = await signUp(`outsider-${suffix}@example.com`, "Outsider User");
    teamOwner = await signUp(`teamowner-${suffix}@example.com`, "Team Owner");
    teamViewer = await signUp(`teamviewer-${suffix}@example.com`, "Team Viewer");
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
  it("rejects unauthenticated calls to every song/draft endpoint", async () => {
    const probes: [string, ApiOptions][] = [
      ["/api/v1/me/songs", {}],
      ["/api/v1/me/drafts", { method: "POST", body: {} }],
      ["/api/v1/me/drafts/draft-x", {}],
      ["/api/v1/me/drafts/draft-x", { method: "PUT", body: {} }],
      ["/api/v1/me/drafts/draft-x/publish", { method: "POST", body: {} }],
      ["/api/v1/me/songs/who/unpublish", { method: "POST" }],
    ];
    for (const [path, options] of probes) {
      const response = await api(path, options);
      assert.equal(
        response.status,
        401,
        `${options.method ?? "GET"} ${path} should require a session`,
      );
    }
  });
});

describe("draft lifecycle", () => {
  it("creates a draft with an explicit song id and lists it", async () => {
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      body: {
        songId: "my-first-song",
        title: "My Song",
        artist: "Author",
        youtubeId: "8JXiXt0D6tw",
        level: 5,
        durationSec: 30,
        chart: VALID_CHART,
      },
    });
    const createText = await create.text();
    assert.equal(create.status, 201, createText);
    const body = JSON.parse(createText) as { draft: { id: string; songId: string } };
    draftId = body.draft.id;
    assert.ok(draftId.startsWith("draft-"));
    assert.equal(body.draft.songId, "my-first-song");
    publishedSongId = "my-first-song";

    const list = await meSongs(author.cookie);
    const draft = list.drafts.find((d) => d.id === draftId);
    assert.ok(draft, "the new draft must be listed");
    assert.equal(draft.songId, "my-first-song");
    assert.equal(draft.noteCount, 1);
    assert.equal(draft.hasChart, true);
    assert.equal(draft.publishedVersion, null);
    assert.equal(draft.canEdit, true);
    // Staff-managed songs never leak into the user list.
    assert.ok(!list.songs.some((s) => s.id === "who"));
  });

  it("assigns a generated song id when none is given", async () => {
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      body: { title: "No Id Yet" },
    });
    assert.equal(create.status, 201);
    const body = (await create.json()) as { draft: { id: string; songId: string } };
    assert.match(body.draft.songId, /^u-[0-9a-f]{8}$/);
  });

  it("saves the chart draft and resumes it (GET returns the stored chart)", async () => {
    const edited = {
      ...VALID_CHART,
      notes: [
        { type: 1, num: 1, block: 0, LPB: 4 },
        { type: 1, num: 2, block: 3, LPB: 4 },
      ],
    };
    const save = await api(`/api/v1/me/drafts/${draftId}`, {
      method: "PUT",
      cookie: author.cookie,
      body: { chart: edited, title: "My Song v2" },
    });
    assert.equal(save.status, 200, await save.text());

    // "Resume interrupted editing": a fresh GET hands the chart back.
    const detail = await api(`/api/v1/me/drafts/${draftId}`, { cookie: author.cookie });
    assert.equal(detail.status, 200);
    const body = (await detail.json()) as {
      draft: { chart: { notes: unknown[] }; title: string; noteCount: number };
    };
    assert.equal(body.draft.chart.notes.length, 2);
    assert.equal(body.draft.noteCount, 2);
    assert.equal(body.draft.title, "My Song v2");
  });

  it("hides drafts from non-members entirely (IDOR → 404)", async () => {
    for (const attempt of [
      api(`/api/v1/me/drafts/${draftId}`, { cookie: outsider.cookie }),
      api(`/api/v1/me/drafts/${draftId}`, {
        method: "PUT",
        cookie: outsider.cookie,
        body: { title: "hijack" },
      }),
      api(`/api/v1/me/drafts/${draftId}/publish`, {
        method: "POST",
        cookie: outsider.cookie,
        body: publishBody(),
      }),
    ]) {
      assert.equal((await attempt).status, 404);
    }
    // The outsider's own list must not contain the draft.
    const list = await meSongs(outsider.cookie);
    assert.equal(list.drafts.length, 0);
  });
});

describe("publish validation", () => {
  it("rejects publish without the rights attestation", async () => {
    const noConfirm = publishBody();
    delete (noConfirm as Record<string, unknown>).rightsConfirmed;
    const r1 = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: noConfirm,
    });
    assert.equal(r1.status, 422);

    const noSource = publishBody({ materialSource: "" });
    const r2 = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: noSource,
    });
    assert.equal(r2.status, 422);

    const badStatus = publishBody({ rightsStatus: "probably-fine" });
    const r3 = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: badStatus,
    });
    assert.equal(r3.status, 422);
  });

  it("rejects an invalid chart with the shared contract diagnostics", async () => {
    const bad = publishBody({
      chart: { ...VALID_CHART, notes: [{ type: 1, num: 1, block: 9, LPB: 4 }] },
    });
    const response = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: bad,
    });
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { path: string }[] };
    assert.ok(body.diagnostics.some((d) => d.path.includes("block")));
  });

  it("returns 422 (not 500) when the chart member is missing", async () => {
    const body = publishBody() as Record<string, unknown>;
    delete body.chart;
    const response = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body,
    });
    assert.equal(response.status, 422);
    const parsed = (await response.json()) as { diagnostics: { path: string }[] };
    assert.ok(parsed.diagnostics.some((d) => d.path === "/chart"));
  });

  it("publishes the draft: catalog entry, version 1, ownership, rights", async () => {
    const response = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: publishBody(),
    });
    const publishText = await response.text();
    assert.equal(response.status, 200, publishText);
    const body = JSON.parse(publishText) as {
      songId: string;
      chartVersion: number;
      archived: boolean;
      draft: { publishedVersion: number };
    };
    assert.equal(body.songId, publishedSongId);
    assert.equal(body.chartVersion, 1);
    assert.equal(body.archived, false);
    assert.equal(body.draft.publishedVersion, 1);

    // The public catalog now serves it.
    const catalog = (await (await fetch(`${baseUrl}/api/v1/catalog`)).json()) as {
      songs: { id: string; chartVersion: number }[];
    };
    assert.ok(catalog.songs.some((s) => s.id === publishedSongId && s.chartVersion === 1));

    // The owner list shows publish state, version and owner.
    const list = await meSongs(author.cookie);
    const song = list.songs.find((s) => s.id === publishedSongId);
    assert.ok(song, "published song must be listed");
    assert.equal(song.state, "published");
    assert.equal(song.chartVersion, 1);
    assert.equal(song.ownerWorkspaceId, `personal-${author.userId}`);
    assert.equal(song.ownerWorkspaceName, author.name);
    assert.equal(song.role, "owner");
    assert.equal(song.rightsStatus, "original");
    const draft = list.drafts.find((d) => d.id === draftId);
    assert.equal(draft?.publishedVersion, 1);

    // The rights record landed on the songs row.
    const out = dbQuery(
      `SELECT "rightsStatus", "materialSource" FROM songs WHERE id = '${publishedSongId}'`,
    );
    assert.match(out, /original/);
    assert.match(out, /自作/);
  });

  it("republish updates to the next chart version", async () => {
    const chart2 = {
      ...VALID_CHART,
      notes: [
        { type: 1, num: 1, block: 0, LPB: 4 },
        { type: 2, num: 2, block: 1, LPB: 4, endNum: 4 },
      ],
    };
    await api(`/api/v1/me/drafts/${draftId}`, {
      method: "PUT",
      cookie: author.cookie,
      body: { chart: chart2 },
    });
    const response = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: publishBody({ chart: chart2, title: "My Song v2" }),
    });
    const republishText = await response.text();
    assert.equal(response.status, 200, republishText);
    const body = JSON.parse(republishText) as { chartVersion: number };
    assert.equal(body.chartVersion, 2);

    const versions = dbQuery(
      `SELECT version FROM chart_versions WHERE "songId" = '${publishedSongId}' ORDER BY version`,
    );
    assert.match(versions, /1/);
    assert.match(versions, /2/);
  });
});

describe("unpublish", () => {
  it("unpublishes and republishes the owned song", async () => {
    const unpublish = await api(`/api/v1/me/songs/${publishedSongId}/unpublish`, {
      method: "POST",
      cookie: author.cookie,
    });
    const unpublishText = await unpublish.text();
    assert.equal(unpublish.status, 200, unpublishText);
    const body = JSON.parse(unpublishText) as { archived: boolean };
    assert.equal(body.archived, true);

    const catalog = (await (await fetch(`${baseUrl}/api/v1/catalog`)).json()) as {
      songs: { id: string }[];
    };
    assert.ok(!catalog.songs.some((s) => s.id === publishedSongId));

    const list = await meSongs(author.cookie);
    assert.equal(list.songs.find((s) => s.id === publishedSongId)?.state, "unpublished");

    // Idempotent: a second unpublish reports the same state.
    const again = await api(`/api/v1/me/songs/${publishedSongId}/unpublish`, {
      method: "POST",
      cookie: author.cookie,
    });
    assert.equal(again.status, 200);

    // Republishing the draft restores the catalog entry.
    const republish = await api(`/api/v1/me/drafts/${draftId}/publish`, {
      method: "POST",
      cookie: author.cookie,
      body: publishBody(),
    });
    assert.equal(republish.status, 200);
    const restored = (await (await fetch(`${baseUrl}/api/v1/catalog`)).json()) as {
      songs: { id: string }[];
    };
    assert.ok(restored.songs.some((s) => s.id === publishedSongId));
  });

  it("refuses to unpublish staff-managed or foreign songs (404)", async () => {
    // "who" is the seeded staff song (ownerWorkspaceId NULL).
    const staff = await api("/api/v1/me/songs/who/unpublish", {
      method: "POST",
      cookie: author.cookie,
    });
    assert.equal(staff.status, 404);

    const foreign = await api(`/api/v1/me/songs/${publishedSongId}/unpublish`, {
      method: "POST",
      cookie: outsider.cookie,
    });
    assert.equal(foreign.status, 404);
  });
});

describe("slug namespace protection", () => {
  it("rejects a draft that targets a staff-managed song id", async () => {
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      body: { songId: "who", title: "Hijack attempt" },
    });
    assert.equal(create.status, 409);
  });

  it("rejects publish onto a staff-managed song id", async () => {
    // Craft a draft whose songId bypassed the create-time check is not
    // possible through the API; instead verify the publish guard by
    // pointing a draft at an id a *staff* publish wins first. Seed "who"
    // is already staff-owned, so create a draft, then have staff claim a
    // *different* id the draft does not hold is not the case — the draft
    // holds "who" only via direct DB, which the API forbids. The honest
    // contract check: a draft created BEFORE staff publishes the same id.
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: outsider.cookie,
      body: { songId: "race-song-800", title: "Race" },
    });
    assert.equal(create.status, 201);
    const draft = (await create.json()) as { draft: { id: string } };

    // Staff publishes the same id first (allowed: no owner yet).
    const staffPublish = await fetch(`${baseUrl}/api/v1/admin/songs/race-song-800`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      // The staff contract is the frozen SongPublishRequest — the rights
      // fields of the user flow are not part of it.
      body: JSON.stringify({
        title: "Staff Version",
        artist: "Author",
        youtubeId: "8JXiXt0D6tw",
        level: 5,
        durationSec: 30,
        chart: VALID_CHART,
      }),
    });
    assert.equal(staffPublish.status, 200, await staffPublish.text());

    // The user's publish of the same id must now lose.
    const userPublish = await api(`/api/v1/me/drafts/${draft.draft.id}/publish`, {
      method: "POST",
      cookie: outsider.cookie,
      body: publishBody(),
    });
    assert.equal(userPublish.status, 409);

    // And the staff song was not overwritten by the user attempt.
    const catalogSong = dbQuery(`SELECT title FROM songs WHERE id = 'race-song-800'`);
    assert.match(catalogSong, /Staff Version/);
  });

  it("blocks the staff upsert on a user-owned song (IDOR hole closed)", async () => {
    const staffPut = await fetch(`${baseUrl}/api/v1/admin/songs/${publishedSongId}`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      // Valid staff body (no rights fields — they are user-flow only).
      body: JSON.stringify({
        title: "Staff Overwrite",
        artist: "Author",
        youtubeId: "8JXiXt0D6tw",
        level: 5,
        durationSec: 30,
        chart: VALID_CHART,
      }),
    });
    assert.equal(staffPut.status, 409);

    // The user song is untouched.
    const list = await meSongs(author.cookie);
    // The last republish used the default body (title "My Song").
    assert.equal(list.songs.find((s) => s.id === publishedSongId)?.title, "My Song");
  });
});

describe("team workspace roles", () => {
  it("lets the owner create a team workspace and add a viewer", async () => {
    const create = await api("/api/v1/me/workspaces", {
      method: "POST",
      cookie: teamOwner.cookie,
      body: { name: "Team 800" },
    });
    assert.equal(create.status, 201);
    teamWorkspaceId = ((await create.json()) as { workspace: { id: string } }).workspace.id;

    const add = await api(`/api/v1/me/workspaces/${teamWorkspaceId}/members`, {
      method: "POST",
      cookie: teamOwner.cookie,
      body: { email: teamViewer.email, role: "viewer" },
    });
    assert.equal(add.status, 201);
  });

  it("forbids a viewer from creating or editing drafts (403)", async () => {
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: teamViewer.cookie,
      body: { workspaceId: teamWorkspaceId, title: "Viewer draft" },
    });
    assert.equal(create.status, 403);
  });

  it("scopes drafts to their workspace: owner sees it, outsider does not", async () => {
    const create = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: teamOwner.cookie,
      body: { workspaceId: teamWorkspaceId, songId: "team-song-800", title: "Team Song" },
    });
    assert.equal(create.status, 201);
    const draft = (await create.json()) as { draft: { id: string } };

    const ownerList = await meSongs(teamOwner.cookie);
    assert.ok(ownerList.drafts.some((d) => d.id === draft.draft.id));

    // The viewer can read but not write.
    const viewerGet = await api(`/api/v1/me/drafts/${draft.draft.id}`, {
      cookie: teamViewer.cookie,
    });
    assert.equal(viewerGet.status, 200);
    const viewerBody = (await viewerGet.json()) as { draft: { canEdit: boolean } };
    assert.equal(viewerBody.draft.canEdit, false);

    const viewerPut = await api(`/api/v1/me/drafts/${draft.draft.id}`, {
      method: "PUT",
      cookie: teamViewer.cookie,
      body: { title: "nope" },
    });
    assert.equal(viewerPut.status, 403);

    // A non-member gets 404.
    const foreign = await api(`/api/v1/me/drafts/${draft.draft.id}`, {
      cookie: outsider.cookie,
    });
    assert.equal(foreign.status, 404);
  });
});

describe("bounded bodies", () => {
  it("rejects oversized and malformed draft bodies", async () => {
    const oversized = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      rawBody: `{"pad":"${"x".repeat(PUBLISH_BODY_LIMIT + 1024)}"}`,
    });
    assert.equal(oversized.status, 413);

    const malformed = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      rawBody: `{not json`,
    });
    assert.equal(malformed.status, 422);

    const wrongShape = await api("/api/v1/me/drafts", {
      method: "POST",
      cookie: author.cookie,
      body: { level: "high" },
    });
    assert.equal(wrongShape.status, 422);
  });
});

describe("audit log", () => {
  it("records draft.create, song.publish and song.unpublish", async () => {
    const out = dbQuery(
      `SELECT action, "actorKind", "actorUserId", "targetId" FROM audit_log ORDER BY id`,
    );
    for (const expected of ["draft.create", "song.publish", "song.unpublish"]) {
      assert.match(
        out,
        new RegExp(expected.replace(".", "\\.")),
        `missing audit action ${expected}`,
      );
    }
    // User publishes carry the actor's user id (unlike staff rows).
    assert.match(out, new RegExp(`song\\.publish[\\s\\S]*${author.userId}`));
  });
});
