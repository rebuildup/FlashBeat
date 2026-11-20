/**
 * Session authentication and workspace authorization helpers (issue #799).
 *
 * Rules every user-owned API follows:
 *
 *   - The identity comes from the Better Auth session cookie resolved by
 *     Void's auth middleware (`requireAuth` reads `c.get("user")`). A
 *     client-supplied `userId` / `ownerId` field is never trusted.
 *   - Every user owns exactly one personal workspace, created lazily on the
 *     first authenticated call (`personal-<userId>`, role `owner`). The id
 *     is deterministic, so concurrent first calls converge through
 *     `ON CONFLICT DO NOTHING` instead of racing a second workspace.
 *   - Authorization resolves through `workspace_members`, the only place a
 *     user ↔ workspace ↔ role relationship is stored. Routes that touch
 *     user-owned objects must look the object up by id **and** check the
 *     caller's membership/role on its `ownerWorkspaceId` — never by
 *     comparing a request field to a stored owner name (IDOR).
 *
 * This file is prefixed with `_` so Void does not turn it into a route.
 */
import { requireAuth, type AuthUser } from "void/auth";
import type { CloudContext } from "void";
import { and, db, eq, lt, sql } from "void/db";
import { auditLog, rateLimits, workspaceMembers, workspaces } from "@schema";

/** Role ranks used by `hasWorkspaceRole`; higher includes lower. */
const ROLE_RANK: Record<string, number> = {
  viewer: 0,
  editor: 1,
  owner: 2,
};

/**
 * The authenticated user of the request, or `null`. Wraps
 * `requireAuth` (which throws a 401 `HTTPException`) so route handlers can
 * answer with the API's JSON error shape instead of Hono's default text.
 */
export function sessionUser(c: CloudContext): AuthUser | null {
  try {
    return requireAuth(c);
  } catch {
    return null;
  }
}

export interface WorkspaceContext {
  user: AuthUser;
  /** Id of the caller's personal workspace (`personal-<userId>`). */
  workspaceId: string;
  /** The caller's role inside that workspace (`owner` for personal). */
  role: string;
}

/** Deterministic id of a user's personal workspace. */
export function personalWorkspaceId(userId: string): string {
  return `personal-${userId}`;
}

/**
 * Return the caller's workspace context, creating the personal workspace
 * and its owner membership on first use. Idempotent: the workspace id and
 * the membership primary key make a repeated or concurrent call a no-op.
 */
export async function workspaceContext(user: AuthUser): Promise<WorkspaceContext> {
  const workspaceId = personalWorkspaceId(user.id);
  const name = (user.name || user.email.split("@")[0] || "workspace").slice(0, 80);

  await db
    .insert(workspaces)
    .values({ id: workspaceId, name, kind: "personal" })
    .onConflictDoNothing();
  await db
    .insert(workspaceMembers)
    .values({ workspaceId, userId: user.id, role: "owner" })
    .onConflictDoNothing();

  const member = await db
    .select({ role: workspaceMembers.role })
    .from(workspaceMembers)
    .where(
      and(
        eq(workspaceMembers.workspaceId, workspaceId),
        eq(workspaceMembers.userId, user.id),
      ),
    )
    .get();

  return { user, workspaceId, role: member?.role ?? "owner" };
}

/** `sessionUser` + `workspaceContext` in one call; `null` when anonymous. */
export async function requireWorkspaceContext(c: CloudContext): Promise<WorkspaceContext | null> {
  const user = sessionUser(c);
  if (!user) {
    return null;
  }
  return workspaceContext(user);
}

/**
 * Role check for object-level authorization. Unknown roles rank below
 * `viewer`, so a malformed membership row can never authorize a write.
 */
export function hasWorkspaceRole(role: string, minimum: "viewer" | "editor" | "owner"): boolean {
  return (ROLE_RANK[role] ?? -1) >= ROLE_RANK[minimum];
}

/** One row of `workspace_members` joined with its workspace. */
export interface Membership {
  workspaceId: string;
  workspaceName: string;
  workspaceKind: string;
  role: string;
}

/** Every workspace the user belongs to, with the caller's role in each. */
export async function listMemberships(userId: string): Promise<Membership[]> {
  return (await db
    .select({
      workspaceId: workspaceMembers.workspaceId,
      workspaceName: workspaces.name,
      workspaceKind: workspaces.kind,
      role: workspaceMembers.role,
    })
    .from(workspaceMembers)
    .innerJoin(workspaces, eq(workspaces.id, workspaceMembers.workspaceId))
    .where(eq(workspaceMembers.userId, userId))) as Membership[];
}

/** The caller's membership row inside one workspace, or `null`. */
export async function findMembership(
  workspaceId: string,
  userId: string,
): Promise<Membership | null> {
  const row = await db
    .select({
      workspaceId: workspaceMembers.workspaceId,
      workspaceName: workspaces.name,
      workspaceKind: workspaces.kind,
      role: workspaceMembers.role,
    })
    .from(workspaceMembers)
    .innerJoin(workspaces, eq(workspaces.id, workspaceMembers.workspaceId))
    .where(
      and(eq(workspaceMembers.workspaceId, workspaceId), eq(workspaceMembers.userId, userId)),
    )
    .get();
  return (row as Membership | undefined) ?? null;
}

/**
 * Append one audit row (issue #799, threat model §4.4). Permission changes,
 * member management and staff operations all record who did what to which
 * object; the row never carries secrets, tokens or chart bodies.
 */
export async function writeAuditLog(entry: {
  actorUserId?: string | null;
  actorKind?: "user" | "staff";
  workspaceId?: string | null;
  action: string;
  targetType?: string | null;
  targetId?: string | null;
  detail?: Record<string, unknown> | null;
}): Promise<void> {
  await db.insert(auditLog).values({
    actorUserId: entry.actorUserId ?? null,
    actorKind: entry.actorKind ?? "user",
    workspaceId: entry.workspaceId ?? null,
    action: entry.action,
    targetType: entry.targetType ?? null,
    targetId: entry.targetId ?? null,
    detail: entry.detail ? JSON.stringify(entry.detail) : null,
  });
}

/**
 * Fixed-window per-subject rate limit for the studio mutation APIs
 * (issue #799, threat model §4.2). The counter lives in `rate_limits` so
 * every Worker instance shares it. The increment and the read happen in
 * one `INSERT … ON CONFLICT DO UPDATE … RETURNING`, so concurrent requests
 * cannot all pass a stale count.
 *
 * @returns `true` when the call is within `limit` hits of the current
 *   `windowSeconds` window.
 */
export async function consumeRateLimit(
  bucket: string,
  subject: string,
  limit: number,
  windowSeconds: number,
): Promise<boolean> {
  const window = windowSeconds * Math.floor(Date.now() / 1000 / windowSeconds);
  const rows = await db
    .insert(rateLimits)
    .values({ bucket, subject, windowStart: window, hits: 1 })
    .onConflictDoUpdate({
      target: [rateLimits.bucket, rateLimits.subject, rateLimits.windowStart],
      set: { hits: sql`${rateLimits.hits} + 1` },
    })
    .returning({ hits: rateLimits.hits });
  const hits = rows[0]?.hits ?? limit + 1;
  // Housekeeping: drop windows that can never be hit again. The table stays
  // small because every row is one (bucket, subject, window) triple.
  await db.delete(rateLimits).where(lt(rateLimits.windowStart, window - windowSeconds));
  return hits <= limit;
}
