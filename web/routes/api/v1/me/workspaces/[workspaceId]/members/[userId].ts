/**
 * `DELETE /api/v1/me/workspaces/{workspaceId}/members/{userId}` — remove a
 * member from a workspace (issue #799).
 *
 * Owner only. The last remaining `owner` can never be removed — that would
 * lock the workspace (including the caller's own access) with no way back
 * in. Removing yourself while other owners remain is allowed: it is a
 * voluntary leave, audited like any other removal.
 */
import { defineHandler } from "void";
import { and, count, db, eq } from "void/db";
import { workspaceMembers } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
  writeAuditLog,
} from "../../../../_authz";

const MEMBER_WRITE_LIMIT = 30;
const MEMBER_WRITE_WINDOW_SECONDS = 60;

export const DELETE = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const workspaceId = c.req.param("workspaceId") ?? "";
  const targetUserId = c.req.param("userId") ?? "";

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

  const target = await db
    .select({ role: workspaceMembers.role })
    .from(workspaceMembers)
    .where(
      and(
        eq(workspaceMembers.workspaceId, workspaceId),
        eq(workspaceMembers.userId, targetUserId),
      ),
    )
    .get();
  if (!target) {
    return c.json({ error: "member not found" }, 404);
  }

  if (target.role === "owner") {
    const owners = await db
      .select({ n: count() })
      .from(workspaceMembers)
      .where(
        and(
          eq(workspaceMembers.workspaceId, workspaceId),
          eq(workspaceMembers.role, "owner"),
        ),
      )
      .get();
    if ((owners?.n ?? 0) <= 1) {
      return c.json(
        { error: "the last owner of a workspace cannot be removed" },
        409,
      );
    }
  }

  await db
    .delete(workspaceMembers)
    .where(
      and(
        eq(workspaceMembers.workspaceId, workspaceId),
        eq(workspaceMembers.userId, targetUserId),
      ),
    );
  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId,
    action: "member.remove",
    targetType: "workspace_member",
    targetId: targetUserId,
  });

  return c.json({ workspaceId, userId: targetUserId, removed: true });
});
