import { Suspense, lazy, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Field, Input } from "@/components/ui";
import { DrawerBackdrop, useDrawerFocus } from "@/app/drawer";
import { css } from "styled-system/css";
import { chartFileName, exportChartJson } from "@/lib/chart/export";
import { importChartJson } from "@/lib/chart/import";
import { createHold, isHold } from "@/lib/chart/long-note";
import {
  addNote,
  createNote,
  dominantResolution,
  holdCount,
  moveNote,
  removeNote,
  setHoldEnd,
  updateChartMeta,
} from "@/lib/chart/notes";
import { validateChart } from "@/lib/chart/schema";
import { snapLine } from "@/lib/chart/snap";
import { noteBeat } from "@/lib/chart/timing";
import type { Chart, ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { EditorToolbar, buttonSecondary, fieldLabel, inputBox } from "./editor-toolbar";
import { useSnapDivision } from "./snap-store";
import { LyricsEditor } from "./lyrics-editor";
import { ValidationPanel } from "./validation-panel";
import { YouTubePreview, type YouTubePreviewHandle } from "./youtube-preview";

/**
 * Konva pulls in a canvas stack that only works in the browser, so the grid
 * is loaded on demand. The editor itself stays server renderable.
 */
const NoteGrid = lazy(() => import("./note-grid").then((module) => ({ default: module.NoteGrid })));

/**
 * The chart editor: toolbar on top, the recessed lane well on the left and
 * the inspector panels on the right.
 *
 * Panels follow design/flashbeat-ui.pen v5: page background #1B1B1B (k1),
 * panels #2D2D2D (k3) with the 16px panel radius, recessed strips and lists
 * #131313 (k0), raised controls #3A3A3A (k4). Controls are #3A3A3A, the work
 * area is the darkest surface on screen.
 */

const layout = css({
  display: "grid",
  gridTemplateRows: "auto minmax(0, 1fr)",
  height: "100%",
  minHeight: 0,
  overflow: "hidden",
});

const body = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: { base: "1fr", lg: "minmax(0, 1fr) 17rem" },
  /**
   * The left column holds the lane well (row 1) and the lyrics panel (row 2).
   * The lyrics panel keeps 20rem at FHD and 45% of the column below `lg`, so
   * the note grid stays usable on a laptop while the lyrics list scrolls on
   * its own. The inspector on the right spans both rows.
   */
  gridTemplateRows: {
    base: "minmax(0, 1fr) minmax(0, 45%)",
    lg: "minmax(0, 1fr) minmax(0, 20rem)",
  },
  minHeight: "0",
  p: "3",
});

const gridPanel = css({
  bg: "#131313",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gridColumn: "1",
  gridRow: "1",
  minHeight: "0",
  minWidth: "0",
  overflow: "visible",
});

/** Placement of the lyrics panel in the left column. */
const lyricsPanel = css({
  gridColumn: "1",
  gridRow: "2",
  minHeight: "0",
  minWidth: "0",
});

const gridHint = css({
  alignItems: "center",
  borderBottomColor: "#222222",
  borderBottomWidth: "1px",
  color: "#8c8c8c",
  display: "flex",
  flexWrap: "wrap",
  fontSize: "2xs",
  gap: "3",
  paddingBottom: "2",
  paddingTop: "2",
  px: "3",
});

const playheadReadout = css({
  color: "#ffffff",
  fontVariantNumeric: "tabular-nums",
  marginRight: "2",
});

const holdReadout = css({
  color: "#8c8c8c",
  fontVariantNumeric: "tabular-nums",
});

const hintText = css({
  color: "#8c8c8c",
  flex: "1 1 18rem",
});

/** Switch between placing taps and placing holds. */
const kindSwitch = css({
  background: "#222222",
  borderRadius: "8px",
  display: "flex",
  gap: "1",
  padding: "1",
});

const kindButton = css({
  background: "transparent",
  borderRadius: "6px",
  color: "#8c8c8c",
  cursor: "pointer",
  fontSize: "2xs",
  paddingBottom: "1",
  paddingTop: "1",
  px: "3",
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

const kindButtonActive = css({
  background: "#3a3a3a",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  fontSize: "2xs",
  paddingBottom: "1",
  paddingTop: "1",
  px: "3",
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

const side = css({
  display: "flex",
  flexDirection: "column",
  gap: "3",
  gridColumn: { base: "1", lg: "2" },
  gridRow: { base: "1", lg: "1 / span 2" },
  minHeight: 0,
  overflowY: "auto",
  /**
   * Narrow viewports: the inspector is an off-canvas drawer over the work
   * area (issue #74). Wide viewports: the persistent right column.
   */
  bg: { base: "#1b1b1b", lg: "transparent" },
  borderLeftColor: { base: "#2d2d2d", lg: "transparent" },
  borderLeftWidth: { base: "1px", lg: "0px" },
  bottom: { base: "0", lg: "auto" },
  height: { base: "100dvh", lg: "auto" },
  maxWidth: "100%",
  pb: { base: "3", lg: "0" },
  position: { base: "fixed", lg: "static" },
  pt: { base: "3", lg: "0" },
  px: { base: "3", lg: "0" },
  right: { base: "0", lg: "auto" },
  top: { base: "0", lg: "auto" },
  width: { base: "17rem", lg: "17rem" },
  zIndex: { base: "popover", lg: "auto" },
});

/** Closed drawers leave the tab order and sit behind the right edge. */
const sideClosed = css({
  transform: { base: "translateX(100%)", lg: "none" },
  visibility: { base: "hidden", lg: "visible" },
});

const sideOpen = css({
  transform: { base: "none", lg: "none" },
  visibility: { base: "visible", lg: "visible" },
});

const sideHeader = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "space-between",
});

const sideClose = css({
  alignItems: "center",
  bg: "#3a3a3a",
  borderRadius: "8px",
  color: "#ffffff",
  display: { base: "flex", lg: "none" },
  height: "sizes.7",
  justifyContent: "center",
  width: "sizes.7",
  _hover: { bg: "#454545" },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "2px" },
});

const sideFrame = css({
  bg: "#2d2d2d",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
  width: { base: "100%", lg: "17rem" },
});

const sideTitle = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const placeholder = css({
  alignItems: "center",
  color: "#8c8c8c",
  display: "flex",
  fontSize: "xs",
  height: "100%",
  justifyContent: "center",
  padding: "4",
  textAlign: "center",
});

export interface ChartEditorProps {
  /** Chart the editor opens with. */
  initialChart: Chart;
  /** YouTube video used by the preview. */
  initialYouTubeId: string;
  /**
   * Called with the chart whenever it changes.
   *
   * The publish screen needs the chart that is on screen, not the one it
   * opened with.
   */
  onChartChange?: (chart: Chart | null) => void;
  /** Called with the contract problems of the current chart. */
  onValidationChange?: (issues: ValidationIssue[]) => void;
}

export function ChartEditor({
  initialChart,
  initialYouTubeId,
  onChartChange,
  onValidationChange,
}: ChartEditorProps) {
  const [chart, setChart] = useState<Chart | null>(initialChart);
  const [importIssues, setImportIssues] = useState<ValidationIssue[]>([]);
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const [resolution, setResolution] = useState(() => dominantResolution(initialChart));
  /**
   * Grid the notes are placed on (issue #180).
   *
   * The toolbar picks the division and the grid places the notes on it.
   * `off` keeps the behaviour the editor had before: the notes sit on the
   * lines-per-beat of the chart.
   */
  const snapDivision = useSnapDivision();
  const [zoom, setZoom] = useState(1);
  const [playheadSec, setPlayheadSec] = useState(0);
  const [youtubeInput, setYoutubeInput] = useState(initialYouTubeId);
  const [youtubeId, setYoutubeId] = useState(initialYouTubeId);
  const [isSideOpen, setIsSideOpen] = useState(false);
  /**
   * Kind of the note the next click on the grid places.
   *
   * A hold is placed with the same gesture as a tap and then resized with
   * the handle at its end, so the operator never has to guess the length
   * of a hold before placing it.
   */
  const [noteKind, setNoteKind] = useState<"tap" | "hold">("tap");

  const previewRef = useRef<YouTubePreviewHandle | null>(null);
  const sideRef = useRef<HTMLElement | null>(null);

  // The inspector is a drawer below the `lg` breakpoint: the focus enters it
  // on open, stays inside it and returns to the trigger when it closes.
  useDrawerFocus({
    open: isSideOpen,
    panelRef: sideRef,
    onClose: useCallback(() => setIsSideOpen(false), []),
  });

  // The callbacks are read through a ref, so a parent that builds a new
  // function on every render does not restart the effects below.
  const chartChangeRef = useRef(onChartChange);
  chartChangeRef.current = onChartChange;
  const validationChangeRef = useRef(onValidationChange);
  validationChangeRef.current = onValidationChange;

  // Every edit re-checks the contract, so the operator sees a broken chart
  // before it reaches the API. A file that could not be read keeps its own
  // problems on screen.
  const issues = useMemo(
    () => (chart ? validateChart(chart) : importIssues),
    [chart, importIssues],
  );

  useEffect(() => {
    chartChangeRef.current?.(chart);
  }, [chart]);

  useEffect(() => {
    validationChangeRef.current?.(issues);
  }, [issues]);

  const handleImportFile = useCallback(async (file: File) => {
    const text = await file.text();
    const result = importChartJson(text);

    if (!result.ok) {
      setImportIssues(result.issues);
      setChart(null);
      setSelectedIndex(null);
      return;
    }

    setImportIssues([]);
    setChart(result.chart);
    setSelectedIndex(null);
    setResolution(dominantResolution(result.chart));
  }, []);

  const handleExport = useCallback(() => {
    if (!chart) {
      return;
    }

    const blob = new Blob([exportChartJson(chart)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = chartFileName(chart);
    link.click();
    URL.revokeObjectURL(url);
  }, [chart]);

  /**
   * Seek the preview from the lyrics panel.
   *
   * The lyrics panel works in seconds; the YouTube player stays the only
   * owner of playback. The request is forwarded to it and the new position
   * comes back through `onTimeUpdate`, so both panels read one playhead.
   */
  const handleLyricsSeek = useCallback((seconds: number) => {
    previewRef.current?.seekTo(seconds);
  }, []);

  /**
   * Place a hold.
   *
   * A hold is placed with the same gesture as a tap and then resized with
   * the handle at its end, so the operator does not have to know the length
   * of the hold before placing it. It starts with the length of one beat,
   * which is the shortest hold the grid can show.
   */
  const handleAddHold = useCallback(
    (block: number, beat: number) => {
      setChart((current) => {
        if (!current) {
          return current;
        }
        // The grid of the toolbar decides where the note lands: the snap
        // division when it is set, the lines per beat of the chart otherwise.
        const line = snapLine(beat * resolution, resolution, snapDivision);
        const existing = current.notes.findIndex(
          (note) => note.block === block && note.num === line,
        );
        if (existing >= 0) {
          setSelectedIndex(existing);
          return current;
        }
        setSelectedIndex(null);
        return addNote(
          current,
          createHold(
            block,
            line / resolution,
            (line + resolution) / resolution,
            resolution,
            snapDivision,
          ),
        );
      });
    },
    [resolution, snapDivision],
  );

  const handleAddNote = useCallback(
    (block: number, beat: number) => {
      if (noteKind === "hold") {
        handleAddHold(block, beat);
        return;
      }

      setChart((current) => {
        if (!current) {
          return current;
        }
        const line = snapLine(beat * resolution, resolution, snapDivision);
        // A cell that already holds a note selects it instead of stacking a
        // duplicate on the same line.
        const existing = current.notes.findIndex(
          (note) => note.block === block && note.num === line,
        );
        if (existing >= 0) {
          setSelectedIndex(existing);
          return current;
        }
        setSelectedIndex(null);
        return addNote(current, createNote(block, line, resolution));
      });
    },
    [handleAddHold, noteKind, resolution, snapDivision],
  );

  const handleMoveNote = useCallback(
    (index: number, block: number, beat: number) => {
      setChart((current) =>
        current ? moveNote(current, index, block, beat, snapDivision) : current,
      );
    },
    [snapDivision],
  );

  /**
   * The end of a hold was dragged to another beat.
   *
   * The end is snapped onto the grid of the note's own resolution, so a hold
   * always covers whole lines and never ends before it starts.
   */
  const handleResizeHold = useCallback(
    (index: number, endBeat: number) => {
      setChart((current) =>
        current ? setHoldEnd(current, index, endBeat, snapDivision) : current,
      );
    },
    [snapDivision],
  );

  const handleDeleteSelected = useCallback(() => {
    setChart((current) => {
      if (!current || selectedIndex === null) {
        return current;
      }
      return removeNote(current, selectedIndex);
    });
    setSelectedIndex(null);
  }, [selectedIndex]);

  // Arrow keys move the selected note by one grid line (beat) or one lane
  // (block), so fine placement never needs the mouse.
  const handleNudgeSelected = useCallback(
    (deltaBeat: number, deltaBlock: number) => {
      if (selectedIndex === null) {
        return;
      }
      setChart((current) => {
        const note = current?.notes[selectedIndex];
        if (!current || !note) {
          return current;
        }
        const beat = Math.max(0, noteBeat(note) + deltaBeat);
        const block = Math.min(7, Math.max(0, note.block + deltaBlock));
        return moveNote(current, selectedIndex, block, beat, snapDivision);
      });
    },
    [selectedIndex, snapDivision],
  );

  /**
   * Change the length of the selected hold by one grid line.
   *
   * The keyboard is the precise way to set the length of a hold: the handle
   * is dragged for the rough length, the arrow keys for the exact one.
   */
  const handleResizeSelectedHold = useCallback(
    (deltaBeat: number) => {
      if (selectedIndex === null) {
        return;
      }
      setChart((current) => {
        const note = current?.notes[selectedIndex];
        if (!current || !note || !isHold(note)) {
          return current;
        }
        const endBeat = noteBeat(note) + deltaBeat + (note.endNum ?? note.num + 1) / note.LPB;
        return setHoldEnd(current, selectedIndex, endBeat, snapDivision);
      });
    },
    [selectedIndex, snapDivision],
  );

  // The selected note disappears with the chart, so the index is kept in range.
  useEffect(() => {
    setSelectedIndex((current) => {
      if (current === null || !chart) {
        return null;
      }
      return current < chart.notes.length ? current : null;
    });
  }, [chart]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) {
        return;
      }

      if (event.key === "Delete" || event.key === "Backspace") {
        event.preventDefault();
        handleDeleteSelected();
        return;
      }

      if (event.key === "Escape") {
        // The drawer closes itself and stops the event before it reaches
        // this handler; here Escape only clears the note selection.
        setSelectedIndex(null);
        return;
      }

      if (selectedIndex !== null) {
        const gridBeat = 1 / resolution;

        // Shift + the horizontal arrows change the length of a hold instead
        // of moving it, so the exact length can be set without the mouse.
        if (event.shiftKey && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
          event.preventDefault();
          handleResizeSelectedHold(event.key === "ArrowLeft" ? -gridBeat : gridBeat);
          return;
        }

        if (event.key === "ArrowLeft") {
          event.preventDefault();
          handleNudgeSelected(-gridBeat, 0);
        } else if (event.key === "ArrowRight") {
          event.preventDefault();
          handleNudgeSelected(gridBeat, 0);
        } else if (event.key === "ArrowUp") {
          event.preventDefault();
          handleNudgeSelected(0, -1);
        } else if (event.key === "ArrowDown") {
          event.preventDefault();
          handleNudgeSelected(0, 1);
        }
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [
    handleDeleteSelected,
    handleNudgeSelected,
    handleResizeSelectedHold,
    selectedIndex,
    resolution,
  ]);

  const noteCount = chart?.notes.length ?? 0;

  const youtubeSummary = useMemo(() => {
    const parsed = parseYouTubeId(youtubeInput);
    if (parsed) {
      return null;
    }
    return youtubeInput.trim().length === 0
      ? "Enter a YouTube URL or a video id."
      : "That is not a YouTube video id.";
  }, [youtubeInput]);

  return (
    <div className={layout}>
      <EditorToolbar
        chart={chart}
        noteCount={noteCount}
        resolution={resolution}
        zoom={zoom}
        canDelete={selectedIndex !== null}
        onNameChange={(name) =>
          setChart((current) => (current ? updateChartMeta(current, { name }) : current))
        }
        onBpmChange={(bpm) =>
          setChart((current) => (current ? updateChartMeta(current, { BPM: bpm }) : current))
        }
        onResolutionChange={setResolution}
        onZoomChange={setZoom}
        onImportFile={handleImportFile}
        onExport={handleExport}
        onDeleteSelected={handleDeleteSelected}
        onToggleSide={() => setIsSideOpen((current) => !current)}
        sideOpen={isSideOpen}
      />

      <div className={body}>
        <section className={gridPanel}>
          <div className={gridHint}>
            <span className={playheadReadout} data-testid="playhead-readout">
              {playheadSec.toFixed(2)}s · beat {((playheadSec * (chart?.BPM ?? 0)) / 60).toFixed(2)}
            </span>
            <span className={holdReadout} data-testid="hold-count">
              {chart ? holdCount(chart) : 0} holds
            </span>
            <div className={kindSwitch} role="group" aria-label="Note kind">
              <button
                type="button"
                className={noteKind === "tap" ? kindButtonActive : kindButton}
                aria-pressed={noteKind === "tap"}
                onClick={() => setNoteKind("tap")}
                data-testid="kind-tap"
              >
                Tap
              </button>
              <button
                type="button"
                className={noteKind === "hold" ? kindButtonActive : kindButton}
                aria-pressed={noteKind === "hold"}
                onClick={() => setNoteKind("hold")}
                data-testid="kind-hold"
              >
                Hold
              </button>
            </div>
            <span className={hintText}>
              Click an empty cell to add a {noteKind === "hold" ? "hold" : "tap note"} · drag to
              move · drag the handle of a hold to change its length · Shift+←/→ resizes a hold ·
              arrows nudge · Del removes · Esc deselects
            </span>
          </div>

          {chart ? (
            <Suspense fallback={<p className={placeholder}>Loading the grid…</p>}>
              <NoteGrid
                chart={chart}
                resolution={resolution}
                zoom={zoom}
                selectedIndex={selectedIndex}
                playheadSec={playheadSec}
                onSelect={setSelectedIndex}
                onAddNote={handleAddNote}
                onMoveNote={handleMoveNote}
                onResizeHold={handleResizeHold}
              />
            </Suspense>
          ) : (
            <p className={placeholder}>
              No chart loaded. Import a chart file or fix the problems listed on the right.
            </p>
          )}
        </section>

        <LyricsEditor
          className={lyricsPanel}
          chart={chart}
          playheadSec={playheadSec}
          onChartChange={setChart}
          onSeek={handleLyricsSeek}
        />

        <aside
          className={`${side} ${isSideOpen ? sideOpen : sideClosed}`}
          ref={sideRef}
          role={isSideOpen ? "dialog" : undefined}
          aria-modal={isSideOpen ? true : undefined}
          aria-label="Editor sidebar"
        >
          <div className={sideHeader}>
            <h3 className={sideTitle}>Editor tools</h3>
            <button
              type="button"
              className={sideClose}
              onClick={() => setIsSideOpen(false)}
              aria-label="Close the editor sidebar"
            >
              <span aria-hidden="true">✕</span>
            </button>
          </div>

          <section className={sideFrame}>
            <h3 className={sideTitle}>Preview</h3>
            <Field.Root>
              <Field.Label className={fieldLabel}>YouTube video</Field.Label>
              <Input
                className={inputBox}
                value={youtubeInput}
                placeholder="https://www.youtube.com/watch?v=..."
                onChange={(event) => setYoutubeInput(event.currentTarget.value)}
                onBlur={() => {
                  const parsed = parseYouTubeId(youtubeInput);
                  if (parsed) {
                    setYoutubeId(parsed);
                  }
                }}
              />
              {youtubeSummary ? <Field.HelperText>{youtubeSummary}</Field.HelperText> : null}
            </Field.Root>

            <YouTubePreview
              ref={previewRef}
              videoId={youtubeId}
              onTimeUpdate={setPlayheadSec}
              onPlayingChange={() => {}}
            />

            <Button
              className={buttonSecondary}
              size="sm"
              onClick={() => previewRef.current?.seekTo(0)}
              data-testid="rewind-button"
            >
              Back to start
            </Button>
          </section>

          <ValidationPanel issues={issues} />
        </aside>

        {isSideOpen ? <DrawerBackdrop onClose={() => setIsSideOpen(false)} /> : null}
      </div>
    </div>
  );
}
