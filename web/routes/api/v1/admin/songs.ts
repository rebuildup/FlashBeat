/**
 * `GET /api/v1/admin/songs` — staff song list (issue #151).
 *
 * The public catalog (`GET /api/v1/catalog`) only lists the songs that are
 * live. The staff list shows every song of the database, archived ones
 * included, so the operator can see what was archived and bring a song back
 * (`POST /api/v1/admin/songs/{songId}` with `{"archived": false}`).
 *
 * The route is staff-only: the same bearer token as the publish route. The
 * token is read from the Worker environment per request and never leaves the
 * server.
 */
import { defineHandler } from "void";
import { env } from "void/env";
import { asc, count, db, eq, max } from "void/db";
import { chartVersions, scores, songs } from "@schema";
import { adminTokenMatches } from "../_publish-model";
import type { AdminSong } from "../_archive-model";

export const GET = defineHandler(async (c) => {
  const authorized = await adminTokenMatches(
    c.req.header("Authorization")?.replace(/^Bearer /, ""),
    env.FLASHBEAT_ADMIN_TOKEN,
  );
  if (!authorized) {
    return c.json({ error: "incorrect staff token" }, 401);
  }

  const rows = await db
    .select({
      id: songs.id,
      title: songs.title,
      artist: songs.artist,
      youtubeId: songs.youtubeId,
      level: songs.level,
      bpm: songs.bpm,
      durationSec: songs.durationSec,
      currentVersion: songs.currentVersion,
      archived: songs.archived,
      archivedAt: songs.archivedAt,
    })
    .from(songs)
    .orderBy(asc(songs.id));

  // How often every song was played and how good the best play was. A song
  // nobody played has no row here; it is reported with `plays: 0` and no
  // best score.
  const playRows = await db
    .select({
      songId: scores.songId,
      plays: count(),
      bestScore: max(scores.score),
    })
    .from(scores)
    .groupBy(scores.songId);

  const playsBySong = new Map(playRows.map((row) => [row.songId, row]));

  const list: AdminSong[] = rows.map((row) => {
    const plays = playsBySong.get(row.id);
    return {
      id: row.id,
      title: row.title,
      artist: row.artist,
      youtubeId: row.youtubeId,
      level: row.level,
      bpm: row.bpm,
      durationSec: row.durationSec,
      chartVersion: row.currentVersion,
      archived: row.archived === 1,
      archivedAt: row.archivedAt,
      plays: plays?.plays ?? 0,
      bestScore: plays?.bestScore ?? null,
    };
  });

  const [total] = await db.select({ value: count() }).from(chartVersions);

  c.header("Cache-Control", "no-store");
  return c.json({ catalogVersion: total.value, songs: list });
});
