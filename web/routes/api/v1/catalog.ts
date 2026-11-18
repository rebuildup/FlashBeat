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
 * `catalogVersion` is the catalog epoch of `catalog_meta` (issue #656):
 * every catalog-visible mutation — publish, archive, unarchive — bumps it
 * inside the same transaction, so polling clients see a change whenever the
 * published catalog changes.
 *
 * Archived songs (issue #151) are not listed. The festival never deletes a
 * song: "removing" one archives it, so the row, its chart snapshots and its
 * scores stay in the database and the song only leaves the public catalog.
 * The staff list (`GET /api/v1/admin/songs`) still shows archived songs.
 */
import { defineHandler } from "void";
import { and, asc, db, eq } from "void/db";
import { chartVersions, songs } from "@schema";
import { buildCatalogResponse, type CatalogRow } from "./_read-model";
import { readCatalogVersion } from "./_catalog-version";

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
    .where(eq(songs.archived, 0))
    .orderBy(asc(songs.id))) as CatalogRow[];

  const catalogVersion = await readCatalogVersion();

  c.header("Cache-Control", "no-store");
  return c.json(buildCatalogResponse(rows, catalogVersion));
});
