import { describe, expect, it } from "vitest";
import { probeCatalog } from "@/lib/publish/connectivity";

const CATALOG = {
  catalogVersion: 1,
  songs: [
    {
      id: "who",
      title: "Who",
      artist: "Azari",
      youtubeId: "dQw4w9WgXcQ",
      level: 5,
      bpm: 128,
      durationSec: 68,
      chartVersion: 1,
      noteCount: 412,
    },
  ],
};

function catalogResponse(): Response {
  return new Response(JSON.stringify(CATALOG), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });
}

describe("probeCatalog", () => {
  it("reports the API as reachable when the catalog answers", async () => {
    const requested: string[] = [];

    const result = await probeCatalog({
      baseUrl: "https://api.example.com",
      fetchImpl: async (input) => {
        requested.push(input);
        return catalogResponse();
      },
    });

    expect(result.reachability).toBe("online");
    expect(result.message).toBeUndefined();
    expect(requested).toEqual(["https://api.example.com/api/v1/catalog"]);
  });

  it("probes the same origin when no origin is set", async () => {
    const requested: string[] = [];

    await probeCatalog({
      baseUrl: "",
      fetchImpl: async (input) => {
        requested.push(input);
        return catalogResponse();
      },
    });

    expect(requested).toEqual(["/api/v1/catalog"]);
  });

  it("accepts an empty catalog as a reachable API", async () => {
    const result = await probeCatalog({
      baseUrl: "",
      fetchImpl: async () =>
        new Response(JSON.stringify({ catalogVersion: 0, songs: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
    });

    expect(result.reachability).toBe("online");
  });

  it("reports a worker without the read API as not connected", async () => {
    const result = await probeCatalog({
      baseUrl: "",
      fetchImpl: async () => new Response("not found", { status: 404 }),
    });

    expect(result.reachability).toBe("offline");
    expect(result.message).toContain("API 未接続: 手順を確認");
    expect(result.message).toContain("404");
  });

  it("reports a body that is not a catalog as not connected", async () => {
    const result = await probeCatalog({
      baseUrl: "",
      fetchImpl: async () => new Response("<html>", { status: 200 }),
    });

    expect(result.reachability).toBe("offline");
  });

  it("reports the API as not connected when the request fails", async () => {
    const result = await probeCatalog({
      baseUrl: "",
      fetchImpl: async () => {
        throw new Error("connection refused");
      },
    });

    expect(result.reachability).toBe("offline");
    expect(result.message).toContain("API 未接続: 手順を確認");
    expect(result.message).toContain("connection refused");
  });
});
