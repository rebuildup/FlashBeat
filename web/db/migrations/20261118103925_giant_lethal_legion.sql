CREATE TABLE `catalog_meta` (
	`id` integer PRIMARY KEY NOT NULL,
	`epoch` integer NOT NULL
);
--> statement-breakpoint
-- Seed the epoch with the current chart count so existing deployments keep
-- a catalogVersion in the same range the COUNT(chart_versions) contract
-- served before this migration (issue #656).
INSERT INTO `catalog_meta` (`id`, `epoch`) SELECT 1, COUNT(*) FROM `chart_versions`;
