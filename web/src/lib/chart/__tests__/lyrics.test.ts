import { describe, expect, it } from "vitest";
import { exportChartJson } from "../export";
import { importChartJson } from "../import";
import type { Chart, ChartLyrics } from "../types";
import {
  activeLyricIndex,
  addLyricRow,
  buildLyricsArrays,
  canSaveLyrics,
  chartWithLyrics,
  formatTimecode,
  formatTimeInput,
  lyricsIssues,
  moveLyricRow,
  parseTimeInput,
  readLyrics,
  removeLyricRow,
  rowInterval,
  rowIssues,
  setLyricEnd,
  setLyricStart,
  sortLyricRows,
  summarizeLyrics,
  updateLyricRow,
} from "../lyrics";
import { listRepositoryCharts, readRepositoryChart } from "./repository-charts";

/**
 * Lyrics model against the real charts of the Unity project.
 *
 * The gate of issue #165 is a working round trip: read the lyrics of a chart,
 * edit one line, write the chart back, read it again. Untouched lines must
 * keep their exact values — `Who` has `endTime < startTime` on all 28 lines and
 * is the regression that must not be corrupted.
 */
function readChart(name: string): Chart {
  const imported = importChartJson(readRepositoryChart(name));
  expect(imported.ok).toBe(true);
  if (!imported.ok) {
    throw new Error(`chart ${name} did not import`);
  }
  return imported.chart;
}

describe("time input", () => {
  it("accepts seconds, m:ss and h:mm:ss", () => {
    expect(parseTimeInput("12")).toBe(12);
    expect(parseTimeInput("12.5")).toBe(12.5);
    expect(parseTimeInput("0:12.5")).toBe(12.5);
    expect(parseTimeInput("1:02:03.250")).toBe(3723.25);
    expect(parseTimeInput("90:00")).toBe(5400);
  });

  it("accepts full-width digits, colons and periods from a Japanese IME", () => {
    expect(parseTimeInput("３．５")).toBe(3.5);
    expect(parseTimeInput("１：０３．２５")).toBe(63.25);
  });

  it("treats an empty field as cleared and a bad value as invalid", () => {
    expect(parseTimeInput("")).toBeNull();
    expect(parseTimeInput("   ")).toBeNull();
    expect(parseTimeInput("abc")).toBeUndefined();
    expect(parseTimeInput("-1")).toBeUndefined();
    expect(parseTimeInput("1:75")).toBeUndefined();
    expect(parseTimeInput("1:2:75")).toBeUndefined();
    expect(parseTimeInput(".")).toBeUndefined();
  });

  it("rounds to milliseconds", () => {
    expect(parseTimeInput("3.427000045776367")).toBe(3.427);
    expect(formatTimeInput(3.427000045776367)).toBe("3.427");
    expect(formatTimeInput(null)).toBe("");
    expect(formatTimeInput(3)).toBe("3");
  });

  it("formats a timecode for readouts", () => {
    expect(formatTimecode(3.427)).toBe("0:03.43");
    expect(formatTimecode(63.2)).toBe("1:03.20");
  });
});

describe("reading the repository lyrics", () => {
  it("keeps Who's 28 reversed intervals readable", () => {
    const chart = readChart("Who.json");
    const document = readLyrics(chart.lyrics);

    expect(document.rows).toHaveLength(28);
    expect(document.rows[0]?.mondai).toBe("交わした約束の言葉の始末");

    const summary = summarizeLyrics(document);
    expect(summary.legacyIntervals).toBe(28);
    expect(summary.arrayLengthMismatch).toBe(false);
    expect(summary.missingStart).toBe(0);

    // The runtime derives the end from the next line, so the line is visible
    // even though its own endTime is before its start.
    const first = rowInterval(document, 0);
    expect(first.start).toBeCloseTo(3.427, 3);
    expect(first.end).toBeCloseTo(7.196, 3);
    expect(first.derived).toBe(true);

    expect(activeLyricIndex(document, 5)).toBe(0);
    expect(activeLyricIndex(document, 7.2)).toBe(1);
    expect(activeLyricIndex(document, 1)).toBeNull();
  });

  it("shows GURU's four text-only rows instead of dropping them", () => {
    const chart = readChart("GURU.json");
    const document = readLyrics(chart.lyrics);

    expect(document.rows).toHaveLength(54);
    const summary = summarizeLyrics(document);
    expect(summary.missingStart).toBe(4);
    expect(summary.arrayLengthMismatch).toBe(true);
    expect(canSaveLyrics(document)).toBe(false);
    expect(lyricsIssues(document).some((issue) => issue.severity === "error")).toBe(true);
  });

  it("returns the untouched lyrics object while the document is clean", () => {
    for (const fileName of listRepositoryCharts()) {
      const chart = readChart(fileName);
      if (!chart.lyrics) {
        continue;
      }

      const document = readLyrics(chart.lyrics);
      expect(buildLyricsArrays(document)).toBe(chart.lyrics);
      expect(exportChartJson(chartWithLyrics(chart, document))).toBe(exportChartJson(chart));
    }
  });
});

describe("editing a real chart", () => {
  it("preserves Who's raw times when only the text is edited", () => {
    const chart = readChart("Who.json");
    const original = chart.lyrics as Required<ChartLyrics>;
    const document = readLyrics(chart.lyrics);

    const edited = updateLyricRow(document, document.rows[0]!.id, {
      mondai: "編集した行",
      furigana: "へんしゅう",
      romaji: "henshu",
    });
    const lyrics = buildLyricsArrays(edited);

    expect(lyrics.mondai![0]).toBe("編集した行");
    expect(lyrics.furigana![0]).toBe("へんしゅう");
    expect(lyrics.romaji![0]).toBe("henshu");
    // Every other text entry and all 28 time values stay exactly as shipped.
    expect(lyrics.startTime).toEqual(original.startTime);
    expect(lyrics.endTime).toEqual(original.endTime);
    expect(lyrics.mondai!.slice(1)).toEqual(original.mondai.slice(1));

    // The raw interval survives even though it is still `end < start`.
    expect(lyrics.endTime![0]).toBe(original.endTime[0]);
    expect(summarizeLyrics(readLyrics(lyrics)).legacyIntervals).toBe(28);
  });

  it("writes only the edited time of a well formed chart", () => {
    const chart = readChart("チュートリアル.json");
    const original = chart.lyrics as Required<ChartLyrics>;
    const document = readLyrics(chart.lyrics);

    const edited = setLyricStart(document, document.rows[1]!.id, 3.25);
    const lyrics = buildLyricsArrays(edited);

    expect(lyrics.startTime![0]).toBe(original.startTime[0]);
    expect(lyrics.startTime![1]).toBe(3.25);
    expect(lyrics.startTime!.slice(2)).toEqual(original.startTime.slice(2));
    // `チュートリアル` ships 53 starts but only 52 ends, so the save writes the
    // derived end of the last line. Every other end stays byte identical.
    expect(lyrics.endTime!.slice(0, original.endTime.length)).toEqual(original.endTime);
    expect(lyrics.endTime).toHaveLength(lyrics.startTime!.length);
    expect(lyrics.mondai).toEqual(original.mondai);
    expect(lyrics.furigana).toEqual(original.furigana);
    expect(lyrics.romaji).toEqual(original.romaji);
  });

  it("normalizes every array after editing GURU's ragged lyrics", () => {
    const chart = readChart("GURU.json");
    const original = chart.lyrics as Required<ChartLyrics>;
    const document = readLyrics(chart.lyrics);

    const edited = updateLyricRow(document, document.rows[0]!.id, { mondai: "あ" });
    const lyrics = buildLyricsArrays(edited);

    expect(lyrics.startTime).toHaveLength(54);
    expect(lyrics.endTime).toHaveLength(54);
    expect(lyrics.furigana).toHaveLength(54);
    expect(lyrics.mondai).toHaveLength(54);
    expect(lyrics.romaji).toHaveLength(54);

    expect(lyrics.startTime!.slice(0, 50)).toEqual(original.startTime);
    expect(lyrics.endTime!.slice(0, 49)).toEqual(original.endTime);
    for (const value of lyrics.endTime!) {
      expect(Number.isFinite(value)).toBe(true);
    }
    for (let index = 0; index < 54; index += 1) {
      expect(lyrics.endTime![index]).toBeGreaterThan(lyrics.startTime![index]!);
    }
  });

  it("repairs an edited interval on save and reports it beforehand", () => {
    const chart = readChart("チュートリアル.json");
    const document = readLyrics(chart.lyrics);
    const row = document.rows[0]!;

    const edited = setLyricEnd(document, row.id, (row.startTime ?? 0) - 0.5);
    const summary = summarizeLyrics(edited);
    expect(summary.repairedIntervals).toBe(1);
    expect(rowIssues(edited, 0).endTime).toBe("End must be after start");
    expect(lyricsIssues(edited).some((issue) => issue.message.includes("repaired"))).toBe(true);

    const lyrics = buildLyricsArrays(edited);
    expect(lyrics.endTime![0]).toBeGreaterThan(lyrics.startTime![0]!);
    expect(lyrics.startTime).toHaveLength(lyrics.mondai!.length);
    expect(lyrics.endTime).toHaveLength(lyrics.mondai!.length);
  });

  it("round trips an edit through export and import", () => {
    const chart = readChart("まにまに.json");
    const document = readLyrics(chart.lyrics);

    const edited = updateLyricRow(document, document.rows[3]!.id, {
      mondai: "新しい本文",
      furigana: "あたらしいほんぶん",
      romaji: "atarashii honbun",
    });
    const written = exportChartJson({ ...chartWithLyrics(chart, edited), videoId: "dQw4w9WgXcQ" });

    const reparsed = importChartJson(written);
    expect(reparsed.ok).toBe(true);
    if (!reparsed.ok) {
      return;
    }

    expect(reparsed.chart.videoId).toBe("dQw4w9WgXcQ");
    expect(reparsed.chart.lyrics?.mondai?.[3]).toBe("新しい本文");
    expect(reparsed.chart.lyrics?.furigana?.[3]).toBe("あたらしいほんぶん");
    expect(reparsed.chart.lyrics?.romaji?.[3]).toBe("atarashii honbun");
    expect(reparsed.chart.notes).toEqual(chart.notes);
  });
});

describe("line operations", () => {
  const chart = readChart("チュートリアル.json");

  it("adds a line after the requested row", () => {
    const document = readLyrics(chart.lyrics);
    const after = document.rows[0]!.id;
    const next = addLyricRow(document, after);

    expect(next.rows).toHaveLength(document.rows.length + 1);
    expect(next.rows[1]!.id).not.toBe(document.rows[1]!.id);
    // The new line starts where the previous one ends, rounded to the
    // millisecond precision every edited time uses.
    expect(next.rows[1]!.startTime).toBeCloseTo(rowInterval(document, 0).end ?? 0, 3);
    expect(next.rows[1]!.endTime).toBeGreaterThan(next.rows[1]!.startTime!);
    expect(next.rows[0]!.id).toBe(document.rows[0]!.id);
  });

  it("clamps an inserted line to the next line start", () => {
    const document = readLyrics(chart.lyrics);
    if (document.rows.length < 2) {
      return;
    }
    const next = addLyricRow(document, document.rows[0]!.id);
    expect(next.rows[1]!.endTime).toBeLessThanOrEqual(rowInterval(next, 2).start ?? Infinity);
  });

  it("removes a line and keeps the other rows", () => {
    const document = readLyrics(chart.lyrics);
    const removed = removeLyricRow(document, document.rows[0]!.id);

    expect(removed.rows).toHaveLength(document.rows.length - 1);
    expect(removed.rows[0]!.id).toBe(document.rows[1]!.id);
    expect(removed.rows[0]!.mondai).toBe(document.rows[1]!.mondai);
  });

  it("swaps the text of neighbouring lines without moving their time slots", () => {
    const document = readLyrics(chart.lyrics);
    const moved = moveLyricRow(document, document.rows[0]!.id, 1);

    expect(moved.rows[0]!.startTime).toBe(document.rows[0]!.startTime);
    expect(moved.rows[1]!.startTime).toBe(document.rows[1]!.startTime);
    expect(moved.rows[0]!.mondai).toBe(document.rows[1]!.mondai);
    expect(moved.rows[1]!.mondai).toBe(document.rows[0]!.mondai);
  });

  it("sorts rows by start time", () => {
    const document = readLyrics({
      startTime: [3, 1, 2],
      endTime: [4, 2, 3],
      mondai: ["c", "a", "b"],
    });
    const sorted = sortLyricRows(document);

    expect(sorted.rows.map((row) => row.mondai)).toEqual(["a", "b", "c"]);
    expect(sorted.rows.map((row) => row.startTime)).toEqual([1, 2, 3]);
  });

  it("keeps the previous end when a start is set from the playhead", () => {
    const document = readLyrics({
      startTime: [1],
      endTime: [2],
      mondai: ["a"],
    });
    const updated = setLyricStart(document, document.rows[0]!.id, 5);

    expect(updated.rows[0]!.startTime).toBe(5);
    expect(updated.rows[0]!.endTime).toBe(9);
    expect(rowIssues(updated, 0)).toEqual({});
  });

  it("keeps unknown keys of the lyrics block", () => {
    const document = readLyrics({ startTime: [1], endTime: [2], mondai: ["a"], custom: [1] });
    const lyrics = buildLyricsArrays(updateLyricRow(document, 0, { mondai: "b" }));

    expect(lyrics.custom).toEqual([1]);
  });

  it("rounds an edited interval to whole milliseconds", () => {
    const document = readLyrics({ startTime: [1], endTime: [2], mondai: ["a"] });
    const edited = updateLyricRow(document, 0, { startTime: 1.23456, endTime: 2.34567 });
    const lyrics = buildLyricsArrays(edited);

    expect(lyrics.startTime).toEqual([1.235]);
    expect(lyrics.endTime).toEqual([2.346]);
  });
});

describe("summary", () => {
  it("counts overlaps against the effective previous end", () => {
    const document = readLyrics({
      startTime: [1, 2, 6],
      endTime: [5, 3, 7],
      mondai: ["a", "b", "c"],
    });

    expect(summarizeLyrics(document).overlaps).toBe(1);
  });

  it("reports a missing start as a blocking error", () => {
    const document = readLyrics({ startTime: [], endTime: [], mondai: ["a"] });

    expect(rowIssues(document, 0).startTime).toBe("Start time is required");
    expect(canSaveLyrics(document)).toBe(false);
    expect(lyricsIssues(document)).toContainEqual({
      severity: "error",
      message: "1 line(s) have no start time. Set a start before exporting the chart.",
    });
  });
});

describe("repairs that keep the timeline in order", () => {
  it("never lets a repaired start land before the previous line", () => {
    const doc = readLyrics({
      startTime: [3.427, undefined as unknown as number],
      endTime: [3.4, 7.1],
      furigana: ["a", "b"],
      mondai: ["いち", "に"],
      romaji: ["i", "ni"],
    });

    // The repair is written when the chart is saved, so the document is
    // dirty first (the operator edited the text of the first line).
    const edited = updateLyricRow(doc, 0, { mondai: "直した行" });
    const arrays = buildLyricsArrays(edited);
    // The second line has no start, so it is repaired with the end of the
    // first line -- but never before the first line starts.
    expect(arrays.startTime?.[1]).toBeGreaterThanOrEqual(arrays.startTime?.[0] ?? 0);
  });

  it("appends a new line after the last one when the id is unknown", () => {
    const doc = readLyrics({
      startTime: [1, 5],
      endTime: [3, 9],
      furigana: ["いち", "に"],
      mondai: ["一行目", "二行目"],
      romaji: ["ichigyoume", "nigyoume"],
    });

    const next = addLyricRow(doc, 999);
    const last = next.rows[next.rows.length - 1];
    expect(last.startTime).toBe(9);
  });

  it("drops the array length warning once the lyrics were edited", () => {
    const doc = readLyrics({
      startTime: [1, 5, 9],
      endTime: [3, 9],
      furigana: ["いち", "に", "さん"],
      mondai: ["一行目", "二行目", "三行目"],
      romaji: ["a", "b", "c"],
    });

    expect(summarizeLyrics(doc).arrayLengthMismatch).toBe(true);

    const edited = updateLyricRow(doc, 0, { mondai: "直した行" });
    expect(summarizeLyrics(edited).arrayLengthMismatch).toBe(false);
  });
});
