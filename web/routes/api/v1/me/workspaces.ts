/**
 * `/api/v1/me/workspaces` — the caller's workspace list and team
 * workspace creation (issue #799).
 *
 * Studio-internal API (not part of the frozen Unity contract). Every
 * handler resolves the caller through the Better Auth session and the
 * `workspace_members` table — a client-supplied owner id is never read.
 *
 *   GET  — every workspace the caller belongs to, with their role.
 *   POST — create a `kind = 'team'` workspace; the caller becomes its
 *          `owner`. Rate limited per user and audited.
 */
import { defineHandler } from "void";
import { db } from "void/db";
import { workspaceMembers, workspaces } from "@schema";
import {
  consumeRateLimit,
  listMemberships,
  requireWorkspaceContext,
  writeAuditLog,
} from "../_authz";
import { readJsonLimited } from "../_request-body";

/** Workspace creation bodies are `{ "name": "…" }` — a few dozen bytes. */
const WORKSPACE_BODY_LIMIT = 8 * 1024;
/** Team workspaces one user may create per minute (threat model §4.2). */
const WORKSPACE_WRITE_LIMIT = 20;
const WORKSPACE_WRITE_WINDOW_SECONDS = 60;

export const GET = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const memberships = await listMemberships(context.user.id);
  return c.json({
    workspaces: memberships.map((m) => ({
      id: m.workspaceId,
      name: m.workspaceName,
      kind: m.workspaceKind,
      role: m.role,
    })),
  });
});

export const POST = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }

  // Per-user rate limit before the body is even read: a flood of oversized
  // or malformed requests still costs the caller a slot.
  if (
    !(await consumeRateLimit(
      "me.workspace.write",
      context.user.id,
      WORKSPACE_WRITE_LIMIT,
      WORKSPACE_WRITE_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  const limited = await readJsonLimited(c, WORKSPACE_BODY_LIMIT);
  if (!limited.ok) {
    return limited.response;
  }
  const body = limited.value;
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return c.json(
      {
        error: "invalid workspace request",
        diagnostics: [{ path: "(body)", message: "must be a JSON object" }],
      },
      422,
    );
  }
  const name = (body as Record<string, unknown>).name;
  if (typeof name !== "string" || name.trim().length === 0 || name.trim().length > 80) {
    return c.json(
      {
        error: "invalid workspace request",
        diagnostics: [
          {
            path: "name",
            message: "must be a string of 1-80 characters",
          },
        ],
      },
      422,
    );
  }
  const trimmedName = name.trim();

  const workspaceId = `ws-${crypto.randomUUID()}`;
  // One batch = one implicit transaction: the workspace row and its owner
  // membership commit together or not at all.
  await db.batch([
    db.insert(workspaces).values({ id: workspaceId, name: trimmedName, kind: "team" }),
    db.insert(workspaceMembers).values({
      workspaceId,
      userId: context.user.id,
      role: "owner",
    }),
  ]);
  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId,
    action: "workspace.create",
    targetType: "workspace",
    targetId: workspaceId,
    detail: { name: trimmedName, kind: "team" },
  });

  return c.json(
    {
      workspace: { id: workspaceId, name: trimmedName, kind: "team", role: "owner" },
    },
    201,
  );
});
