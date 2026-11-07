import { Portal } from "@ark-ui/react/portal";
import { createListCollection } from "@ark-ui/react/collection";
import { Button, Field, Input, Select } from "@/components/ui";
import { DraftInput, parseIntegerOrNull } from "@/components/ui/input";
import { useCallback, useRef } from "react";
import { css } from "styled-system/css";
import {
  SNAP_SETTINGS,
  snapDivisionLabel,
  isSnapEnabled,
  type SnapSetting,
} from "@/lib/chart/snap";
import type { Chart } from "@/lib/chart/types";
import { setSnapDivision, useSnapDivision } from "./snap-store";

/**
 * Toolbar of the chart editor.
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
  justifyContent: "space-between",
  minHeight: "3rem",
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

const field = css({ flex: "1 1 8rem", minWidth: "8rem", maxWidth: "20rem" });
const wideField = css({ flex: "1 1 10rem", minWidth: "10rem", maxWidth: "24rem" });
/**
 * Definite flex basis for the Grid/Snap/View selects: the Park UI select
 * recipe gives the root `width: full`, which as an auto basis made every
 * select claim a whole row and stacked the compact toolbar into four rows
 * (issue #166). A definite basis overrides that width on the flex line.
 */
const selectRoot = css({ flex: "0 0 6.5rem", minWidth: "6.5rem", flexShrink: 0 });

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

const counter = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  paddingInline: "1",
});

const sideToggle = css({
  display: { base: "flex", lg: "none" },
  alignItems: "center",
  justifyContent: "center",
});

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
 *
 * The resolution of the chart is therefore never finer than the division,
 * and never coarser either: a division of 1/4 with a resolution of 1/16
 * would place the note on a line of its own, so the two go together.
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
  onNameChange(name: string): void;
  onBpmChange(bpm: number): void;
  onResolutionChange(resolution: number): void;
  /** Called when the operator picks another division. */
  onSnapDivisionChange?(division: SnapSetting): void;
  onZoomChange(zoom: number): void;
  onImportFile(file: File): void;
  onExport(): void;
  onDeleteSelected(): void;
  onToggleSide?: () => void;
  /** The inspector drawer is open; the toggle button follows the state. */
  sideOpen?: boolean;
}

export function EditorToolbar({
  chart,
  noteCount,
  resolution,
  snapDivision: snapDivisionProp,
  zoom,
  canDelete,
  onNameChange,
  onBpmChange,
  onResolutionChange,
  onSnapDivisionChange,
  onZoomChange,
  onImportFile,
  onExport,
  onDeleteSelected,
  onToggleSide,
  sideOpen = false,
}: EditorToolbarProps) {
  const fileInputRef = useRef<HTMLInputElement | null>(null);

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

  return (
    <header className={bar}>
      <div className={group}>
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
        <Button
          className={buttonSecondary}
          size="sm"
          variant="outline"
          onClick={onDeleteSelected}
          disabled={!canDelete}
          data-testid="delete-button"
        >
          Delete
          <kbd className={keycap} aria-hidden="true">
            Del
          </kbd>
        </Button>
        {onToggleSide && (
          <button
            type="button"
            className={sideToggle}
            onClick={onToggleSide}
            aria-label={sideOpen ? "Close the editor sidebar" : "Open the editor sidebar"}
            aria-expanded={sideOpen}
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              aria-hidden="true"
            >
              <rect x="3" y="3" width="7" height="7" rx="1" />
              <rect x="14" y="3" width="7" height="7" rx="1" />
              <rect x="3" y="14" width="7" height="7" rx="1" />
              <rect x="14" y="14" width="7" height="7" rx="1" />
            </svg>
          </button>
        )}
        <span className={counter} data-testid="note-count">
          {noteCount} notes
        </span>
      </div>

      <div className={group}>
        <Field.Root className={wideField}>
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
            format={(bpm) => (Number.isFinite(bpm) ? String(bpm) : "")}
            disabled={!chart}
            inputMode="numeric"
            parse={parseIntegerOrNull}
            onCommit={(bpm) => onBpmChange(bpm)}
          />
        </Field.Root>

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
          <Select.Label className={fieldLabel}>Grid</Select.Label>
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
            <Select.ValueText placeholder="1/4" />
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
          <Select.Label className={fieldLabel}>View</Select.Label>
          <Select.Control className={selectControl}>
            <Select.ValueText placeholder="Zoom 1" />
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
      </div>
    </header>
  );
}
