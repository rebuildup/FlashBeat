// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { loader, type Props } from "../chart.server";
import ChartPage from "../chart";
import { savePublishDraft } from "@/lib/publish/draft";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";

/**
 * Issue #98 — the publish screen hands a failed publish back to the editor
 * as `/chart?draft=…`. The editor has to open that URL with the very chart
 * the operator was about to publish, not with the published chart of the
 * song.
 *
 * The page is tested as a page: the loader reads the query, the page reads
 * the draft and the editor shows it. The YouTube preview and the Konva grid
 * are stubbed — neither a network nor a canvas is needed here.
 */

vi.mock("@/components/chart-editor/youtube-preview", async () => {
  const { forwardRef, useImperativeHandle } = await import("react");
  return {
    YouTubePreview: forwardRef(function PreviewStub(_props: unknown, ref) {
      useImperativeHandle(ref, () => ({
        play: () => {},
        pause: () => {},
        seekTo: () => {},
      }));
      return null;
    }),
  };
});

vi.mock("@/components/chart-editor/note-grid", () => ({
  NoteGrid: () => null,
}));

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}
if (typeof globalThis.ResizeObserver === "undefined") {
  (globalThis as { ResizeObserver?: unknown }).ResizeObserver = ResizeObserverStub;
}
if (typeof Element.prototype.scrollTo !== "function") {
  Element.prototype.scrollTo = function scrollTo(): void {};
}
if (typeof Element.prototype.scrollIntoView !== "function") {
  Element.prototype.scrollIntoView = function scrollIntoView(): void {};
}

/** The published chart of the song, which the draft has to win over. */
const publishedChart = { ...SAMPLE_CHART, name: "Published chart", BPM: 120 };

/** The catalog entry the page resolves `songId=who` against. */
const CATALOG_SONG = {
  id: "who",
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  bpm: 120,
  durationSec: 68,
  chartVersion: 2,
  noteCount: publishedChart.notes.length,
};

/**
 * `fetch` stub. The catalog always answers; the chart answers with
 * `chartStatus`, so a failing load can be staged without a network.
 */
function stubFetch(options: { chartStatus?: number } = {}): void {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: string) => {
      if (input.endsWith("/catalog")) return Response.json({ songs: [CATALOG_SONG] });
      if (options.chartStatus !== undefined && options.chartStatus !== 200)
        return new Response("unavailable", { status: options.chartStatus });
      return Response.json({ songId: "who", chartVersion: 2, chart: publishedChart });
    }),
  );
}

let root: Root | null = null;
let container: HTMLElement | null = null;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  sessionStorage.clear();
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  stubFetch();
});

afterEach(() => {
  act(() => {
    root?.unmount();
  });
  root = null;
  container?.remove();
  container = null;
  vi.unstubAllGlobals();
});

/** Props exactly as the route hands them over, for a `/chart` query string. */
async function propsFor(query: string): Promise<Props> {
  const parsed = new URL(`/chart?${query}`, "https://studio.test");
  return (await loader({ req: { url: parsed.href } } as never)) as Props;
}

/** The id of a stored draft, as the publish screen's handoff URL carries it. */
function draftIdOf(draft: {
  songId: string | null;
  version: string | null;
  chart: typeof SAMPLE_CHART;
}): string {
  const url = new URL(savePublishDraft(draft, "editor"), "https://studio.test");
  return url.searchParams.get("draft")!;
}

/** Renders the page and lets the parallel catalog / chart load settle. */
async function renderPage(props: Props): Promise<void> {
  await act(async () => {
    root!.render(<ChartPage {...props} />);
  });
  await act(async () => {
    await Promise.resolve();
  });
}

/** The chart the editor opened with, read from its name field. */
function editorChartName(): string | undefined {
  return container!.querySelector<HTMLInputElement>('[data-testid="song-name-input"]')?.value;
}

describe("the chart page", () => {
  it("restores the draft the publish screen handed back", async () => {
    const draftChart = { ...SAMPLE_CHART, name: "Restored draft", BPM: 190 };
    const parsed = new URL(
      savePublishDraft({ songId: "who", version: "2", chart: draftChart }, "editor"),
      "https://studio.test",
    );

    const props = await propsFor(parsed.searchParams.toString());

    expect(props.draftId).toBe(parsed.searchParams.get("draft"));

    await renderPage(props);

    expect(container!.querySelector('[data-testid="draft-restored"]')).not.toBeNull();
    expect(editorChartName()).toBe("Restored draft");
  });

  /**
   * A draft that does not belong to the open song or version is refused: the
   * operator must not publish another song's edits by accident.
   */
  it.each([
    ["another song", { songId: "other", version: "2" }],
    ["another version", { songId: "who", version: "3" }],
  ])("keeps the published chart for a draft of %s", async (_label, draftOf) => {
    const draftChart = { ...SAMPLE_CHART, name: "Someone else's edits", BPM: 190 };
    const query = `songId=who&version=2&draft=${draftIdOf({ ...draftOf, chart: draftChart })}`;

    await renderPage(await propsFor(query));

    expect(container!.querySelector('[data-testid="draft-restored"]')).toBeNull();
    expect(container!.querySelector('[data-testid="draft-error"]')?.textContent).toContain(
      "belongs to another song or version",
    );
    expect(editorChartName()).toBe("Published chart");
  });

  /** The id comes from session storage, so a hostile one must not be trusted. */
  it("keeps the published chart for an invalid draft identifier", async () => {
    await renderPage(await propsFor("songId=who&version=2&draft=../../etc/passwd"));

    expect(container!.querySelector('[data-testid="draft-error"]')?.textContent).toContain(
      "Invalid local draft identifier",
    );
    expect(editorChartName()).toBe("Published chart");
  });

  /**
   * The draft lives in this tab's session storage, so a bookmarked URL or a
   * copy opened in another tab finds the id but not the draft. That is a
   * notice, not a failure: the published chart still opens.
   */
  it("keeps the published chart when the draft is gone from this tab", async () => {
    await renderPage(
      await propsFor("songId=who&version=2&draft=0b7c1f4e-9a63-4c1d-8f52-2a6f0e3d5a19"),
    );

    expect(container!.querySelector('[data-testid="draft-error"]')?.textContent).toContain(
      "This local draft is unavailable",
    );
    expect(editorChartName()).toBe("Published chart");
  });

  /** The operator's own chart is the point: it outlives a failing published chart. */
  it("keeps the draft when the published chart cannot be loaded", async () => {
    stubFetch({ chartStatus: 503 });
    const draftChart = { ...SAMPLE_CHART, name: "Partly edited", BPM: 190 };
    const query = `songId=who&version=2&draft=${draftIdOf({ songId: "who", version: "2", chart: draftChart })}`;

    await renderPage(await propsFor(query));

    expect(container!.querySelector('[data-testid="draft-restored"]')).not.toBeNull();
    expect(container!.textContent).toContain("Chart v2 unavailable (503)");
    expect(editorChartName()).toBe("Partly edited");
  });

  /** Without a draft the page shows the published chart and no draft notice. */
  it("shows no draft notice when the page was opened without a draft", async () => {
    await renderPage(await propsFor("songId=who&version=2"));

    expect(container!.querySelector('[data-testid="draft-restored"]')).toBeNull();
    expect(container!.querySelector('[data-testid="draft-error"]')).toBeNull();
    expect(editorChartName()).toBe("Published chart");
  });
});
