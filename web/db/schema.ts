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
import {
  check,
  index,
  integer,
  primaryKey,
  real,
  sqliteTable,
  text,
  uniqueIndex,
} from "void/schema-d1";

/**
 * Ownership model of the Web Studio (issue #799).
 *
 * Better Auth owns `user` / `account` / `session` / `verification`; these
 * two tables add the workspace layer on top:
 *
 *   workspaces        — a personal workspace per user (`kind = 'personal'`,
 *                       id `personal-<userId>`) or a team workspace
 *                       (`kind = 'team'`) that memberships share.
 *   workspace_members — user ↔ workspace with a role. Authorization always
 *                       resolves through this table, never through a
 *                       client-supplied owner id.
 */
export const workspaces = sqliteTable(
  "workspaces",
  {
    id: text("id").primaryKey(),
    name: text("name").notNull(),
    kind: text("kind").notNull().default("personal"),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    check("workspaces_name_length", sql`length(${table.name}) BETWEEN 1 AND 80`),
    check("workspaces_kind", sql`${table.kind} IN ('personal', 'team')`),
  ],
);

export const workspaceMembers = sqliteTable(
  "workspace_members",
  {
    workspaceId: text("workspaceId")
      .notNull()
      .references(() => workspaces.id, { onDelete: "cascade" }),
    /** Better Auth `user.id`; no FK because `user` is auth-managed. */
    userId: text("userId").notNull(),
    role: text("role").notNull().default("owner"),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    primaryKey({ columns: [table.workspaceId, table.userId] }),
    check("workspace_members_role", sql`${table.role} IN ('owner', 'editor', 'viewer')`),
    // "Which workspaces does this user belong to" is the hot lookup.
    index("workspace_members_user_idx").on(table.userId),
  ],
);

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
    /**
     * Archive flag (issue #151): 1 = the song is archived.
     *
     * The festival never deletes a song. "Removing" a song means archiving
     * it: the row, its chart snapshots and its scores stay in the database,
     * the song simply leaves the public catalog. Unarchiving brings it back
     * with its history intact.
     */
    archived: integer("archived").notNull().default(0),
    /** When the song was archived (`null` while it is live). */
    archivedAt: text("archivedAt"),
    /**
     * Owning workspace of the song (issue #799). `null` for the songs the
     * staff publish flow wrote before ownership existed; the user publish
     * flow (issue #800) sets it to the author's workspace.
     */
    ownerWorkspaceId: text("ownerWorkspaceId").references(() => workspaces.id),
    /**
     * Better Auth `user.id` of the creator (issue #799). Plain text without
     * a foreign key: the `user` table belongs to Void-managed auth and is
     * not part of this Drizzle schema.
     */
    createdByUserId: text("createdByUserId"),
    /**
     * Rights declaration recorded at user publish time (issue #800).
     *
     * `rightsStatus` is one of `original` / `licensed` / `permission` /
     * `other`; `materialSource` names where the material came from and
     * `license` carries the terms the uploader declared. All three stay
     * `NULL` on staff-published songs — the staff flow has no rights
     * attestation, so "no record" must stay distinguishable from
     * "attested". The columns are additive and nullable so existing rows
     * need no backfill.
     */
    materialSource: text("materialSource"),
    rightsStatus: text("rightsStatus"),
    license: text("license"),
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
    // The rights columns (`materialSource` / `rightsStatus` / `license`)
    // carry no CHECK constraints: SQLite cannot add one to the existing
    // `songs` table, so — like the `archived` flag (issue #151) — the
    // bounds live in the publish API (`_me-model.ts`), not in the schema.
    // The archive flag is a boolean stored as 0/1. SQLite cannot add a CHECK
    // constraint to an existing table, so the flag is written as 0 or 1 by
    // the archive route alone; the invariant lives in the API, not in the
    // table.
    // The public catalog filters on the flag, so it needs an index.
    index("songs_archived_idx").on(table.archived),
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
    /**
     * Scoring formula version the score was computed with (ADR-0028,
     * ADR-0029): 1 = the original formula, 2 = the v2 formula with the
     * combo continuation and no-miss bonuses, 3 = the v3 formula that adds
     * the combo-milestone and final bonuses. Rows written before the column
     * existed are backfilled to 1 by the follow-up migration. Different
     * versions are never ranked together.
     */
    scoringVersion: integer("scoringVersion").notNull().default(1),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    check("scores_version_positive", sql`${table.version} >= 1`),
    check("scores_nickname_length", sql`length(${table.nickname}) BETWEEN 1 AND 16`),
    check(
      "scores_judgments_non_negative",
      sql`${table.p} >= 0 AND ${table.g} >= 0 AND ${table.b} >= 0 AND ${table.m} >= 0`,
    ),
    // scoring v4 (ADR-0033, Issue #736): v4 scores may reach 2,000,000
    // (base + bonus); versions 1-3 stay capped at 1,000,000.
    check(
      "scores_score_range",
      sql`${table.score} >= 0 AND ${table.score} <= 2000000 AND (${table.scoringVersion} >= 4 OR ${table.score} <= 1000000)`,
    ),
    // SQLite cannot add a CHECK constraint to an existing table, so the
    // scoringVersion >= 1 invariant lives in the API (the submission schema
    // enforces `minimum: 1`), not in the table — same precedent as the
    // `songs.archived` flag (issue #151).
    // Leaderboard reads one (song, version, scoringVersion) window ordered
    // by score desc; the versions are never mixed (ADR-0028).
    index("scores_leaderboard_idx").on(
      table.songId,
      table.version,
      table.scoringVersion,
      table.score,
    ),
  ],
);

/**
 * Working copy of a user-owned song (issue #800).
 *
 * One draft per intended public `songId`: the row holds the metadata and
 * the chart JSON the owner is still editing, so an interrupted session
 * resumes from the server instead of browser storage. `songs` stays the
 * table of *published* artifacts — a draft never appears in the catalog,
 * the admin list or `chart_versions` until `POST
 * /api/v1/me/drafts/{id}/publish` moves it there atomically.
 *
 * Ownership mirrors `songs`: `ownerWorkspaceId` is the workspace that may
 * edit and publish, `createdByUserId` the Better Auth user who opened the
 * draft. `publishedVersion` records the `chart_versions.version` the last
 * publish produced (`null` = never published), which is how the studio
 * tells "new song" apart from "update of a live song".
 *
 * `songId` is the intended public id (`^[a-z0-9-]{3,64}$`, same as
 * `songs.id`). It is UNIQUE across drafts — two workspaces can never hold
 * a draft for the same public id — while `songs` enforces the global
 * first-publisher-wins rule at publish time.
 */
export const songDrafts = sqliteTable(
  "song_drafts",
  {
    /** `draft-<uuid>`; opaque to clients. */
    id: text("id").primaryKey(),
    ownerWorkspaceId: text("ownerWorkspaceId")
      .notNull()
      .references(() => workspaces.id, { onDelete: "cascade" }),
    /** Better Auth `user.id` of the creator; plain text like `songs`. */
    createdByUserId: text("createdByUserId").notNull(),
    /** Intended public song id; `null` until the draft names one. */
    songId: text("songId"),
    title: text("title").notNull().default(""),
    artist: text("artist").notNull().default(""),
    youtubeId: text("youtubeId"),
    level: integer("level"),
    durationSec: real("durationSec"),
    /** Serialized chart document (legacy chart.v1 JSON), `null` until saved. */
    chartJson: text("chartJson"),
    noteCount: integer("noteCount").notNull().default(0),
    /** Rights fields staged for publish; required again at publish time. */
    materialSource: text("materialSource"),
    rightsStatus: text("rightsStatus"),
    license: text("license"),
    /** `chart_versions.version` of the last publish; `null` = never. */
    publishedVersion: integer("publishedVersion"),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
    updatedAt: text("updatedAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    // One draft per public song id across ALL workspaces: a second
    // workspace cannot even reserve an id another draft already claims.
    // SQLite UNIQUE allows multiple NULLs, so drafts without a songId are
    // unaffected.
    uniqueIndex("song_drafts_songId_key").on(table.songId),
    check(
      "song_drafts_songId_format",
      sql`${table.songId} IS NULL OR (length(${table.songId}) BETWEEN 3 AND 64 AND ${table.songId} NOT GLOB '*[^a-z0-9-]*')`,
    ),
    check("song_drafts_title_length", sql`length(${table.title}) <= 160`),
    check("song_drafts_artist_length", sql`length(${table.artist}) <= 160`),
    check(
      "song_drafts_youtube_id_format",
      sql`${table.youtubeId} IS NULL OR (length(${table.youtubeId}) = 11 AND ${table.youtubeId} NOT GLOB '*[^A-Za-z0-9_-]*')`,
    ),
    check(
      "song_drafts_level_range",
      sql`${table.level} IS NULL OR ${table.level} BETWEEN 1 AND 99`,
    ),
    check(
      "song_drafts_duration_range",
      sql`${table.durationSec} IS NULL OR (${table.durationSec} > 0 AND ${table.durationSec} <= 1800)`,
    ),
    check("song_drafts_note_count_range", sql`${table.noteCount} >= 0`),
    check(
      "song_drafts_rights_status",
      sql`${table.rightsStatus} IS NULL OR ${table.rightsStatus} IN ('original', 'licensed', 'permission', 'other')`,
    ),
    check(
      "song_drafts_published_version",
      sql`${table.publishedVersion} IS NULL OR ${table.publishedVersion} >= 1`,
    ),
    // "Which drafts does this workspace own" is the hot lookup.
    index("song_drafts_workspace_idx").on(table.ownerWorkspaceId),
  ],
);

export type Song = typeof songs.$inferSelect;
export type SongDraft = typeof songDrafts.$inferSelect;
export type NewSongDraft = typeof songDrafts.$inferInsert;
export type Workspace = typeof workspaces.$inferSelect;
export type WorkspaceMember = typeof workspaceMembers.$inferSelect;
export type NewSong = typeof songs.$inferInsert;
export type ChartVersion = typeof chartVersions.$inferSelect;
export type NewChartVersion = typeof chartVersions.$inferInsert;
export type Score = typeof scores.$inferSelect;
export type NewScore = typeof scores.$inferInsert;

/**
 * Single-row catalog epoch (issue #656).
 *
 * `catalogVersion` used to be `COUNT(chart_versions)`, which cannot see
 * archive/unarchive: those flip `songs.archived` without touching
 * `chart_versions`, so the published catalog changed while the counter
 * stayed still. The epoch is bumped by every catalog-visible mutation
 * (publish, archive, unarchive) inside the same transaction, so the value
 * is a monotonic "catalog changed" token for polling clients.
 */
export const catalogMeta = sqliteTable("catalog_meta", {
  /** Always 1: the table holds exactly one row. */
  id: integer("id").primaryKey(),
  epoch: integer("epoch").notNull(),
});

export type CatalogMeta = typeof catalogMeta.$inferSelect;

/**
 * Audit log of permission-relevant operations (issue #799, threat model
 * §4.4). Every workspace/member mutation and every staff publish/archive
 * appends one row; user publish operations (issue #800) write here too.
 * Rows are append-only — nothing in the API updates or deletes them.
 */
export const auditLog = sqliteTable(
  "audit_log",
  {
    id: integer("id").primaryKey({ autoIncrement: true }),
    /** Better Auth `user.id` of the actor; `null` for staff-token calls. */
    actorUserId: text("actorUserId"),
    /** `user` (session) or `staff` (FLASHBEAT_ADMIN_TOKEN). */
    actorKind: text("actorKind").notNull().default("user"),
    workspaceId: text("workspaceId"),
    /** Dotted verb, e.g. `workspace.create`, `member.add`, `song.publish`. */
    action: text("action").notNull(),
    targetType: text("targetType"),
    targetId: text("targetId"),
    /** Free-form JSON context (never secrets or chart bodies). */
    detail: text("detail"),
    createdAt: text("createdAt")
      .notNull()
      .default(sql`(datetime('now'))`),
  },
  (table) => [
    check("audit_log_actor_kind", sql`${table.actorKind} IN ('user', 'staff')`),
    index("audit_log_workspace_idx").on(table.workspaceId),
    index("audit_log_target_idx").on(table.targetType, table.targetId),
  ],
);

export type AuditLogEntry = typeof auditLog.$inferSelect;
export type NewAuditLogEntry = typeof auditLog.$inferInsert;

/**
 * Per-user fixed-window counters for the studio mutation APIs (issue #799,
 * threat model §4.2). Better Auth's own `rateLimit` table covers `/api/auth`
 * only; app endpoints count here, keyed by `(bucket, subject)` where the
 * subject is normally the Better Auth user id.
 */
export const rateLimits = sqliteTable(
  "rate_limits",
  {
    /** Limit family, e.g. `me.workspace.write`. */
    bucket: text("bucket").notNull(),
    /** Who the limit applies to (user id, or `anon:<ip>` fallback). */
    subject: text("subject").notNull(),
    /** Window start as unix seconds, aligned to the window length. */
    windowStart: integer("windowStart").notNull(),
    hits: integer("hits").notNull(),
  },
  (table) => [
    primaryKey({ columns: [table.bucket, table.subject, table.windowStart] }),
    check("rate_limits_hits_positive", sql`${table.hits} >= 1`),
  ],
);

export type RateLimitRow = typeof rateLimits.$inferSelect;
