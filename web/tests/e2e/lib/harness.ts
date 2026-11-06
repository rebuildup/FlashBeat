/**
 * Stage-by-stage production verification harness (issue #97, refs #41).
 *
 * Pure logic of the E2E harness: every stage takes its transport (`fetchImpl`)
 * as a parameter, so the unit tests can mock `fetch` and the CLI (`run.ts`)
 * passes the global one. Each check becomes one report entry
 * `{ stage, expected, actual, pass }`; nothing here throws on a failed check.
 */
import Ajv2020 from "ajv/dist/2020.js";
import type { ErrorObject } from "ajv";
import chartSchema from "../../../../contracts/schemas/chart.v1.schema.json" with { type: "json" };
import { publishAndVerify } from "../../../src/lib/publish/client.ts";
import type { PublishFetch, SongPublishRequest } from "../../../src/lib/publish/types.ts";

/** One entry of the JSON report. */
export interface StageEntry {
  stage: string;
  expected: unknown;
  actual: unknown;
  pass: boolean;
}

/** One entry of `GET /api/v1/catalog` (`SongSummary` of the contract). */
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
export interface Catalog {
  catalogVersion: number;
  songs: CatalogSong[];
}

/** Body of `GET /api/v1/charts/{songId}?version=N`. */
export interface ChartSnapshot {
  songId: string;
  chartVersion: number;
  chart: unknown;
}

export type FetchJson = (
  url: string,
  init?: RequestInit,
) => Promise<{ status: number; body: unknown }>;

/** `fetch` + JSON parse; a body that is not JSON is reported as `null`. */
export function makeFetchJson(fetchImpl: PublishFetch): FetchJson {
  return async (url, init) => {
    const response = await fetchImpl(url, init);
    let body: unknown = null;
    try {
      body = await response.json();
    } catch {
      body = null;
    }
    return { status: response.status, body };
  };
}

/** Build one report entry. */
export function entry(
  stage: string,
  expected: unknown,
  actual: unknown,
  pass: boolean,
): StageEntry {
  return { stage, expected, actual, pass };
}

/**
 * Structural equality of two JSON values, key order insensitive.
 * Same rules as `sameJson` in `src/lib/publish/client.ts`.
 */
export function sameJson(left: unknown, right: unknown): boolean {
  if (left === right) return true;
  if (left === null || right === null || typeof left !== "object" || typeof right !== "object")
    return false;
  if (Array.isArray(left) !== Array.isArray(right)) return false;
  const a = Object.keys(left).sort();
  const b = Object.keys(right).sort();
  return (
    a.length === b.length &&
    a.every(
      (key, index) =>
        key === b[index] &&
        sameJson((left as Record<string, unknown>)[key], (right as Record<string, unknown>)[key]),
    )
  );
}

/** The YouTube video id shape of the contract (`youtubeId` metadata field). */
export const YOUTUBE_ID_PATTERN = /^[\w-]{11}$/;

/**
 * Stage 1: `GET /api/v1/catalog`.
 *
 * Records `catalogVersion`, the songs and their ids; verifies the response
 * status and the shape the read API froze (issue #15).
 */
export async function catalogStage(
  fetchJson: FetchJson,
  baseUrl: string,
): Promise<{ entries: StageEntry[]; catalog: Catalog | null }> {
  const entries: StageEntry[] = [];
  const { status, body } = await fetchJson(`${baseUrl}/api/v1/catalog`, { cache: "no-store" });
  entries.push(entry("catalog.status", 200, status, status === 200));

  const catalog = body as Catalog | null;
  const shaped =
    catalog !== null &&
    typeof catalog === "object" &&
    Number.isInteger(catalog.catalogVersion) &&
    catalog.catalogVersion >= 0 &&
    Array.isArray(catalog.songs) &&
    catalog.songs.every(
      (song) =>
        typeof song?.id === "string" &&
        song.id.length > 0 &&
        typeof song?.title === "string" &&
        typeof song?.artist === "string" &&
        typeof song?.youtubeId === "string" &&
        Number.isInteger(song?.chartVersion) &&
        song.chartVersion >= 1 &&
        Number.isInteger(song?.noteCount) &&
        song.noteCount >= 0,
    );
  entries.push(
    entry(
      "catalog.shape",
      "catalogVersion integer >= 0, songs[] with id/title/artist/youtubeId/chartVersion/noteCount",
      describeBody(body),
      shaped,
    ),
  );
  if (!shaped || catalog === null) {
    return { entries, catalog: null };
  }

  entries.push(
    entry(
      "catalog.catalogVersion",
      "integer >= 0",
      catalog.catalogVersion,
      Number.isInteger(catalog.catalogVersion) && catalog.catalogVersion >= 0,
    ),
  );

  const ids = catalog.songs.map((song) => song.id);
  const sortedUnique = ids.every(
    (id, index) => (index === 0 || ids[index - 1] < id) && ids.indexOf(id) === index,
  );
  entries.push(
    entry("catalog.songIds.sortedUnique", "ascending, duplicate free", ids, sortedUnique),
  );

  return { entries, catalog };
}

/** Pick the catalog entry the later stages use. */
export function pickSong(
  catalog: Catalog,
  songId: string | null,
): { entry: StageEntry; song: CatalogSong | null } {
  if (songId === null) {
    const song = catalog.songs[0] ?? null;
    return {
      entry: entry("chart.songPicked", "first catalog entry", song?.id ?? null, song !== null),
      song,
    };
  }
  const song = catalog.songs.find((candidate) => candidate.id === songId) ?? null;
  return { entry: entry("chart.songPicked", songId, song?.id ?? null, song !== null), song };
}

/**
 * Stage 2: `GET /api/v1/charts/{songId}?version=N` for the catalog entry.
 *
 * Verifies the identity pair (`songId`, `chartVersion`), the catalog
 * `noteCount` against `chart.notes.length`, and the catalog `youtubeId`.
 * The legacy chart format carries no `youtubeId`, so the video id can only be
 * checked on the catalog entry itself (well-formed 11 character id).
 */
export async function chartStage(
  fetchJson: FetchJson,
  baseUrl: string,
  song: CatalogSong,
): Promise<{ entries: StageEntry[]; snapshot: ChartSnapshot | null }> {
  const entries: StageEntry[] = [];
  const url = `${baseUrl}/api/v1/charts/${encodeURIComponent(song.id)}?version=${song.chartVersion}`;
  const { status, body } = await fetchJson(url, { cache: "no-store" });
  entries.push(entry("chart.status", 200, status, status === 200));

  const snapshot = body as ChartSnapshot | null;
  const notes = (snapshot?.chart as { notes?: unknown } | null)?.notes;
  const noteCount = Array.isArray(notes) ? notes.length : null;
  const chartVersion = typeof snapshot?.chartVersion === "number" ? snapshot.chartVersion : null;
  const songId = typeof snapshot?.songId === "string" ? snapshot.songId : null;

  entries.push(entry("chart.songId", song.id, songId, songId === song.id));
  entries.push(
    entry(
      "chart.chartVersion",
      song.chartVersion,
      chartVersion,
      chartVersion === song.chartVersion,
    ),
  );
  entries.push(entry("chart.noteCount", song.noteCount, noteCount, noteCount === song.noteCount));
  entries.push(
    entry(
      "catalog.youtubeId",
      "11 character video id",
      song.youtubeId,
      YOUTUBE_ID_PATTERN.test(song.youtubeId),
    ),
  );

  return { entries, snapshot: status === 200 ? snapshot : null };
}

/** Ajv issues of one value against the frozen chart contract. */
export function chartSchemaIssues(chart: unknown): Array<{ path: string; message: string }> {
  const ajv = new Ajv2020({ allErrors: true, strict: false });
  const validate = ajv.compile(chartSchema);
  if (validate(chart)) {
    return [];
  }
  return (validate.errors ?? []).map((error: ErrorObject) => ({
    path: error.instancePath,
    message: error.message ?? "invalid value",
  }));
}

/**
 * Stage 3: validate the snapshot chart against
 * `contracts/schemas/chart.v1.schema.json`.
 */
export function schemaStage(snapshot: ChartSnapshot | null): StageEntry[] {
  const issues = chartSchemaIssues(snapshot?.chart ?? null);
  return [
    entry(
      "chart.schema",
      "valid per contracts/schemas/chart.v1.schema.json",
      issues.length === 0 ? "valid" : issues,
      issues.length === 0,
    ),
  ];
}

/** Options of the optional write stage. */
export interface WriteStageOptions {
  baseUrl: string;
  songId: string;
  token: string;
  request: SongPublishRequest;
  fetchImpl: PublishFetch;
  /** `catalogVersion` and `chartVersion` observed before the publish. */
  catalogVersionBefore: number;
  chartVersionBefore: number;
}

/**
 * Stage 4 (optional, `--write`): publish a modified copy with the existing
 * `publishAndVerify` client, then re-GET the catalog and the chart and assert
 * `chartVersion + 1`, `catalogVersion + 1` and exact snapshot equality.
 */
export async function writeStage(options: WriteStageOptions): Promise<StageEntry[]> {
  const { baseUrl, songId, token, request, fetchImpl } = options;
  const entries: StageEntry[] = [];

  const result = await publishAndVerify({ baseUrl, songId, token, request, fetchImpl });
  entries.push(
    entry(
      "write.publishResponse",
      {
        ok: true,
        songId,
        chartVersion: options.chartVersionBefore + 1,
        catalogVersion: options.catalogVersionBefore + 1,
      },
      result.ok
        ? { ok: true, ...result.response }
        : { ok: false, code: result.failure.code, message: result.failure.message },
      result.ok &&
        result.response.songId === songId &&
        result.response.chartVersion === options.chartVersionBefore + 1 &&
        result.response.catalogVersion === options.catalogVersionBefore + 1,
    ),
  );
  if (!result.ok) {
    return entries;
  }

  const fetchJson = makeFetchJson(fetchImpl);
  const catalogResult = await fetchJson(`${baseUrl}/api/v1/catalog`, { cache: "no-store" });
  const catalog = catalogResult.body as Catalog | null;
  const republished = catalog?.songs?.find((candidate) => candidate.id === songId) ?? null;
  entries.push(
    entry(
      "write.catalogVersion",
      options.catalogVersionBefore + 1,
      catalog?.catalogVersion ?? null,
      catalog?.catalogVersion === options.catalogVersionBefore + 1,
    ),
  );
  entries.push(
    entry(
      "write.catalogChartVersion",
      options.chartVersionBefore + 1,
      republished?.chartVersion ?? null,
      republished?.chartVersion === options.chartVersionBefore + 1,
    ),
  );
  entries.push(
    entry(
      "write.catalogNoteCount",
      request.chart.notes.length,
      republished?.noteCount ?? null,
      republished?.noteCount === request.chart.notes.length,
    ),
  );

  const chartResult = await fetchJson(
    `${baseUrl}/api/v1/charts/${encodeURIComponent(songId)}?version=${options.chartVersionBefore + 1}`,
    { cache: "no-store" },
  );
  const snapshot = chartResult.body as ChartSnapshot | null;
  entries.push(
    entry("write.snapshot.songId", songId, snapshot?.songId ?? null, snapshot?.songId === songId),
  );
  entries.push(
    entry(
      "write.snapshot.chartVersion",
      options.chartVersionBefore + 1,
      snapshot?.chartVersion ?? null,
      snapshot?.chartVersion === options.chartVersionBefore + 1,
    ),
  );
  entries.push(
    entry(
      "write.snapshot.exact",
      request.chart,
      snapshot?.chart ?? null,
      sameJson(request.chart, snapshot?.chart ?? null),
    ),
  );

  return entries;
}

/** Short body description for report entries, never a secret carrier. */
function describeBody(body: unknown): string {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return `unexpected body: ${typeof body}`;
  }
  return "object";
}

/** Build a `SongPublishRequest` from a catalog entry and its chart snapshot. */
export function buildPublishRequest(
  song: CatalogSong,
  snapshot: ChartSnapshot,
  modify: (chart: Record<string, unknown>) => void,
): SongPublishRequest {
  const chart = structuredClone(snapshot.chart) as Record<string, unknown>;
  modify(chart);
  return {
    title: song.title,
    artist: song.artist,
    youtubeId: song.youtubeId,
    level: song.level,
    durationSec: song.durationSec,
    chart: chart as SongPublishRequest["chart"],
  };
}
