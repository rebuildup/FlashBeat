/**
 * `POST /api/v1/me/drafts/{draftId}/publish` — publish or update a
 * user-owned song (issue #800).
 *
 * The per-account counterpart of `PUT /api/v1/admin/songs/{songId}`: no
 * staff token, the caller's session + workspace role is the authority.
 * The song fields go through the *same* frozen contract validation and
 * semantic checks as the staff route (`_publish-model.ts`), and the
 * rights attestation (`rightsConfirmed`, `materialSource`, `rightsStatus`,
 * `license`) is mandatory — publishing without it is a 422.
 *
 * Ownership rules (`_me-model.ts` header):
 *
 *   - The draft's `songId` becomes the public id. A `songs` row owned by
 *     the same workspace is updated (new `chart_versions` snapshot,
 *     `currentVersion + 1`, `archived` cleared — publish means "live");
 *     a row owned by another workspace answers 409, a staff-managed row
 *     (`ownerWorkspaceId NULL`) answers 409 as well: user publishes never
 *     overwrite foreign or staff ids.
 *   - `ownerWorkspaceId` is written once at first publish and never
 *     changed afterwards, so the check-then-write race cannot hand a song
 *     to another workspace: a concurrent first publish loses on the
 *     `songs`/`chart_versions` primary keys and gets a 409.
 *
 *   - 401 — no session. 404 — unknown draft or non-member (IDOR).
 *     403 — `viewer` role. 409 — song id conflict / concurrent publish.
 *     422 — contract or rights validation failed; nothing is written.
 *     200 — `{ songId, chartVersion, catalogVersion, archived, archivedAt,
 *     draft }`; every catalog-visible write bumps `catalog_meta` in the
 *     same batch and lands in `audit_log` (`song.publish`, actor = user).
 */
import { defineHandler } from "void";
import { and, db, eq, sql } from "void/db";
import { chartVersions, songDrafts, songs, type SongDraft } from "@schema";
import {
  consumeRateLimit,
  findMembership,
  hasWorkspaceRole,
  requireWorkspaceContext,
  writeAuditLog,
} from "../../../_authz";
import { readJsonLimited, PUBLISH_BODY_LIMIT } from "../../../_request-body";
import { semanticDiagnostics, type PublishDiagnostic } from "../../../_publish-model";
import { bumpCatalogVersion, readCatalogVersion } from "../../../_catalog-version";
import { validateUserPublishBody } from "../../../_me-model";

/** Publishes run the full contract validation; keep them modest per user. */
const PUBLISH_LIMIT = 20;
const PUBLISH_WINDOW_SECONDS = 60;

async function findDraft(draftId: string): Promise<SongDraft | undefined> {
  return db.select().from(songDrafts).where(eq(songDrafts.id, draftId)).get();
}

export const POST = defineHandler(async (c) => {
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
    return c.json({ error: "editor role required to publish" }, 403);
  }

  if (
    !(await consumeRateLimit(
      "me.song.publish",
      context.user.id,
      PUBLISH_LIMIT,
      PUBLISH_WINDOW_SECONDS,
    ))
  ) {
    return c.json({ error: "rate limit exceeded" }, 429);
  }

  const limited = await readJsonLimited(c, PUBLISH_BODY_LIMIT);
  if (!limited.ok) {
    return limited.response;
  }
  const validation = validateUserPublishBody(limited.value);
  if (!validation.ok) {
    return c.json({ error: "invalid publish request", diagnostics: validation.diagnostics }, 422);
  }
  const { request, materialSource, rightsStatus, license } = validation.publish;

  // The same semantic layer the staff route applies (8 lanes, beat
  // positions, note kinds, duration bound, shared chart rules).
  const semantics = semanticDiagnostics(request);
  if (semantics.length > 0) {
    return c.json({ error: "invalid publish request", diagnostics: semantics }, 422);
  }

  const songId = draft.songId;
  if (songId === null || !/^[a-z0-9-]{3,64}$/.test(songId)) {
    return c.json(
      {
        error: "invalid publish request",
        diagnostics: [
          {
            path: "songId",
            message: "the draft must name a public song id (^[a-z0-9-]{3,64}$) before publishing",
          } satisfies PublishDiagnostic,
        ],
      },
      422,
    );
  }

  const existing = await db
    .select({
      ownerWorkspaceId: songs.ownerWorkspaceId,
      currentVersion: songs.currentVersion,
    })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();

  if (existing && existing.ownerWorkspaceId !== draft.ownerWorkspaceId) {
    // First publisher wins. A staff-managed row (`NULL`) and another
    // workspace's row are both off-limits; the messages differ so the
    // operator learns which rule stopped them, but neither writes.
    const staffManaged = existing.ownerWorkspaceId === null;
    return c.json(
      {
        error: staffManaged
          ? "song id is managed by staff"
          : "song id is owned by another workspace",
        diagnostics: [
          {
            path: "songId",
            message: staffManaged
              ? `"${songId}" is a staff-published song; choose a different song id`
              : `"${songId}" is already published by another workspace; choose a different song id`,
          } satisfies PublishDiagnostic,
        ],
      },
      409,
    );
  }

  const chartVersion = (existing?.currentVersion ?? 0) + 1;
  const chartJson = JSON.stringify(request.chart);
  const noteCount = (request.chart.notes as unknown[]).length;

  const songValues = {
    title: request.title,
    artist: request.artist,
    youtubeId: request.youtubeId,
    bpm: request.chart.BPM as number,
    level: request.level,
    durationSec: request.durationSec,
    materialSource,
    rightsStatus,
    license,
  };

  // One batch = one implicit transaction. For a first publish the `songs`
  // row is a plain INSERT: a concurrent first publish of the same id loses
  // on the primary key and the whole batch fails (409 below). For an
  // update the row is UPDATEd guarded by `ownerWorkspaceId`, which can
  // never change after insert — so the guard is belt-and-braces, not the
  // race decider (the chart_versions PK is).
  const songStatement = existing
    ? db
        .update(songs)
        .set({
          ...songValues,
          currentVersion: chartVersion,
          // Publish means live: a user republish restores an archived
          // (unpublished) song. Staff moderation re-archives on top.
          archived: 0,
          archivedAt: null,
        })
        .where(and(eq(songs.id, songId), eq(songs.ownerWorkspaceId, draft.ownerWorkspaceId)))
    : db.insert(songs).values({
        id: songId,
        ...songValues,
        currentVersion: chartVersion,
        archived: 0,
        archivedAt: null,
        ownerWorkspaceId: draft.ownerWorkspaceId,
        createdByUserId: draft.createdByUserId,
      });

  try {
    await db.batch([
      songStatement,
      db
        .insert(chartVersions)
        .values({ songId, version: chartVersion, json: chartJson, noteCount }),
      db
        .update(songDrafts)
        .set({
          title: request.title,
          artist: request.artist,
          youtubeId: request.youtubeId,
          level: request.level,
          durationSec: request.durationSec,
          chartJson,
          noteCount,
          materialSource,
          rightsStatus,
          license,
          publishedVersion: chartVersion,
          updatedAt: sql`(datetime('now'))`,
        })
        .where(eq(songDrafts.id, draftId)),
      bumpCatalogVersion(),
    ]);
  } catch {
    return c.json(
      {
        error: "concurrent publish",
        diagnostics: [
          {
            path: "(request)",
            message: `"${songId}" was just published by another request; retry to publish as the next version`,
          } satisfies PublishDiagnostic,
        ],
      },
      409,
    );
  }

  await writeAuditLog({
    actorUserId: context.user.id,
    workspaceId: draft.ownerWorkspaceId,
    action: "song.publish",
    targetType: "song",
    targetId: songId,
    detail: { chartVersion, draftId, via: "user" },
  });

  const catalogVersion = await readCatalogVersion();

  return c.json({
    songId,
    chartVersion,
    catalogVersion,
    archived: false,
    archivedAt: null,
    draft: { id: draftId, publishedVersion: chartVersion },
  });
});
