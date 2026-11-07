import { describe, expect, it } from "vitest";
import { publishAndVerify } from "../client";
import { SAMPLE_CHART } from "../../chart/fixtures";

const body = { title: "Who", artist: "Azari", youtubeId: "dQw4w9WgXcQ", level: 5, durationSec: 68, chart: SAMPLE_CHART };
describe("persisted publish verification", () => {
  for (const mismatch of [false, true]) it(mismatch ? "rejects a different persisted chart" : "confirms exact persisted snapshot without sending token to GET", async () => {
    const calls: string[] = [];
    const result = await publishAndVerify({ baseUrl: "", songId: "who", token: "runtime-only", request: body,
      fetchImpl: async (url, init) => {
        calls.push(url);
        if (init?.method === "PUT") return Response.json({ songId: "who", chartVersion: 3, catalogVersion: 8 });
        expect(init?.headers).toBeUndefined();
        expect(init?.cache).toBe("no-store");
        if (url.includes("catalog")) return Response.json({ catalogVersion: 8, songs: [{ id: "who", ...body, chartVersion: 3, noteCount: SAMPLE_CHART.notes.length }] });
        return Response.json({ songId: "who", chartVersion: 3, chart: mismatch ? { ...SAMPLE_CHART, BPM: 999 } : SAMPLE_CHART });
      },
    });
    expect(result.ok).toBe(!mismatch);
    expect(calls).toContain("/api/v1/charts/who?version=3");
  });
  it("never claims success when readback is unavailable", async () => {
    const result = await publishAndVerify({ baseUrl: "", songId: "who", token: "runtime-only", request: body, fetchImpl: async (_url, init) => init?.method === "PUT" ? Response.json({ songId: "who", chartVersion: 3, catalogVersion: 8 }) : new Response("offline", { status: 503 }) });
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.failure.message).toContain("may have succeeded");
  });
});
