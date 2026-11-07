import type { SongSummary } from "../../app/types";
import { importChartLenient } from "../chart/import";
import type { Chart, ValidationIssue } from "../chart/types";

/** One published song with the exact chart version the Catalog selected. */
export interface SongSession {
  song: SongSummary;
  chart: Chart;
  chartVersion: number;
  /**
   * Problems of the published chart that were tolerated while reading it.
   *
   * A chart that was published before the semantic rules were unified is read
   * leniently, so it can be repaired instead of being dropped (issue #208).
   * The problems are warnings: the publish API stays strict, so they still
   * have to be fixed before the chart can be published again.
   */
  issues: ValidationIssue[];
}

export async function loadSongSession(
  songId: string,
  version: string,
  request: typeof fetch = fetch,
): Promise<SongSession> {
  if (
    !/^[a-z0-9-]+$/.test(songId) ||
    !/^[1-9][0-9]*$/.test(version) ||
    !Number.isSafeInteger(Number(version))
  ) {
    throw new Error(
      "Invalid song or chart version. Return to Catalog and select a published chart.",
    );
  }
  const catalogResponse = await request("/api/v1/catalog", { cache: "no-store" });
  if (!catalogResponse.ok) throw new Error(`Catalog unavailable (${catalogResponse.status})`);
  const catalog = (await catalogResponse.json()) as { songs: SongSummary[] };
  const song = catalog.songs.find((entry) => entry.id === songId);
  if (!song) throw new Error("Song no longer available in Catalog.");
  const response = await request(
    `/api/v1/charts/${encodeURIComponent(songId)}?version=${version}`,
    { cache: "no-store" },
  );
  if (!response.ok)
    throw new Error(
      `Chart v${version} unavailable (${response.status}). Return to Catalog or retry.`,
    );
  const snapshot = (await response.json()) as {
    songId?: string;
    chartVersion?: number;
    chart?: unknown;
  };
  if (snapshot.songId !== songId || snapshot.chartVersion !== Number(version))
    throw new Error("Chart identity mismatch.");
  const imported = importChartLenient(JSON.stringify(snapshot.chart));
  if (!imported.ok) throw new Error("Published chart failed validation.");
  return {
    song,
    chart: imported.chart,
    chartVersion: Number(version),
    issues: imported.issues ?? [],
  };
}
