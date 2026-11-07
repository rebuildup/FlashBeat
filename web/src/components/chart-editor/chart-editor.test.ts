import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { SAMPLE_CHART, MIXED_LPB_OVERLAP_CHART } from "@/lib/chart/fixtures";
import { importChartLenient } from "@/lib/chart/import";
import { ChartEditor } from "./chart-editor";

/**
 * The chart editor renders its inspector once. The inspector is the
 * persistent column on wide viewports and the drawer on narrow ones, so the
 * preview and the validation list must never exist twice (issue #74).
 */
describe("ChartEditor", () => {
  const html = renderToStaticMarkup(
    createElement(ChartEditor, {
      initialChart: SAMPLE_CHART,
      initialYouTubeId: "dQw4w9WgXcQ",
    }),
  );

  it("renders the inspector once", () => {
    expect(html.match(/aria-label="Editor sidebar"/g) ?? []).toHaveLength(1);
  });

  it("renders the preview once", () => {
    expect(html.match(/YouTube video/g) ?? []).toHaveLength(1);
  });

  it("keeps the drawer toggle for the narrow viewports", () => {
    expect(html).toContain("Open the editor sidebar");
  });
});

/**
 * The switch between placing taps and placing holds (issue #164).
 *
 * The hold is placed with the same gesture as a tap and is resized with the
 * handle at its end, so the operator needs a way to say which kind of note
 * the next click places. The switch is part of the grid bar, next to the
 * note count, and it must not live in the toolbar: the toolbar belongs to
 * another issue.
 */
describe("ChartEditor note kinds", () => {
  const html = renderToStaticMarkup(
    createElement(ChartEditor, {
      initialChart: SAMPLE_CHART,
      initialYouTubeId: "dQw4w9WgXcQ",
    }),
  );

  it("offers a switch between tap and hold", () => {
    expect(html).toContain('aria-label="Note kind"');
    expect(html).toContain('data-testid="kind-tap"');
    expect(html).toContain('data-testid="kind-hold"');
  });

  it("places taps by default", () => {
    expect(html).toMatch(/aria-pressed="true"[^>]*data-testid="kind-tap"/);
    expect(html).toMatch(/aria-pressed="false"[^>]*data-testid="kind-hold"/);
  });

  it("shows how many holds the chart has", () => {
    // The sample chart of the repository carries no hold.
    expect(html).toContain('data-testid="hold-count"');
    expect(html).toContain("0 holds");
  });

  it("tells the operator how to change the length of a hold", () => {
    expect(html).toContain("drag the handle of a hold to change its length");
  });
});

/**
 * The warnings of a chart that was read leniently (issue #208).
 *
 * The chart of the fixture overlaps two holds of a mixed `LPB`. It is loaded
 * with a warning, so the operator can repair it instead of being locked out
 * by "Published chart failed validation".
 */
describe("ChartEditor with a chart that was read leniently", () => {
  const loaded = importChartLenient(JSON.stringify(MIXED_LPB_OVERLAP_CHART));
  const html = renderToStaticMarkup(
    createElement(ChartEditor, {
      initialChart: loaded.ok ? loaded.chart : SAMPLE_CHART,
      initialYouTubeId: "dQw4w9WgXcQ",
      initialIssues: loaded.ok ? loaded.issues : undefined,
    }),
  );

  it("shows the problem of the loaded chart as a warning", () => {
    expect(html).toContain('data-testid="validation-warning-summary"');
    expect(html).toContain("1 warning");
    expect(html).toContain("overlaps another hold on lane 0");
  });

  it("does not report the problem as an error", () => {
    expect(html).not.toContain('data-testid="validation-error-summary"');
  });
});
