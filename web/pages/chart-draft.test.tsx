// @vitest-environment jsdom
import { act } from "react";
import type { ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { savePublishDraft } from "@/lib/publish/draft";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import type { Chart } from "@/lib/chart/types";
import type { SongSession } from "@/lib/song-session/load";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

/**
 * Issue #98 — publish 画面から戻された下書きを、譜面ページが実際に復元する。
 *
 * `src/lib/publish/__tests__/draft.test.ts` は storage の読み書きだけを固定して
 * おり、ページがその値を受け取ってエディタへ渡すかは見ていない。ここでは
 * ページを描画して次を固定する。
 *
 * - 下書きの URL で開くと、編集内容がエディタの初期譜面になる (公開済み譜面より優先)。
 * - 下書きが別の曲・別の版のものなら、公開済み譜面のままにして通知だけ出す。
 * - 下書きの id が不正でもページは落ちない。
 *
 * エディタと Studio の外枠はモックする。Konva の描画とカタログの取得は
 * この Issue の対象ではなく、モックしても「ページが何をエディタへ渡すか」は
 * 本番と同じ経路 (props → `initialChart`) で観測できる。
 */

const seen = vi.hoisted(() => ({ chart: null as Chart | null }));
const loadSongSession = vi.hoisted(() => vi.fn());

vi.mock("@/app/app-shell", () => ({
  StudioShell: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));

vi.mock("@/components/chart-editor/chart-editor", () => ({
  ChartEditor: ({ initialChart }: { initialChart: Chart }) => {
    seen.chart = initialChart;
    return <div data-testid="chart-editor" data-chart-name={initialChart.name} />;
  },
}));

vi.mock("@/lib/song-session/load", () => ({ loadSongSession }));

import ChartPage from "./chart";

const PUBLISHED_CHART: Chart = { ...SAMPLE_CHART, name: "published-chart", BPM: 120 };

const SESSION: SongSession = {
  song: {
    id: "who",
    title: "Who",
    artist: "Azari",
    youtubeId: "dQw4w9WgXcQ",
    level: 10,
    bpm: 120,
    durationSec: 214,
    chartVersion: 3,
    noteCount: PUBLISHED_CHART.notes.length,
  },
  chart: PUBLISHED_CHART,
  chartVersion: 3,
  issues: [],
};

/** 編集途中の譜面。名前と BPM を公開済み譜面から変えて区別する。 */
const EDITED_CHART: Chart = { ...SAMPLE_CHART, name: "unsaved-edits", BPM: 157 };

let root: Root | null = null;
let container: HTMLElement | null = null;

beforeEach(() => {
  sessionStorage.clear();
  seen.chart = null;
  loadSongSession.mockReset();
  loadSongSession.mockResolvedValue(SESSION);
});

afterEach(() => {
  if (root !== null) act(() => root?.unmount());
  root = null;
  container?.remove();
  container = null;
  sessionStorage.clear();
});

async function renderPage(props: {
  songId: string | null;
  version: string | null;
  draftId: string | null;
}): Promise<HTMLElement> {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  await act(async () => {
    root?.render(<ChartPage {...props} />);
  });
  return container;
}

/** publish 画面と同じ経路で下書きを保存し、その id を返す。 */
function storeDraft(draft: {
  songId: string | null;
  version: string | null;
  chart: Chart;
}): string {
  const url = new URL(savePublishDraft(draft, "editor"), "https://example.test");
  return url.searchParams.get("draft") ?? "";
}

describe("chart page restores the publish draft (issue #98)", () => {
  it("opens the edited chart instead of the published one", async () => {
    const draftId = storeDraft({ songId: "who", version: "3", chart: EDITED_CHART });
    const view = await renderPage({ songId: "who", version: "3", draftId });

    expect(view.querySelector('[data-testid="draft-restored"]')).not.toBeNull();
    expect(view.querySelector('[data-testid="draft-error"]')).toBeNull();
    expect(seen.chart?.name).toBe("unsaved-edits");
    expect(seen.chart?.BPM).toBe(157);
    // 公開済み譜面は読めているが、下書きが勝つ。
    expect(loadSongSession).toHaveBeenCalledWith("who", "3");
  });

  it("keeps the published chart for a draft of another song", async () => {
    const draftId = storeDraft({ songId: "other", version: "3", chart: EDITED_CHART });
    const view = await renderPage({ songId: "who", version: "3", draftId });

    expect(view.querySelector('[data-testid="draft-restored"]')).toBeNull();
    expect(view.querySelector('[data-testid="draft-error"]')?.textContent).toContain(
      "another song",
    );
    expect(seen.chart?.name).toBe("published-chart");
  });

  it("keeps the published chart for a draft of another version", async () => {
    const draftId = storeDraft({ songId: "who", version: "2", chart: EDITED_CHART });
    const view = await renderPage({ songId: "who", version: "3", draftId });

    expect(view.querySelector('[data-testid="draft-error"]')).not.toBeNull();
    expect(seen.chart?.name).toBe("published-chart");
  });

  it("survives an invalid draft identifier", async () => {
    const view = await renderPage({ songId: "who", version: "3", draftId: "../../etc/passwd" });

    expect(view.querySelector('[data-testid="draft-error"]')?.textContent).toContain(
      "Invalid local draft identifier",
    );
    expect(seen.chart?.name).toBe("published-chart");
  });

  it("shows no draft notice when the page was opened without a draft", async () => {
    const view = await renderPage({ songId: "who", version: "3", draftId: null });

    expect(view.querySelector('[data-testid="draft-restored"]')).toBeNull();
    expect(view.querySelector('[data-testid="draft-error"]')).toBeNull();
    expect(seen.chart?.name).toBe("published-chart");
  });

  it("keeps the draft on screen when the published chart fails to load", async () => {
    loadSongSession.mockRejectedValueOnce(new Error("Chart v3 unavailable (404)."));
    const draftId = storeDraft({ songId: "who", version: "3", chart: EDITED_CHART });
    const view = await renderPage({ songId: "who", version: "3", draftId });

    expect(view.querySelector('[data-testid="draft-restored"]')).not.toBeNull();
    expect(view.textContent).toContain("Chart v3 unavailable (404).");
    expect(seen.chart?.name).toBe("unsaved-edits");
  });
});
