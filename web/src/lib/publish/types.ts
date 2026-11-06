import type { Chart } from "@/lib/chart/types";

/**
 * Contract types of the publish endpoint.
 *
 * `PUT /api/v1/admin/songs/{songId}` is frozen in `contracts/openapi.v1.yaml`.
 * The shapes below mirror `SongPublishRequest` and the 200 response of that
 * route, so the client never has to describe the wire format twice.
 */

/** Body of `PUT /api/v1/admin/songs/{songId}`. */
export interface SongPublishRequest {
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  chart: Chart;
}

/** Response of a successful publish. */
export interface SongPublishResponse {
  songId: string;
  chartVersion: number;
  catalogVersion: number;
}

/** Why a publish did not succeed. */
export type PublishFailureCode =
  | "unauthorized"
  | "invalid"
  | "not-found"
  | "server"
  | "network"
  | "malformed";

export interface PublishFailure {
  code: PublishFailureCode;
  /** Message shown to the operator. */
  message: string;
  /** HTTP status, when the failure came from a response. */
  status?: number;
  /** Field level problems reported by the API. */
  details?: string[];
}

/** Result of a publish. The failure is a value, never an exception. */
export type PublishResult =
  | { ok: true; response: SongPublishResponse }
  | { ok: false; failure: PublishFailure };

/** The part of `fetch` the publish client needs. */
export type PublishFetch = (input: string, init?: RequestInit) => Promise<Response>;
