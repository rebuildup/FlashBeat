import { describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { publishSong, publishUrl } from "@/lib/publish/client";
import type { SongPublishRequest } from "@/lib/publish/types";

const REQUEST: SongPublishRequest = {
  title: "Who",
  artist: "Azari",
  youtubeId: "dQw4w9WgXcQ",
  level: 5,
  durationSec: 68,
  chart: SAMPLE_CHART,
};

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("publishUrl", () => {
  it("builds the path of the publish route", () => {
    expect(publishUrl("", "who")).toBe("/api/v1/admin/songs/who");
    expect(publishUrl("https://api.example.com", "who")).toBe(
      "https://api.example.com/api/v1/admin/songs/who",
    );
    expect(publishUrl("https://api.example.com/", "who")).toBe(
      "https://api.example.com/api/v1/admin/songs/who",
    );
  });
});

describe("publishSong", () => {
  it("sends a PUT with the staff token and the body", async () => {
    const fetchImpl = vi.fn(async (_input: string, _init?: RequestInit) =>
      jsonResponse(200, { songId: "who", chartVersion: 2, catalogVersion: 7 }),
    );

    const result = await publishSong({
      baseUrl: "https://api.example.com",
      songId: "who",
      token: "secret-token",
      request: REQUEST,
      fetchImpl,
    });

    expect(result.ok).toBe(true);
    if (!result.ok) {
      return;
    }
    expect(result.response).toEqual({ songId: "who", chartVersion: 2, catalogVersion: 7 });

    expect(fetchImpl).toHaveBeenCalledTimes(1);
    const [url, init] = fetchImpl.mock.calls[0];
    expect(url).toBe("https://api.example.com/api/v1/admin/songs/who");
    expect(init?.method).toBe("PUT");
    const headers = init?.headers as Record<string, string> | undefined;
    expect(headers?.["Authorization"]).toBe("Bearer secret-token");
    expect(JSON.parse(String(init?.body))).toEqual(REQUEST);
  });

  it("never sends a request without a token", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, {}));

    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "   ",
      request: REQUEST,
      fetchImpl,
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("unauthorized");
    expect(fetchImpl).not.toHaveBeenCalled();
  });

  it("reports a rejected token", async () => {
    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "wrong",
      request: REQUEST,
      fetchImpl: async () => jsonResponse(401, { error: "Incorrect staff token" }),
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("unauthorized");
    expect(result.failure.status).toBe(401);
    expect(result.failure.details).toEqual(["Incorrect staff token"]);
  });

  it("reports a rejected song with the problems of the API", async () => {
    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "secret",
      request: REQUEST,
      fetchImpl: async () =>
        jsonResponse(422, { error: "Invalid song", issues: ["level must be between 1 and 99"] }),
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("invalid");
    expect(result.failure.status).toBe(422);
    expect(result.failure.details).toEqual(["Invalid song", "level must be between 1 and 99"]);
  });

  it("reports a missing endpoint", async () => {
    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "secret",
      request: REQUEST,
      fetchImpl: async () => jsonResponse(404, { error: "Not found" }),
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("not-found");
    expect(result.failure.status).toBe(404);
  });

  it("reports a body that does not match the contract", async () => {
    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "secret",
      request: REQUEST,
      fetchImpl: async () => jsonResponse(200, { songId: "who", chartVersion: "2" }),
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("malformed");
  });

  it("reports a request that never reached the API", async () => {
    const result = await publishSong({
      baseUrl: "",
      songId: "who",
      token: "secret",
      request: REQUEST,
      fetchImpl: async () => {
        throw new Error("connection refused");
      },
    });

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.failure.code).toBe("network");
    expect(result.failure.message).toContain("connection refused");
  });
});
