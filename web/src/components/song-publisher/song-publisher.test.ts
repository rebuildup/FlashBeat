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

/**
 * Issue #158, finding #9 — the primary action of the publish screen was cut
 * off below the viewport: the side column showed the metadata card first and
 * the publish card with its button last.
 *
 * The publish card therefore comes first in the side column, so "Review
 * publish" is on screen without scrolling.
 */
describe("SongPublisher side column order", () => {
  const html = renderToStaticMarkup(
    createElement(SongPublisher, {
      initialChart: SAMPLE_CHART,
      initialYouTubeId: "dQw4w9WgXcQ",
    }),
  );

  it("puts the publish card with the primary action first", () => {
    const card = html.indexOf('data-testid="publish-card"');
    const action = html.indexOf("Review publish");
    const metadata = html.indexOf("Song metadata");

    expect(card).toBeGreaterThan(-1);
    expect(action).toBeGreaterThan(-1);
    expect(metadata).toBeGreaterThan(-1);
    expect(card).toBeLessThan(action);
    expect(action).toBeLessThan(metadata);
  });

  it("keeps the publish card the first card of the side column", () => {
    const column = html.slice(html.indexOf('aria-label="Publish sidebar"'));
    const firstSection = column.indexOf("<section");
    const firstTagEnd = column.indexOf(">", firstSection);

    expect(firstSection).toBeGreaterThan(-1);
    expect(column.slice(firstSection, firstTagEnd)).toContain('data-testid="publish-card"');
  });
});
