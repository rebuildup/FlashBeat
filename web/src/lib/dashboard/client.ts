/**
 * Client of the dashboard API (issue #151).
 *
 * The studio and the API are served by the same worker, so every request
 * goes to `/api/v1/…` of the current origin and lands in the D1 of that
 * deployment.
 *
 * The parsers are pure and exported: the vitest suite pins them without a
 * server, and the UI can never render a half filled table — an answer that
 * does not have the shape of the contract is an error.
 */
import type {
  AdminSong,
  AdminSongsResponse,
  ArchiveResponse,
  PlayerStats,
  RankingEntry,
  RankingSong,
  RankingsResponse,
  SongStats,
  StatsResponse,
  StatsTotals,
} from "./types";

/** A JSON object with unknown members, or null for everything else. */
function asObject(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  return Object.fromEntries(Object.entries(value));
}

function asNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function asString(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

/** A number that may be `null` (the API reports "nobody played it" as null). */
function asNullableNumber(value: unknown): number | null {
  return asNumber(value);
}

/**
 * Read the ranking board.
 *
 * The API validates its answer against the frozen contract; the fields the
 * dashboard renders are narrowed here.
 */
export function parseRankings(body: unknown): RankingsResponse {
  const answer = asObject(body);
  if (answer === null) {
    throw new Error("GET /api/v1/rankings answered no object");
  }
  const catalogVersion = asNumber(answer.catalogVersion);
  if (catalogVersion === null || !Array.isArray(answer.songs)) {
    throw new Error("GET /api/v1/rankings answered a broken board");
  }

  return {
    catalogVersion,
    songs: answer.songs.map((entry) => parseRankingSong(entry)),
  };
}

function parseRankingSong(value: unknown): RankingSong {
  const entry = asObject(value);
  if (entry === null) {
    throw new Error("GET /api/v1/rankings answered a broken song");
  }
  const songId = asString(entry.songId);
  const title = asString(entry.title);
  const artist = asString(entry.artist);
  const level = asNumber(entry.level);
  const chartVersion = asNumber(entry.chartVersion);
  const plays = asNumber(entry.plays);
  if (
    songId === null ||
    title === null ||
    artist === null ||
    level === null ||
    chartVersion === null ||
    plays === null ||
    !Array.isArray(entry.entries)
  ) {
    throw new Error("GET /api/v1/rankings answered a broken song");
  }

  return {
    songId,
    title,
    artist,
    level,
    chartVersion,
    plays,
    entries: entry.entries.map((item) => parseRankingEntry(item)),
  };
}

function parseRankingEntry(value: unknown): RankingEntry {
  const entry = asObject(value);
  if (entry === null) {
    throw new Error("GET /api/v1/rankings answered a broken entry");
  }
  const nickname = asString(entry.nickname);
  const score = asNumber(entry.score);
  if (nickname === null || score === null) {
    throw new Error("GET /api/v1/rankings answered a broken entry");
  }
  return { nickname, score };
}

/** Read the statistics answer. */
export function parseStats(body: unknown): StatsResponse {
  const answer = asObject(body);
  if (answer === null) {
    throw new Error("GET /api/v1/stats answered no object");
  }
  const generatedAt = asString(answer.generatedAt);
  const totals = asObject(answer.totals);
  if (generatedAt === null || totals === null) {
    throw new Error("GET /api/v1/stats answered a broken summary");
  }
  if (!Array.isArray(answer.perSong) || !Array.isArray(answer.levelDistribution)) {
    throw new Error("GET /api/v1/stats answered a broken summary");
  }
  if (!Array.isArray(answer.topPlayers)) {
    throw new Error("GET /api/v1/stats answered a broken summary");
  }

  return {
    generatedAt,
    totals: parseTotals(totals),
    perSong: answer.perSong.map((item) => parseSongStats(item)),
    levelDistribution: answer.levelDistribution.map((item) => {
      const entry = asObject(item);
      const level = entry === null ? null : asNumber(entry.level);
      const songs = entry === null ? null : asNumber(entry.songs);
      if (level === null || songs === null) {
        throw new Error("GET /api/v1/stats answered a broken level distribution");
      }
      return { level, songs };
    }),
    topPlayers: answer.topPlayers.map((item) => parsePlayerStats(item)),
  };
}

function parseTotals(totals: Record<string, unknown>): StatsTotals {
  const numbers: [keyof StatsTotals, number | null][] = [
    ["songs", asNumber(totals.songs)],
    ["liveSongs", asNumber(totals.liveSongs)],
    ["archivedSongs", asNumber(totals.archivedSongs)],
    ["charts", asNumber(totals.charts)],
    ["scores", asNumber(totals.scores)],
    ["players", asNumber(totals.players)],
    ["playsLast24h", asNumber(totals.playsLast24h)],
  ];
  const broken = numbers.filter(([, value]) => value === null);
  if (broken.length > 0) {
    throw new Error("GET /api/v1/stats answered a broken summary");
  }
  return Object.fromEntries(numbers) as unknown as StatsTotals;
}

function parseSongStats(value: unknown): SongStats {
  const entry = asObject(value);
  if (entry === null) {
    throw new Error("GET /api/v1/stats answered a broken song");
  }
  const songId = asString(entry.songId);
  const title = asString(entry.title);
  const artist = asString(entry.artist);
  const level = asNumber(entry.level);
  const bpm = asNumber(entry.bpm);
  const durationSec = asNumber(entry.durationSec);
  const chartVersion = asNumber(entry.chartVersion);
  const plays = asNumber(entry.plays);
  const distinctPlayers = asNumber(entry.distinctPlayers);
  if (
    songId === null ||
    title === null ||
    artist === null ||
    level === null ||
    bpm === null ||
    durationSec === null ||
    chartVersion === null ||
    plays === null ||
    distinctPlayers === null ||
    typeof entry.archived !== "boolean"
  ) {
    throw new Error("GET /api/v1/stats answered a broken song");
  }

  return {
    songId,
    title,
    artist,
    level,
    bpm,
    durationSec,
    chartVersion,
    archived: entry.archived,
    plays,
    distinctPlayers,
    averageScore: asNullableNumber(entry.averageScore),
    bestScore: asNullableNumber(entry.bestScore),
    averageAccuracy: asNullableNumber(entry.averageAccuracy),
  };
}

function parsePlayerStats(value: unknown): PlayerStats {
  const entry = asObject(value);
  if (entry === null) {
    throw new Error("GET /api/v1/stats answered a broken player");
  }
  const nickname = asString(entry.nickname);
  const plays = asNumber(entry.plays);
  const totalScore = asNumber(entry.totalScore);
  const bestScore = asNumber(entry.bestScore);
  if (nickname === null || plays === null || totalScore === null || bestScore === null) {
    throw new Error("GET /api/v1/stats answered a broken player");
  }
  return { nickname, plays, totalScore, bestScore };
}

/** Read the staff song list. */
export function parseAdminSongs(body: unknown): AdminSongsResponse {
  const answer = asObject(body);
  if (answer === null) {
    throw new Error("GET /api/v1/admin/songs answered no object");
  }
  const catalogVersion = asNumber(answer.catalogVersion);
  if (catalogVersion === null || !Array.isArray(answer.songs)) {
    throw new Error("GET /api/v1/admin/songs answered a broken list");
  }
  return {
    catalogVersion,
    songs: answer.songs.map((item) => parseAdminSong(item)),
  };
}

function parseAdminSong(value: unknown): AdminSong {
  const entry = asObject(value);
  if (entry === null) {
    throw new Error("GET /api/v1/admin/songs answered a broken song");
  }
  const id = asString(entry.id);
  const title = asString(entry.title);
  const artist = asString(entry.artist);
  const youtubeId = asString(entry.youtubeId);
  const level = asNumber(entry.level);
  const bpm = asNumber(entry.bpm);
  const durationSec = asNumber(entry.durationSec);
  const chartVersion = asNumber(entry.chartVersion);
  const plays = asNumber(entry.plays);
  if (
    id === null ||
    title === null ||
    artist === null ||
    youtubeId === null ||
    level === null ||
    bpm === null ||
    durationSec === null ||
    chartVersion === null ||
    plays === null ||
    typeof entry.archived !== "boolean"
  ) {
    throw new Error("GET /api/v1/admin/songs answered a broken song");
  }
  return {
    id,
    title,
    artist,
    youtubeId,
    level,
    bpm,
    durationSec,
    chartVersion,
    archived: entry.archived,
    archivedAt: asString(entry.archivedAt),
    plays,
    bestScore: asNullableNumber(entry.bestScore),
  };
}

/** Read the answer of the archive route. */
export function parseArchiveResponse(body: unknown): ArchiveResponse {
  const answer = asObject(body);
  if (answer === null) {
    throw new Error("the archive request answered no object");
  }
  const songId = asString(answer.songId);
  const catalogVersion = asNumber(answer.catalogVersion);
  if (songId === null || catalogVersion === null || typeof answer.archived !== "boolean") {
    throw new Error("the archive request answered a broken answer");
  }
  return {
    songId,
    archived: answer.archived,
    archivedAt: asString(answer.archivedAt),
    catalogVersion,
  };
}

/** Load the ranking board of every live song. */
export async function loadRankings(limit?: number): Promise<RankingsResponse> {
  const query = limit === undefined ? "" : `?limit=${encodeURIComponent(String(limit))}`;
  const response = await fetch(`/api/v1/rankings${query}`, {
    headers: { accept: "application/json" },
  });
  if (!response.ok) {
    throw new Error(`GET /api/v1/rankings answered ${response.status}`);
  }
  return parseRankings(await response.json());
}

/** Load the festival statistics. */
export async function loadStats(): Promise<StatsResponse> {
  const response = await fetch("/api/v1/stats", { headers: { accept: "application/json" } });
  if (!response.ok) {
    throw new Error(`GET /api/v1/stats answered ${response.status}`);
  }
  return parseStats(await response.json());
}

/** Load the staff song list (archived songs included). */
export async function loadAdminSongs(token: string): Promise<AdminSongsResponse> {
  const response = await fetch("/api/v1/admin/songs", {
    headers: { accept: "application/json", authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new Error(`GET /api/v1/admin/songs answered ${response.status}`);
  }
  return parseAdminSongs(await response.json());
}

/**
 * Archive or unarchive a song.
 *
 * The festival never deletes a song: this flips the archive flag, so the
 * song leaves the public catalog and keeps its chart snapshots and scores.
 */
export async function setSongArchived(
  token: string,
  songId: string,
  archived: boolean,
): Promise<ArchiveResponse> {
  const response = await fetch(`/api/v1/admin/songs/${encodeURIComponent(songId)}`, {
    method: "POST",
    headers: {
      accept: "application/json",
      authorization: `Bearer ${token}`,
      "content-type": "application/json",
    },
    body: JSON.stringify({ archived }),
  });
  if (!response.ok) {
    throw new Error(await readArchiveFailure(response));
  }
  return parseArchiveResponse(await response.json());
}

/** The reason of a rejected archive request. */
async function readArchiveFailure(response: Response): Promise<string> {
  if (response.status === 401) {
    return "The staff token was rejected.";
  }
  if (response.status === 404) {
    return "The song does not exist.";
  }
  try {
    const body = (await response.json()) as { error?: string };
    if (typeof body.error === "string") {
      return body.error;
    }
  } catch {
    // The answer was not JSON; the status is all we have.
  }
  return `The archive request answered ${response.status}.`;
}
