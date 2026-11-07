import { createMockPublishFetch, FIXTURE_TOKEN } from "./mock";
import type { PublishFetch } from "./types";

/**
 * Where the publish request goes.
 *
 * `live` sends the request to the API of the contract. `fixture` answers
 * locally, which is the rehearsal mode and the mode the studio falls back to
 * when the API cannot be reached.
 */
export type PublishTarget = "live" | "fixture";

export interface PublishTransport {
  /** The target the request goes to. */
  target: PublishTarget;
  /** Prefix of the request URL. */
  baseUrl: string;
  /** Transport used for the request. */
  fetch: PublishFetch;
  /**
   * Help text for the staff token field.
   *
   * Only the fixture has a known token. The real API has a secret, so no hint
   * is shown against it.
   */
  tokenHint?: string;
}

/**
 * Build the transport of a publish request.
 *
 * `apiOrigin` is the origin of the API. Empty means the same origin as the
 * studio, which is the case when the worker serves the pages and the API.
 */
export function resolvePublishTransport(
  apiOrigin: string | undefined,
  target: PublishTarget,
): PublishTransport {
  if (target === "fixture") {
    return {
      target: "fixture",
      baseUrl: "",
      fetch: createMockPublishFetch(),
      tokenHint: `Fixture API: the token is ${FIXTURE_TOKEN}`,
    };
  }

  return {
    target: "live",
    baseUrl: (apiOrigin ?? "").trim(),
    fetch: (input, init) => fetch(input, init),
  };
}
