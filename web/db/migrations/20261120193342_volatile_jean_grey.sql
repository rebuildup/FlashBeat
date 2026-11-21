CREATE TABLE `song_drafts` (
	`id` text PRIMARY KEY NOT NULL,
	`ownerWorkspaceId` text NOT NULL,
	`createdByUserId` text NOT NULL,
	`songId` text,
	`title` text DEFAULT '' NOT NULL,
	`artist` text DEFAULT '' NOT NULL,
	`youtubeId` text,
	`level` integer,
	`durationSec` real,
	`chartJson` text,
	`noteCount` integer DEFAULT 0 NOT NULL,
	`materialSource` text,
	`rightsStatus` text,
	`license` text,
	`publishedVersion` integer,
	`createdAt` text DEFAULT (datetime('now')) NOT NULL,
	`updatedAt` text DEFAULT (datetime('now')) NOT NULL,
	FOREIGN KEY (`ownerWorkspaceId`) REFERENCES `workspaces`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "song_drafts_songId_format" CHECK("song_drafts"."songId" IS NULL OR (length("song_drafts"."songId") BETWEEN 3 AND 64 AND "song_drafts"."songId" NOT GLOB '*[^a-z0-9-]*')),
	CONSTRAINT "song_drafts_title_length" CHECK(length("song_drafts"."title") <= 160),
	CONSTRAINT "song_drafts_artist_length" CHECK(length("song_drafts"."artist") <= 160),
	CONSTRAINT "song_drafts_youtube_id_format" CHECK("song_drafts"."youtubeId" IS NULL OR (length("song_drafts"."youtubeId") = 11 AND "song_drafts"."youtubeId" NOT GLOB '*[^A-Za-z0-9_-]*')),
	CONSTRAINT "song_drafts_level_range" CHECK("song_drafts"."level" IS NULL OR "song_drafts"."level" BETWEEN 1 AND 99),
	CONSTRAINT "song_drafts_duration_range" CHECK("song_drafts"."durationSec" IS NULL OR ("song_drafts"."durationSec" > 0 AND "song_drafts"."durationSec" <= 1800)),
	CONSTRAINT "song_drafts_note_count_range" CHECK("song_drafts"."noteCount" >= 0),
	CONSTRAINT "song_drafts_rights_status" CHECK("song_drafts"."rightsStatus" IS NULL OR "song_drafts"."rightsStatus" IN ('original', 'licensed', 'permission', 'other')),
	CONSTRAINT "song_drafts_published_version" CHECK("song_drafts"."publishedVersion" IS NULL OR "song_drafts"."publishedVersion" >= 1)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `song_drafts_songId_key` ON `song_drafts` (`songId`);--> statement-breakpoint
CREATE INDEX `song_drafts_workspace_idx` ON `song_drafts` (`ownerWorkspaceId`);--> statement-breakpoint
ALTER TABLE `songs` ADD `materialSource` text;--> statement-breakpoint
ALTER TABLE `songs` ADD `rightsStatus` text;--> statement-breakpoint
ALTER TABLE `songs` ADD `license` text;