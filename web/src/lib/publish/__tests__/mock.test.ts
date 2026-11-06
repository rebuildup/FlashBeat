import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { publishSong } from "@/lib/publish/client";
import { createMockPublishFetch } from "@/lib/publish/mock";
import type { SongPublishRequest } from "@/lib/publish/types";

const REQUEST: SongPublishRequest = {
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  durationSec: 68,
  chart: SAMPLE_CHART,
};

describe("createMockPublishFetch", () => {
  it("answers a publish like the contract does", async () => {
    const fixture = createMockPublishFetch();

    const response = await fixture("/api/v1/admin/songs/who", {
      method: "PUT",
      headers: { Authorization: "Bearer fixture-token" },
      body: JSON.stringify(REQUEST),
    });

    expect(response.status).toBe(200);
    await expect(response.json()).resolves.toEqual({
      songId: "who",
      chartVersion: 1,
      catalogVersion: 1,
    });
  });

  it("moves the versions with every publish", async () => {
    const fixture = createMockPublishFetch();

    for (let index = 0; index < 2; index += 1) {
      await fixture("/api/v1/admin/songs/who", {
        method: "PUT",
        headers: { Authorization: "Bearer fixture-token" },
        body: JSON.stringify(REQUEST),
      });
    }

    expect(fixture.versions).toEqual({ chartVersion: 2, catalogVersion: 2 });
  });

  it("rejects a wrong staff token", async () => {
    const fixture = createMockPublishFetch();

    const response = await fixture("/api/v1/admin/songs/who", {
      method: "PUT",
      headers: { Authorization: "Bearer wrong" },
      body: JSON.stringify(REQUEST),
    });

    expect(response.status).toBe(401);
  });

  it("rejects a body that breaks the contract", async () => {
    const fixture = createMockPublishFetch();

    const response = await fixture("/api/v1/admin/songs/who", {
      method: "PUT",
      headers: { Authorization: "Bearer fixture-token" },
      body: JSON.stringify({ ...REQUEST, level: 0 }),
    });

    expect(response.status).toBe(422);
  });

  it("rejects a chart that breaks the chart contract", async () => {
    const fixture = createMockPublishFetch();

    const response = await fixture("/api/v1/admin/songs/who", {
      method: "PUT",
      headers: { Authorization: "Bearer fixture-token" },
      body: JSON.stringify({ ...REQUEST, chart: { ...SAMPLE_CHART, BPM: "fast" } }),
    });

    expect(response.status).toBe(422);
  });

  it("answers 404 on another route", async () => {
    const fixture = createMockPublishFetch();

    const response = await fixture("/api/v1/catalog", { method: "GET" });

    expect(response.status).toBe(404);
  });
});

describe("publishSong against the fixture", () => {
  it("publishes a song without a backend", async () => {
    const fixture = createMockPublishFetch();

    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "fixture-token",
      request: REQUEST,
      fetchImpl: fixture,
    });

    expect(result.ok).toBe(true);
    if (!result.ok) {
      return;
    }
    expect(result.response.songId).toBe("who");
    expect(result.response.chartVersion).toBe(1);
  });

  it("reports the rejected token of the fixture", async () => {
    const fixture = createMockPublishFetch();

    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "not-the-token",
      request: REQUEST,
      fetchImpl: fixture,
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("unauthorized");
  });
});
