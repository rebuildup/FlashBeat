import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import {
  buildPublishRequest,
  validateSongMetadata,
  type SongMetadataDraft,
} from "@/lib/publish/validation";

const VALID_DRAFT: SongMetadataDraft = {
  songId: "who",
  title: "Who",
  artist: "Azari",
  youtube: "dQw4w9WgXcQ",
  level: 5,
  durationSec: 68,
};

function fieldsOf(issues: { field: string }[]): string[] {
  return issues.map((issue) => issue.field).sort();
}

describe("validateSongMetadata", () => {
  it("accepts a draft that satisfies the contract", () => {
    expect(validateSongMetadata(VALID_DRAFT)).toEqual([]);
  });

  it("reports every broken field at once", () => {
    const issues = validateSongMetadata({
      songId: "Who!",
      title: " ",
      artist: "x".repeat(161),
      youtube: "not a video",
      level: 0,
      durationSec: Number.NaN,
    });

    expect(fieldsOf(issues)).toEqual([
      "artist",
      "durationSec",
      "level",
      "songId",
      "title",
      "youtube",
    ]);
  });

  it("keeps the level inside the range of the contract", () => {
    expect(validateSongMetadata({ ...VALID_DRAFT, level: 100 })).toHaveLength(1);
    expect(validateSongMetadata({ ...VALID_DRAFT, level: 99 })).toEqual([]);
    expect(validateSongMetadata({ ...VALID_DRAFT, level: 1 })).toEqual([]);
    expect(validateSongMetadata({ ...VALID_DRAFT, level: 10.5 })).toHaveLength(1);
  });

  it("keeps the duration inside the range of the contract", () => {
    expect(validateSongMetadata({ ...VALID_DRAFT, durationSec: 0 })).toHaveLength(1);
    expect(validateSongMetadata({ ...VALID_DRAFT, durationSec: 1801 })).toHaveLength(1);
    expect(validateSongMetadata({ ...VALID_DRAFT, durationSec: 1800 })).toEqual([]);
  });

  it("accepts a YouTube URL as well as a video id", () => {
    expect(
      validateSongMetadata({
        ...VALID_DRAFT,
        youtube: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
      }),
    ).toEqual([]);
    expect(
      validateSongMetadata({ ...VALID_DRAFT, youtube: "https://youtu.be/dQw4w9WgXcQ" }),
    ).toEqual([]);
  });
});

describe("buildPublishRequest", () => {
  it("builds the body of the publish route", () => {
    const built = buildPublishRequest(VALID_DRAFT, SAMPLE_CHART);

    expect(built.ok).toBe(true);
    if (!built.ok) {
      return;
    }

    expect(built.request).toEqual({
      title: "Who",
      artist: "Azari",
      youtubeId: "dQw4w9WgXcQ",
      level: 5,
      durationSec: 68,
      chart: SAMPLE_CHART,
    });
  });

  it("turns a YouTube URL into the video id of the contract", () => {
    const built = buildPublishRequest(
      { ...VALID_DRAFT, youtube: "https://www.youtube.com/watch?v=dQw4w9WgXcQ" },
      SAMPLE_CHART,
    );

    expect(built.ok).toBe(true);
    if (!built.ok) {
      return;
    }

    expect(built.request.youtubeId).toBe("dQw4w9WgXcQ");
  });

  it("trims the title and the artist", () => {
    const built = buildPublishRequest(
      { ...VALID_DRAFT, title: "  Who  ", artist: "  Azari  " },
      SAMPLE_CHART,
    );

    expect(built.ok).toBe(true);
    if (!built.ok) {
      return;
    }

    expect(built.request.title).toBe("Who");
    expect(built.request.artist).toBe("Azari");
  });

  it("refuses a draft that breaks the contract", () => {
    const built = buildPublishRequest({ ...VALID_DRAFT, title: "" }, SAMPLE_CHART);

    expect(built.ok).toBe(false);
    if (built.ok) {
      return;
    }

    expect(fieldsOf(built.issues)).toEqual(["title"]);
  });
});
