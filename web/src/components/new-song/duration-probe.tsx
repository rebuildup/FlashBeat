import { useEffect, useRef, useState } from "react";
import { css } from "styled-system/css";
import { loadYouTubeIframeApi, type YTPlayer } from "@/lib/new-song/iframe-api";

/**
 * Embedded YouTube player that reports the video duration.
 *
 * oEmbed does not carry the duration, so step 1 of the new-song flow embeds
 * this small player and fills the numeric field from `getDuration()` once the
 * player is ready. Offline the probe degrades to an error note and the
 * operator types the duration by hand — the flow never blocks on YouTube.
 */

const frame = css({
  aspectRatio: "16 / 9",
  bg: "black",
  borderRadius: "8px",
  maxWidth: "320px",
  overflow: "hidden",
  position: "relative",
  width: "full",
});

const status = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  margin: "0",
});

const errorStatus = css({
  color: "#ff6b6b",
});

export interface DurationProbeProps {
  videoId: string;
  /** Called once with the player duration in seconds, when it is known. */
  onDuration: (seconds: number) => void;
}

export function DurationProbe({ videoId, onDuration }: DurationProbeProps) {
  const hostRef = useRef<HTMLDivElement | null>(null);
  const playerRef = useRef<YTPlayer | null>(null);
  const durationRef = useRef(onDuration);
  durationRef.current = onDuration;
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");

  useEffect(() => {
    let disposed = false;
    setState("loading");

    const start = async () => {
      try {
        const YT = await loadYouTubeIframeApi();
        if (disposed || !hostRef.current) {
          return;
        }
        const player = new YT.Player(hostRef.current, {
          videoId,
          playerVars: { rel: 0, modestbranding: 1 },
          events: {
            onReady: () => {
              if (disposed) {
                return;
              }
              const duration = player.getDuration();
              setState("ready");
              if (Number.isFinite(duration) && duration > 0) {
                durationRef.current(duration);
              }
            },
          },
        });
        playerRef.current = player;
      } catch {
        if (!disposed) {
          setState("error");
        }
      }
    };

    void start();

    return () => {
      disposed = true;
      playerRef.current?.destroy();
      playerRef.current = null;
    };
  }, [videoId]);

  return (
    <div>
      <div className={frame}>
        <div ref={hostRef} data-testid="duration-probe-player" />
      </div>
      {state === "loading" ? (
        <p className={status}>プレイヤーを読み込み中… / Loading the player…</p>
      ) : null}
      {state === "error" ? (
        <p className={`${status} ${errorStatus}`} data-testid="duration-probe-error">
          プレイヤーを読み込めませんでした（オフライン）。再生時間は手動で入力してください。 / The
          player could not load (offline). Enter the duration manually.
        </p>
      ) : null}
    </div>
  );
}
