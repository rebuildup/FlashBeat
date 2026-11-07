import { validateChart } from "@/lib/chart/schema";
import type { PublishFetch } from "./types";

/**
 * Fixture implementation of the publish route.
 *
 * The API of issue #16 does not exist yet, so the studio publishes against
 * this transport. It answers like the contract does: the same path, the same
 * bearer token, the same 401 / 422 / 200 answers. The screen code is the
 * same in both modes, only the transport changes.
 */

const PUBLISH_PATH = /^\/api\/v1\/admin\/songs\/([^/]+)$/;

const ALLOWED_FIELDS: Record<string, true> = {
  artist: true,
  chart: true,
  durationSec: true,
  level: true,
  title: true,
  youtubeId: true,
};

/**
 * Token the fixture accepts.
 *
 * The fixture has no secret, so the token is public. It is only ever shown
 * while the screen runs against the fixture, never against the real API.
 */
export const FIXTURE_TOKEN = "fixture-token";

export interface MockPublishOptions {
  /** Token the fixture accepts. Defaults to {@link FIXTURE_TOKEN}. */
  token?: string;
  /** Chart version of the first successful publish. Defaults to `1`. */
  firstChartVersion?: number;
  /** Catalog version of the first successful publish. Defaults to `1`. */
  firstCatalogVersion?: number;
}

export interface MockPublishFetch extends PublishFetch {
  /** Versions the fixture handed out so far. */
  readonly versions: { chartVersion: number; catalogVersion: number };
}

/**
 * Build a `fetch` that answers the publish route locally.
 *
 * The versions increase with every successful publish, so the operator sees
 * the catalog move the way the real API moves it.
 */
export function createMockPublishFetch(options: MockPublishOptions = {}): MockPublishFetch {
  const expectedToken = options.token ?? FIXTURE_TOKEN;
  let chartVersion = (options.firstChartVersion ?? 1) - 1;
  let catalogVersion = (options.firstCatalogVersion ?? 1) - 1;

  const fetchImpl: PublishFetch = async (input, init) => {
    const url = new URL(input, "http://fixture.local");
    const match = PUBLISH_PATH.exec(url.pathname);
    if (!match) {
      return jsonResponse(404, { error: `No publish route for ${url.pathname}` });
    }

    const songId = decodeURIComponent(match[1] ?? "");
    const method = (init?.method ?? "GET").toUpperCase();
    if (method !== "PUT") {
      return jsonResponse(405, { error: `The publish route does not answer ${method}` });
    }

    const headers = new Headers(init?.headers);
    const authorization = headers.get("authorization");
    if (authorization !== `Bearer ${expectedToken}`) {
      return jsonResponse(401, { error: "Incorrect staff token" });
    }

    let body: unknown;
    try {
      body = JSON.parse(typeof init?.body === "string" ? init.body : "");
    } catch {
      return jsonResponse(422, { error: "The request body is not valid JSON" });
    }

    const issues = validatePublishBody(body);
    if (issues.length > 0) {
      return jsonResponse(422, { error: "Invalid song", issues });
    }

    chartVersion += 1;
    catalogVersion += 1;

    return jsonResponse(200, { songId, chartVersion, catalogVersion });
  };

  Object.defineProperty(fetchImpl, "versions", {
    get: () => ({ chartVersion, catalogVersion }),
    enumerable: true,
  });

  return fetchImpl as MockPublishFetch;
}

/**
 * Check a request body against the contract.
 *
 * The chart is checked with the frozen chart schema, so a chart the API would
 * reject never reaches the success path.
 */
export function validatePublishBody(body: unknown): string[] {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return ["The request body must be a JSON object"];
  }

  const record = body as Record<string, unknown>;
  const issues: string[] = [];

  for (const key of Object.keys(record)) {
    if (ALLOWED_FIELDS[key] === undefined) {
      issues.push(`Unexpected field '${key}'`);
    }
  }

  const title = record["title"];
  if (typeof title !== "string" || title.length === 0) {
    issues.push("'title' must be a string with at least one character");
  } else if (title.length > 160) {
    issues.push("'title' must be 160 characters or fewer");
  }

  const artist = record["artist"];
  if (typeof artist !== "string") {
    issues.push("'artist' must be a string");
  } else if (artist.length > 160) {
    issues.push("'artist' must be 160 characters or fewer");
  }

  const youtubeId = record["youtubeId"];
  if (typeof youtubeId !== "string" || !/^[A-Za-z0-9_-]{11}$/.test(youtubeId)) {
    issues.push("'youtubeId' must be an 11 character video id");
  }

  const level = record["level"];
  if (typeof level !== "number" || !Number.isInteger(level) || level < 1 || level > 99) {
    issues.push("'level' must be a whole number between 1 and 99");
  }

  const durationSec = record["durationSec"];
  if (
    typeof durationSec !== "number" ||
    !Number.isFinite(durationSec) ||
    durationSec <= 0 ||
    durationSec > 1800
  ) {
    issues.push("'durationSec' must be a number between 0 and 1800");
  }

  for (const issue of validateChart(record["chart"])) {
    issues.push(`chart${issue.path}: ${issue.message}`);
  }

  return issues;
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
