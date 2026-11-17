-- void:allow-destructive
DROP INDEX `scores_leaderboard_idx`;--> statement-breakpoint
ALTER TABLE `scores` ADD `scoringVersion` integer DEFAULT 1 NOT NULL;--> statement-breakpoint
CREATE INDEX `scores_leaderboard_idx` ON `scores` (`songId`,`version`,`scoringVersion`,`score`);