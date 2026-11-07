/**
 * YouTube IFrame API loader for the new-song flow.
 *
 * The chart editor has its own loader (`components/chart-editor/
 * youtube-preview.tsx`) that is not exported and outside this flow's file
 * ownership, so the probe here loads the same script with the same
 * once-per-page guarantee. The media is never proxied.
 */

interface YTPlayer {
  playVideo(): void;
  pauseVideo(): void;
  getCurrentTime(): number;
  getDuration(): number;
  getPlayerState(): number;
  seekTo(seconds: number, allowSeekAhead: boolean): void;
  destroy(): void;
}

interface YTNamespace {
  Player: new (
    element: HTMLElement | string,
    options: {
      videoId: string;
      playerVars?: Record<string, string | number>;
      events?: {
        onReady?: () => void;
        onStateChange?: (event: { data: number }) => void;
        onError?: (event: { data: number }) => void;
      };
    },
  ) => YTPlayer;
  PlayerState: {
    UNSTARTED: number;
    ENDED: number;
    PLAYING: number;
    PAUSED: number;
    BUFFERING: number;
    CUED: number;
  };
}

declare global {
  interface Window {
    YT?: YTNamespace;
    onYouTubeIframeAPIReady?: () => void;
  }
}

export type { YTPlayer, YTNamespace };

let apiPromise: Promise<YTNamespace> | null = null;

/**
 * Load the YouTube IFrame API once per page.
 *
 * Rejects when the script cannot be loaded (offline), so the caller can fall
 * back to manual input instead of breaking the flow.
 */
export function loadYouTubeIframeApi(): Promise<YTNamespace> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("The YouTube API needs a browser."));
  }

  if (window.YT?.Player) {
    return Promise.resolve(window.YT);
  }

  if (!apiPromise) {
    const { promise, resolve, reject } = Promise.withResolvers<YTNamespace>();
    apiPromise = promise;

    const previous = window.onYouTubeIframeAPIReady;
    window.onYouTubeIframeAPIReady = () => {
      previous?.();
      if (window.YT?.Player) {
        resolve(window.YT);
      } else {
        reject(new Error("The YouTube API loaded without a player."));
      }
    };

    const script = document.createElement("script");
    script.src = "https://www.youtube.com/iframe_api";
    script.async = true;
    script.onerror = () => {
      apiPromise = null;
      reject(new Error("The YouTube IFrame API could not be loaded."));
    };
    document.head.appendChild(script);
  }

  return apiPromise;
}
