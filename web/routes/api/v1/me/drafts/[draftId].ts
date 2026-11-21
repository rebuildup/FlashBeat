/**
 * `/api/v1/me/drafts/{draftId}` — read and save one song draft (issue #800).
 *
 *   GET — the draft with its chart JSON parsed, so the editor can resume
 *         an interrupted session. Any workspace member (viewer included)
 *         may read; a non-member gets 404.
 *   PUT — save the working copy (metadata and/or chart). **Editor or
 *         owner only** — a viewer gets 403. The chart is stored
 *         leniently: it must be a JSON object, but the strict contract
 *         validation runs at publish time, so a half-finished chart can
 *         always be saved. Per-save writes are NOT audited (they are
 *         high-frequency); create/publish/unpublish are.
 *
 * Both handlers resolve the draft by id and then check the caller's
 * membership on `ownerWorkspaceId` — the draft id alone never
 * authorizes (IDOR).
 */
import { defineHandler } from "void";
import { and, db, eq, sql } from "void/db";
import { songDrafts, songs, type SongDraft } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
} from "../../_authz";
import { readJsonLimited, PUBLISH_BODY_LIMIT } from "../../_request-body";
import { validateDraftBody, toMeDraft, type DraftPatch } from "../../_me-model";

const DRAFT_WRITE_LIMIT = 60;
const DRAFT_WRITE_WINDOW_SECONDS = 60;

async function findDraft(draftId: string): Promise<SongDraft | undefined> {
  return db.select().from(songDrafts).where(eq(songDrafts.id, draftId)).get();
}

/** Draft detail shape shared by GET and the PUT response. */
function draftDetail(row: SongDraft, workspaceName: string, role: string, canEdit: boolean) {
  return {
    ...toMeDraft(row, workspaceName, role, canEdit),
    createdByUserId: row.createdByUserId,
    materialSource: row.materialSource,
    rightsStatus: row.rightsStatus,
    license: row.license,
    chart: row.chartJson === null ? null : (JSON.parse(row.chartJson) as unknown),
  };
}

export const GET = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const draftId = c.req.param("draftId") ?? "";

  const draft = await findDraft(draftId);
  if (!draft) {
    return c.json({ error: "draft not found" }, 404);
  }
  const membership = await findMembership(draft.ownerWorkspaceId, context.user.id);
  if (!membership) {
    // 404, not 403: a draft the caller cannot see must not be
    // distinguishable from one that does not exist (IDOR).
    return c.json({ error: "draft not found" }, 404);
  }

  c.header("Cache-Control", "no-store");
  return c.json({
    draft: draftDetail(
      draft,
      membership.workspaceName,
      membership.role,
      hasWorkspaceRole(membership.role, "editor"),
    ),
  });
});

export const PUT = defineHandler(async (c) => {
  const context = await requireWorkspaceContext(c);
  if (!context) {
    return c.json({ error: "authentication required" }, 401);
  }
  const draftId = c.req.param("draftId") ?? "";

  const draft = await findDraft(draftId);
  if (!draft) {
    return c.json({ error: "draft not found" }, 404);
  }
  const membership = await findMembership(draft.ownerWorkspaceId, context.user.id);
  if (!membership) {
    return c.json({ error: "draft not found" }, 404);
  }
  if (!hasWorkspaceRole(membership.role, "editor")) {
    return c.json({ error: "editor role required to edit drafts" }, 403);
  }

  if (
    !(await consumeRateLimit(
      "me.draft.write",
      context.user.id,
      DRAFT_WRITE_LIMIT,
      DRAFT_WRITE_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  const limited = await readJsonLimited(c, PUBLISH_BODY_LIMIT);
  if (!limited.ok) {
    return limited.response;
  }
  const validation = validateDraftBody(limited.value);
  if (!validation.ok) {
    return c.json({ error: "invalid draft request", diagnostics: validation.diagnostics }, 422);
  }
  const patch = validation.patch;

  // A songId change re-runs the ownership check: the id must be free or
  // already owned by this workspace (the update flow). The UNIQUE index
  // on song_drafts.songId catches a racing draft of another workspace.
  if (patch.songId !== undefined && patch.songId !== draft.songId) {
    if (patch.songId !== null) {
      const conflict = await songIdAvailable(patch.songId, draft.ownerWorkspaceId, draft.id);
      if (!conflict.ok) {
        return c.json(
          {
            error: "song id is already in use",
            diagnostics: [{ path: "songId", message: conflict.message }],
          },
          409,
        );
      }
    }
  }

  const set = draftUpdateSet(patch);
  try {
    await db
      .update(songDrafts)
      .set(set)
      .where(
        and(eq(songDrafts.id, draftId), eq(songDrafts.ownerWorkspaceId, draft.ownerWorkspaceId)),
      );
  } catch {
    // UNIQUE(songId) race.
    return c.json(
      {
        error: "song id is already in use",
        diagnostics: [{ path: "songId", message: "another draft already reserved this song id" }],
      },
      409,
    );
  }

  const updated = await findDraft(draftId);
  if (!updated) {
    return c.json({ error: "draft not found" }, 404);
  }
  return c.json({
    draft: draftDetail(updated, membership.workspaceName, membership.role, true),
  });
});

/**
 * Whether `songId` may move onto `draftId`: free, or held by a `songs` row
 * of the same workspace (update flow), and not reserved by a *different*
 * draft. The draft's own current id is always allowed.
 */
async function songIdAvailable(
  songId: string,
  workspaceId: string,
  draftId: string,
): Promise<{ ok: true } | { ok: false; message: string }> {
  const song = await db
    .select({ ownerWorkspaceId: songs.ownerWorkspaceId })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();
  if (song && song.ownerWorkspaceId !== workspaceId) {
    return {
      ok: false,
      message: "this song id is published by another workspace or managed by staff",
    };
  }
  const other = await db
    .select({ id: songDrafts.id })
    .from(songDrafts)
    .where(and(eq(songDrafts.songId, songId), sql`${songDrafts.id} != ${draftId}`))
    .get();
  if (other) {
    return { ok: false, message: "another draft already reserved this song id" };
  }
  return { ok: true };
}

/** Column updates of a validated patch; absent fields stay untouched. */
function draftUpdateSet(patch: DraftPatch) {
  const set: Record<string, unknown> = {
    updatedAt: sql`(datetime('now'))`,
  };
  if (patch.songId !== undefined) set.songId = patch.songId;
  if (patch.title !== undefined) set.title = patch.title;
  if (patch.artist !== undefined) set.artist = patch.artist;
  if (patch.youtubeId !== undefined) set.youtubeId = patch.youtubeId;
  if (patch.level !== undefined) set.level = patch.level;
  if (patch.durationSec !== undefined) set.durationSec = patch.durationSec;
  if (patch.chart !== undefined) {
    set.chartJson = JSON.stringify(patch.chart);
    set.noteCount = patch.noteCount ?? 0;
  }
  if (patch.materialSource !== undefined) set.materialSource = patch.materialSource;
  if (patch.rightsStatus !== undefined) set.rightsStatus = patch.rightsStatus;
  if (patch.license !== undefined) set.license = patch.license;
  return set;
}
