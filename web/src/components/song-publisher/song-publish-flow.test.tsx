// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import type { SongSummary } from "@/app/types";
import { SongPublisher } from "./song-publisher";

/**
 * Publish state machine of the publish screen (issue #73).
 *
 * The screen must never show a success while the API did not accept the
 * request: a failed request keeps the form and reports the failure, a
 * success only comes out of a 2xx response whose body matches the contract
 * and whose published snapshot the client read back. The stubbed transport
 * below answers the same routes the real API does.
 */

vi.mock("@/components/chart-editor/note-grid", () => ({ NoteGrid: () => null }));
vi.mock("@/components/chart-editor/youtube-preview", () => ({ YouTubePreview: () => null }));

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

async function press(label: string): Promise<void> {
  const button = Array.from(document.body.querySelectorAll("button")).find(
    (node) => node.textContent?.trim() === label,
  );
  if (!button) throw new Error(`button not found: ${label}`);
  await act(async () => {
    button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
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

/** Run one publish attempt against a stubbed API. */
async function publishWith(
  putStatus: number,
  putBody: unknown,
  readBack: { ok: boolean } = { ok: true },
): Promise<void> {
  const request = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.includes("/api/v1/admin/songs/")) {
      return new Response(JSON.stringify(putBody), {
        status: putStatus,
        headers: { "Content-Type": "application/json" },
      });
    }
    const catalog = {
      catalogVersion: 8,
      songs: [{ ...SONG, chartVersion: 3 }],
    };
    if (url.includes("/api/v1/catalog")) {
      return new Response(JSON.stringify(catalog), {
        status: readBack.ok ? 200 : 503,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response(JSON.stringify({ songId: SONG.id, chartVersion: 3, chart: SAMPLE_CHART }), {
      status: readBack.ok ? 200 : 503,
      headers: { "Content-Type": "application/json" },
    });
  });
  vi.stubGlobal("fetch", vi.fn(request));

  await press("Review publish");
  typeToken();
  await press("Publish");
  await act(async () => {});
}

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
  vi.unstubAllGlobals();
});

describe("SongPublisher publish state machine (issue #73)", () => {
  it("reports a rejected publish and keeps the form", async () => {
    await renderPublisher();
    await publishWith(401, { error: "Incorrect staff token" });

    expect(document.body.querySelector('[data-testid="publish-error"]')).not.toBeNull();
    expect(document.body.querySelector('[data-testid="publish-success"]')).toBeNull();
    // The form keeps the draft: the operator can fix and retry without retyping.
    const values = Array.from(document.body.querySelectorAll("input")).map((input) => input.value);
    expect(values).toContain("who");
  });

  it("reports a published song when the API accepted and verified the request", async () => {
    await renderPublisher();
    await publishWith(200, { songId: SONG.id, chartVersion: 3, catalogVersion: 8 });

    expect(document.body.querySelector('[data-testid="publish-success"]')).not.toBeNull();
    expect(document.body.querySelector('[data-testid="publish-error"]')).toBeNull();
  });

  it("does not claim success when the response body is not a publish response", async () => {
    await renderPublisher();
    await publishWith(200, { songId: SONG.id });

    expect(document.body.querySelector('[data-testid="publish-error"]')).not.toBeNull();
    expect(document.body.querySelector('[data-testid="publish-success"]')).toBeNull();
  });

  it("does not claim success when the published snapshot cannot be read back", async () => {
    await renderPublisher();
    await publishWith(200, { songId: SONG.id, chartVersion: 3, catalogVersion: 8 }, { ok: false });

    expect(document.body.querySelector('[data-testid="publish-error"]')).not.toBeNull();
    expect(document.body.querySelector('[data-testid="publish-success"]')).toBeNull();
  });
});
