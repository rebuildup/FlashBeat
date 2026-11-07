// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { CatalogPanel } from "./catalog-panel";
import type { SongSummary } from "./types";

/**
 * Issue #61 — the merge of #156 left the pre-thumbnail rowBody behind, so the
 * table row rendered title/artist twice and pushed every grid cell one column
 * right. The row carries exactly one body (thumbnail + text) and its grid
 * children match the header columns.
 */

let root: Root | null = null;
let container: HTMLElement | null = null;

const song: SongSummary = {
  id: "who",
  title: "Who",
  artist: "Azari",
  youtubeId: "8JXiXt0D6tw",
  level: 5,
  bpm: 128,
  durationSec: 68,
  chartVersion: 1,
  noteCount: 139,
};

function renderPanel(): HTMLElement {
  container = document.createElement("div");
  document.body.appendChild(container);
  root = createRoot(container);
  act(() => {
    root!.render(
      <CatalogPanel
        status="ready"
        songs={[song]}
        total={1}
        onRetry={() => {}}
        hasFilters={false}
        onClearFilters={() => {}}
      />,
    );
  });
  return container;
}

afterEach(() => {
  if (root && container) {
    act(() => {
      root!.unmount();
    });
  }
  container?.remove();
  root = null;
  container = null;
});

describe("CatalogPanel table row (issue #61)", () => {
  it("renders exactly one row body per row", () => {
    const container = renderPanel();
    const row = container.querySelector('[data-testid="catalog-list"] > div + div') as HTMLElement;
    expect(row).not.toBeNull();
    const bodies = row.querySelectorAll('[data-testid="catalog-row-body"]');
    expect(bodies).toHaveLength(1);
  });

  it("shows the title once per row outside the row link", () => {
    const container = renderPanel();
    const row = container.querySelector('[data-testid="catalog-list"] > div + div') as HTMLElement;
    // 行内のタイトル表示は 1 回 (rowLink の visuallyHidden は行名の読み上げ用)。
    const titles = Array.from(row.querySelectorAll("span")).filter(
      (span) => span.childElementCount === 0 && span.textContent === song.title,
    );
    expect(titles).toHaveLength(1);
  });

  it("fills one cell per header column", () => {
    const container = renderPanel();
    const row = container.querySelector('[data-testid="catalog-list"] > div + div') as HTMLElement;
    // 行リンクは position:absolute でグリッド流れから外れるため列を消費しない。
    const cells = Array.from(row.children).filter(
      (child) => !(child instanceof HTMLElement) || !child.matches("a[data-row-link]"),
    );
    expect(cells).toHaveLength(7);
  });

  it("keeps every value under its own header column", () => {
    const container = renderPanel();
    const row = container.querySelector('[data-testid="catalog-list"] > div + div') as HTMLElement;
    const cells = Array.from(row.children).filter(
      (child): child is HTMLElement =>
        child instanceof HTMLElement && !child.matches("a[data-row-link]"),
    );
    expect(cells[1]?.textContent).toBe("5");
    expect(cells[2]?.textContent).toBe("128");
    expect(cells[3]?.textContent).toBe("1:08");
    expect(cells[4]?.textContent).toBe("139");
    expect(cells[5]?.textContent).toBe("v1");
  });

  it("keeps the action links of the row in the last cell", () => {
    const container = renderPanel();
    const row = container.querySelector('[data-testid="catalog-list"] > div + div') as HTMLElement;
    const cells = Array.from(row.children).filter(
      (child): child is HTMLElement =>
        child instanceof HTMLElement && !child.matches("a[data-row-link]"),
    );
    const last = cells[cells.length - 1];
    expect(last?.querySelector("a[href^='/publish']")).not.toBeNull();
    expect(last?.querySelector("a[href^='https://youtu.be/']")).not.toBeNull();
  });
});
