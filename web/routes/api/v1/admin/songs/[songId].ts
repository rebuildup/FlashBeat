/**
 * `PUT /api/v1/admin/songs/{songId}` — staff publish endpoint (issue #16).
 *
 * Contract: `contracts/openapi.v1.yaml` (`publishSong`). One atomic upsert:
 * the song metadata is written, the chart text is appended to
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
