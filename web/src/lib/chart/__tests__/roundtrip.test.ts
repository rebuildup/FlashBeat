import { describe, expect, it } from "vitest";
import { exportChartJson } from "../export";
import { importChartJson } from "../import";
import { listRepositoryCharts, readRepositoryChart } from "./repository-charts";

/**
 * Import/export round trip against the real charts of the Unity project.
 *
 * Every chart in `Assets/Game/Resources` is read, written back and read again.
 * The written file must describe exactly the same chart, including the
 * lyrics block and the fields the contract does not know about.
 */
describe("chart round trip", () => {
  const charts = listRepositoryCharts();

  // Probe charts without a single note. The frozen contract asks for at least
  // one note, so the editor refuses them and shows the validation error.
  const chartsWithoutNotes = ["E2EProbeSong.json", "ニセモノ.json"];

  it("covers every chart of the Unity project", () => {
    expect(charts.length).toBeGreaterThan(20);
  });

  for (const fileName of charts) {
    it(`round trips ${fileName}`, () => {
      const original = readRepositoryChart(fileName);

      const first = importChartJson(original);
      if (!first.ok) {
        expect(chartsWithoutNotes).toContain(fileName);
        expect(first.issues.some((issue) => issue.keyword === "minItems")).toBe(true);
        return;
      }

      const written = exportChartJson(first.chart);

      const second = importChartJson(written);
      expect(second.ok).toBe(true);
      if (!second.ok) {
        return;
      }

      // Same chart, same notes, same lyrics, same unknown fields.
      expect(second.chart).toEqual(first.chart);

      // Writing the re-imported chart again produces the very same file.
      expect(exportChartJson(second.chart)).toBe(written);
    });
  }

  it("keeps fields the contract does not know about", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    // `videoId` is a field the Unity charts carry next to the contract fields.
    const withVideoId = { ...imported.chart, videoId: "dQw4w9WgXcQ" };

    const written = exportChartJson(withVideoId);
    expect(written).toContain("videoId");

    const reimported = importChartJson(written);
    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    expect(reimported.chart.videoId).toBe("dQw4w9WgXcQ");
    expect(reimported.chart.notes).toEqual(imported.chart.notes);
  });

  it("keeps the lyrics block untouched", () => {
    const result = importChartJson(readRepositoryChart("Who.json"));

    expect(result.ok).toBe(true);
    if (!result.ok) {
      return;
    }

    expect(result.chart.lyrics).toBeDefined();

    const written = exportChartJson(result.chart);
    const reparsed = importChartJson(written);

    expect(reparsed.ok).toBe(true);
    if (!reparsed.ok) {
      return;
    }

    expect(reparsed.chart.lyrics).toEqual(result.chart.lyrics);
  });

  it("refuses the probe charts without notes", () => {
    for (const fileName of chartsWithoutNotes) {
      const result = importChartJson(readRepositoryChart(fileName));

      expect(result.ok).toBe(false);
      if (result.ok) {
        continue;
      }

      expect(result.issues.map((issue) => issue.keyword)).toContain("minItems");
    }
  });
});
