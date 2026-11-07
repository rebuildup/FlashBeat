import { validatePublishBody } from "../publish/mock";
import type { PublishFetch } from "../publish/types";

/**
 * In-repo fixture of the whole studio API for the new-song flow.
 *
 * `createMockPublishFetch` answers only the PUT route, but the flow reads the
 * catalog back (`publishAndVerify`) and checks `songId` uniqueness against
 * `GET /api/v1/catalog`. This fixture serves both sides of the contract in
 * memory, exactly like `routes/api/v1/**` does against D1. It writes nothing
 * and is used by the vitest suite and by the dev-only `fixture` transport.
 */

const PUBLISH_PATH = /^\/api\/v1\/admin\/songs\/([^/]+)$/;
const CHART_PATH = /^\/api\/v1\/charts\/([^/]+)$/;

interface FixtureSong {
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  bpm: number;
  chart: unknown;
  chartVersion: number;
  /** `chart.notes.length`, kept at publish time so readers never cast. */
  noteCount: number;
}

export interface FixtureStudioFetch extends PublishFetch {
  /** Published state, for assertions. */
  readonly songs: ReadonlyMap<string, FixtureSong>;
}

export interface FixtureStudioOptions {
  /** Token the fixture accepts. Defaults to `fixture-token`. */
  token?: string;
}

export function createFixtureStudioFetch(options: FixtureStudioOptions = {}): FixtureStudioFetch {
  const expectedToken = options.token ?? "fixture-token";
  const songs = new Map<string, FixtureSong>();

  const fetchImpl = (async (input, init) => {
    const url = new URL(input, "http://fixture.local");
    const method = (init?.method ?? "GET").toUpperCase();

    const publishMatch = PUBLISH_PATH.exec(url.pathname);
    if (publishMatch) {
      return handlePublish(decodeURIComponent(publishMatch[1] ?? ""), method, init, expectedToken, songs);
    }

    if (url.pathname === "/api/v1/catalog" && method === "GET") {
      return jsonResponse(200, {
        catalogVersion: totalVersions(songs),
        songs: catalogSongs(songs),
      });
    }

    const chartMatch = CHART_PATH.exec(url.pathname);
    if (chartMatch && method === "GET") {
      const songId = decodeURIComponent(chartMatch[1] ?? "");
      const version = Number(url.searchParams.get("version") ?? "");
      const song = songs.get(songId);
      if (!song || !Number.isInteger(version) || version !== song.chartVersion) {
        return jsonResponse(404, { error: "No such chart version" });
      }
      return jsonResponse(200, { songId, chartVersion: song.chartVersion, chart: song.chart });
    }

    return jsonResponse(404, { error: `No fixture route for ${method} ${url.pathname}` });
  }) as FixtureStudioFetch;

  Object.defineProperty(fetchImpl, "songs", {
    get: () => songs as ReadonlyMap<string, FixtureSong>,
    enumerable: true,
  });

  return fetchImpl;
}

function handlePublish(
  songId: string,
  method: string,
  init: RequestInit | undefined,
  expectedToken: string,
  songs: Map<string, FixtureSong>,
): Response {
  if (method !== "PUT") {
    return jsonResponse(405, { error: `The publish route does not answer ${method}` });
  }

  const headers = new Headers(init?.headers);
  if (headers.get("authorization") !== `Bearer ${expectedToken}`) {
    return jsonResponse(401, { error: "Incorrect staff token" });
  }

  let body: unknown;
  try {
    body = JSON.parse(typeof init?.body === "string" ? init.body : "");
  } catch {
    return jsonResponse(422, { error: "The request body is not valid JSON" });
  }

  const issues = validatePublishBody(body);
  if (issues.length > 0) {
    return jsonResponse(422, { error: "Invalid song", issues });
  }

  // validatePublishBody has checked every field below; the cast only names
  // the validated shape.
  const request = body as {
    title: string;
    artist: string;
    youtubeId: string;
    level: number;
    durationSec: number;
    chart: { BPM: number; notes: unknown[] };
  };

  const previous = songs.get(songId);
  const chartVersion = (previous?.chartVersion ?? 0) + 1;
  songs.set(songId, {
    title: request.title,
    artist: request.artist,
    youtubeId: request.youtubeId,
    level: request.level,
    durationSec: request.durationSec,
    bpm: request.chart.BPM,
    chart: request.chart,
    chartVersion,
    noteCount: request.chart.notes.length,
  });

  return jsonResponse(200, { songId, chartVersion, catalogVersion: totalVersions(songs) });
}

function totalVersions(songs: ReadonlyMap<string, FixtureSong>): number {
  let total = 0;
  for (const song of songs.values()) {
    total += song.chartVersion;
  }
  return total;
}

function catalogSongs(songs: ReadonlyMap<string, FixtureSong>) {
  return [...songs.entries()]
    .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0))
    .map(([id, song]) => ({
      id,
      title: song.title,
      artist: song.artist,
      youtubeId: song.youtubeId,
      level: song.level,
      bpm: song.bpm,
      durationSec: song.durationSec,
      chartVersion: song.chartVersion,
      noteCount: song.noteCount,
    }));
}

function jsonResponse(status: number, body: unknown): Response {
  return Response.json(body, {
    status,
    headers: { "Cache-Control": "no-store" },
  });
}
