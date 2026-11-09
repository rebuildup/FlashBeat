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

let root: Root | null = null;
let container: HTMLElement | null = null;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: string) => {
      if (input.endsWith("/catalog")) {
        return Response.json({
          songs: [
            {
              id: "who",
              title: "Who",
              artist: "Azari",
              youtubeId: "dQw4w9WgXcQ",
              level: 5,
              bpm: 120,
              durationSec: 68,
              chartVersion: 2,
              noteCount: publishedChart.notes.length,
            },
          ],
        });
      }
      return Response.json({ songId: "who", chartVersion: 2, chart: publishedChart });
    }),
  );
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

describe("the chart page", () => {
  it("restores the draft the publish screen handed back", async () => {
    const draftChart = { ...SAMPLE_CHART, name: "Restored draft", BPM: 190 };
    const url = savePublishDraft({ songId: "who", version: "2", chart: draftChart }, "editor");
    const parsed = new URL(url, "https://studio.test");

    const props = (await loader({ req: { url: parsed.href } } as never)) as Props;

    expect(props.draftId).toBe(parsed.searchParams.get("draft"));

    await act(async () => {
      root!.render(<ChartPage {...props} />);
    });
    // The published session is fetched in parallel; the draft must survive it.
    await act(async () => {
      await Promise.resolve();
    });

    expect(container!.querySelector('[data-testid="draft-restored"]')).not.toBeNull();
    const name = container!.querySelector<HTMLInputElement>('[data-testid="song-name-input"]');
    expect(name?.value).toBe("Restored draft");
  });
});
