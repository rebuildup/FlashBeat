import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { publishUrl } from "@/lib/publish/client";
import { resolvePublishTransport } from "@/lib/publish/transport";

describe("resolvePublishTransport", () => {
  it("publishes to the API of the current origin when no origin is configured", () => {
    const transport = resolvePublishTransport();

    expect(transport.mode).toBe("live");
    expect(transport.baseUrl).toBe("");
    expect(publishUrl(transport.baseUrl, "who")).toBe("/api/v1/admin/songs/who");
  });

  it("publishes to the configured origin", () => {
    const transport = resolvePublishTransport("https://api.example.com");

    expect(transport.mode).toBe("live");
    expect(transport.baseUrl).toBe("https://api.example.com");
    expect(publishUrl(transport.baseUrl, "who")).toBe(
      "https://api.example.com/api/v1/admin/songs/who",
    );
  });

  it("answers locally only when the fixture is asked for explicitly", async () => {
    const transport = resolvePublishTransport("fixture");

    expect(transport.mode).toBe("fixture");

    // The fixture answers the publish route with the contract answers; the
    // deployed API would reject this token with 401.
    const response = await transport.fetch(publishUrl(transport.baseUrl, "who"), {
      method: "PUT",
      headers: {
        Authorization: "Bearer fixture-token",
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        title: "Who",
        artist: "Azari",
        youtubeId: "dQw4w9WgXcQ",
        level: 5,
        durationSec: 68,
        chart: SAMPLE_CHART,
      }),
    });

    expect(response.status).toBe(200);
  });
});
