import { parseCatalogSongs } from "@/lib/catalog/read";
import type { PublishFetch } from "./types";

/**
 * Reachability of the festival API.
 *
 * `checking` is the state between the start of the screen and the answer of
 * the probe.
 */
export type ApiReachability = "checking" | "online" | "offline";

export interface ApiProbeOptions {
  /** Origin of the API. Empty means the same origin as the studio. */
  baseUrl: string;
  /** Timeout of the probe in milliseconds. Defaults to `5000`. */
  timeoutMs?: number;
  /** Transport used for the probe. */
  fetchImpl?: PublishFetch;
}

export interface ApiProbeResult {
  reachability: Exclude<ApiReachability, "checking">;
  /** Message for the operator when the API did not answer. */
  message?: string;
}

/**
 * Check whether the API answers.
 *
 * `GET /api/v1/catalog` is public and cheap. The answer has to be a catalog
 * of the contract, because a worker without the read API is not a studio
 * that can publish either.
 */
export async function probeCatalog(options: ApiProbeOptions): Promise<ApiProbeResult> {
  const doFetch: PublishFetch = options.fetchImpl ?? ((input, init) => fetch(input, init));
  const base = options.baseUrl.replace(/\/+$/, "");

  try {
    const response = await doFetch(`${base}/api/v1/catalog`, {
      method: "GET",
      headers: { Accept: "application/json" },
      signal: AbortSignal.timeout(options.timeoutMs ?? 5000),
    });

    if (!response.ok) {
      return {
        reachability: "offline",
        message: `API 未接続: 手順を確認 — the catalog answered ${response.status}.`,
      };
    }

    if (parseCatalogSongs((await response.json()) as unknown) === null) {
      return {
        reachability: "offline",
        message: "API 未接続: 手順を確認 — the catalog route did not answer with a catalog.",
      };
    }

    return { reachability: "online" };
  } catch (error) {
    return {
      reachability: "offline",
      message: `API 未接続: 手順を確認 — ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}
