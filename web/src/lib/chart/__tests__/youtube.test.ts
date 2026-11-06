import { describe, expect, it } from "vitest";
import { parseYouTubeId } from "../youtube";

describe("parseYouTubeId", () => {
  it("accepts a bare video id", () => {
    expect(parseYouTubeId("dQw4w9WgXcQ")).toBe("dQw4w9WgXcQ");
  });

  it("reads the id of a watch url", () => {
    expect(parseYouTubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ")).toBe("dQw4w9WgXcQ");
  });

  it("reads the id of a short url", () => {
    expect(parseYouTubeId("https://youtu.be/dQw4w9WgXcQ")).toBe("dQw4w9WgXcQ");
  });

  it("reads the id of an embed url", () => {
    expect(parseYouTubeId("https://www.youtube.com/embed/dQw4w9WgXcQ")).toBe("dQw4w9WgXcQ");
  });

  it("reads the id of a shorts url", () => {
    expect(parseYouTubeId("https://www.youtube.com/shorts/dQw4w9WgXcQ")).toBe("dQw4w9WgXcQ");
  });

  it("keeps extra query parameters out of the id", () => {
    expect(parseYouTubeId("https://www.youtube.com/watch?list=PL123&v=dQw4w9WgXcQ&t=30")).toBe(
      "dQw4w9WgXcQ",
    );
  });

  it("rejects anything that is not a video id", () => {
    expect(parseYouTubeId("")).toBeNull();
    expect(parseYouTubeId("   ")).toBeNull();
    expect(parseYouTubeId("not an id")).toBeNull();
    expect(parseYouTubeId("https://example.com/watch?v=dQw4w9WgXcQ")).toBeNull();
    expect(parseYouTubeId("https://www.youtube.com/watch?v=tooshort")).toBeNull();
  });
});
