import { useState } from "react";
import { css } from "styled-system/css";

/**
 * YouTube thumbnail for a catalog row (issue #156).
 *
 * `hqdefault.jpg` (480x360) is used because `maxresdefault.jpg` is often
 * absent for old videos. The image is decorative-adjacent: the row already
 * names the song in text, so a failed load removes the <img> entirely and
 * the row keeps its layout (`rowBody` has a fixed-height text stack).
 */

/** Thumbnail URL for a YouTube video id (`dQw4w9WgXcQ` → `i.ytimg.com/...`). */
export function youtubeThumbUrl(youtubeId: string): string {
  return `https://i.ytimg.com/vi/${encodeURIComponent(youtubeId)}/hqdefault.jpg`;
}

/**
 * Render decision for one row's thumbnail, pure so the vitest suite can pin
 * the fallback contract: no id → nothing, failed load → hidden image.
 */
export type ThumbnailState = "image" | "hidden";

export function thumbnailState(youtubeId: string, failed: boolean): ThumbnailState {
  if (youtubeId === "") return "hidden";
  return failed ? "hidden" : "image";
}

const thumbnail = css({
  aspectRatio: "16 / 9",
  bg: "gray.a3",
  border: "1px solid",
  borderColor: "border",
  borderRadius: "l1",
  display: "block",
  flexShrink: "0",
  height: "2.25rem",
  objectFit: "cover",
  width: "4rem",
});

export function SongThumbnail({ youtubeId, title }: { youtubeId: string; title: string }) {
  const [failed, setFailed] = useState(false);
  if (thumbnailState(youtubeId, failed) === "hidden") return null;
  return (
    <img
      alt={`${title} thumbnail`}
      className={thumbnail}
      decoding="async"
      loading="lazy"
      onError={() => setFailed(true)}
      src={youtubeThumbUrl(youtubeId)}
    />
  );
}
