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
