/**
 * `/api/v1/me/workspaces/{workspaceId}/members` — member list and member
 * addition for one workspace (issue #799).
 *
 *   GET  — every member of the workspace. Any member (viewer included)
 *          may list; a non-member gets 404 so workspace ids cannot be
 *          enumerated.
 *   POST — add a member by account email. **Owner only**; the granted role
 *          is limited to `editor`/`viewer` — `owner` is never assignable
 *          through this API, so nobody can self-elevate to owner.
 *
 * Member management is the permission-change surface of the threat model:
 * every add/remove writes an `audit_log` row and the change takes effect
 * on the member's very next request (roles are read from
 * `workspace_members` per request, never cached).
 */
import { defineHandler } from "void";
import { and, db, eq, sql } from "void/db";
import { workspaceMembers } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
  writeAuditLog,
} from "../../../_authz";
import { readJsonLimited } from "../../../_request-body";

const MEMBER_BODY_LIMIT = 8 * 1024;
const MEMBER_WRITE_LIMIT = 30;
const MEMBER_WRITE_WINDOW_SECONDS = 60;

/** Roles this API may grant. `owner` stays unreachable on purpose. */
const GRANTABLE_ROLES = new Set(["editor", "viewer"]);

interface MemberRow {
  userId: string;
  role: string;
  createdAt: string;
  name: string | null;
  email: string;
}

export const GET = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const workspaceId = c.req.param("workspaceId") ?? "";
  const membership = await findMembership(workspaceId, context.user.id);
  if (!membership) {
    // 404, not 403: a workspace the caller cannot see must not be
    // distinguishable from one that does not exist (IDOR).
    return c.json({ error: "workspace not found" }, 404);
  }

  // The auth `user` table is managed by Void/Better Auth and is not part of
  // the Drizzle schema, so the join goes through a parameterized `sql`
  // template — still fully bound, never string-concatenated.
  const rows = (await db.all(sql`
    SELECT m."userId" AS "userId", m."role" AS "role",
           m."createdAt" AS "createdAt",
           u."name" AS "name", u."email" AS "email"
    FROM "workspace_members" m
    JOIN "user" u ON u."id" = m."userId"
    WHERE m."workspaceId" = ${workspaceId}
    ORDER BY m."createdAt" ASC, m."userId" ASC
  `)) as MemberRow[];

  return c.json({
    workspaceId,
    members: rows.map((row) => ({
      userId: row.userId,
      name: row.name,
      email: row.email,
      role: row.role,
      createdAt: row.createdAt,
    })),
  });
});

export const POST = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const workspaceId = c.req.param("workspaceId") ?? "";

  const membership = await findMembership(workspaceId, context.user.id);
  if (!membership) {
    return c.json({ error: "workspace not found" }, 404);
  }
  if (!hasWorkspaceRole(membership.role, "owner")) {
    return c.json({ error: "only a workspace owner can manage members" }, 403);
  }

  if (
    !(await consumeRateLimit(
      "me.member.write",
      context.user.id,
      MEMBER_WRITE_LIMIT,
      MEMBER_WRITE_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  const limited = await readJsonLimited(c, MEMBER_BODY_LIMIT);
  if (!limited.ok) {
    return limited.response;
  }
  const body = limited.value;
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return c.json(
      {
        error: "invalid member request",
        diagnostics: [{ path: "(body)", message: "must be a JSON object" }],
      },
      422,
    );
  }
  const fields = body as Record<string, unknown>;
  const email = typeof fields.email === "string" ? fields.email.trim() : "";
  const role = fields.role === undefined ? "editor" : fields.role;
  if (email.length === 0 || email.length > 320 || !email.includes("@")) {
    return c.json(
      {
        error: "invalid member request",
        diagnostics: [{ path: "email", message: "must be an account email address" }],
      },
      422,
    );
  }
  if (typeof role !== "string" || !GRANTABLE_ROLES.has(role)) {
    return c.json(
      {
        error: "invalid member request",
        diagnostics: [
          {
            path: "role",
            message: "must be \"editor\" or \"viewer\" — owner cannot be granted here",
          },
        ],
      },
      422,
    );
  }

  // Resolve the account by its immutable id through the auth `user` table.
  // The lookup is parameterized; an unknown email is a plain 404.
  const target = (await db.get(
    sql`SELECT "id" AS "id" FROM "user" WHERE "email" = ${email} LIMIT 1`,
  )) as { id: string } | undefined;
  if (!target) {
    return c.json({ error: "user not found" }, 404);
  }
  if (target.id === context.user.id) {
    return c.json({ error: "already a member of this workspace" }, 409);
  }

  const existing = await db
    .select({ role: workspaceMembers.role })
    .from(workspaceMembers)
    .where(
      and(
        eq(workspaceMembers.workspaceId, workspaceId),
        eq(workspaceMembers.userId, target.id),
      ),
    )
    .get();
  if (existing) {
    return c.json({ error: "user is already a member of this workspace" }, 409);
  }

  try {
    await db.insert(workspaceMembers).values({
      workspaceId,
      userId: target.id,
      role,
    });
  } catch {
    // Two concurrent adds of the same (workspace, user) race on the
    // primary key; the loser gets a conflict instead of a 500.
    return c.json({ error: "user is already a member of this workspace" }, 409);
  }
  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId,
    action: "member.add",
    targetType: "workspace_member",
    targetId: target.id,
    detail: { role },
  });

  return c.json({ workspaceId, userId: target.id, role }, 201);
});
