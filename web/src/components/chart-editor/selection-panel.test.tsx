import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { SelectionPanel } from "./selection-panel";

/**
 * The selected-note inspector (issue #216).
 *
 * A first-time operator has to be able to say what is selected and to edit
 * it without the keyboard. The panel is rendered without a browser: the
 * static markup proves the readouts and the buttons exist, the chart editor
 * tests prove the handlers are wired.
 */
describe("SelectionPanel", () => {
  const handlers = {
    onDeselect: vi.fn(),
    onSetKind: vi.fn(),
    onResizeHold: vi.fn(),
    onNudge: vi.fn(),
    onDelete: vi.fn(),
  };

  const empty = renderToStaticMarkup(
    createElement(SelectionPanel, {
      chart: SAMPLE_CHART,
      selectedIndex: null,
      ...handlers,
    }),
  );

  it("tells the operator how to select a note", () => {
    expect(empty).toContain('data-testid="selection-empty"');
    expect(empty).toContain("Click a note on the lanes");
  });

  const selected = renderToStaticMarkup(
    createElement(SelectionPanel, {
      chart: SAMPLE_CHART,
      // First note of the sample chart: lane 4 (displayed 5), line 48 at LPB 4.
      selectedIndex: 0,
      ...handlers,
    }),
  );

  it("names the lane, the beat and the time of the selection", () => {
    expect(selected).toContain('data-testid="selection-lane"');
    expect(selected).toContain(">5<");
    expect(selected).toContain('data-testid="selection-beat"');
    expect(selected).toContain("12.00");
    // 12 beats at 95 BPM = 7.58 s.
    expect(selected).toContain('data-testid="selection-time"');
    expect(selected).toContain("7.58s");
  });

  it("offers the mouse-only edits", () => {
    expect(selected).toContain('data-testid="selection-kind"');
    expect(selected).toContain('data-testid="selection-kind-toggle"');
    expect(selected).toContain('data-testid="selection-nudge-left"');
    expect(selected).toContain('data-testid="selection-nudge-right"');
    expect(selected).toContain('data-testid="selection-nudge-up"');
    expect(selected).toContain('data-testid="selection-nudge-down"');
    expect(selected).toContain('data-testid="selection-delete"');
    expect(selected).toContain('data-testid="selection-clear"');
  });

  it("shows the length controls only for a hold", () => {
    expect(selected).not.toContain('data-testid="selection-length-inc"');

    const hold = renderToStaticMarkup(
      createElement(SelectionPanel, {
        chart: {
          ...SAMPLE_CHART,
          notes: [{ type: 2, num: 8, endNum: 16, block: 2, LPB: 4 }],
        },
        selectedIndex: 0,
        ...handlers,
      }),
    );

    expect(hold).toContain(">Hold<");
    expect(hold).toContain('data-testid="selection-length-inc"');
    expect(hold).toContain('data-testid="selection-length-dec"');
    expect(hold).toContain("2.00 beats");
  });
});
