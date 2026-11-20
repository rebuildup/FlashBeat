/**
 * Bounded JSON body reader for the mutation APIs (issue #799, threat
 * model "データサイズ・リソース攻撃").
 *
 * `c.req.json()` buffers the whole body before parsing, so a caller can
 * make the Worker allocate an arbitrary amount of memory with one request.
 * This reader enforces the limit twice:
 *
 *   1. A `Content-Length` pre-check rejects an honest oversized request
 *      before a single body byte is read (cheap 413).
 *   2. The body stream is counted chunk by chunk; a request that lies about
 *      (or omits) `Content-Length` is still cut off at `maxBytes`.
 *
 * Only then is the text parsed as JSON — invalid JSON is a 422, exactly
 * like the previous `c.req.json()` handlers reported it.
 *
 * Prefixed with `_` so Void does not turn it into a route.
 */
import type { CloudContext } from "void";

/** `POST /api/v1/scores` payloads are a few hundred bytes of JSON. */
export const SCORE_SUBMISSION_BODY_LIMIT = 32 * 1024;
/**
 * `PUT /api/v1/admin/songs/{songId}` carries a whole chart document. The
 * bound is generous for real charts but still far below anything that
 * could pressure the Worker; D1's own row limit is the harder bound.
 */
export const PUBLISH_BODY_LIMIT = 2 * 1024 * 1024;
/** Archive/unarchive bodies are `{"archived": bool}` — a few bytes. */
export const ARCHIVE_BODY_LIMIT = 16 * 1024;

export type LimitedJsonBody =
  | { ok: true; value: unknown }
  | { ok: false; response: Response };

/**
 * True when a declared `Content-Length` already exceeds the limit. A
 * missing or unparsable header cannot be judged here — the streaming count
 * in `readJsonLimited` covers it.
 */
export function contentLengthExceeded(
  contentLength: string | null | undefined,
  maxBytes: number,
): boolean {
  if (contentLength == null) {
    return false;
  }
  const length = Number(contentLength);
  return Number.isFinite(length) && length > maxBytes;
}

/**
 * Read and parse the request body with a hard byte cap.
 *
 * @returns `{ ok: true, value }` with the parsed JSON, or
 *   `{ ok: false, response }` with a ready `413`/`422` JSON response the
 *   handler returns unchanged.
 */
export async function readJsonLimited(
  c: CloudContext,
  maxBytes: number,
): Promise<LimitedJsonBody> {
  if (contentLengthExceeded(c.req.header("content-length"), maxBytes)) {
    return {
      ok: false,
      response: c.json(
        {
          error: `request body exceeds the ${maxBytes} byte limit`,
          diagnostics: [
            { path: "(body)", message: `must not exceed ${maxBytes} bytes` },
          ],
        },
        413,
      ),
    };
  }

  const body = c.req.raw.body;
  const chunks: Uint8Array[] = [];
  let total = 0;
  if (body !== null) {
    const reader = body.getReader();
    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) {
          break;
        }
        total += value.byteLength;
        if (total > maxBytes) {
          await reader.cancel();
          return {
            ok: false,
            response: c.json(
              {
                error: `request body exceeds the ${maxBytes} byte limit`,
                diagnostics: [
                  { path: "(body)", message: `must not exceed ${maxBytes} bytes` },
                ],
              },
              413,
            ),
          };
        }
        chunks.push(value);
      }
    } finally {
      reader.releaseLock();
    }
  }

  const bytes = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }

  let value: unknown;
  try {
    value = JSON.parse(new TextDecoder().decode(bytes));
  } catch {
    return {
      ok: false,
      response: c.json(
        {
          error: "request body is not valid JSON",
          diagnostics: [{ path: "(body)", message: "must be a single JSON object" }],
        },
        422,
      ),
    };
  }

  // Structural limits (threat model §4.2): a body inside the byte cap can
  // still be pathologically nested or node-dense. The walk is iterative so
  // a deeply nested document cannot overflow the stack here either.
  const limits = jsonStructureLimits(value);
  if (limits !== null) {
    return {
      ok: false,
      response: c.json(
        {
          error: "request body exceeds structural limits",
          diagnostics: [{ path: "(body)", message: limits }],
        },
        422,
      ),
    };
  }
  return { ok: true, value };
}

/**
 * Hard bounds on the parsed JSON graph. Generous on purpose — the byte cap
 * is the real limit; these exist so a small-but-dense or deeply nested
 * document is rejected before any handler walks it.
 */
const MAX_JSON_DEPTH = 100;
const MAX_JSON_NODES = 1_000_000;
const MAX_JSON_STRING = 1024 * 1024;

/**
 * Returns a diagnostic string when `value` breaks the structural limits,
 * `null` when it is acceptable. Counts every object key, array element and
 * scalar as one node.
 */
export function jsonStructureLimits(value: unknown): string | null {
  let nodes = 0;
  // Each stack entry is [value, depth].
  const stack: [unknown, number][] = [[value, 0]];
  while (stack.length > 0) {
    const [current, depth] = stack.pop() as [unknown, number];
    if (depth > MAX_JSON_DEPTH) {
      return `nesting deeper than ${MAX_JSON_DEPTH} levels`;
    }
    nodes += 1;
    if (nodes > MAX_JSON_NODES) {
      return `more than ${MAX_JSON_NODES} values`;
    }
    if (typeof current === "string" && current.length > MAX_JSON_STRING) {
      return `a string longer than ${MAX_JSON_STRING} characters`;
    }
    if (Array.isArray(current)) {
      for (const item of current) {
        stack.push([item, depth + 1]);
      }
    } else if (typeof current === "object" && current !== null) {
      for (const key of Object.keys(current)) {
        nodes += 1;
        if (nodes > MAX_JSON_NODES) {
          return `more than ${MAX_JSON_NODES} values`;
        }
        stack.push([(current as Record<string, unknown>)[key], depth + 1]);
      }
    }
  }
  return null;
}
