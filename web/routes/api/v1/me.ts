/**
 * `GET /api/v1/me` — the caller's session identity and workspace (issue
 * #799).
 *
 * This is the studio-internal counterpart of the public read API: it is
 * not part of `contracts/openapi.v1.yaml` (the frozen Unity contract) and
 * exists so the Web Studio can learn who is signed in and which workspace
 * owns their songs. It is also the smallest end-to-end proof of the auth
 * foundation: session cookie → `user` row → personal workspace +
 * membership row.
 *
 *   - 401 — no valid session. Checked before anything is written; an
 *     anonymous caller never creates a workspace.
 *   - 200 — `{ user, workspace }`. The personal workspace is created
 *     lazily on this first authenticated call (`_authz.ts`).
 *
 * `Cache-Control` is handled by Void's auth middleware: responses to
 * signed-in requests are `no-store`, so one account's identity can never
 * be served from a shared cache to another.
 */
import { defineHandler } from "void";
import { requireWorkspaceContext, sessionUser } from "./_authz";

export const GET = defineHandler(async (c) => {
  const user = sessionUser(c);
  if (!user) {
    return c.json({ error: "authentication required" }, 401);
  }
  const context = await requireWorkspaceContext(c);
  if (!context) {
    // Unreachable while sessionUser succeeded, kept so a future auth
    // change cannot silently return a half-populated identity.
    return c.json({ error: "authentication required" }, 401);
  }

  return c.json({
    user: {
      id: user.id,
      email: user.email,
      name: user.name,
      emailVerified: user.emailVerified,
    },
    workspace: {
      id: context.workspaceId,
      role: context.role,
    },
  });
});
