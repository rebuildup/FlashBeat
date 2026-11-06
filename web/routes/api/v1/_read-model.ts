/**
 * Shared read model for the festival read API (issue #15).
 *
 * The helpers here are pure: they only map database rows onto the response
 * shapes of `contracts/openapi.v1.yaml`. The route handlers in
 * `routes/api/v1/catalog.ts` and `routes/api/v1/charts/[songId].ts` own the
 * queries, this module owns the shapes.
 */

/**
 * `SongId` path parameter of the contract: `^[a-z0-9-]{3,64}$`.
 *
 * A song id that does not match can never exist in `songs` (the same pattern is
 * a CHECK constraint on the column), so it is answered with 404 like any other
 * unknown song.
 */
export const SONG_ID_PATTERN = /^[a-z0-9-]{3,64}$/;

/** One entry of `GET /api/v1/catalog` (`SongSummary` in the contract). */
export interface CatalogSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  noteCount: number;
}

/** Body of `GET /api/v1/catalog`. */
export interface CatalogResponse {
  catalogVersion: number;
  songs: CatalogSong[];
}

/**
 * Row of the catalog join: the song row plus the note count of its currently
 * published chart version.
 */
export interface CatalogRow {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  currentVersion: number;
  noteCount: number;
}

/** Body of `GET /api/v1/charts/{songId}`. */
export interface ChartResponse {
  songId: string;
  chartVersion: number;
  chart: unknown;
}

/**
 * Map a catalog row onto the contract shape. `songs.currentVersion` is the
 * published chart version, so it is exposed as `chartVersion`.
 */
export function toCatalogSong(row: CatalogRow): CatalogSong {
  return {
    id: row.id,
    title: row.title,
    artist: row.artist,
    youtubeId: row.youtubeId,
    level: row.level,
    bpm: row.bpm,
    durationSec: row.durationSec,
    chartVersion: row.currentVersion,
    noteCount: row.noteCount,
  };
}

/**
 * Build the catalog body. `rows` must already be ordered (the query orders by
 * song id) so the list order is stable between polls.
 */
export function buildCatalogResponse(
  rows: readonly CatalogRow[],
  catalogVersion: number,
): CatalogResponse {
  return {
    catalogVersion,
    songs: rows.map(toCatalogSong),
  };
}

/**
 * Parse the required `version` query parameter of `GET /api/v1/charts/{songId}`.
 *
 * The contract declares it as a required integer with `minimum: 1`, so a
 * missing, non-numeric or out-of-range value is a client error (400) instead
 * of a lookup for a version that cannot exist.
 */
export function parseChartVersion(raw: string | undefined): number | null {
  if (raw === undefined || !/^[0-9]+$/.test(raw)) {
    return null;
  }
  const version = Number(raw);
  if (!Number.isSafeInteger(version) || version < 1) {
    return null;
  }
  return version;
}

/**
 * Build the chart body from the stored chart text.
 *
 * `chart_versions.json` holds the chart file verbatim (the byte identical copy
 * of `Assets/Resources/<song>.json`), so the published chart is identical to
 * the one the Unity player ships with.
 */
export function buildChartResponse(
  songId: string,
  chartVersion: number,
  chartJson: string,
): ChartResponse {
  return {
    songId,
    chartVersion,
    chart: JSON.parse(chartJson),
  };
}
