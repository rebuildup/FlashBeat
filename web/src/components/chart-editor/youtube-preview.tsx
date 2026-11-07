import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from "react";
import { css } from "styled-system/css";
import { Pause, Play } from "lucide-react";
import { Button } from "@/components/ui";

/**
 * YouTube preview.
 *
 * The player is the YouTube IFrame API, loaded straight from YouTube. The
 * media is never proxied through the FlashBeat Worker or the API.
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

let apiPromise: Promise<YTNamespace> | null = null;

/**
 * Load the YouTube IFrame API once per page.
 *
 * The script is served by YouTube. When it cannot be loaded the preview shows
 * an error instead of breaking the editor.
 */
function loadYouTubeApi(): Promise<YTNamespace> {
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

export interface YouTubePreviewHandle {
  play(): void;
  pause(): void;
  seekTo(seconds: number): void;
}

export interface YouTubePreviewProps {
  videoId: string;
  /** Called with the current playback position in seconds. */
  onTimeUpdate(seconds: number): void;
  /** Called when the player starts or stops playing. */
  onPlayingChange(playing: boolean): void;
}

const frame = css({
  bg: "black",
  borderRadius: "8px",
  overflow: "hidden",
  position: "relative",
  width: "full",
});

const playerBox = css({
  aspectRatio: "16 / 9",
  width: "full",
});

const errorText = css({
  color: "#ff6b6b",
  fontSize: "2xs",
  lineHeight: "1.4",
  padding: "2",
});

const controls = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "space-between",
  paddingTop: "2",
});

const time = css({
  color: "#b0b0b0",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
});

/** v5 Button Secondary: raised #3A3A3A with white label. */
const raisedButton = css({
  bg: "#3a3a3a",
  color: "#ffffff",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "#454545" },
  _disabled: { opacity: 0.4 },
});

/** The icon of a player control button. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

export const YouTubePreview = forwardRef<YouTubePreviewHandle, YouTubePreviewProps>(
  function YouTubePreview({ videoId, onTimeUpdate, onPlayingChange }, ref) {
    const hostRef = useRef<HTMLDivElement | null>(null);
    const playerRef = useRef<YTPlayer | null>(null);
    const timerRef = useRef<number | null>(null);
    const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
    const [errorMessage, setErrorMessage] = useState("");
    const [position, setPosition] = useState(0);

    useImperativeHandle(ref, () => ({
      play() {
        playerRef.current?.playVideo();
      },
      pause() {
        playerRef.current?.pauseVideo();
      },
      seekTo(seconds: number) {
        playerRef.current?.seekTo(seconds, true);
        setPosition(seconds);
        onTimeUpdate(seconds);
      },
    }));

    useEffect(() => {
      let disposed = false;

      const start = async () => {
        try {
          const YT = await loadYouTubeApi();
          if (disposed || !hostRef.current) {
            return;
          }

          const player = new YT.Player(hostRef.current, {
            videoId,
            playerVars: { rel: 0, modestbranding: 1 },
            events: {
              onReady: () => {
                setStatus("ready");
                timerRef.current = window.setInterval(() => {
                  const current = playerRef.current;
                  if (!current) {
                    return;
                  }
                  const time = current.getCurrentTime();
                  setPosition(time);
                  onTimeUpdate(time);
                }, 200);
              },
              onStateChange: (event) => {
                onPlayingChange(event.data === YT.PlayerState.PLAYING);
              },
            },
          });

          playerRef.current = player;
        } catch (error) {
          if (disposed) {
            return;
          }
          setStatus("error");
          setErrorMessage(error instanceof Error ? error.message : String(error));
        }
      };

      void start();

      return () => {
        disposed = true;
        if (timerRef.current) {
          clearInterval(timerRef.current);
          timerRef.current = null;
        }
        playerRef.current?.destroy();
        playerRef.current = null;
      };
      // The player is rebuilt for every video.
    }, [videoId]);

    return (
      <div>
        <div className={frame}>
          <div ref={hostRef} className={playerBox} data-testid="youtube-player" />
          {status === "loading" ? <p className={status}>Loading the YouTube player…</p> : null}
          {status === "error" ? (
            <p className={errorText} data-testid="youtube-error">
              {errorMessage} The preview needs a network connection to YouTube; editing works
              without it.
            </p>
          ) : null}
        </div>

        <div className={controls}>
          <Button
            className={raisedButton}
            size="sm"
            onClick={() => {
              playerRef.current?.playVideo();
            }}
          >
            <Play className={buttonIcon} size={14} aria-hidden="true" />
            Play
          </Button>
          <Button
            className={raisedButton}
            size="sm"
            onClick={() => {
              playerRef.current?.pauseVideo();
            }}
          >
            <Pause className={buttonIcon} size={14} aria-hidden="true" />
            Pause
          </Button>
          <span className={time} data-testid="youtube-time">
            {position.toFixed(1)}s
          </span>
        </div>
      </div>
    );
  },
);
