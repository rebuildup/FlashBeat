import { expect, it } from "vitest";
import { SAMPLE_CHART } from "../../chart/fixtures";
import { publishAndVerify } from "../../publish/client";
import type { SongPublishRequest } from "../../publish/types";
import { createFixtureStudioFetch } from "../fixture-api";

const request: SongPublishRequest = {
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  durationSec: 68,
  chart: SAMPLE_CHART,
};

it("publishes and serves the exact snapshot back to publishAndVerify", async () => {
  const fetchImpl = createFixtureStudioFetch();
  const result = await publishAndVerify({
    baseUrl: "",
    songId: "who",
    token: "fixture-token",
    request,
    fetchImpl,
  });
  expect(result.ok).toBe(true);
  if (result.ok) {
    expect(result.response).toEqual({ songId: "who", chartVersion: 1, catalogVersion: 1 });
  }

  const catalog = (await (await fetchImpl("/api/v1/catalog")).json()) as {
    catalogVersion: number;
    songs: Array<Record<string, unknown>>;
  };
  expect(catalog.catalogVersion).toBe(1);
  expect(catalog.songs).toEqual([
    {
      id: "who",
      title: "Who",
      artist: "Azari",
      youtubeId: "dQw4w9WgXcQ",
      level: 5,
      bpm: SAMPLE_CHART.BPM,
      durationSec: 68,
      chartVersion: 1,
      noteCount: SAMPLE_CHART.notes.length,
    },
  ]);
});

it("bumps the chart version on a republish and moves the catalog", async () => {
  const fetchImpl = createFixtureStudioFetch();
  const first = await publishAndVerify({ baseUrl: "", songId: "who", token: "fixture-token", request, fetchImpl });
  const second = await publishAndVerify({
    baseUrl: "",
    songId: "who",
    token: "fixture-token",
    request: { ...request, level: 6 },
    fetchImpl,
  });
  expect(first.ok).toBe(true);
  expect(second.ok).toBe(true);
  if (second.ok) {
    expect(second.response).toEqual({ songId: "who", chartVersion: 2, catalogVersion: 2 });
  }
  expect(fetchImpl.songs.get("who")?.level).toBe(6);
});

it("rejects a wrong token before reading the body", async () => {
  const fetchImpl = createFixtureStudioFetch();
  const result = await publishAndVerify({ baseUrl: "", songId: "who", token: "nope", request, fetchImpl });
  expect(result.ok).toBe(false);
  if (!result.ok) {
    expect(result.failure.status).toBe(401);
  }
  expect(fetchImpl.songs.size).toBe(0);
});

it("rejects a body that breaks the contract with 422 diagnostics", async () => {
  const fetchImpl = createFixtureStudioFetch();
  const result = await publishAndVerify({
    baseUrl: "",
    songId: "who",
    token: "fixture-token",
    request: { ...request, level: 100 },
    fetchImpl,
  });
  expect(result.ok).toBe(false);
  if (!result.ok) {
    expect(result.failure.status).toBe(422);
    expect(result.failure.details?.join("\n")).toContain("level");
  }
});

it("answers 404 for an unknown chart version like the read API", async () => {
  const fetchImpl = createFixtureStudioFetch();
  await publishAndVerify({ baseUrl: "", songId: "who", token: "fixture-token", request, fetchImpl });
  const response = await fetchImpl("/api/v1/charts/who?version=2");
  expect(response.status).toBe(404);
});
