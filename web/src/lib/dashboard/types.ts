/**
 * Types of the dashboard API (issue #151).
 *
 * They mirror the schemas of `contracts/openapi.v1.yaml` (`RankingSong`,
 * `StatsResponse`, `AdminSong`), so the dashboard and the API always mean the
 * same thing. The client parses the answers field by field: a broken answer
 * is an error and not a half filled table.
 */

/** One entry of a leaderboard. */
export interface RankingEntry {
  nickname: string;
  score: number;
}

/** The ranking board of one song. */
export interface RankingSong {
  songId: string;
  title: string;
  artist: string;
  level: number;
  chartVersion: number;
  plays: number;
  entries: RankingEntry[];
}

/** Body of `GET /api/v1/rankings`. */
export interface RankingsResponse {
  catalogVersion: number;
  songs: RankingSong[];
}

/** The statistics of one song. */
export interface SongStats {
  songId: string;
  title: string;
  artist: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  archived: boolean;
  plays: number;
  distinctPlayers: number;
  averageScore: number | null;
  bestScore: number | null;
  averageAccuracy: number | null;
}

/** One player of the "best players" list. */
export interface PlayerStats {
  nickname: string;
  plays: number;
  totalScore: number;
  bestScore: number;
}

/** The totals of the festival. */
export interface StatsTotals {
  songs: number;
  liveSongs: number;
  archivedSongs: number;
  charts: number;
  scores: number;
  players: number;
  playsLast24h: number;
}

/** Body of `GET /api/v1/stats`. */
export interface StatsResponse {
  generatedAt: string;
  totals: StatsTotals;
  perSong: SongStats[];
  levelDistribution: { level: number; songs: number }[];
  topPlayers: PlayerStats[];
}

/** One song of the staff list (`GET /api/v1/admin/songs`). */
export interface AdminSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  archived: boolean;
  archivedAt: string | null;
  plays: number;
  bestScore: number | null;
}

/** Body of `GET /api/v1/admin/songs`. */
export interface AdminSongsResponse {
  catalogVersion: number;
  songs: AdminSong[];
}

/** Body of `POST /api/v1/admin/songs/{songId}` (the archive route). */
export interface ArchiveResponse {
  songId: string;
  archived: boolean;
  archivedAt: string | null;
  catalogVersion: number;
}
