import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { SongPublisher } from "./song-publisher";

/**
 * The publish screen renders its inspector once. The inspector is the
 * persistent column on wide viewports and the drawer on narrow ones, so the
 * metadata form must never exist twice in the document (issue #74).
 */
describe("SongPublisher", () => {
  const html = renderToStaticMarkup(
    createElement(SongPublisher, {
      initialChart: SAMPLE_CHART,
      initialYouTubeId: "dQw4w9WgXcQ",
    }),
  );

  it("renders the metadata form once", () => {
    expect(html.match(/Song metadata/g) ?? []).toHaveLength(1);
  });

  it("renders the inspector once", () => {
    expect(html.match(/aria-label="Publish sidebar"/g) ?? []).toHaveLength(1);
  });

  it("keeps the drawer toggle for the narrow viewports", () => {
    expect(html).toContain("Open the publish sidebar");
  });
});
