import { expect, it } from "vitest";
import { fetchOembedMetadata, oembedRequestUrl } from "../oembed";

it("builds the CORS oEmbed endpoint of the watch URL", () => {
  expect(oembedRequestUrl("dQw4w9WgXcQ")).toBe(
    "https://www.youtube.com/oembed?url=" +
      encodeURIComponent("https://www.youtube.com/watch?v=dQw4w9WgXcQ") +
      "&format=json",
  );
});

it("maps the oEmbed body to title and author", async () => {
  const result = await fetchOembedMetadata("dQw4w9WgXcQ", async (url) => {
    expect(url).toContain("www.youtube.com/oembed");
    return Response.json({ title: "Who", author_name: "Azari" });
  });
  expect(result).toEqual({ ok: true, title: "Who", author: "Azari" });
});

it("treats a private or deleted video as not-found", async () => {
  const result = await fetchOembedMetadata("dQw4w9WgXcQ", async () => new Response("", { status: 404 }));
  expect(result).toEqual({ ok: false, reason: "not-found" });
});

it("reports an unreachable network as offline instead of throwing", async () => {
  const result = await fetchOembedMetadata("dQw4w9WgXcQ", async () => {
    throw new Error("network down");
  });
  expect(result).toEqual({ ok: false, reason: "offline" });
});

it("rejects a broken oEmbed body as malformed", async () => {
  const result = await fetchOembedMetadata("dQw4w9WgXcQ", async () => Response.json({ author_name: "Azari" }));
  expect(result).toEqual({ ok: false, reason: "malformed" });
});

it("does not fetch for a video id that cannot be valid", async () => {
  let called = false;
  const result = await fetchOembedMetadata("short", async () => {
    called = true;
    return Response.json({});
  });
  expect(called).toBe(false);
  expect(result).toEqual({ ok: false, reason: "invalid-id" });
});
