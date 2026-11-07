import { createMockPublishFetch } from "./mock";
import type { PublishFetch } from "./types";

/**
 * Where the publish request goes.
 *
 * The studio and the API are served by the same worker, so the default is the
 * API of the current origin: the request goes to
 * `/api/v1/admin/songs/{songId}` of the host the studio was loaded from and
 * lands in the D1 of that deployment. `VITE_FLASHBEAT_API_ORIGIN` moves it to
 * another origin; the value is a public URL, the staff token is never part of
 * it. The explicit value `fixture` selects the bundled fixture, which answers
 * locally and writes nothing.
 */
export type PublishTransportMode = "fixture" | "live";

export interface PublishTransport {
  mode: PublishTransportMode;
  /** Prefix of the request URL. */
  baseUrl: string;
  /** Transport used for the request. */
  fetch: PublishFetch;
}

export function resolvePublishTransport(
  apiOrigin?: string,
  allowFixture = false,
): PublishTransport {
  const origin = (apiOrigin ?? "").trim();

  if (origin === "fixture" && !allowFixture) {
    throw new Error("Fixture publishing is disabled. Use the live API.");
  }

  if (origin === "fixture") {
    return {
      mode: "fixture",
      baseUrl: "",
      fetch: createMockPublishFetch(),
    };
  }

  if (origin.length === 0) {
    return {
      mode: "live",
      baseUrl: "",
      fetch: (input, init) => fetch(input, init),
    };
  }

  return {
    mode: "live",
    baseUrl: origin,
    fetch: (input, init) => fetch(input, init),
  };
}
