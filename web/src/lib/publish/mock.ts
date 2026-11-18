import { validateChart } from "@/lib/chart/schema";
import type { PublishFetch } from "./types";

/**
 * Fixture implementation of the publish route, for tests and the dev-only
 * `fixture` transport.
 *
 * It answers like the deployed contract does: the same path, the same
 * bearer token, the same 401 / 422 / 200 answers. It writes nothing; the
 * live `PUT /api/v1/admin/songs/{songId}` of issue #16 is the real thing.
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

export interface MockPublishOptions {
  /** Token the fixture accepts. Defaults to `fixture-token`. */
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
  const expectedToken = options.token ?? "fixture-token";
  let chartVersion = (options.firstChartVersion ?? 1) - 1;
  let catalogVersion = (options.firstCatalogVersion ?? 1) - 1;

  const fetchImpl: PublishFetch = async (input, init) => {
    const url = new URL(input, "http://fixture.local");
    const match = PUBLISH_PATH.exec(url.pathname);
    if (!match) {
      return jsonResponse(404, { error: `公開ルートが見つかりません: ${url.pathname}` });
    }

    const songId = decodeURIComponent(match[1] ?? "");
    const method = (init?.method ?? "GET").toUpperCase();
    if (method !== "PUT") {
      return jsonResponse(405, { error: `公開ルートは${method}に応答しません` });
    }

    const headers = new Headers(init?.headers);
    const authorization = headers.get("authorization");
    if (authorization !== `Bearer ${expectedToken}`) {
      return jsonResponse(401, { error: "スタッフトークンが正しくありません" });
    }

    let body: unknown;
    try {
      body = JSON.parse(typeof init?.body === "string" ? init.body : "");
    } catch {
      return jsonResponse(422, { error: "リクエストボディが有効なJSONではありません" });
    }

    const issues = validatePublishBody(body);
    if (issues.length > 0) {
      return jsonResponse(422, { error: "曲が無効です", issues });
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
    return ["リクエストボディはJSONオブジェクトである必要があります"];
  }

  const record = body as Record<string, unknown>;
  const issues: string[] = [];

  for (const key of Object.keys(record)) {
    if (ALLOWED_FIELDS[key] === undefined) {
      issues.push(`不明なフィールド '${key}'`);
    }
  }

  const title = record["title"];
  if (typeof title !== "string" || title.length === 0) {
    issues.push("'title'は1文字以上の文字列である必要があります");
  } else if (title.length > 160) {
    issues.push("'title'は160文字以内である必要があります");
  }

  const artist = record["artist"];
  if (typeof artist !== "string") {
    issues.push("'artist'は文字列である必要があります");
  } else if (artist.length > 160) {
    issues.push("'artist'は160文字以内である必要があります");
  }

  const youtubeId = record["youtubeId"];
  if (typeof youtubeId !== "string" || !/^[A-Za-z0-9_-]{11}$/.test(youtubeId)) {
    issues.push("'youtubeId'は11文字の動画IDである必要があります");
  }

  const level = record["level"];
  if (typeof level !== "number" || !Number.isInteger(level) || level < 1 || level > 99) {
    issues.push("'level'は1〜99の整数である必要があります");
  }

  const durationSec = record["durationSec"];
  if (
    typeof durationSec !== "number" ||
    !Number.isFinite(durationSec) ||
    durationSec <= 0 ||
    durationSec > 1800
  ) {
    issues.push("'durationSec'は0〜1800の数値である必要があります");
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
