/**
 * `GET /api/v1/rankings` — the ranking board of every live song (issue #151).
 *
 * `GET /api/v1/leaderboards/{songId}?version=N` answers the ranking of one
 * song and one exact chart version. The dashboard wants the ranking board of
 * the whole festival in one request, so this endpoint joins the live songs
 * with the best scores of their currently published chart version.
 *
 * Only songs that are in the public catalog are listed: an archived song
 * keeps its scores, but it is not part of the festival any more.
 *
 * The answer is never cached (`Cache-Control: no-store`): a score submitted a
 * second ago has to show up.
 */
import { defineHandler } from "void";
import { and, asc, count, db, desc, eq } from "void/db";
import { chartVersions, scores, songs } from "@schema";

/** Largest number of entries per song the endpoint serves. */
const MAX_ENTRIES_PER_SONG = 10;

/** Default number of entries per song. */
const DEFAULT_ENTRIES_PER_SONG = 10;

export const GET = defineHandler(async (c) => {
  const limit = parseLimit(c.req.query("limit"));

  const songRows = await db
    .select({
      id: songs.id,
      title: songs.title,
      artist: songs.artist,
      level: songs.level,
      currentVersion: songs.currentVersion,
    })
    .from(songs)
    .innerJoin(
      chartVersions,
      and(eq(chartVersions.songId, songs.id), eq(chartVersions.version, songs.currentVersion)),
    )
    .where(eq(songs.archived, 0))
    .orderBy(asc(songs.id));

  // The scores of every live song, ordered the way a leaderboard is ordered:
  // score descending, earliest submission first on ties.
  const scoreRows = await db
    .select({
      songId: scores.songId,
      nickname: scores.nickname,
      score: scores.score,
    })
    .from(scores)
    .innerJoin(songs, eq(songs.id, scores.songId))
    .innerJoin(
      chartVersions,
      and(eq(chartVersions.songId, scores.songId), eq(chartVersions.version, scores.version)),
    )
    .where(eq(songs.archived, 0))
    .orderBy(asc(scores.songId), desc(scores.score), asc(scores.createdAt), asc(scores.id));

  const playsBySong = new Map<string, number>();
  const entriesBySong = new Map<string, { nickname: string; score: number }[]>();
  for (const row of scoreRows) {
    playsBySong.set(row.songId, (playsBySong.get(row.songId) ?? 0) + 1);
    const entries = entriesBySong.get(row.songId);
    if (entries === undefined) {
      entriesBySong.set(row.songId, [{ nickname: row.nickname, score: row.score }]);
    } else if (entries.length < limit) {
      entries.push({ nickname: row.nickname, score: row.score });
    }
  }

  const [total] = await db.select({ value: count() }).from(chartVersions);

  c.header("Cache-Control", "no-store");
  return c.json({
    catalogVersion: total.value,
    songs: songRows.map((song) => ({
      songId: song.id,
      title: song.title,
      artist: song.artist,
      level: song.level,
      chartVersion: song.currentVersion,
      plays: playsBySong.get(song.id) ?? 0,
      entries: entriesBySong.get(song.id) ?? [],
    })),
  });
});

/**
 * Parse the optional `limit` query parameter.
 *
 * The value is the number of entries per song. Anything that is not a
 * positive integer falls back to the default, so a broken client sees a
 * valid board instead of an error.
 */
function parseLimit(raw: string | undefined): number {
  if (raw === undefined || !/^[0-9]+$/.test(raw)) {
    return DEFAULT_ENTRIES_PER_SONG;
  }
  const value = Number(raw);
  if (!Number.isSafeInteger(value) || value < 1) {
    return DEFAULT_ENTRIES_PER_SONG;
  }
  return Math.min(value, MAX_ENTRIES_PER_SONG);
}
