PRAGMA foreign_keys=OFF;--> statement-breakpoint
CREATE TABLE `__new_scores` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`songId` text NOT NULL,
	`version` integer NOT NULL,
	`nickname` text NOT NULL,
	`p` integer DEFAULT 0 NOT NULL,
	`g` integer DEFAULT 0 NOT NULL,
	`b` integer DEFAULT 0 NOT NULL,
	`m` integer DEFAULT 0 NOT NULL,
	`score` integer NOT NULL,
	`scoringVersion` integer DEFAULT 1 NOT NULL,
	`createdAt` text DEFAULT (datetime('now')) NOT NULL,
	FOREIGN KEY (`songId`) REFERENCES `songs`(`id`) ON UPDATE no action ON DELETE cascade,
	CONSTRAINT "scores_version_positive" CHECK("__new_scores"."version" >= 1),
	CONSTRAINT "scores_nickname_length" CHECK(length("__new_scores"."nickname") BETWEEN 1 AND 16),
	CONSTRAINT "scores_judgments_non_negative" CHECK("__new_scores"."p" >= 0 AND "__new_scores"."g" >= 0 AND "__new_scores"."b" >= 0 AND "__new_scores"."m" >= 0),
	CONSTRAINT "scores_score_range" CHECK("__new_scores"."score" >= 0 AND "__new_scores"."score" <= 2000000 AND ("__new_scores"."scoringVersion" >= 4 OR "__new_scores"."score" <= 1000000))
);
--> statement-breakpoint
INSERT INTO `__new_scores`("id", "songId", "version", "nickname", "p", "g", "b", "m", "score", "scoringVersion", "createdAt") SELECT "id", "songId", "version", "nickname", "p", "g", "b", "m", "score", "scoringVersion", "createdAt" FROM `scores`;--> statement-breakpoint
DROP TABLE `scores`;--> statement-breakpoint
ALTER TABLE `__new_scores` RENAME TO `scores`;--> statement-breakpoint
PRAGMA foreign_keys=ON;--> statement-breakpoint
CREATE INDEX `scores_leaderboard_idx` ON `scores` (`songId`,`version`,`scoringVersion`,`score`);