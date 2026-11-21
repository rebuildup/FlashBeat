/**
 * Unit tests for the bounded JSON body reader (issue #799).
 *
 * `readJsonLimited` is exercised directly with a minimal context double so
 * the byte-cap logic is verified deterministically — the dev-server
 * integration suite cannot send bodies near the ~2 MiB ceiling the local
 * workerd runtime enforces before a request reaches the Worker.
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";
import type { CloudContext } from "void";
import { readJsonLimited } from "../routes/api/v1/_request-body.ts";

const LIMIT = 1024;

/** Minimal CloudContext double: only what `readJsonLimited` touches. */
function fakeContext(raw: Request): CloudContext {
  return {
    req: {
      header: (name: string) => raw.headers.get(name),
      raw,
    },
    json: (data: unknown, status?: number) =>
      new Response(JSON.stringify(data), {
        status: status ?? 200,
        headers: { "content-type": "application/json" },
      }),
  } as unknown as CloudContext;
}

function requestWith(body: BodyInit | null, headers: Record<string, string> = {}): Request {
  return new Request("http://localhost/api/v1/test", {
    method: "POST",
    body,
    headers,
  });
}

describe("readJsonLimited", () => {
  it("rejects an honest oversized Content-Length before reading the body", async () => {
    // The body is a single byte — the declared length alone must trigger 413.
    const raw = requestWith("x", { "content-length": String(LIMIT + 1) });
    const result = await readJsonLimited(fakeContext(raw), LIMIT);
    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.response.status, 413);
    }
  });

  it("cuts off a streamed body that exceeds the limit", async () => {
    // No Content-Length: the chunk-by-chunk counter is the only guard.
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(new TextEncoder().encode("x".repeat(LIMIT)));
        controller.enqueue(new TextEncoder().encode("x".repeat(1)));
        controller.close();
      },
    });
    const raw = new Request("http://localhost/api/v1/test", {
      method: "POST",
      body: stream,
      // undici requires `duplex: "half"` for stream bodies; the DOM
      // RequestInit type does not declare it.
      duplex: "half",
    } as RequestInit);
    const result = await readJsonLimited(fakeContext(raw), LIMIT);
    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.response.status, 413);
    }
  });

  it("accepts a body exactly at the limit", async () => {
    const payload = JSON.stringify({ pad: "x".repeat(LIMIT - 32) });
    assert.ok(payload.length <= LIMIT);
    const raw = requestWith(payload, { "content-type": "application/json" });
    const result = await readJsonLimited(fakeContext(raw), LIMIT);
    assert.equal(result.ok, true);
    if (result.ok) {
      assert.deepEqual(result.value, { pad: "x".repeat(LIMIT - 32) });
    }
  });

  it("returns 422 for malformed JSON inside the limit", async () => {
    const raw = requestWith("{not json", { "content-type": "application/json" });
    const result = await readJsonLimited(fakeContext(raw), LIMIT);
    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.response.status, 422);
    }
  });

  it("returns 422 for a deeply nested document inside the byte cap", async () => {
    const depth = 200;
    const payload = "[".repeat(depth) + "1" + "]".repeat(depth);
    const raw = requestWith(payload);
    const result = await readJsonLimited(fakeContext(raw), LIMIT * 4);
    assert.equal(result.ok, false);
    if (!result.ok) {
      assert.equal(result.response.status, 422);
    }
  });
});
