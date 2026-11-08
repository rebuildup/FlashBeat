// @vitest-environment jsdom
import { act, createRef } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { ChartEditor, type ChartEditorHandle } from "./chart-editor";

/**
 * The imperative handle of the chart editor (#232 follow-up).
 *
 * The preview inside the editor is the only owner of playback. A page that
 * wants to start the song (`chart.tsx`) holds a ref and calls `play()` /
 * `pause()` / `seekTo(seconds)` instead of looking for a `Play` button in
 * the markup, so the contract has to exist and reach the preview.
 *
 * The preview and the Konva grid are replaced by stubs: this test is about
 * the forwarding, and neither a YouTube network nor a canvas is needed.
 */

const preview = vi.hoisted(() => ({
  play: vi.fn(),
  pause: vi.fn(),
  seekTo: vi.fn(),
}));

vi.mock("./youtube-preview", async () => {
  const { forwardRef, useImperativeHandle } = await import("react");
  return {
    YouTubePreview: forwardRef(function PreviewStub(_props: unknown, ref) {
      useImperativeHandle(ref, () => preview);
      return null;
    }),
  };
});

vi.mock("./note-grid", () => ({
  NoteGrid: () => null,
}));

let container: HTMLDivElement;
let root: ReturnType<typeof createRoot>;

beforeEach(() => {
  preview.play.mockClear();
  preview.pause.mockClear();
  preview.seekTo.mockClear();
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
});

afterEach(() => {
  act(() => {
    root.unmount();
  });
  container.remove();
});

function mount(): ChartEditorHandle {
  const ref = createRef<ChartEditorHandle>();
  act(() => {
    root.render(
      <ChartEditor ref={ref} initialChart={SAMPLE_CHART} initialYouTubeId="dQw4w9WgXcQ" />,
    );
  });
  if (!ref.current) {
    throw new Error("ChartEditor did not expose its transport handle.");
  }
  return ref.current;
}

describe("ChartEditor imperative handle", () => {
  // Mounting the editor is the expensive part (a jsdom render of the whole
  // work area); the assertions are instant. The timeout covers a cold mount
  // while the rest of the suite runs in parallel.
  const mountTimeout = 30_000;

  it(
    "exposes play, pause and seekTo",
    () => {
      const handle = mount();
      expect(typeof handle.play).toBe("function");
      expect(typeof handle.pause).toBe("function");
      expect(typeof handle.seekTo).toBe("function");
    },
    mountTimeout,
  );

  it(
    "forwards play and pause to the preview",
    () => {
      const handle = mount();
      act(() => {
        handle.play();
      });
      expect(preview.play).toHaveBeenCalledTimes(1);
      act(() => {
        handle.pause();
      });
      expect(preview.pause).toHaveBeenCalledTimes(1);
    },
    mountTimeout,
  );

  it(
    "forwards seekTo to the preview and moves the readout",
    () => {
      const handle = mount();
      act(() => {
        handle.seekTo(5);
      });
      expect(preview.seekTo).toHaveBeenCalledWith(5);
      expect(container.textContent).toContain("5.00s");
    },
    mountTimeout,
  );
});
