CREATE TABLE `chart_versions` (
	`songId` text NOT NULL,
	`version` integer NOT NULL,
	`json` text NOT NULL,
	`noteCount` integer NOT NULL,
	`createdAt` text DEFAULT (datetime('now')) NOT NULL,
	PRIMARY KEY(`songId`, `version`),
	FOREIGN KEY (`songId`) REFERENCES `songs`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "chart_versions_version_positive" CHECK("chart_versions"."version" >= 1),
	CONSTRAINT "chart_versions_note_count_range" CHECK("chart_versions"."noteCount" >= 0)
);
--> statement-breakpoint
CREATE INDEX `chart_versions_song_idx` ON `chart_versions` (`songId`);--> statement-breakpoint
CREATE TABLE `scores` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`songId` text NOT NULL,
	`version` integer NOT NULL,
	`nickname` text NOT NULL,
	`p` integer DEFAULT 0 NOT NULL,
	`g` integer DEFAULT 0 NOT NULL,
	`b` integer DEFAULT 0 NOT NULL,
	`m` integer DEFAULT 0 NOT NULL,
	`score` integer NOT NULL,
	`createdAt` text DEFAULT (datetime('now')) NOT NULL,
	FOREIGN KEY (`songId`) REFERENCES `songs`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "scores_version_positive" CHECK("scores"."version" >= 1),
	CONSTRAINT "scores_nickname_length" CHECK(length("scores"."nickname") BETWEEN 1 AND 16),
	CONSTRAINT "scores_judgments_non_negative" CHECK("scores"."p" >= 0 AND "scores"."g" >= 0 AND "scores"."b" >= 0 AND "scores"."m" >= 0),
	CONSTRAINT "scores_score_range" CHECK("scores"."score" BETWEEN 0 AND 1000000)
);
--> statement-breakpoint
CREATE INDEX `scores_leaderboard_idx` ON `scores` (`songId`,`version`,`score`);--> statement-breakpoint
CREATE TABLE `songs` (
	`id` text PRIMARY KEY NOT NULL,
	`title` text NOT NULL,
	`artist` text DEFAULT '' NOT NULL,
	`youtubeId` text NOT NULL,
	`bpm` integer NOT NULL,
	`level` integer NOT NULL,
	`durationSec` real NOT NULL,
	`currentVersion` integer DEFAULT 1 NOT NULL,
	CONSTRAINT "songs_id_format" CHECK(length("songs"."id") BETWEEN 3 AND 64 AND "songs"."id" NOT GLOB '*[^a-z0-9-]*'),
	CONSTRAINT "songs_title_length" CHECK(length("songs"."title") BETWEEN 1 AND 160),
	CONSTRAINT "songs_artist_length" CHECK(length("songs"."artist") <= 160),
	CONSTRAINT "songs_youtube_id_format" CHECK(length("songs"."youtubeId") = 11 AND "songs"."youtubeId" NOT GLOB '*[^A-Za-z0-9_-]*'),
	CONSTRAINT "songs_bpm_range" CHECK("songs"."bpm" BETWEEN 1 AND 600),
	CONSTRAINT "songs_level_range" CHECK("songs"."level" BETWEEN 1 AND 99),
	CONSTRAINT "songs_duration_range" CHECK("songs"."durationSec" > 0 AND "songs"."durationSec" <= 1800),
	CONSTRAINT "songs_current_version_positive" CHECK("songs"."currentVersion" >= 1)
);
