/**
 * `GET /api/v1/catalog` — public list of the published songs.
 *
 * Contract: `contracts/openapi.v1.yaml` (`getCatalog`). Unity refreshes this
 * on the select screen and every 5-10s while selecting, so the response is
 * never cached (`Cache-Control: no-store`).
 *
 * Only songs with a published chart version are listed: the join on
 * `chart_versions(songId, version = songs.currentVersion)` is what makes a
 * song "published", and it also guarantees that every entry carries the note
 * count the contract requires.
 *
 * `catalogVersion` is the number of published chart snapshots
 * (`COUNT(chart_versions)`). It is a simple, monotonic counter: every publish
 * adds a chart version, so the number changes whenever the published charts
 * change. The publish endpoint (#16) returns the same value.
 */
import { defineHandler } from "void";
import { and, asc, count, db, eq } from "void/db";
import { chartVersions, songs } from "@schema";
import { buildCatalogResponse, type CatalogRow } from "./_read-model";

export const GET = defineHandler(async (c) => {
  const rows = (await db
    .select({
      id: songs.id,
      title: songs.title,
      artist: songs.artist,
      youtubeId: songs.youtubeId,
      level: songs.level,
      bpm: songs.bpm,
      durationSec: songs.durationSec,
      currentVersion: songs.currentVersion,
      noteCount: chartVersions.noteCount,
    })
    .from(songs)
    .innerJoin(
      chartVersions,
      and(eq(chartVersions.songId, songs.id), eq(chartVersions.version, songs.currentVersion)),
    )
    .orderBy(asc(songs.id))) as CatalogRow[];

  const [total] = await db.select({ value: count() }).from(chartVersions);

  c.header("Cache-Control", "no-store");
  return c.json(buildCatalogResponse(rows, total.value));
});
