/**
 * `GET /api/v1/stats` — festival statistics (issue #151).
 *
 * The dashboard needs more than a song list: how many songs are live, how
 * often each of them was played, how good the plays are and who the best
 * players are. The endpoint answers all of that in one request, so the
 * dashboard does not have to fan out over every song.
 *
 * Contract: `contracts/openapi.v1.yaml` (`getStats`).
 *
 * Definitions (they are part of the contract, so the dashboard and the API
 * always mean the same thing):
 *
 *   - **accuracy** of a play = `(perfect + 0.6 * great + 0.3 * bad) / notes`,
 *     with the note count of the played chart version. A miss counts as zero.
 *     The average of a song is the average of its plays — a plain mean over
 *     plays, not a note-count-weighted one.
 *   - **plays** are the score submissions; a player is a distinct nickname.
 *     Like the ranking board, every aggregate counts only plays of the
 *     *currently published* chart version of each song: the response labels
 *     a song with `songs.currentVersion`, so a play of a superseded snapshot
 *     must not leak into `plays`, `averageScore`, `bestScore` or
 *     `averageAccuracy`.
 *   - **live** songs are the songs that are in the public catalog (not
 *     archived and with a published chart version). Archived songs are
 *     counted separately — the festival never deletes a song.
 *   - **scoring version**: every aggregate counts only the latest scoring
 *     version the server supports (ADR-0028, ADR-0029, ADR-0033). v1–v4 scores
 *     live on different scales, so mixing them in `plays`, `averageScore`,
 *     `bestScore` or `topPlayers` would silently compare incomparable
 *     numbers; older rows stay readable through the versioned leaderboard.
 *
 * The response is never cached (`Cache-Control: no-store`): a score submitted
 * a second ago has to show up.
 */
import { defineHandler } from "void";
import { and, count, db, desc, eq, sql } from "void/db";
import { chartVersions, scores, songs } from "@schema";
import { LATEST_SCORING_VERSION } from "./_score-model";

/** How many songs the "most played" and "best players" lists carry. */
const TOP_LIST_SIZE = 10;

export const GET = defineHandler(async (c) => {
  // 1. The songs and their state.
  const songRows = await db
    .select({
      id: songs.id,
      title: songs.title,
      artist: songs.artist,
      level: songs.level,
      bpm: songs.bpm,
      durationSec: songs.durationSec,
      currentVersion: songs.currentVersion,
      archived: songs.archived,
      noteCount: chartVersions.noteCount,
    })
    .from(songs)
    .leftJoin(
      chartVersions,
      and(eq(chartVersions.songId, songs.id), eq(chartVersions.version, songs.currentVersion)),
    )
    .orderBy(songs.id);

  // 2. The plays per song, with the accuracy of each play computed from the
  //    note count of the chart version it was played on and averaged per
  //    play (the contract: "this is the average of the plays"). Only plays of
  //    the currently published chart version count, like the rankings.
  const playRows = await db
    .select({
      songId: scores.songId,
      plays: count(),
      distinctPlayers: sql<number>`count(distinct ${scores.nickname})`,
      averageScore: sql<number>`avg(${scores.score})`,
      bestScore: sql<number>`max(${scores.score})`,
      averageAccuracy: sql<number>`avg((${scores.p} + 0.6 * ${scores.g} + 0.3 * ${scores.b}) / ${chartVersions.noteCount})`,
    })
    .from(scores)
    .innerJoin(songs, eq(songs.id, scores.songId))
    .innerJoin(
      chartVersions,
      and(eq(chartVersions.songId, scores.songId), eq(chartVersions.version, scores.version)),
    )
    .where(
      and(
        // Issue #656: archived songs leave every aggregate, matching the
        // rankings scope — their history stays, it just stops counting.
        eq(songs.archived, 0),
        eq(scores.scoringVersion, LATEST_SCORING_VERSION),
        eq(scores.version, songs.currentVersion),
      ),
    )
    .groupBy(scores.songId);

  const playsBySong = new Map(playRows.map((row) => [row.songId, row]));

  const perSong = songRows.map((row) => {
    const plays = playsBySong.get(row.id);
    return {
      songId: row.id,
      title: row.title,
      artist: row.artist,
      level: row.level,
      bpm: row.bpm,
      durationSec: row.durationSec,
      chartVersion: row.currentVersion,
      archived: row.archived === 1,
      plays: plays?.plays ?? 0,
      distinctPlayers: plays?.distinctPlayers ?? 0,
      averageScore: plays?.averageScore ?? null,
      bestScore: plays?.bestScore ?? null,
      averageAccuracy: plays?.averageAccuracy ?? null,
    };
  });

  // 3. The totals — the same scope as the per-song rows: the latest scoring
  //    version and the currently published chart version of each song.
  const [scoreTotals] = await db
    .select({
      scores: count(),
      players: sql<number>`count(distinct ${scores.nickname})`,
    })
    .from(scores)
    .innerJoin(songs, eq(songs.id, scores.songId))
    .where(
      and(
        eq(songs.archived, 0),
        eq(scores.scoringVersion, LATEST_SCORING_VERSION),
        eq(scores.version, songs.currentVersion),
      ),
    );

  const [lastDay] = await db
    .select({ value: count() })
    .from(scores)
    .innerJoin(songs, eq(songs.id, scores.songId))
    .where(
      and(
        eq(songs.archived, 0),
        eq(scores.scoringVersion, LATEST_SCORING_VERSION),
        eq(scores.version, songs.currentVersion),
        sql`${scores.createdAt} >= datetime('now', '-1 day')`,
      ),
    );

  const [chartCount] = await db.select({ value: count() }).from(chartVersions);

  const liveSongs = perSong.filter((song) => !song.archived).length;
  const archivedSongs = perSong.filter((song) => song.archived).length;

  // 4. The level distribution of the live songs (the difficulty axis of the
  //    dashboard chart).
  const levelCounts = new Map<number, number>();
  for (const song of perSong) {
    if (song.archived) continue;
    levelCounts.set(song.level, (levelCounts.get(song.level) ?? 0) + 1);
  }
  const levelDistribution = [...levelCounts.entries()]
    .map(([level, songsAtLevel]) => ({ level, songs: songsAtLevel }))
    .sort((a, b) => a.level - b.level);

  // 5. The best players: the sum of their scores decides, the best single
  //    play breaks the tie.
  const topPlayers = await db
    .select({
      nickname: scores.nickname,
      plays: count(),
      totalScore: sql<number>`sum(${scores.score})`,
      bestScore: sql<number>`max(${scores.score})`,
    })
    .from(scores)
    .innerJoin(songs, eq(songs.id, scores.songId))
    .where(
      and(
        eq(songs.archived, 0),
        eq(scores.scoringVersion, LATEST_SCORING_VERSION),
        eq(scores.version, songs.currentVersion),
      ),
    )
    .groupBy(scores.nickname)
    .orderBy(desc(sql`sum(${scores.score})`), desc(sql`max(${scores.score})`))
    .limit(TOP_LIST_SIZE);

  c.header("Cache-Control", "no-store");
  return c.json({
    generatedAt: new Date().toISOString(),
    totals: {
      songs: perSong.length,
      liveSongs,
      archivedSongs,
      charts: chartCount.value,
      scores: scoreTotals.scores,
      players: scoreTotals.players,
      playsLast24h: lastDay.value,
    },
    perSong,
    levelDistribution,
    topPlayers,
  });
});
