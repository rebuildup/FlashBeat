/**
 * YouTube oEmbed lookup for the new-song flow.
 *
 * `https://www.youtube.com/oembed?url=…&format=json` is CORS-enabled, so the
 * browser fetches the title and the author directly from YouTube; the media
 * is never proxied. oEmbed carries no duration — that comes from the embedded
 * player (`components/new-song/duration-probe.tsx`) or manual input.
 */

export type OembedFailureReason = "invalid-id" | "offline" | "not-found" | "malformed";

export type OembedResult =
  | { ok: true; title: string; author: string }
  | { ok: false; reason: OembedFailureReason };

/** The exact oEmbed endpoint of a video id. */
export function oembedRequestUrl(videoId: string): string {
  const watch = `https://www.youtube.com/watch?v=${videoId}`;
  return `https://www.youtube.com/oembed?url=${encodeURIComponent(watch)}&format=json`;
}

/**
 * Fetch title and author of a video.
 *
 * The result is a value, never an exception: offline, a private/deleted
 * video and a broken answer are ordinary outcomes the form shows next to
 * the manual fields.
 */
export async function fetchOembedMetadata(
  videoId: string,
  fetchImpl: typeof fetch = fetch,
  timeoutMs = 8000,
): Promise<OembedResult> {
  if (!/^[A-Za-z0-9_-]{11}$/.test(videoId)) {
    return { ok: false, reason: "invalid-id" };
  }

  try {
    const response = await fetchImpl(oembedRequestUrl(videoId), {
      signal: AbortSignal.timeout(timeoutMs),
    });

    if (response.status === 401 || response.status === 403 || response.status === 404) {
      return { ok: false, reason: "not-found" };
    }
    if (!response.ok) {
      return { ok: false, reason: "offline" };
    }

    const body = (await response.json()) as { title?: unknown; author_name?: unknown };
    if (typeof body.title !== "string" || body.title.trim().length === 0) {
      return { ok: false, reason: "malformed" };
    }
    return {
      ok: true,
      title: body.title,
      author: typeof body.author_name === "string" ? body.author_name : "",
    };
  } catch {
    return { ok: false, reason: "offline" };
  }
}
