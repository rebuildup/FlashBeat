import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { publishSong } from "@/lib/publish/client";
import { FIXTURE_TOKEN } from "@/lib/publish/mock";
import { resolvePublishTransport } from "@/lib/publish/transport";
import type { SongPublishRequest } from "@/lib/publish/types";

const REQUEST: SongPublishRequest = {
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  durationSec: 68,
  chart: SAMPLE_CHART,
};

describe("resolvePublishTransport", () => {
  it("publishes against the same origin when no API origin is set", () => {
    const transport = resolvePublishTransport("", "live");

    expect(transport.target).toBe("live");
    expect(transport.baseUrl).toBe("");
  });

  it("publishes against the given origin", () => {
    const transport = resolvePublishTransport("https://api.example.com", "live");

    expect(transport.target).toBe("live");
    expect(transport.baseUrl).toBe("https://api.example.com");
  });

  it("shows the fixture token, because the operator has to be able to publish", () => {
    const transport = resolvePublishTransport("", "fixture");

    expect(transport.tokenHint).toContain(FIXTURE_TOKEN);
  });

  it("publishes with the token the hint shows", async () => {
    const transport = resolvePublishTransport("", "fixture");

    const result = await publishSong({
      baseUrl: transport.baseUrl,
      songId: "who",
      token: FIXTURE_TOKEN,
      request: REQUEST,
      fetchImpl: transport.fetch,
    });

    expect(result.ok).toBe(true);
  });

  it("never shows a token hint against the real API", () => {
    const transport = resolvePublishTransport("https://api.example.com", "live");

    expect(transport.tokenHint).toBeUndefined();
  });
});
