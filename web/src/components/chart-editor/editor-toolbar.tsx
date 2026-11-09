import { Portal } from "@ark-ui/react/portal";
import { createListCollection } from "@ark-ui/react/collection";
import { Button, Field, Input, Select } from "@/components/ui";
import { DraftInput, parseIntegerOrNull } from "@/components/ui/input";
import { useCallback, useEffect, useRef } from "react";
import { css } from "styled-system/css";
import { Redo2, Trash2, Undo2 } from "lucide-react";
import {
  SNAP_SETTINGS,
  snapDivisionLabel,
  isSnapEnabled,
  type SnapSetting,
} from "@/lib/chart/snap";
import type { Chart } from "@/lib/chart/types";
import { setSnapDivision, useSnapDivision } from "./snap-store";

/**
 * Toolbar of the chart editor (issue #216).
 *
 * The bar is the transport first: Play/Pause, the playhead slider and the
 * time readout are always visible, because listening to the song is the
 * main way a first-time operator understands the chart. Undo/Redo and the
 * selected-note Delete sit next to them.
 *
 * Everything that configures the chart rather than performs an edit (song
 * name, BPM, note resolution, snap, zoom, import/export) is grouped into the
 * `Chart setup` disclosure. Nothing was removed: every control is still
 * wired to the same handler as before.
 *
 * Styled after design/flashbeat-ui.pen v5: the bar sits on the page
 * background (#1B1B1B) and only the controls are raised (#3A3A3A, k4); the
 * primary action is the light button (#EDEDED with #1B1B1B text), inputs are
 * recessed wells (#222222, k2), keycaps and tags keep the small 4px radius.
 */

const bar = css({
  alignItems: "center",
  borderBottomColor: "#2d2d2d",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  minHeight: "3.5rem",
  paddingBottom: "2",
  paddingTop: "2",
  px: "3",
});

const group = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const transport = css({
  alignItems: "center",
  bg: "#222222",
  borderRadius: "10px",
  display: "flex",
  /** Narrow: the transport wraps instead of pushing the toolbar sideways. */
  flexWrap: "wrap",
  gap: "2",
  maxWidth: "100%",
  minWidth: "0",
  padding: "1",
  paddingRight: "3",
});

const transportButton = css({
  bg: "#ededed",
  color: "#1b1b1b",
  borderRadius: "8px",
  minWidth: "5.5rem",
  _hover: { bg: "#dedede" },
  _disabled: { opacity: 0.4 },
});

const seek = css({
  accentColor: "#3fa9f5",
  cursor: "pointer",
  width: "clamp(7rem, 13vw, 13rem)",
  _disabled: { cursor: "default", opacity: 0.4 },
});

const readout = css({
  alignItems: "baseline",
  color: "#ffffff",
  display: "flex",
  fontVariantNumeric: "tabular-nums",
  gap: "1",
  minWidth: { base: "0", md: "9.5rem" },
  whiteSpace: "nowrap",
});

const readoutTime = css({ fontSize: "sm", fontWeight: "medium" });

const readoutBeat = css({ color: "#8c8c8c", fontSize: "2xs" });

const divider = css({
  alignSelf: "stretch",
  bg: "#2d2d2d",
  marginY: "1",
  width: "1px",
});

const spacer = css({ flex: "1" });

const counter = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  paddingInline: "1",
});

const details = css({
  /**
   * Wide viewports: the disclosure is pushed to the right edge of the bar, so
   * the `Chart setup` popover (anchored to it, `right: 0`) never starts left
   * of the viewport — at 1280 the bar wraps and the disclosure used to land
   * on the left of the second row, which put the panel at x=-20.
   * Narrow viewports: the panel is a fixed sheet, so no offset is needed.
   */
  marginLeft: { base: "0", md: "auto" },
  position: "relative",
});

/**
 * Native disclosure: no portal, no extra state, and the controls are in the
 * DOM (and therefore reachable) even while the panel is closed.
 */
const summary = css({
  alignItems: "center",
  bg: "#3a3a3a",
  borderRadius: "8px",
  color: "#ffffff",
  cursor: "pointer",
  display: "flex",
  fontSize: "2xs",
  fontWeight: "medium",
  gap: "2",
  height: "sizes.8",
  listStyle: "none",
  paddingInline: "3",
  userSelect: "none",
  _hover: { bg: "#454545" },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
  "&::-webkit-details-marker": { display: "none" },
});

const summaryMeta = css({
  color: "#b0b0b0",
  fontVariantNumeric: "tabular-nums",
  fontWeight: "normal",
});

/**
 * The `Chart setup` panel.
 *
 * Wide viewports: a popover anchored to the disclosure. Narrow viewports: the
 * anchor would be pushed off-screen, so the panel becomes a sheet that owns
 * its own scroll (issue #74) — the controls stay reachable instead of being
 * clipped away.
 */
const popover = css({
  bg: "#2d2d2d",
  borderColor: "#131313",
  borderRadius: "12px",
  borderWidth: "1px",
  display: "grid",
  gap: "3",
  left: { base: "1rem", md: "auto" },
  maxHeight: { base: "calc(100dvh - 6rem)", md: "none" },
  maxWidth: "calc(100vw - 2rem)",
  overflowY: { base: "auto", md: "visible" },
  padding: "3",
  position: { base: "fixed", md: "absolute" },
  right: { base: "1rem", md: "0" },
  top: { base: "4rem", md: "calc(100% + 8px)" },
  width: { base: "auto", md: "23rem" },
  zIndex: "popover",
});

const popoverTitle = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const settingsGrid = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
});

const wideCell = css({ gridColumn: "1 / -1" });

const field = css({ minWidth: "0" });

const selectRoot = css({ width: "full", minWidth: "0" });

const settingsActions = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const settingsHint = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  lineHeight: "1.4",
  margin: "0",
});

export const fieldLabel = css({
  color: "#b0b0b0",
  fontWeight: "normal",
});

export const inputBox = css({
  bg: "#222222",
  borderColor: "transparent",
  borderRadius: "8px",
  color: "#ffffff",
  _placeholder: { color: "#8c8c8c" },
  _focusVisible: {
    borderColor: "#3fa9f5",
    outline: "2px solid",
    outlineColor: "#3fa9f5",
    outlineOffset: "1px",
  },
});

const selectControl = css({
  alignItems: "center",
  bg: "#3a3a3a",
  borderColor: "transparent",
  borderRadius: "8px",
  color: "#ffffff",
  cursor: "pointer",
  display: "flex",
  gap: "2",
  height: "sizes.8",
  justifyContent: "space-between",
  paddingInline: "3",
  textAlign: "left",
  width: "full",
  _hover: { bg: "#454545" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "#3fa9f5",
    outlineOffset: "1px",
  },
});

const fileInput = css({ display: "none" });

const keycap = css({
  bg: "#3a3a3a",
  borderRadius: "4px",
  color: "#b0b0b0",
  display: "inline-block",
  fontFamily: "mono",
  fontSize: "2xs",
  lineHeight: "1.2",
  marginLeft: "1",
  padding: "0 0.5rem",
});

/** The icon of a toolbar button. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

/** v5 Button Primary: the light raised button. */
const buttonPrimary = css({
  bg: "#ededed",
  color: "#1b1b1b",
  borderRadius: "8px",
  _hover: { bg: "#dedede" },
  _disabled: { opacity: 0.4 },
});

/** v5 Button Secondary: raised #3A3A3A with white label. */
export const buttonSecondary = css({
  bg: "#3a3a3a",
  color: "#ffffff",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "#454545" },
  _disabled: { opacity: 0.4 },
});

const RESOLUTIONS = [
  { label: "1/4", value: "4" },
  { label: "1/8", value: "8" },
  { label: "1/16", value: "16" },
];

/**
 * The grid the notes are placed on (issue #180).
 *
 * `off` places the notes freely, the divisions snap them onto that step of
 * the beat. The grid follows the tempo of the chart.
 */
const SNAP_DIVISIONS = SNAP_SETTINGS.map((division) => ({
  label: snapDivisionLabel(division),
  value: String(division),
}));

const ZOOMS = [
  { label: "0.5×", value: "0.5" },
  { label: "1×", value: "1" },
  { label: "2×", value: "2" },
];

const resolutionCollection = createListCollection<{ label: string; value: string }>({
  items: RESOLUTIONS,
});

const snapCollection = createListCollection<{ label: string; value: string }>({
  items: SNAP_DIVISIONS,
});

const zoomCollection = createListCollection<{ label: string; value: string }>({ items: ZOOMS });

/**
 * Read a division back from the value of the select.
 *
 * The select works with strings, so the value is parsed back into the
 * setting it stands for. A value the toolbar does not know falls back to
 * the quarter beat, so the grid is never left in an unknown state.
 */
export function parseSnapDivision(value: string): SnapSetting {
  if (value === "off") {
    return "off";
  }
  const parsed = Number.parseInt(value, 10);
  const known = SNAP_SETTINGS.find((setting) => setting === parsed);
  return known === undefined ? 4 : known;
}

/**
 * The resolution the notes need for a division.
 *
 * A note carries its own resolution (`LPB`): the line index of the note is
 * counted in those lines. A 1/16 grid therefore needs a note of at least
 * 16 lines per beat, otherwise the line the note lands on rounds the grid
 * away again.
 */
export function resolutionForDivision(division: SnapSetting, resolution: number): number {
  if (!isSnapEnabled(division)) {
    return resolution;
  }
  return division;
}

export interface EditorToolbarProps {
  chart: Chart | null;
  noteCount: number;
  resolution: number;
  /**
   * Grid the notes are placed on (issue #180).
   *
   * When the prop is left out the toolbar writes into the shared snap store,
   * which the grid reads, so the editor itself does not have to carry the
   * state.
   */
  snapDivision?: SnapSetting;
  zoom: number;
  canDelete: boolean;

  /** True while the preview is playing. */
  playing?: boolean;
  /** Play / pause the preview. */
  onTogglePlay?(): void;
  /** Jump back to the start of the song. */
  onRestart?(): void;
  /** Current preview position in seconds. */
  playheadSec?: number;
  /** End of the seekable timeline in seconds. */
  timelineSec?: number;
  /** Seek the preview to `seconds`. */
  onSeek?(seconds: number): void;

  /** True when there is a chart step to take back. */
  canUndo?: boolean;
  canRedo?: boolean;
  onUndo?(): void;
  onRedo?(): void;

  onNameChange(name: string): void;
  onBpmChange(bpm: number): void;
  onResolutionChange(resolution: number): void;
  /** Called when the operator picks another division. */
  onSnapDivisionChange?(division: SnapSetting): void;
  onZoomChange(zoom: number): void;
  onImportFile(file: File): void;
  onExport(): void;
  onDeleteSelected(): void;
}

export function EditorToolbar({
  chart,
  noteCount,
  resolution,
  snapDivision: snapDivisionProp,
  zoom,
  canDelete,
  playing = false,
  onTogglePlay,
  onRestart,
  playheadSec = 0,
  timelineSec = 60,
  onSeek,
  canUndo = false,
  canRedo = false,
  onUndo,
  onRedo,
  onNameChange,
  onBpmChange,
  onResolutionChange,
  onSnapDivisionChange,
  onZoomChange,
  onImportFile,
  onExport,
  onDeleteSelected,
}: EditorToolbarProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const detailsRef = useRef<HTMLDetailsElement | null>(null);

  /**
   * Close the chart setup when the operator clicks elsewhere or presses
   * Escape. A native `<details>` only closes through its summary, but the
   * panel floats over the work area, so it must not stay open by accident.
   */
  useEffect(() => {
    const onPointerDown = (event: PointerEvent) => {
      const element = detailsRef.current;
      if (!element?.open) {
        return;
      }
      if (event.target instanceof Node && element.contains(event.target)) {
        return;
      }
      element.open = false;
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        detailsRef.current?.removeAttribute("open");
      }
    };

    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, []);

  /**
   * The division the grid uses.
   *
   * The prop wins when it is given; otherwise the toolbar reads and writes
   * the shared snap store, which the grid reads as well.
   */
  const storeDivision = useSnapDivision();
  const snapDivision = snapDivisionProp ?? storeDivision;

  const handleSnapDivisionChange = useCallback(
    (division: SnapSetting) => {
      setSnapDivision(division);
      onSnapDivisionChange?.(division);
      /*
       * A note carries its own resolution, so the resolution of the chart
       * follows the division: a 1/16 grid needs notes of 16 lines per beat,
       * otherwise the line the note lands on rounds the grid away again.
       */
      const wanted = resolutionForDivision(division, resolution);
      if (wanted !== resolution) {
        onResolutionChange(wanted);
      }
    },
    [onResolutionChange, onSnapDivisionChange, resolution],
  );

  const bpm = chart?.BPM ?? 0;
  const beat = bpm > 0 ? (playheadSec * bpm) / 60 : 0;
  const maxSec = Math.max(1, timelineSec);

  return (
    <header className={bar}>
      <div className={transport} role="group" aria-label="Playback">
        <Button
          className={transportButton}
          size="sm"
          onClick={onTogglePlay}
          disabled={!onTogglePlay || !chart}
          aria-pressed={playing}
          data-testid="transport-toggle"
        >
          {playing ? "Pause" : "Play"}
        </Button>
        <Button
          className={buttonSecondary}
          size="sm"
          onClick={onRestart}
          disabled={!onRestart}
          data-testid="transport-restart"
        >
          Restart
        </Button>
        <input
          className={seek}
          type="range"
          min={0}
          max={maxSec}
          step={0.01}
          value={Math.min(playheadSec, maxSec)}
          onChange={(event) => onSeek?.(Number.parseFloat(event.currentTarget.value))}
          disabled={!onSeek}
          aria-label="Playhead"
          data-testid="playhead-slider"
        />
        <span className={readout} data-testid="transport-time">
          <span className={readoutTime}>{playheadSec.toFixed(2)}s</span>
          <span className={readoutBeat}>· beat {beat.toFixed(2)}</span>
        </span>
      </div>

      <div className={divider} aria-hidden="true" />

      <div className={group} role="group" aria-label="Edit history">
        <Button
          className={buttonSecondary}
          size="sm"
          onClick={onUndo}
          disabled={!canUndo}
          title="Undo (Ctrl/Cmd+Z)"
          data-testid="undo-button"
        >
          <Undo2 className={buttonIcon} size={14} aria-hidden="true" />
          Undo
        </Button>
        <Button
          className={buttonSecondary}
          size="sm"
          onClick={onRedo}
          disabled={!canRedo}
          title="Redo (Ctrl/Cmd+Shift+Z)"
          data-testid="redo-button"
        >
          <Redo2 className={buttonIcon} size={14} aria-hidden="true" />
          Redo
        </Button>
        <Button
          className={buttonSecondary}
          size="sm"
          onClick={onDeleteSelected}
          disabled={!canDelete}
          title="Delete the selected note (Del)"
          data-testid="delete-button"
        >
          <Trash2 className={buttonIcon} size={14} aria-hidden="true" />
          Delete
          <kbd className={keycap} aria-hidden="true">
            Del
          </kbd>
        </Button>
      </div>

      <span className={spacer} />

      <span className={counter} data-testid="note-count">
        {noteCount} {noteCount === 1 ? "note" : "notes"}
      </span>

      <details ref={detailsRef} className={details} data-testid="chart-settings">
        <summary className={summary} data-testid="chart-settings-summary">
          Chart setup
          <span className={summaryMeta}>
            {chart ? `${chart.BPM} BPM · 1/${resolution}` : "no chart"}
          </span>
        </summary>

        <div className={popover}>
          <h3 className={popoverTitle}>Chart setup</h3>
          <p className={settingsHint}>
            Song, grid and file tools. Placing and editing notes never needs this panel.
          </p>

          <div className={settingsGrid}>
            <Field.Root className={wideCell}>
              <Field.Label className={fieldLabel}>Song name</Field.Label>
              <Input
                className={inputBox}
                data-testid="song-name-input"
                value={chart?.name ?? ""}
                disabled={!chart}
                onChange={(event) => onNameChange(event.currentTarget.value)}
              />
            </Field.Root>

            <Field.Root className={field}>
              <Field.Label className={fieldLabel}>BPM</Field.Label>
              {/*
                BPM は draft 入力 (issue #163): 空文字や「-」の途中状態でも
                表示が確定値に戻されず、blur / Enter で整数として確定する。
                parse できないテキストは確定値の表示へ戻る。
              */}
              <DraftInput<number>
                className={inputBox}
                data-testid="bpm-input"
                value={chart?.BPM ?? Number.NaN}
                format={(value) => (Number.isFinite(value) ? String(value) : "")}
                disabled={!chart}
                inputMode="numeric"
                parse={parseIntegerOrNull}
                onCommit={(value) => onBpmChange(value)}
              />
            </Field.Root>

            <div className={field}>
              <Select.Root
                className={selectRoot}
                collection={resolutionCollection}
                value={[String(resolution)]}
                onValueChange={(details) => {
                  const [next] = details.value;
                  if (next) {
                    onResolutionChange(Number.parseInt(next, 10));
                  }
                }}
              >
                <Select.Label className={fieldLabel}>Resolution</Select.Label>
                <Select.Control className={selectControl}>
                  <Select.ValueText placeholder="1/4" />
                  <Select.Indicator />
                </Select.Control>
                <Portal>
                  <Select.Positioner>
                    <Select.Content>
                      <Select.List>
                        {RESOLUTIONS.map((item) => (
                          <Select.Item key={item.value} item={item}>
                            <Select.ItemText>{item.label}</Select.ItemText>
                            <Select.ItemIndicator />
                          </Select.Item>
                        ))}
                      </Select.List>
                    </Select.Content>
                  </Select.Positioner>
                </Portal>
              </Select.Root>
              <p className={settingsHint}>How finely notes are written.</p>
            </div>

            <div className={field}>
              <Select.Root
                className={selectRoot}
                collection={snapCollection}
                value={[String(snapDivision)]}
                onValueChange={(details) => {
                  const [next] = details.value;
                  if (next !== undefined) {
                    handleSnapDivisionChange(parseSnapDivision(next));
                  }
                }}
                data-testid="snap-select"
              >
                <Select.Label className={fieldLabel}>Snap</Select.Label>
                <Select.Control className={selectControl}>
                  <Select.ValueText placeholder="off" />
                  <Select.Indicator />
                </Select.Control>
                <Portal>
                  <Select.Positioner>
                    <Select.Content>
                      <Select.List>
                        {SNAP_DIVISIONS.map((item) => (
                          <Select.Item key={item.value} item={item}>
                            <Select.ItemText>{item.label}</Select.ItemText>
                            <Select.ItemIndicator />
                          </Select.Item>
                        ))}
                      </Select.List>
                    </Select.Content>
                  </Select.Positioner>
                </Portal>
              </Select.Root>
              <p className={settingsHint}>Where a click lands on the beat.</p>
            </div>

            <div className={field}>
              <Select.Root
                className={selectRoot}
                collection={zoomCollection}
                value={[String(zoom)]}
                onValueChange={(details) => {
                  const [next] = details.value;
                  if (next) {
                    onZoomChange(Number.parseFloat(next));
                  }
                }}
              >
                <Select.Label className={fieldLabel}>Zoom</Select.Label>
                <Select.Control className={selectControl}>
                  <Select.ValueText placeholder="1×" />
                  <Select.Indicator />
                </Select.Control>
                <Portal>
                  <Select.Positioner>
                    <Select.Content>
                      <Select.List>
                        {ZOOMS.map((item) => (
                          <Select.Item key={item.value} item={item}>
                            <Select.ItemText>{item.label}</Select.ItemText>
                            <Select.ItemIndicator />
                          </Select.Item>
                        ))}
                      </Select.List>
                    </Select.Content>
                  </Select.Positioner>
                </Portal>
              </Select.Root>
              <p className={settingsHint}>Height of the timeline.</p>
            </div>
          </div>

          <div className={settingsActions}>
            <input
              ref={fileInputRef}
              className={fileInput}
              type="file"
              accept="application/json,.json"
              data-testid="import-input"
              onChange={(event) => {
                const file = event.currentTarget.files?.[0];
                if (file) {
                  onImportFile(file);
                }
                event.currentTarget.value = "";
              }}
            />
            <Button
              className={buttonSecondary}
              size="sm"
              variant="outline"
              onClick={() => fileInputRef.current?.click()}
              data-testid="import-button"
            >
              Import
            </Button>
            <Button
              className={buttonPrimary}
              size="sm"
              onClick={onExport}
              disabled={!chart}
              data-testid="export-button"
            >
              Export
            </Button>
            <span className={settingsHint}>JSON stays byte-compatible with the Unity charts.</span>
          </div>

          <p className={settingsHint} style={{ margin: 0 }}>
            Keyboard: arrows nudge · Shift+←/→ resizes a hold · Del deletes · Esc deselects ·
            Ctrl/Cmd+Z undoes.
          </p>
        </div>
      </details>
    </header>
  );
}
