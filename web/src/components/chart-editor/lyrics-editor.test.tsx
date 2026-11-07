// @vitest-environment jsdom
import { act, useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { Chart } from "@/lib/chart/types";
import { LyricsEditor } from "./lyrics-editor";

// React 19 warns (and skips flushing) unless the test says it is act-aware.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

/**
 * The lyrics panel through the real DOM (issue #165).
 *
 * The panel is exercised with a parent that echoes `onChartChange` back into
 * `chart`, exactly like the chart editor does. That is the contract the panel
 * relies on to keep its local document in sync with the chart on screen.
 */

const BASE_CHART: Chart = {
  name: "Test Song",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [],
  lyrics: {
    startTime: [1, 5],
    // The second line is the legacy `Who` shape: end before start.
    endTime: [3, 4],
    furigana: ["いち", "に"],
    mondai: ["一行目", "二行目"],
    romaji: ["ichigyoume", "nigyoume"],
  },
};

let root: Root | null = null;
let container: HTMLElement | null = null;
let emitted: Chart | null = null;

function render(playheadSec = 0, onSeek?: (seconds: number) => void): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);

  function Harness() {
    const [chart, setChart] = useState<Chart>(BASE_CHART);
    return (
      <LyricsEditor
        chart={chart}
        playheadSec={playheadSec}
        onSeek={onSeek}
        onChartChange={(next) => {
          emitted = next;
          setChart(next);
        }}
      />
    );
  }

  act(() => {
    root!.render(<Harness />);
  });
  return container;
}

function byTestId(scope: HTMLElement, id: string): HTMLElement {
  const element = scope.querySelector<HTMLElement>(`[data-testid="${id}"]`);
  if (!element) {
    throw new Error(`missing ${id}`);
  }
  return element;
}

/** Type into a controlled input the way a browser does. */
function type(input: HTMLInputElement, value: string) {
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")?.set;
  setter?.call(input, value);
  act(() => {
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function pressEnter(input: HTMLInputElement) {
  act(() => {
    input.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
  });
}

beforeEach(() => {
  emitted = null;
});

afterEach(() => {
  if (root && container) {
    act(() => {
      root!.unmount();
    });
  }
  container?.remove();
  root = null;
  container = null;
  emitted = null;
});

describe("LyricsEditor", () => {
  it("shows the text, furigana and romaji of every line", () => {
    const ui = render();
    const row = byTestId(ui, "lyrics-row-0");

    expect((byTestId(ui, "lyrics-text-0") as HTMLInputElement).value).toBe("一行目");
    expect((byTestId(ui, "lyrics-furigana-0") as HTMLInputElement).value).toBe("いち");
    expect((byTestId(ui, "lyrics-romaji-0") as HTMLInputElement).value).toBe("ichigyoume");
    expect(row.textContent).toContain("0:01.00 - 0:03.00");
  });

  it("marks a legacy interval and derives its end for display", () => {
    const ui = render();
    const legacy = byTestId(ui, "lyrics-row-1");

    expect(legacy.textContent).toContain("legacy end");
    // end 4 < start 5, so the rendered range uses the +4s fallback.
    expect(legacy.textContent).toContain("0:05.00 - 0:09.00");
  });

  it("highlights the line at the playhead", () => {
    const ui = render(1.5);

    expect(byTestId(ui, "lyrics-row-0").getAttribute("data-active")).toBe("true");
    expect(byTestId(ui, "lyrics-row-1").getAttribute("data-active")).toBe("false");
    expect(byTestId(ui, "lyrics-now").textContent).toContain("Line 1");
  });

  it("writes a text edit without touching the stored times", () => {
    const ui = render();
    type(byTestId(ui, "lyrics-text-0") as HTMLInputElement, "変更した行");

    expect(emitted?.lyrics?.mondai?.[0]).toBe("変更した行");
    // The `Who` shape of the second line survives a text-only edit.
    expect(emitted?.lyrics?.startTime).toEqual([1, 5]);
    expect(emitted?.lyrics?.endTime).toEqual([3, 4]);
    expect(emitted?.lyrics?.romaji).toEqual(["ichigyoume", "nigyoume"]);
  });

  it("does not rewrite the chart when a time field is only focused", () => {
    const ui = render();
    const start = byTestId(ui, "lyrics-start-0") as HTMLInputElement;

    act(() => {
      start.dispatchEvent(new FocusEvent("focus", { bubbles: false }));
      start.dispatchEvent(new FocusEvent("blur", { bubbles: false }));
    });

    // No edit happened, so nothing was written back: the legacy end of the
    // second line stays exactly as the chart had it.
    expect(emitted).toBeNull();
  });

  it("parses seconds and full-width IME digits when a time is committed", () => {
    const ui = render();
    const end = byTestId(ui, "lyrics-end-0") as HTMLInputElement;

    type(end, "２．５");
    pressEnter(end);

    expect(emitted?.lyrics?.endTime?.[0]).toBe(2.5);
  });

  it("repairs an edited interval on save and keeps every array aligned", () => {
    const ui = render();
    const end = byTestId(ui, "lyrics-end-1") as HTMLInputElement;

    type(end, "2");
    pressEnter(end);

    const lyrics = emitted?.lyrics;
    expect(lyrics?.startTime).toHaveLength(2);
    expect(lyrics?.endTime).toHaveLength(2);
    expect(lyrics?.furigana).toHaveLength(2);
    expect(lyrics?.mondai).toHaveLength(2);
    expect(lyrics?.romaji).toHaveLength(2);
    // An end before the start is repaired to start + 4s on save.
    expect(lyrics?.endTime?.[1]).toBe(9);
  });

  it("adds a line and emits all five parallel arrays", () => {
    const ui = render();

    act(() => {
      byTestId(ui, "lyrics-add").click();
    });

    const lyrics = emitted?.lyrics;
    expect(lyrics?.startTime).toHaveLength(3);
    expect(lyrics?.endTime).toHaveLength(3);
    expect(lyrics?.furigana).toHaveLength(3);
    expect(lyrics?.mondai).toHaveLength(3);
    expect(lyrics?.romaji).toHaveLength(3);
    expect(byTestId(ui, "lyrics-count").textContent).toContain("3 lines");
  });

  it("deletes a line", () => {
    const ui = render();

    act(() => {
      ui.querySelector<HTMLButtonElement>('[aria-label="Delete line 1"]')?.click();
    });

    expect(emitted?.lyrics?.mondai).toEqual(["二行目"]);
    expect(byTestId(ui, "lyrics-count").textContent).toContain("1 line");
  });

  it("seeks the preview to the start of a line", () => {
    const onSeek = vi.fn();
    const ui = render(0, onSeek);

    act(() => {
      ui.querySelector<HTMLButtonElement>('[aria-label="Seek to line 2"]')?.click();
    });

    expect(onSeek).toHaveBeenCalledWith(5);
  });

  it("sets a line start from the playhead", () => {
    const ui = render(6.25);

    act(() => {
      ui.querySelector<HTMLButtonElement>('[aria-label="Set line 2 start to the playhead"]')?.click();
    });

    expect(emitted?.lyrics?.startTime?.[1]).toBe(6.25);
  });
});
