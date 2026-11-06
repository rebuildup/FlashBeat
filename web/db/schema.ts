/**
 * D1 schema for the festival API (contracts/openapi.v1.yaml).
 *
 * Three tables, owned by issue #14:
 *   songs         — published song metadata + the currently published version
 *   chart_versions— immutable chart snapshot per (song, version)
 *   scores        — public score submissions
 *
 * Column names and types mirror the contract 1:1. Constraints are expressed as
 * SQLite CHECK constraints so a bad row can never reach the API response
 * shape, no matter which client writes it.
 */
import { sql } from "void/db";
import { check, index, integer, primaryKey, real, sqliteTable, text } from "void/schema-d1";

export const songs = sqliteTable(
  "songs",
  {
    id: text("id").primaryKey(),
    title: text("title").notNull(),
    artist: text("artist").notNull().default(""),
    youtubeId: text("youtubeId").notNull(),
    bpm: integer("bpm").notNull(),
    level: integer("level").notNull(),
    durationSec: real("durationSec").notNull(),
    currentVersion: integer("currentVersion").notNull().default(1),
  },
  (table) => [
    // SongId parameter of the contract: ^[a-z0-9-]{3,64}$
    check(
      "songs_id_format",
      sql`length(${table.id}) BETWEEN 3 AND 64 AND ${table.id} NOT GLOB '*[^a-z0-9-]*'`,
    ),
    check("songs_title_length", sql`length(${table.title}) BETWEEN 1 AND 160`),
    check("songs_artist_length", sql`length(${table.artist}) <= 160`),
    // youtubeId: ^[A-Za-z0-9_-]{11}$
    check(
      "songs_youtube_id_format",
      sql`length(${table.youtubeId}) = 11 AND ${table.youtubeId} NOT GLOB '*[^A-Za-z0-9_-]*'`,
    ),
    check("songs_bpm_range", sql`${table.bpm} BETWEEN 1 AND 600`),
    check("songs_level_range", sql`${table.level} BETWEEN 1 AND 99`),
    check("songs_duration_range", sql`${table.durationSec} > 0 AND ${table.durationSec} <= 1800`),
    check("songs_current_version_positive", sql`${table.currentVersion} >= 1`),
  ],
);

export const chartVersions = sqliteTable(
  "chart_versions",
  {
    songId: text("songId")
      .notNull()
      .references(() => songs.id, { onDelete: "cascade" }),
    version: integer("version").notNull(),
    json: text("json").notNull(),
    noteCount: integer("noteCount").notNull(),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    primaryKey({ columns: [table.songId, table.version] }),
    check("chart_versions_version_positive", sql`${table.version} >= 1`),
    check("chart_versions_note_count_range", sql`${table.noteCount} >= 0`),
    index("chart_versions_song_idx").on(table.songId),
  ],
);

export const scores = sqliteTable(
  "scores",
  {
    id: integer("id").primaryKey({ autoIncrement: true }),
    songId: text("songId")
      .notNull()
      .references(() => songs.id, { onDelete: "cascade" }),
    version: integer("version").notNull(),
    nickname: text("nickname").notNull(),
    p: integer("p").notNull().default(0),
    g: integer("g").notNull().default(0),
    b: integer("b").notNull().default(0),
    m: integer("m").notNull().default(0),
    score: integer("score").notNull(),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    check("scores_version_positive", sql`${table.version} >= 1`),
    check("scores_nickname_length", sql`length(${table.nickname}) BETWEEN 1 AND 16`),
    check("scores_judgments_non_negative", sql`${table.p} >= 0 AND ${table.g} >= 0 AND ${table.b} >= 0 AND ${table.m} >= 0`),
    check("scores_score_range", sql`${table.score} BETWEEN 0 AND 1000000`),
    // Leaderboard reads one (song, version) window ordered by score desc.
    index("scores_leaderboard_idx").on(table.songId, table.version, table.score),
  ],
);

export type Song = typeof songs.$inferSelect;
export type NewSong = typeof songs.$inferInsert;
export type ChartVersion = typeof chartVersions.$inferSelect;
export type NewChartVersion = typeof chartVersions.$inferInsert;
export type Score = typeof scores.$inferSelect;
export type NewScore = typeof scores.$inferInsert;
