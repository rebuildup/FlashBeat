/**
 * `GET /api/v1/charts/{songId}?version=N` — one immutable chart snapshot.
 *
 * Contract: `contracts/openapi.v1.yaml` (`getChart`). The version is exact:
 * the client asks for the version it saw in the catalog and gets that
 * snapshot or a 404, never a newer one. Unity keeps the snapshot until the
 * play session ends, so the response is never cached
 * (`Cache-Control: no-store`).
 */
import { defineHandler } from "void";
import { and, db, eq } from "void/db";
import { chartVersions } from "@schema";
import { SONG_ID_PATTERN, buildChartResponse, parseChartVersion, type ChartResponse } from "../_read-model";

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

  const row = await db
    .select({ json: chartVersions.json })
    .from(chartVersions)
    .where(and(eq(chartVersions.songId, songId), eq(chartVersions.version, version)))
    .get();
  if (!row) {
    return c.json({ error: "chart version not found" }, 404);
  }

  let response: ChartResponse;
  try {
    response = buildChartResponse(songId, version, row.json);
  } catch {
    return c.json({ error: "stored chart is not valid JSON" }, 500);
  }

  c.header("Cache-Control", "no-store");
  return c.json(response);
});
