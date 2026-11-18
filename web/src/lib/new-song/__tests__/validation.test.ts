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

it("reports every broken field at once, in Japanese", () => {
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
    expect(message.length).toBeGreaterThan(0);
  }
  expect(byField.get("youtube")).toContain("11文字");
  expect(byField.get("songId")).toContain("3〜64文字");
  expect(byField.get("level")).toContain("1〜99");
  expect(byField.get("durationSec")).toContain("0より長く");
  expect(byField.get("title")).toContain("曲名");
  expect(byField.get("artist")).toContain("160");
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

it("flags a taken song id with the fixed message", () => {
  expect(SONG_ID_TAKEN).toBe("このIDは使用済みです。別のIDを選んでください。");
});

it("blocks zero notes and warns below twenty", () => {
  expect(judgeNoteCount(0)?.level).toBe("error");
  expect(judgeNoteCount(0)?.message).toContain("1つ以上");
  expect(judgeNoteCount(19)?.level).toBe("warn");
  expect(judgeNoteCount(19)?.message).toContain("19個");
  expect(judgeNoteCount(20)).toBeNull();
  expect(judgeNoteCount(SAMPLE_CHART.notes.length)).toBeNull();
});
