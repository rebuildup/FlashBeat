/**
 * YouTube helpers for the preview.
 *
 * The preview uses the YouTube IFrame API directly. The media is never
 * proxied through the FlashBeat API or the Worker.
 */

const VIDEO_ID = /^[A-Za-z0-9_-]{11}$/;

/**
 * Extract the video id from a YouTube URL or a bare id.
 *
 * Accepts `https://www.youtube.com/watch?v=ID`, `https://youtu.be/ID`,
 * `https://www.youtube.com/embed/ID` and a bare 11 character id. Returns
 * `null` for anything else.
 */
export function parseYouTubeId(input: string): string | null {
  const value = input.trim();
  if (value.length === 0) {
    return null;
  }

  if (VIDEO_ID.test(value)) {
    return value;
  }

  let url: URL;
  try {
    url = new URL(value);
  } catch {
    return null;
  }

  const host = url.hostname.replace(/^www\./, "");

  if (host === "youtu.be") {
    const id = url.pathname.slice(1).split("/")[0] ?? "";
    return VIDEO_ID.test(id) ? id : null;
  }

  if (host === "youtube.com" || host === "m.youtube.com" || host === "music.youtube.com") {
    const queryId = url.searchParams.get("v");
    if (queryId && VIDEO_ID.test(queryId)) {
      return queryId;
    }

    const segments = url.pathname.split("/").filter(Boolean);
    if (segments[0] === "embed" || segments[0] === "shorts" || segments[0] === "live") {
      const id = segments[1] ?? "";
      return VIDEO_ID.test(id) ? id : null;
    }
  }

  return null;
}
