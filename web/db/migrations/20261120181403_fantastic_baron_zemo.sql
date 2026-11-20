CREATE TABLE `audit_log` (
	`id` integer PRIMARY KEY AUTOINCREMENT NOT NULL,
	`actorUserId` text,
	`actorKind` text DEFAULT 'user' NOT NULL,
	`workspaceId` text,
	`action` text NOT NULL,
	`targetType` text,
	`targetId` text,
	`detail` text,
	`createdAt` text DEFAULT (datetime('now')) NOT NULL,
	CONSTRAINT "audit_log_actor_kind" CHECK("audit_log"."actorKind" IN ('user', 'staff'))
);
--> statement-breakpoint
CREATE INDEX `audit_log_workspace_idx` ON `audit_log` (`workspaceId`);--> statement-breakpoint
CREATE INDEX `audit_log_target_idx` ON `audit_log` (`targetType`,`targetId`);--> statement-breakpoint
CREATE TABLE `rate_limits` (
	`bucket` text NOT NULL,
	`subject` text NOT NULL,
	`windowStart` integer NOT NULL,
	`hits` integer NOT NULL,
	PRIMARY KEY(`bucket`, `subject`, `windowStart`),
	CONSTRAINT "rate_limits_hits_positive" CHECK("rate_limits"."hits" >= 1)
);
