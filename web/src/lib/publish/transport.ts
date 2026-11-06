import { createMockPublishFetch } from "./mock";
import type { PublishFetch } from "./types";

/**
 * Where the publish request goes.
 *
 * The studio runs against the fixture until the API of issue #16 answers.
 * `VITE_FLASHBEAT_API_ORIGIN` switches to the real endpoint: it holds the
 * origin of the API, or `/` when the API is served by the same worker. The
 * value is a public URL, the staff token is never part of it.
 */
export type PublishTransportMode = "fixture" | "live";

export interface PublishTransport {
  mode: PublishTransportMode;
  /** Prefix of the request URL. */
  baseUrl: string;
  /** Transport used for the request. */
  fetch: PublishFetch;
}

export function resolvePublishTransport(apiOrigin?: string): PublishTransport {
  const origin = (apiOrigin ?? "").trim();

  if (origin.length === 0) {
    return {
      mode: "fixture",
      baseUrl: "",
      fetch: createMockPublishFetch(),
    };
  }

  return {
    mode: "live",
    baseUrl: origin,
    fetch: (input, init) => fetch(input, init),
  };
}
