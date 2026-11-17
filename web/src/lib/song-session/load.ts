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
      "曲または譜面バージョンが不正です。カタログに戻り、公開済みの譜面を選んでください。",
    );
  }
  const catalogResponse = await request("/api/v1/catalog", { cache: "no-store" });
  if (!catalogResponse.ok) throw new Error(`カタログを取得できません（${catalogResponse.status}）`);
  const catalog = (await catalogResponse.json()) as { songs: SongSummary[] };
  const song = catalog.songs.find((entry) => entry.id === songId);
  if (!song) throw new Error("この曲はカタログにありません。");
  const response = await request(
    `/api/v1/charts/${encodeURIComponent(songId)}?version=${version}`,
    { cache: "no-store" },
  );
  if (!response.ok)
    throw new Error(
      `譜面 v${version} を取得できません（${response.status}）。カタログに戻るか再試行してください。`,
    );
  const snapshot = (await response.json()) as {
    songId?: string;
    chartVersion?: number;
    chart?: unknown;
  };
  if (snapshot.songId !== songId || snapshot.chartVersion !== Number(version))
    throw new Error("譜面の識別情報が一致しません。");
  const imported = importChartLenient(JSON.stringify(snapshot.chart));
  if (!imported.ok) throw new Error("公開済みの譜面が検証に失敗しました。");
  return {
    song,
    chart: imported.chart,
    chartVersion: Number(version),
    issues: imported.issues ?? [],
  };
}
