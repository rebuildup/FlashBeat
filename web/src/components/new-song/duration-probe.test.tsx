// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { DurationProbe } from "./duration-probe";
import { SongInfoStep } from "./song-info-step";
import { EMPTY_NEW_SONG_INFO, type NewSongInfo } from "@/lib/new-song/draft";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

/**
 * Issue #640 — the duration probe must report player errors and the step
 * must drop the previous video's measurement when the video id changes.
 *
 * The YouTube IFrame API is replaced by a fake whose players the test
 * drives by hand (`onReady` / `onError`); no network is touched.
 */

interface FakePlayerEvents {
  onReady?: () => void;
  onError?: (event: { data: number }) => void;
}

interface FakePlayer {
  videoId: string;
  events: FakePlayerEvents;
  destroyed: boolean;
  getDuration(): number;
  destroy(): void;
}

const players = vi.hoisted(() => [] as FakePlayer[]);

vi.mock("@/lib/new-song/iframe-api", () => ({
  loadYouTubeIframeApi: () =>
    Promise.resolve({
      Player: class implements FakePlayer {
        videoId: string;
        events: FakePlayerEvents;
        destroyed = false;
        constructor(_element: unknown, options: { videoId: string; events?: FakePlayerEvents }) {
          this.videoId = options.videoId;
          this.events = options.events ?? {};
          players.push(this);
        }
        getDuration() {
          return 200;
        }
        destroy() {
          this.destroyed = true;
        }
      },
      PlayerState: { UNSTARTED: -1, ENDED: 0, PLAYING: 1, PAUSED: 2, BUFFERING: 3, CUED: 5 },
    }),
}));

let root: Root | null = null;
let container: HTMLElement | null = null;

function render(element: React.ReactElement): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(element);
  });
  return container;
}

/** Let the async player bootstrap settle. */
async function flush(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
  });
}

function text(element: HTMLElement): string {
  return element.textContent ?? "";
}

function probeButton(): HTMLButtonElement {
  const button = Array.from(container!.querySelectorAll("button")).find((node) =>
    node.textContent?.includes("プレイヤーから自動取得"),
  );
  if (!button) throw new Error("probe button not found");
  return button as HTMLButtonElement;
}

const INFO_A: NewSongInfo = {
  ...EMPTY_NEW_SONG_INFO,
  youtube: "https://youtu.be/dQw4w9WgXcQ",
  title: "Song A",
  songId: "song-a",
};

const INFO_B: NewSongInfo = {
  ...EMPTY_NEW_SONG_INFO,
  youtube: "https://youtu.be/aaaaaaaaaaa",
  title: "Song B",
  songId: "song-b",
};

afterEach(() => {
  act(() => root?.unmount());
  container?.remove();
  root = null;
  container = null;
  players.length = 0;
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("DurationProbe (issue #640)", () => {
  it("reports the duration once the player is ready", async () => {
    const onDuration = vi.fn();
    render(<DurationProbe videoId="dQw4w9WgXcQ" onDuration={onDuration} />);
    await flush();

    expect(players).toHaveLength(1);
    act(() => {
      players[0]!.events.onReady?.();
    });
    expect(onDuration).toHaveBeenCalledWith(200);
  });

  it("shows the error state when the player reports an error", async () => {
    const view = render(<DurationProbe videoId="dQw4w9WgXcQ" onDuration={vi.fn()} />);
    await flush();

    act(() => {
      players[0]!.events.onError?.({ data: 5 });
    });
    expect(view.querySelector('[data-testid="duration-probe-error"]')).not.toBeNull();
    expect(text(view)).toContain("プレイヤーを読み込めませんでした");
  });
});

describe("SongInfoStep duration probe (issue #640)", () => {
  it("drops the previous video's duration when the video id changes", async () => {
    // oEmbed must fail quietly; the probe is driven by the fake player.
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.reject(new TypeError("offline"))),
    );

    const onChange = vi.fn();
    const element = (info: NewSongInfo) => (
      <SongInfoStep
        info={info}
        takenIds={new Set()}
        catalogError={null}
        onChange={onChange}
        onAdvance={vi.fn()}
      />
    );

    render(element(INFO_A));
    await flush();
    expect(players).toHaveLength(1);
    expect(players[0]!.videoId).toBe("dQw4w9WgXcQ");

    // The first video measures 200s: the auto-fill button unlocks.
    act(() => {
      players[0]!.events.onReady?.();
    });
    expect(probeButton().disabled).toBe(false);

    // Switching the video must not keep the stale measurement.
    act(() => {
      root!.render(element(INFO_B));
    });
    await flush();

    expect(players).toHaveLength(2);
    expect(players[0]!.destroyed).toBe(true);
    expect(players[1]!.videoId).toBe("aaaaaaaaaaa");
    expect(probeButton().disabled).toBe(true);
  });
});
