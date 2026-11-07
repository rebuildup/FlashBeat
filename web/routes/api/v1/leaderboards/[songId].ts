/**
 * `GET /api/v1/leaderboards/{songId}?version=N` — public top-10 leaderboard
 * of one chart version (issue #133).
 *
 * Contract: `contracts/openapi.v1.yaml` (`getLeaderboard`). The version is
 * exact, like `GET /api/v1/charts/{songId}`: an unknown song or version is a
 * 404, a missing or non-positive-integer version is a 400. The response is
 * never cached (`Cache-Control: no-store`) — a fresh submission must be
 * visible to the next poll.
 *
 * Order: score descending, then earliest submission first on ties
 * (`createdAt`, then the autoincrement id for the same second). At most 10
 * entries; an empty leaderboard is `entries: []`.
 */
import { defineHandler } from "void";
import { and, asc, db, desc, eq } from "void/db";
import { chartVersions, scores } from "@schema";
import { SONG_ID_PATTERN, parseChartVersion } from "../_read-model";
import { buildLeaderboardResponse } from "../_score-model";

/** Size of the leaderboard the contract freezes (`maxItems: 10`). */
const LEADERBOARD_SIZE = 10;

export const GET = defineHandler(async (c) => {
  // A missing path parameter cannot match a song id, so it is answered with
  // the same 404 as an unknown song.
  const songId = c.req.param("songId") ?? "";
  const version = parseChartVersion(c.req.query("version"));
  if (version === null) {
    return c.json({ error: "version must be an integer greater than or equal to 1" }, 400);
  }

  if (!SONG_ID_PATTERN.test(songId)) {
    return c.json({ error: "song not found" }, 404);
  }

  const chart = await db
    .select({ version: chartVersions.version })
    .from(chartVersions)
    .where(and(eq(chartVersions.songId, songId), eq(chartVersions.version, version)))
    .get();
  if (!chart) {
    return c.json({ error: "leaderboard not found" }, 404);
  }

  const rows = await db
    .select({ nickname: scores.nickname, score: scores.score })
    .from(scores)
    .where(and(eq(scores.songId, songId), eq(scores.version, version)))
    .orderBy(desc(scores.score), asc(scores.createdAt), asc(scores.id))
    .limit(LEADERBOARD_SIZE);

  c.header("Cache-Control", "no-store");
  return c.json(buildLeaderboardResponse(songId, version, rows));
});
