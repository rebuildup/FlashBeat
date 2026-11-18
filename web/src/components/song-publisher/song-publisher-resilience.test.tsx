// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import type { SongSummary } from "@/app/types";
import { publishAndVerify } from "@/lib/publish/client";
import { SongPublisher } from "./song-publisher";

/**
 * Issue #639 — `handlePublish` must release the in-flight guard even when
 * `publishAndVerify` throws outside its contract, so the screen never sticks
 * in the publishing state.
 */

vi.mock("@/components/chart-editor/note-grid", () => ({ NoteGrid: () => null }));
vi.mock("@/components/chart-editor/youtube-preview", () => ({ YouTubePreview: () => null }));
vi.mock("@/lib/publish/client", () => ({
  publishAndVerify: vi.fn(),
}));

const publishAndVerifyMock = vi.mocked(publishAndVerify);

const SONG: SongSummary = {
  id: "who",
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  bpm: 120,
  durationSec: 68,
  chartVersion: 3,
  noteCount: SAMPLE_CHART.notes.length,
};

let container: HTMLElement | null = null;
let root: Root | null = null;

async function renderPublisher(): Promise<void> {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  await act(async () => {
    root!.render(
      <SongPublisher
        initialChart={SAMPLE_CHART}
        initialYouTubeId={SONG.youtubeId}
        initialSong={SONG}
        initialVersion={SONG.chartVersion}
      />,
    );
  });
}

function buttonByText(text: string): HTMLButtonElement {
  const button = Array.from(document.body.querySelectorAll("button")).find(
    (node) => node.textContent?.trim() === text,
  );
  if (!button) throw new Error(`button not found: ${text}`);
  return button as HTMLButtonElement;
}

async function press(label: string): Promise<void> {
  await act(async () => {
    buttonByText(label).dispatchEvent(new MouseEvent("click", { bubbles: true }));
  });
}

function typeToken(): void {
  const token = document.body.querySelector('input[type="password"]');
  if (!(token instanceof HTMLInputElement)) throw new Error("token input not found");
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
  act(() => {
    setter.call(token, "fixture-token");
    token.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

/** Open the review dialog and press publish once. */
async function attemptPublish(): Promise<void> {
  await press("公開内容を確認");
  typeToken();
  await press("公開する");
}

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
  publishAndVerifyMock.mockReset();
});

describe("SongPublisher publish resilience (issue #639)", () => {
  it("recovers when the publish call throws outside its contract", { timeout: 30000 }, async () => {
    publishAndVerifyMock.mockRejectedValue(new Error("contract violation"));
    await renderPublisher();

    await attemptPublish();
    await attemptPublish();

    // The throw is reported and the in-flight guard is released, so a second
    // attempt reaches the transport again instead of sticking on 公開中.
    expect(publishAndVerifyMock).toHaveBeenCalledTimes(2);
    expect(document.body.querySelector('[data-testid="publish-error"]')).not.toBeNull();
    expect(buttonByText("公開内容を確認").disabled).toBe(false);
  });
});
