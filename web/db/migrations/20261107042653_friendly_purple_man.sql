ALTER TABLE `songs` ADD `archived` integer DEFAULT 0 NOT NULL;--> statement-breakpoint
ALTER TABLE `songs` ADD `archivedAt` text;--> statement-breakpoint
CREATE INDEX `songs_archived_idx` ON `songs` (`archived`);