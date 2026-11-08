import { css } from "styled-system/css";
import { holdLengthBeats, holdLengthSec, holdEndBeat, isHold } from "@/lib/chart/long-note";
import { noteBeat, noteTimeSec } from "@/lib/chart/timing";
import type { Chart } from "@/lib/chart/types";

/**
 * Inspector of the selected note (issue #216).
 *
 * The panel is the answer to "what did I just do?" — it names the lane, the
 * beat, the time and the kind of the selected note, and it offers the same
 * edits the grid offers with the mouse (move, change the length, delete), so
 * a first-time operator never needs the keyboard or a hidden menu.
 *
 * All edits are governed by the chart editor's history, so every action here
 * can be taken back with Undo.
 */

const frame = css({
  bg: "#2d2d2d",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  padding: "3",
});

const header = css({
  alignItems: "center",
  display: "flex",
  justifyContent: "space-between",
});

const title = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const clearButton = css({
  alignItems: "center",
  bg: "transparent",
  borderColor: "transparent",
  borderRadius: "6px",
  color: "#b0b0b0",
  cursor: "pointer",
  display: "flex",
  fontSize: "xs",
  height: "sizes.6",
  justifyContent: "center",
  width: "sizes.6",
  _hover: { bg: "#3a3a3a", color: "#ffffff" },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

const empty = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  lineHeight: "1.5",
  margin: "0",
});

const readouts = css({
  display: "grid",
  gap: "2",
  gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
});

const readout = css({
  bg: "#131313",
  borderRadius: "8px",
  display: "flex",
  flexDirection: "column",
  gap: "0.5",
  padding: "2",
});

const readoutLabel = css({
  color: "#8c8c8c",
  fontSize: "2xs",
});

const readoutValue = css({
  color: "#ffffff",
  fontFamily: "mono",
  fontSize: "sm",
  fontVariantNumeric: "tabular-nums",
  lineHeight: "1.2",
});

const kindRow = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const kindBadge = css({
  bg: "#3a3a3a",
  borderRadius: "4px",
  color: "#ffffff",
  fontSize: "2xs",
  fontWeight: "medium",
  padding: "0.5 2",
});

const kindDetail = css({
  color: "#b0b0b0",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
});

const row = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
});

const rowLabel = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  marginRight: "auto",
});

const action = css({
  bg: "#3a3a3a",
  borderColor: "transparent",
  borderRadius: "8px",
  color: "#ffffff",
  cursor: "pointer",
  fontSize: "2xs",
  lineHeight: "1.4",
  padding: "1.5 2",
  _disabled: { cursor: "default", opacity: 0.4 },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
  _hover: { bg: "#454545" },
});

const stepButton = css({
  alignItems: "center",
  bg: "#3a3a3a",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  display: "flex",
  fontFamily: "mono",
  fontSize: "xs",
  height: "sizes.7",
  justifyContent: "center",
  width: "sizes.7",
  _hover: { bg: "#454545" },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

const nudgeGrid = css({
  display: "grid",
  gap: "1",
  gridTemplateColumns: "repeat(3, 1.75rem)",
  gridTemplateRows: "repeat(2, 1.75rem)",
  justifyContent: "end",
});

const deleteButton = css({
  bg: "#3a2f2f",
  borderRadius: "8px",
  color: "#ffb3b3",
  cursor: "pointer",
  fontSize: "2xs",
  fontWeight: "medium",
  padding: "1.5 2",
  width: "full",
  _hover: { bg: "#4a3434" },
  _focusVisible: { outline: "2px solid #f56b6b", outlineOffset: "1px" },
});

const hint = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  lineHeight: "1.4",
  margin: "0",
  textAlign: "center",
});

export interface SelectionPanelProps {
  chart: Chart | null;
  /** Index of the selected note, `null` when nothing is selected. */
  selectedIndex: number | null;
  /** Clear the selection. */
  onDeselect(): void;
  /** Turn the selected note into a tap or a hold. */
  onSetKind(kind: "tap" | "hold"): void;
  /** Add `deltaBeat` beats to the length of the selected hold. */
  onResizeHold(deltaBeat: number): void;
  /** Move the selected note by beats (horizontal) and lanes (vertical). */
  onNudge(deltaBeat: number, deltaBlock: number): void;
  onDelete(): void;
}

export function SelectionPanel({
  chart,
  selectedIndex,
  onDeselect,
  onSetKind,
  onResizeHold,
  onNudge,
  onDelete,
}: SelectionPanelProps) {
  const note = chart && selectedIndex !== null ? chart.notes[selectedIndex] : undefined;

  return (
    <section className={frame} data-testid="selection-panel" aria-label="Selected note">
      <div className={header}>
        <h3 className={title}>Selected note</h3>
        {note ? (
          <button
            type="button"
            className={clearButton}
            onClick={onDeselect}
            aria-label="Clear the note selection"
            data-testid="selection-clear"
          >
            <span aria-hidden="true">✕</span>
          </button>
        ) : null}
      </div>

      {!note ? (
        <p className={empty} data-testid="selection-empty">
          Click a note on the lanes to read its lane, beat and time here. Every edit keeps an Undo
          step.
        </p>
      ) : (
        <>
          <div className={readouts}>
            <div className={readout}>
              <span className={readoutLabel}>Lane</span>
              <strong className={readoutValue} data-testid="selection-lane">
                {note.block + 1}
              </strong>
            </div>
            <div className={readout}>
              <span className={readoutLabel}>Beat</span>
              <strong className={readoutValue} data-testid="selection-beat">
                {noteBeat(note).toFixed(2)}
              </strong>
            </div>
            <div className={readout}>
              <span className={readoutLabel}>Time</span>
              <strong className={readoutValue} data-testid="selection-time">
                {noteTimeSec(note, chart?.BPM ?? 0).toFixed(2)}s
              </strong>
            </div>
          </div>

          <div className={kindRow}>
            <span className={kindBadge} data-testid="selection-kind">
              {isHold(note) ? "Hold" : "Tap"}
            </span>
            {isHold(note) ? (
              <span className={kindDetail} data-testid="selection-length">
                {holdLengthBeats(note).toFixed(2)} beats ·{" "}
                {(chart && chart.BPM > 0 ? holdLengthSec(note, chart.BPM) : 0).toFixed(2)}s · ends
                at beat {holdEndBeat(note).toFixed(2)}
              </span>
            ) : (
              <span className={kindDetail}>press once</span>
            )}
          </div>

          <div className={row}>
            <span className={rowLabel}>Kind</span>
            <button
              type="button"
              className={action}
              onClick={() => onSetKind(isHold(note) ? "tap" : "hold")}
              data-testid="selection-kind-toggle"
            >
              {isHold(note) ? "Make Tap" : "Make Hold"}
            </button>
          </div>

          {isHold(note) ? (
            <div className={row}>
              <span className={rowLabel}>Length</span>
              <button
                type="button"
                className={stepButton}
                onClick={() => onResizeHold(-1 / note.LPB)}
                aria-label="Shorten the hold by one step"
                data-testid="selection-length-dec"
              >
                −
              </button>
              <button
                type="button"
                className={stepButton}
                onClick={() => onResizeHold(1 / note.LPB)}
                aria-label="Lengthen the hold by one step"
                data-testid="selection-length-inc"
              >
                +
              </button>
            </div>
          ) : null}

          <div className={row}>
            <span className={rowLabel}>Move by one step</span>
            <div className={nudgeGrid} role="group" aria-label="Move the selected note">
              <span />
              <button
                type="button"
                className={stepButton}
                onClick={() => onNudge(0, -1)}
                aria-label="Move the note one lane up"
                data-testid="selection-nudge-up"
              >
                ↑
              </button>
              <span />
              <button
                type="button"
                className={stepButton}
                onClick={() => onNudge(-1 / note.LPB, 0)}
                aria-label="Move the note one step earlier"
                data-testid="selection-nudge-left"
              >
                ←
              </button>
              <button
                type="button"
                className={stepButton}
                onClick={() => onNudge(0, 1)}
                aria-label="Move the note one lane down"
                data-testid="selection-nudge-down"
              >
                ↓
              </button>
              <button
                type="button"
                className={stepButton}
                onClick={() => onNudge(1 / note.LPB, 0)}
                aria-label="Move the note one step later"
                data-testid="selection-nudge-right"
              >
                →
              </button>
            </div>
          </div>

          <button
            type="button"
            className={deleteButton}
            onClick={onDelete}
            data-testid="selection-delete"
          >
            Delete note
          </button>
          <p className={hint}>Deleted by mistake? Undo is at the top left of the toolbar.</p>
        </>
      )}
    </section>
  );
}
