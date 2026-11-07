import { expect, it } from "vitest";
import { SAMPLE_CHART } from "../../chart/fixtures";
import { parseYouTubeId } from "../../chart/youtube";
import { judgeNoteCount, validateNewSongInfo, SONG_ID_TAKEN } from "../validation";

const VALID_INFO = {
  youtube: "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  title: "Who",
  artist: "Azari",
  songId: "who",
  level: 10,
  durationSec: 68,
};

it("accepts a complete info without issues", () => {
  expect(validateNewSongInfo(VALID_INFO)).toEqual([]);
});

it("reports every broken field at once, in Japanese and English", () => {
  const issues = validateNewSongInfo({
    youtube: "not a url",
    title: "",
    artist: "a".repeat(161),
    songId: "Who!",
    level: 0,
    durationSec: 0,
  });
  const byField = new Map(issues.map((issue) => [issue.field, issue.message]));
  expect(issues).toHaveLength(6);
  for (const message of byField.values()) {
    expect(message.ja.length).toBeGreaterThan(0);
    expect(message.en.length).toBeGreaterThan(0);
  }
  expect(byField.get("youtube")?.en).toContain("11 character");
  expect(byField.get("songId")?.en).toContain("3 to 64");
  expect(byField.get("level")?.en).toContain("between 1 and 99");
  expect(byField.get("durationSec")?.ja).toContain("0より長く");
  expect(byField.get("title")?.ja).toContain("曲名");
  expect(byField.get("artist")?.en).toContain("160");
});

it("parses every YouTube URL form the booth produces, with extra params", () => {
  const urls = [
    "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLx&t=42s",
    "https://youtu.be/dQw4w9WgXcQ?si=abc123",
    "https://www.youtube.com/shorts/dQw4w9WgXcQ?feature=share",
    "https://music.youtube.com/watch?v=dQw4w9WgXcQ&si=xyz",
    "dQw4w9WgXcQ",
  ];
  for (const url of urls) {
    expect(parseYouTubeId(url), url).toBe("dQw4w9WgXcQ");
    expect(validateNewSongInfo({ ...VALID_INFO, youtube: url })).toEqual([]);
  }
});

it("rejects a URL whose id is not 11 characters", () => {
  expect(parseYouTubeId("https://youtu.be/short")).toBeNull();
  expect(validateNewSongInfo({ ...VALID_INFO, youtube: "https://youtu.be/short" })).toHaveLength(1);
});

it("flags a taken song id with the fixed bilingual message", () => {
  expect(SONG_ID_TAKEN.ja).toBe("このIDは使用済みです。別のIDを選んでください。");
  expect(SONG_ID_TAKEN.en).toBe("This ID is already in use. Choose another one.");
});

it("blocks zero notes and warns below twenty", () => {
  expect(judgeNoteCount(0)?.level).toBe("error");
  expect(judgeNoteCount(0)?.message.en).toContain("at least one note");
  expect(judgeNoteCount(19)?.level).toBe("warn");
  expect(judgeNoteCount(19)?.message.ja).toContain("19個");
  expect(judgeNoteCount(20)).toBeNull();
  expect(judgeNoteCount(SAMPLE_CHART.notes.length)).toBeNull();
});
