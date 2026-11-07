import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
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
