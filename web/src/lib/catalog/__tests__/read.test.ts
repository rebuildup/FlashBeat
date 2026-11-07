import { describe, expect, it } from "vitest";
import { fetchCatalogSongs, parseCatalogSongs } from "@/lib/catalog/read";

const CATALOG = {
  catalogVersion: 3,
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

describe("parseCatalogSongs", () => {
  it("reads the songs of a catalog body", () => {
    const songs = parseCatalogSongs(CATALOG);

    expect(songs).toHaveLength(1);
    expect(songs?.[0]?.id).toBe("who");
    expect(songs?.[0]?.noteCount).toBe(412);
  });

  it("accepts a catalog without songs", () => {
    expect(parseCatalogSongs({ catalogVersion: 0, songs: [] })).toEqual([]);
  });

  it("rejects a body that does not match the contract", () => {
    expect(parseCatalogSongs({ songs: [{ id: "who" }] })).toBeNull();
    expect(parseCatalogSongs({ catalogVersion: 1 })).toBeNull();
    expect(parseCatalogSongs(null)).toBeNull();
    expect(parseCatalogSongs("nope")).toBeNull();
  });
});

describe("fetchCatalogSongs", () => {
  it("reads the published songs", async () => {
    const requested: string[] = [];

    const songs = await fetchCatalogSongs({
      fetchImpl: async (input) => {
        requested.push(input);
        return new Response(JSON.stringify(CATALOG), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      },
    });

    expect(songs).toHaveLength(1);
    expect(requested).toEqual(["/api/v1/catalog"]);
  });

  it("reports an empty catalog as an empty list", async () => {
    const songs = await fetchCatalogSongs({
      fetchImpl: async () =>
        new Response(JSON.stringify({ catalogVersion: 0, songs: [] }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
    });

    expect(songs).toEqual([]);
  });

  it("reports an unreachable API as unknown", async () => {
    const songs = await fetchCatalogSongs({
      fetchImpl: async () => {
        throw new Error("connection refused");
      },
    });

    expect(songs).toBeNull();
  });

  it("reports an answer that is not a catalog as unknown", async () => {
    const songs = await fetchCatalogSongs({
      fetchImpl: async () => new Response("<html>", { status: 200 }),
    });

    expect(songs).toBeNull();
  });
});
