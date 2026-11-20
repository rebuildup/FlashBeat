/**
 * `POST /api/v1/me/songs/{songId}/unpublish` — take a user-owned song out
 * of the public catalog (issue #800).
 *
 * The per-account counterpart of the staff archive route: it flips the
 * same `songs.archived` soft-delete flag, so the song, its chart
 * snapshots, its scores and its ranking survive and the owner can bring
 * it back by publishing the draft again (user publish clears `archived`).
 * There is still no `DELETE` anywhere.
 *
 *   - 401 — no session. 404 — unknown song, staff-managed song
 *     (`ownerWorkspaceId NULL`) or non-member (IDOR: another workspace's
 *     song is indistinguishable from a missing one). 403 — `viewer`.
 *   - 200 — `{ songId, archived, archivedAt, catalogVersion }`,
 *     idempotent: unpublishing an already-unpublished song reports the
 *     same state without bumping the catalog epoch.
 *
 * Staff moderation is unchanged: `POST /api/v1/admin/songs/{songId}`
 * archives any song, user-owned included.
 */
import { defineHandler } from "void";
import { and, db, eq } from "void/db";
import { songs } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
  writeAuditLog,
} from "../../../_authz";
import { bumpCatalogVersion, readCatalogVersion } from "../../../_catalog-version";

const UNPUBLISH_LIMIT = 30;
const UNPUBLISH_WINDOW_SECONDS = 60;

export const POST = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const songId = c.req.param("songId") ?? "";

  const song = await db
    .select({
      id: songs.id,
      ownerWorkspaceId: songs.ownerWorkspaceId,
      archived: songs.archived,
      archivedAt: songs.archivedAt,
    })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();

  // Staff-managed songs (`ownerWorkspaceId NULL`) are not the caller's:
  // 404 keeps them indistinguishable from unknown ids.
  if (!song || song.ownerWorkspaceId === null) {
    return c.json({ error: "song not found" }, 404);
  }
  const membership = await findMembership(song.ownerWorkspaceId, context.user.id);
  if (!membership) {
    return c.json({ error: "song not found" }, 404);
  }
  if (!hasWorkspaceRole(membership.role, "editor")) {
    return c.json({ error: "editor role required to unpublish" }, 403);
  }

  if (
    !(await consumeRateLimit(
      "me.song.write",
      context.user.id,
      UNPUBLISH_LIMIT,
      UNPUBLISH_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  if (song.archived === 1) {
    // Idempotent: nothing changed, so the catalog epoch stays put.
    return c.json({
      songId,
      archived: true,
      archivedAt: song.archivedAt,
      catalogVersion: await readCatalogVersion(),
    });
  }

  const archivedAt = new Date().toISOString();
  await db.batch([
    db
      .update(songs)
      .set({ archived: 1, archivedAt })
      .where(and(eq(songs.id, songId), eq(songs.ownerWorkspaceId, song.ownerWorkspaceId))),
    bumpCatalogVersion(),
  ]);

  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId: song.ownerWorkspaceId,
    action: "song.unpublish",
    targetType: "song",
    targetId: songId,
  });

  return c.json({
    songId,
    archived: true,
    archivedAt,
    catalogVersion: await readCatalogVersion(),
  });
});
