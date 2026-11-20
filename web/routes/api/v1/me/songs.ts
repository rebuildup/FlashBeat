/**
 * `GET /api/v1/me/songs` — the caller's songs and drafts (issue #800).
 *
 * Studio-internal API (not part of the frozen Unity contract). One call
 * answers "自分の曲と下書きの一覧": every `songs` row owned by a workspace
 * the caller belongs to, plus every `song_drafts` row of those workspaces.
 *
 *   - 401 — no valid session; checked before anything is read.
 *   - 200 — `{ songs, drafts }`. Each entry carries the owning workspace
 *     (id + name) and the caller's role in it, so the studio can show
 *     公開状態・バージョン・所有者 and gate edit/publish controls without
 *     a second request. Staff-managed songs (`ownerWorkspaceId NULL`) are
 *     never listed — they are not the caller's.
 *
 * `Cache-Control: no-store` is applied by Void's auth middleware for
 * signed-in requests.
 */
import { defineHandler } from "void";
import { inArray } from "void/db";
import { db } from "void/db";
import { songDrafts, songs } from "@schema";
import { hasWorkspaceRole, listMemberships, requireWorkspaceContext } from "../_authz";
import { toMeDraft, toMeSong, type MeSongListResponse } from "../_me-model";

export const GET = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }

  const memberships = await listMemberships(context.user.id);
  const roleByWorkspace = new Map(memberships.map((m) => [m.workspaceId, m.role]));
  const nameByWorkspace = new Map(memberships.map((m) => [m.workspaceId, m.workspaceName]));
  const workspaceIds = memberships.map((m) => m.workspaceId);

  const body: MeSongListResponse = { songs: [], drafts: [] };
  if (workspaceIds.length === 0) {
    return c.json(body);
  }

  const songRows = await db
    .select()
    .from(songs)
    .where(inArray(songs.ownerWorkspaceId, workspaceIds));
  const draftRows = await db
    .select()
    .from(songDrafts)
    .where(inArray(songDrafts.ownerWorkspaceId, workspaceIds));

  for (const row of songRows) {
    const workspaceId = row.ownerWorkspaceId ?? "";
    const role = roleByWorkspace.get(workspaceId) ?? "viewer";
    body.songs.push(
      toMeSong(
        row,
        nameByWorkspace.get(workspaceId) ?? workspaceId,
        role,
        hasWorkspaceRole(role, "editor"),
      ),
    );
  }
  for (const row of draftRows) {
    const role = roleByWorkspace.get(row.ownerWorkspaceId) ?? "viewer";
    body.drafts.push(
      toMeDraft(
        row,
        nameByWorkspace.get(row.ownerWorkspaceId) ?? row.ownerWorkspaceId,
        role,
        hasWorkspaceRole(role, "editor"),
      ),
    );
  }

  // Stable order: published songs by id, drafts by updatedAt desc so the
  // most recently touched work sits on top.
  body.songs.sort((a, b) => a.id.localeCompare(b.id));
  body.drafts.sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));

  c.header("Cache-Control", "no-store");
  return c.json(body);
});
