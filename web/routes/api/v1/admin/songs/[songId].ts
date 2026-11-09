/**
 * `PUT /api/v1/admin/songs/{songId}` — staff publish endpoint (issue #16).
 * `POST /api/v1/admin/songs/{songId}` — staff archive endpoint (issue #151).
 *
 * Contract: `contracts/openapi.v1.yaml` (`publishSong`, `archiveSong`). One
 * atomic upsert: the song metadata is written, the chart text is appended to
 * `chart_versions` as a new immutable snapshot, and `songs.currentVersion`
 * moves to it. `catalogVersion` is `COUNT(chart_versions)` (see the read
 * API), so every successful publish bumps it by one and the response reports
 * the same value `GET /api/v1/catalog` serves.
 *
 * Behaviour:
 *   - 401 — bearer token does not match the `FLASHBEAT_ADMIN_TOKEN` runtime
 *     secret (constant-time compare, see `_publish-model.ts`). Auth is
 *     checked before the body is read, so an unauthenticated caller learns
 *     nothing about the validation.
 *   - 422 — the body or the song id is invalid. The body lists one actionable
 *     diagnostic per problem; a rejected publish writes nothing, so the
 *     already-published chart keeps serving.
 *   - 200 — published; returns the new `chartVersion` and `catalogVersion`.
 *
 * The token is read from the Worker environment per request (`env.ts`); it is
 * never embedded in the client bundle, logged or returned.
 */
import { defineHandler } from "void";
import { env } from "void/env";
import { count, db, eq } from "void/db";
import { chartVersions, songs } from "@schema";
import {
  adminTokenMatches,
  semanticDiagnostics,
  validatePublishRequest,
  type PublishDiagnostic,
} from "../../_publish-model";
import { SONG_ID_PATTERN } from "../../_read-model";
import {
  buildArchiveResponse,
  validateArchiveRequest,
  type ArchiveDiagnostic,
} from "../../_archive-model";

export const PUT = defineHandler(async (c) => {
  const songId = c.req.param("songId") ?? "";

  const authorized = await adminTokenMatches(
    c.req.header("Authorization")?.replace(/^Bearer /, ""),
    env.FLASHBEAT_ADMIN_TOKEN,
  );
  if (!authorized) {
    return c.json({ error: "incorrect staff token" }, 401);
  }

  if (!SONG_ID_PATTERN.test(songId)) {
    return c.json(
      {
        error: "invalid song id",
        diagnostics: [
          {
            path: "songId",
            message: "must match ^[a-z0-9-]{3,64}$ (lowercase letters, digits and hyphens)",
          } satisfies PublishDiagnostic,
        ],
      },
      422,
    );
  }

  let body: unknown;
  try {
    body = await c.req.json();
  } catch {
    return c.json(
      {
        error: "request body is not valid JSON",
        diagnostics: [
          { path: "(body)", message: "must be a single JSON object" } satisfies PublishDiagnostic,
        ],
      },
      422,
    );
  }

  const validation = validatePublishRequest(body);
  if (!validation.ok) {
    return c.json({ error: "invalid publish request", diagnostics: validation.diagnostics }, 422);
  }
  const request = validation.request;

  const semantics = semanticDiagnostics(request);
  if (semantics.length > 0) {
    return c.json({ error: "invalid publish request", diagnostics: semantics }, 422);
  }

  // The new snapshot is appended after the last published one; a first
  // publish starts at version 1.
  const existing = await db
    .select({ currentVersion: songs.currentVersion })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();
  const chartVersion = (existing?.currentVersion ?? 0) + 1;
  const chartJson = JSON.stringify(request.chart);
  const noteCount = (request.chart.notes as unknown[]).length;

  // D1 executes a batch inside one implicit transaction: the song upsert and
  // the immutable chart_versions row commit together or not at all, so a
  // failed publish can never leave a song pointing at a chart that was not
  // stored.
  try {
    await db.batch([
      db
        .insert(songs)
        .values({
          id: songId,
          title: request.title,
          artist: request.artist,
          youtubeId: request.youtubeId,
          bpm: request.chart.BPM as number,
          level: request.level,
          durationSec: request.durationSec,
          currentVersion: chartVersion,
        })
        .onConflictDoUpdate({
          target: songs.id,
          set: {
            title: request.title,
            artist: request.artist,
            youtubeId: request.youtubeId,
            bpm: request.chart.BPM as number,
            level: request.level,
            durationSec: request.durationSec,
            currentVersion: chartVersion,
          },
        }),
      db
        .insert(chartVersions)
        .values({ songId, version: chartVersion, json: chartJson, noteCount }),
    ]);
  } catch {
    // Two concurrent publishes of the same song can compute the same version;
    // the primary key of chart_versions rejects the loser.
    return c.json(
      {
        error: "concurrent publish",
        diagnostics: [
          {
            path: "(request)",
            message: `version ${chartVersion} of "${songId}" was just published by another request; retry to publish as the next version`,
          } satisfies PublishDiagnostic,
        ],
      },
      409,
    );
  }

  const [total] = await db.select({ value: count() }).from(chartVersions);

  return c.json({ songId, chartVersion, catalogVersion: total.value });
});

/**
 * `POST /api/v1/admin/songs/{songId}` — archive or unarchive a song (issue #151).
 *
 * The festival never deletes a song. This route flips the `songs.archived`
 * flag: an archived song leaves the public catalog (`GET /api/v1/catalog`)
 * but keeps its row, its chart snapshots and its scores, and it can be brought
 * back with `{"archived": false}`. There is no `DELETE` route for a song.
 *
 * Behaviour:
 *   - 401 — the staff token does not match (same constant-time compare as
 *     the publish route). Auth is checked before the body is read.
 *   - 404 — no song with that id exists.
 *   - 422 — the body is not `{"archived": <boolean>}`; one diagnostic per
 *     problem, nothing is written.
 *   - 200 — the flag was written; the answer reports the new state and the
 *     current `catalogVersion`.
 */
export const POST = defineHandler(async (c) => {
  const songId = c.req.param("songId") ?? "";

  const authorized = await adminTokenMatches(
    c.req.header("Authorization")?.replace(/^Bearer /, ""),
    env.FLASHBEAT_ADMIN_TOKEN,
  );
  if (!authorized) {
    return c.json({ error: "incorrect staff token" }, 401);
  }

  if (!SONG_ID_PATTERN.test(songId)) {
    return c.json(
      {
        error: "invalid song id",
        diagnostics: [
          {
            path: "songId",
            message: "must match ^[a-z0-9-]{3,64}$ (lowercase letters, digits and hyphens)",
          } satisfies ArchiveDiagnostic,
        ],
      },
      422,
    );
  }

  let body: unknown;
  try {
    body = await c.req.json();
  } catch {
    return c.json(
      {
        error: "request body is not valid JSON",
        diagnostics: [
          { path: "(body)", message: "must be a single JSON object" } satisfies ArchiveDiagnostic,
        ],
      },
      422,
    );
  }

  const validation = validateArchiveRequest(body);
  if (!validation.ok) {
    return c.json({ error: "invalid archive request", diagnostics: validation.diagnostics }, 422);
  }
  const archived = validation.request.archived;

  const existing = await db
    .select({ archived: songs.archived })
    .from(songs)
    .where(eq(songs.id, songId))
    .get();
  if (!existing) {
    return c.json({ error: "song not found" }, 404);
  }

  // The archive moment is written by the server; the client never chooses it.
  const archivedAt = archived ? new Date().toISOString() : null;
  await db
    .update(songs)
    .set({ archived: archived ? 1 : 0, archivedAt })
    .where(eq(songs.id, songId));

  const [total] = await db.select({ value: count() }).from(chartVersions);

  return c.json(buildArchiveResponse(songId, archived, archivedAt, total.value));
});
