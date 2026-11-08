import {
  Suspense,
  forwardRef,
  lazy,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  useState,
} from "react";
import { Button, Field, Input } from "@/components/ui";
import { css } from "styled-system/css";
import { Rewind } from "lucide-react";
import { chartFileName, exportChartJson } from "@/lib/chart/export";
import { importChartLenient } from "@/lib/chart/import";
import { mergeLoadWarnings } from "@/lib/chart/issues";
import { createHold, holdEndBeat, isHold } from "@/lib/chart/long-note";
import {
  addNote,
  createNote,
  dominantResolution,
  holdCount,
  moveNote,
  removeNote,
  setHoldEnd,
  setNoteKind as setChartNoteKind,
  updateChartMeta,
} from "@/lib/chart/notes";
import { validateChart } from "@/lib/chart/schema";
import { snapLine } from "@/lib/chart/snap";
import { beatToSeconds, noteBeat } from "@/lib/chart/timing";
import { NOTE_TYPE_HOLD, NOTE_TYPE_TAP, type Chart, type ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { EditorToolbar, buttonSecondary, fieldLabel, inputBox } from "./editor-toolbar";
import { useSnapDivision } from "./snap-store";
import { LyricsEditor } from "./lyrics-editor";
import { SelectionPanel } from "./selection-panel";
import { ValidationPanel } from "./validation-panel";
import { YouTubePreview, type YouTubePreviewHandle } from "./youtube-preview";
import { useChartHistory } from "./use-chart-history";

/**
 * Konva pulls in a canvas stack that only works in the browser, so the grid
 * is loaded on demand. The editor itself stays server renderable.
 */
const NoteGrid = lazy(() => import("./note-grid").then((module) => ({ default: module.NoteGrid })));

/**
 * The chart editor (issue #216).
 *
 * The screen is arranged around the two things a first-time operator needs
 * at the same time — the song and the lanes:
 *
 * 1. the toolbar: Play/Pause, the playhead slider, Undo/Redo and Delete,
 *    plus the `Chart setup` disclosure for everything that configures the
 *    chart instead of editing it;
 * 2. the work area: the video preview next to the 8-lane grid, with the
 *    note kind (Tap/Hold) and the current playhead always visible;
 * 3. the inspector: the selected note (lane/beat/time/kind/length) and its
 *    mouse-only edits, followed by the contract check;
 * 4. the lyrics panel below the work area, secondary to the note grid.
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
  gridTemplateColumns: { base: "1fr", lg: "minmax(0, 1fr) 20rem" },
  gridTemplateRows: { base: "auto auto auto", lg: "minmax(0, 1fr) 15rem" },
  minHeight: "0",
  overflowX: "hidden",
  overflowY: { base: "auto", lg: "hidden" },
  p: "3",
});

/**
 * The work area: the video preview and the lanes share one row, so the
 * operator sees the music and the notes at the same time (issue #216).
 */
const mainRow = css({
  display: "grid",
  gap: "3",
  gridColumn: "1",
  gridRow: "1",
  gridTemplateColumns: { base: "1fr", lg: "minmax(19rem, 0.85fr) minmax(0, 1.7fr)" },
  gridTemplateRows: { base: "auto auto", lg: "minmax(0, 1fr)" },
  minHeight: "0",
  minWidth: "0",
});

const previewPanel = css({
  alignSelf: { base: "stretch", lg: "start" },
  bg: "#131313",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  minHeight: "0",
  minWidth: "0",
  overflow: "hidden",
  padding: "3",
});

const panelHeader = css({
  alignItems: "center",
  display: "flex",
  justifyContent: "space-between",
});

const panelTitle = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const playState = css({
  bg: "#222222",
  borderRadius: "4px",
  color: "#b0b0b0",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  padding: "0.5 2",
});

const previewFooter = css({
  display: "grid",
  gap: "2",
});

/**
 * The middle of the preview column explains the two note shapes while the
 * video is on screen, so the shapes on the lanes are never guessed
 * (issue #216). It only fills the space the 16:9 player leaves.
 */
const previewGuide = css({
  borderTopColor: "#222222",
  borderTopWidth: "1px",
  display: "grid",
  gap: "3",
  paddingTop: "3",
});

const guideList = css({
  display: "grid",
  gap: "2",
  listStyle: "none",
  margin: "0",
  padding: "0",
});

const guideRow = css({
  alignItems: "flex-start",
  display: "grid",
  gap: "2",
  gridTemplateColumns: "2.25rem minmax(0, 1fr)",
});

const guideShape = css({
  alignItems: "center",
  display: "flex",
  height: "1rem",
  justifyContent: "center",
});

const guideText = css({
  color: "#b0b0b0",
  fontSize: "xs",
  lineHeight: "1.5",
  margin: "0",
});

const guideStrong = css({
  color: "#ffffff",
  fontWeight: "medium",
});

const swatchTap = css({ bg: "#ededed", width: "1rem" });
const swatchHold = css({ bg: "#8a8a8a", width: "1.5rem" });
const swatchPlayhead = css({ bg: "#3fa9f5", height: "12px", width: "2px" });

const previewSummary = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  margin: "0",
});

const gridPanel = css({
  bg: "#131313",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  minHeight: { base: "30rem", lg: "0" },
  minWidth: "0",
  overflow: "hidden",
  position: "relative",
});

const gridHeader = css({
  borderBottomColor: "#222222",
  borderBottomWidth: "1px",
  display: "grid",
  gap: "2",
  paddingBottom: "2",
  paddingTop: "2",
  px: "3",
});

const gridHeaderTop = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const sectionLabel = css({
  color: "#8c8c8c",
  fontSize: "2xs",
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
  alignItems: "flex-start",
  background: "transparent",
  borderRadius: "6px",
  color: "#8c8c8c",
  cursor: "pointer",
  display: "flex",
  flexDirection: "column",
  gap: "0.5",
  paddingBottom: "1",
  paddingTop: "1",
  px: "3",
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
  _hover: { color: "#ffffff" },
});

const kindButtonActive = css({
  alignItems: "flex-start",
  background: "#3a3a3a",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  display: "flex",
  flexDirection: "column",
  gap: "0.5",
  paddingBottom: "1",
  paddingTop: "1",
  px: "3",
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

const kindName = css({
  fontSize: "xs",
  fontWeight: "medium",
  lineHeight: "1.1",
});

const kindHint = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  lineHeight: "1.1",
});

const selectionChip = css({
  bg: "#222222",
  borderRadius: "8px",
  color: "#b0b0b0",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  padding: "1 3",
});

const selectionChipActive = css({
  bg: "rgba(63,169,245,0.14)",
  borderRadius: "8px",
  color: "#9ad3fa",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  padding: "1 3",
});

const playheadChip = css({
  color: "#ffffff",
  fontFamily: "mono",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  marginLeft: "auto",
  whiteSpace: "nowrap",
});

const gridHint = css({
  alignItems: "center",
  color: "#8c8c8c",
  display: "flex",
  flexWrap: "wrap",
  fontSize: "2xs",
  gap: "3",
});

const holdReadout = css({
  color: "#8c8c8c",
  fontVariantNumeric: "tabular-nums",
});

const hintText = css({
  color: "#8c8c8c",
  flex: "1 1 16rem",
});

const emptyOverlay = css({
  alignItems: "center",
  bottom: "4",
  display: "flex",
  justifyContent: "center",
  left: "0",
  pointerEvents: "none",
  position: "absolute",
  right: "0",
  top: "6rem",
});

const emptyCard = css({
  bg: "#222222",
  borderColor: "#3a3a3a",
  borderRadius: "12px",
  borderStyle: "dashed",
  borderWidth: "1px",
  color: "#b0b0b0",
  fontSize: "sm",
  padding: "4",
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

const lyricsPanel = css({
  gridColumn: "1",
  gridRow: "2",
  minHeight: "0",
  minWidth: "0",
  overflow: "hidden",
});

const side = css({
  display: "flex",
  flexDirection: "column",
  gap: "3",
  gridColumn: { base: "1", lg: "2" },
  gridRow: { base: "2", lg: "1 / span 2" },
  minHeight: 0,
  minWidth: 0,
  overflowY: "auto",
});

/** The icon of a side panel button. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
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
  /**
   * Problems of the chart it opens with, as warnings.
   *
   * A chart that was published before the semantic rules were unified is read
   * leniently, so it can be repaired instead of being dropped (issue #208).
   * The problems stay visible in the validation panel until the operator
   * fixes them; the publish path itself stays strict.
   *
   * The value is read once, when the editor opens: a different chart is a
   * different editor (the page gives it a new key).
   */
  initialIssues?: ValidationIssue[];
}

/**
 * What a parent may do to the editor's transport (#232 follow-up).
 *
 * The preview inside the editor is the only owner of playback, so a page that
 * wants to start the song (the chart page's "Try it" CTA) must not reach for
 * a `Play` button in the markup. It calls the editor instead, and the editor
 * forwards the request to its preview:
 *
 * - `play()` — start the video (the same thing the toolbar's Play does);
 * - `pause()` — stop it;
 * - `seekTo(seconds)` — move the playhead and the video together.
 *
 * None of the three throw when the YouTube player could not load (offline
 * exhibition); the playhead can still be positioned.
 */
export interface ChartEditorHandle {
  play(): void;
  pause(): void;
  seekTo(seconds: number): void;
}

export const ChartEditor = forwardRef<ChartEditorHandle, ChartEditorProps>(function ChartEditor(
  {
    initialChart,
    initialYouTubeId,
    onChartChange,
    onValidationChange,
    initialIssues,
  }: ChartEditorProps,
  ref,
) {
  /**
   * Chart and selection live in one history, so Undo/Redo puts back the
   * edit *and* the note it was made on (issue #216).
   */
  const { chart, selectedIndex, canUndo, canRedo, commit, select, undo, redo, reset } =
    useChartHistory(initialChart);

  const [importIssues, setImportIssues] = useState<ValidationIssue[]>([]);
  /**
   * Warnings of the chart the editor was opened with (or with the file the
   * operator picked). They are the problems the chart already had when it was
   * read; the live validation of the editor stays strict.
   */
  const [loadWarnings, setLoadWarnings] = useState<ValidationIssue[]>(initialIssues ?? []);
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
  const [playing, setPlaying] = useState(false);
  const [youtubeInput, setYoutubeInput] = useState(initialYouTubeId);
  const [youtubeId, setYoutubeId] = useState(initialYouTubeId);
  /**
   * Kind of the note the next click on the grid places.
   *
   * A hold is placed with the same gesture as a tap and then resized with
   * the handle at its end, so the operator never has to guess the length
   * of a hold before placing it.
   */
  const [noteKind, setNoteKind] = useState<"tap" | "hold">("tap");

  const previewRef = useRef<YouTubePreviewHandle | null>(null);

  // The callbacks are read through a ref, so a parent that builds a new
  // function on every render does not restart the effects below.
  const chartChangeRef = useRef(onChartChange);
  chartChangeRef.current = onChartChange;
  const validationChangeRef = useRef(onValidationChange);
  validationChangeRef.current = onValidationChange;

  // Every edit re-checks the contract, so the operator sees a broken chart
  // before it reaches the API. A file that could not be read keeps its own
  // problems on screen.
  //
  // The problems the chart was read with stay warnings (issue #208): they
  // were published under the old rules and can be repaired. Everything the
  // operator breaks while editing is an error again.
  const issues = useMemo(
    () => (chart ? mergeLoadWarnings(validateChart(chart), loadWarnings) : importIssues),
    [chart, importIssues, loadWarnings],
  );

  useEffect(() => {
    chartChangeRef.current?.(chart);
  }, [chart]);

  useEffect(() => {
    validationChangeRef.current?.(issues);
  }, [issues]);

  const handleImportFile = useCallback(
    async (file: File) => {
      const text = await file.text();
      // The file is read leniently: a chart whose relations are broken is
      // loaded and the violations are shown as warnings, so a chart that was
      // published before the rules were unified can be repaired here instead
      // of being dropped (issue #208).
      const result = importChartLenient(text);

      if (!result.ok) {
        setImportIssues(result.issues);
        setLoadWarnings([]);
        reset(null);
        return;
      }

      setImportIssues([]);
      setLoadWarnings(result.issues ?? []);
      reset(result.chart);
      setResolution(dominantResolution(result.chart));
    },
    [reset],
  );

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
   * End of the seekable timeline.
   *
   * The slider covers the chart (its last note plus a little) and never less
   * than half a minute, so a new chart can still be scrubbed before the
   * first note is placed.
   */
  const timelineSec = useMemo(() => {
    const bpm = chart?.BPM ?? 0;
    if (!chart || bpm <= 0) {
      return 60;
    }
    let lastBeat = 0;
    for (const note of chart.notes) {
      lastBeat = Math.max(lastBeat, noteBeat(note));
      if (isHold(note)) {
        lastBeat = Math.max(lastBeat, holdEndBeat(note));
      }
    }
    return Math.max(30, Math.ceil(beatToSeconds(lastBeat, bpm)) + 10);
  }, [chart]);

  /**
   * Move the playhead and the video together.
   *
   * The YouTube player stays the owner of playback, but the readout must
   * follow the slider immediately: when the preview cannot load (offline
   * exhibition) the timeline is still usable for positioning the grid.
   */
  const handleSeek = useCallback(
    (seconds: number) => {
      const next = Math.max(0, Math.min(timelineSec, seconds));
      setPlayheadSec(next);
      previewRef.current?.seekTo(next);
    },
    [timelineSec],
  );

  /**
   * Start/stop the preview.
   *
   * The toolbar and the imperative handle (`play()`/`pause()`) share these,
   * so a parent that drives the editor and an operator who presses the
   * toolbar button see the same state.
   */
  const handlePlay = useCallback(() => {
    previewRef.current?.play();
    setPlaying(true);
  }, []);

  const handlePause = useCallback(() => {
    previewRef.current?.pause();
    setPlaying(false);
  }, []);

  const handleTogglePlay = useCallback(() => {
    if (!chart) {
      return;
    }
    if (playing) {
      handlePause();
    } else {
      handlePlay();
    }
  }, [chart, handlePause, handlePlay, playing]);

  /**
   * Forward the parent's transport calls to the preview (#232 follow-up).
   *
   * `chart.tsx` no longer finds a `Play` button in the markup: it holds a ref
   * to this editor and calls `play()`. The contract is `ChartEditorHandle`.
   */
  useImperativeHandle(
    ref,
    () => ({
      play: handlePlay,
      pause: handlePause,
      seekTo: handleSeek,
    }),
    [handlePlay, handlePause, handleSeek],
  );

  const handleRestart = useCallback(() => {
    handleSeek(0);
  }, [handleSeek]);

  /**
   * Seek the preview from the lyrics panel.
   *
   * The lyrics panel works in seconds; the YouTube player stays the only
   * owner of playback. The request is forwarded to it and the new position
   * comes back through `onTimeUpdate`, so both panels read one playhead.
   */
  const handleLyricsSeek = useCallback(
    (seconds: number) => {
      handleSeek(seconds);
    },
    [handleSeek],
  );

  /**
   * Place the note the kind switch selected.
   *
   * A hold is placed with the same gesture as a tap and then resized with
   * the handle at its end, so the operator does not have to know the length
   * of the hold before placing it. It starts with the length of one beat,
   * which is the shortest hold the grid can show.
   */
  const handleAddNote = useCallback(
    (block: number, beat: number) => {
      if (!chart) {
        return;
      }
      // The grid of the toolbar decides where the note lands: the snap
      // division when it is set, the lines per beat of the chart otherwise.
      const line = snapLine(beat * resolution, resolution, snapDivision);
      // A cell that already holds a note selects it instead of stacking a
      // duplicate on the same line.
      const existing = chart.notes.findIndex((note) => note.block === block && note.num === line);
      if (existing >= 0) {
        select(existing);
        return;
      }

      const note =
        noteKind === "hold"
          ? createHold(
              block,
              line / resolution,
              (line + resolution) / resolution,
              resolution,
              snapDivision,
            )
          : createNote(block, line, resolution);

      // The just-placed note becomes the selection, so the inspector and the
      // Delete/Undo controls work right after the placement gesture.
      commit(addNote(chart, note), chart.notes.length);
    },
    [chart, commit, noteKind, resolution, select, snapDivision],
  );

  const handleMoveNote = useCallback(
    (index: number, block: number, beat: number) => {
      if (!chart) {
        return;
      }
      commit(moveNote(chart, index, block, beat, snapDivision), index);
    },
    [chart, commit, snapDivision],
  );

  /**
   * The end of a hold was dragged to another beat.
   *
   * The end is snapped onto the grid of the note's own resolution, so a hold
   * always covers whole lines and never ends before it starts.
   */
  const handleResizeHold = useCallback(
    (index: number, endBeat: number) => {
      if (!chart) {
        return;
      }
      commit(setHoldEnd(chart, index, endBeat, snapDivision), index);
    },
    [chart, commit, snapDivision],
  );

  const handleDeleteSelected = useCallback(() => {
    if (!chart || selectedIndex === null) {
      return;
    }
    // The history records the selected note with the chart, so Undo brings
    // the note back *and* selects it again.
    commit(removeNote(chart, selectedIndex), null);
  }, [chart, commit, selectedIndex]);

  // Arrow keys move the selected note by one grid line (beat) or one lane
  // (block), so fine placement never needs the mouse.
  const handleNudgeSelected = useCallback(
    (deltaBeat: number, deltaBlock: number) => {
      if (!chart || selectedIndex === null) {
        return;
      }
      const note = chart.notes[selectedIndex];
      if (!note) {
        return;
      }
      const beat = Math.max(0, noteBeat(note) + deltaBeat);
      const block = Math.min(7, Math.max(0, note.block + deltaBlock));
      commit(moveNote(chart, selectedIndex, block, beat, snapDivision), selectedIndex);
    },
    [chart, commit, selectedIndex, snapDivision],
  );

  /**
   * Change the length of the selected hold by `deltaBeat`.
   *
   * The handle is dragged for the rough length, the inspector's +/− buttons
   * and the arrow keys for the exact one.
   */
  const handleResizeSelectedHold = useCallback(
    (deltaBeat: number) => {
      if (!chart || selectedIndex === null) {
        return;
      }
      const note = chart.notes[selectedIndex];
      if (!note || !isHold(note)) {
        return;
      }
      commit(
        setHoldEnd(chart, selectedIndex, holdEndBeat(note) + deltaBeat, snapDivision),
        selectedIndex,
      );
    },
    [chart, commit, selectedIndex, snapDivision],
  );

  const handleSetKind = useCallback(
    (kind: "tap" | "hold") => {
      if (!chart || selectedIndex === null) {
        return;
      }
      const type = kind === "hold" ? NOTE_TYPE_HOLD : NOTE_TYPE_TAP;
      commit(setChartNoteKind(chart, selectedIndex, type), selectedIndex);
    },
    [chart, commit, selectedIndex],
  );

  const handleDeselect = useCallback(() => {
    select(null);
  }, [select]);

  const handleNameChange = useCallback(
    (name: string) => {
      if (!chart) {
        return;
      }
      commit(updateChartMeta(chart, { name }), selectedIndex);
    },
    [chart, commit, selectedIndex],
  );

  const handleBpmChange = useCallback(
    (bpm: number) => {
      if (!chart) {
        return;
      }
      commit(updateChartMeta(chart, { BPM: bpm }), selectedIndex);
    },
    [chart, commit, selectedIndex],
  );

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) {
        return;
      }
      if (target?.isContentEditable) {
        return;
      }

      const modifier = event.metaKey || event.ctrlKey;

      if (modifier && event.key.toLowerCase() === "z") {
        event.preventDefault();
        if (event.shiftKey) {
          redo();
        } else {
          undo();
        }
        return;
      }

      if (modifier && event.key.toLowerCase() === "y") {
        event.preventDefault();
        redo();
        return;
      }

      if (event.code === "Space") {
        // A focused button/link/summary keeps its own Space behaviour.
        if (target?.closest("button, a, summary, [role='button'], [role='combobox']")) {
          return;
        }
        event.preventDefault();
        handleTogglePlay();
        return;
      }

      if (event.key === "Delete" || event.key === "Backspace") {
        event.preventDefault();
        handleDeleteSelected();
        return;
      }

      if (event.key === "Escape") {
        select(null);
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
    handleTogglePlay,
    redo,
    resolution,
    select,
    selectedIndex,
    undo,
  ]);

  const noteCount = chart?.notes.length ?? 0;
  const selectedNote = selectedIndex !== null ? chart?.notes[selectedIndex] : undefined;
  const playheadBeat = chart && chart.BPM > 0 ? (playheadSec * chart.BPM) / 60 : 0;

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
        playing={playing}
        onTogglePlay={handleTogglePlay}
        onRestart={handleRestart}
        playheadSec={playheadSec}
        timelineSec={timelineSec}
        onSeek={handleSeek}
        canUndo={canUndo}
        canRedo={canRedo}
        onUndo={undo}
        onRedo={redo}
        onNameChange={handleNameChange}
        onBpmChange={handleBpmChange}
        onResolutionChange={setResolution}
        onZoomChange={setZoom}
        onImportFile={handleImportFile}
        onExport={handleExport}
        onDeleteSelected={handleDeleteSelected}
      />

      <div className={body}>
        <div className={mainRow}>
          <section
            className={previewPanel}
            data-testid="editor-preview-panel"
            aria-label="Song preview"
          >
            <div className={panelHeader}>
              <h3 className={panelTitle}>Song preview</h3>
              <span className={playState} data-testid="preview-state">
                {playing ? "Playing" : "Paused"}
              </span>
            </div>

            <YouTubePreview
              ref={previewRef}
              videoId={youtubeId}
              onTimeUpdate={setPlayheadSec}
              onPlayingChange={setPlaying}
              showControls={false}
            />

            <div className={previewGuide}>
              <ul className={guideList}>
                <li className={guideRow}>
                  <span className={guideShape}>
                    <span className={swatchTap} aria-hidden="true" />
                  </span>
                  <p className={guideText}>
                    <span className={guideStrong}>Tap</span> — press the key once. The note is the
                    short white bar.
                  </p>
                </li>
                <li className={guideRow}>
                  <span className={guideShape}>
                    <span className={swatchHold} aria-hidden="true" />
                  </span>
                  <p className={guideText}>
                    <span className={guideStrong}>Hold</span> — keep it pressed. Drag the blue
                    handle at its end to change the length.
                  </p>
                </li>
                <li className={guideRow}>
                  <span className={guideShape}>
                    <span className={swatchPlayhead} aria-hidden="true" />
                  </span>
                  <p className={guideText}>
                    <span className={guideStrong}>Playhead</span> — the blue line follows the video
                    and the slider above.
                  </p>
                </li>
              </ul>
            </div>

            <div className={previewFooter}>
              <Field.Root>
                <Field.Label className={fieldLabel}>YouTube video</Field.Label>
                <Input
                  className={inputBox}
                  data-testid="youtube-input"
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

              <p className={previewSummary}>
                {noteCount} {noteCount === 1 ? "note" : "notes"} · {chart ? holdCount(chart) : 0}{" "}
                holds · {chart?.BPM ?? 0} BPM
              </p>
            </div>

            <Button
              className={buttonSecondary}
              size="sm"
              onClick={() => previewRef.current?.seekTo(0)}
              data-testid="rewind-button"
            >
              <Rewind className={buttonIcon} size={14} aria-hidden="true" />
              Back to start
            </Button>
          </section>

          <section className={gridPanel} aria-label="Note editor">
            <div className={gridHeader}>
              <div className={gridHeaderTop}>
                <span className={sectionLabel}>Add note</span>
                <div className={kindSwitch} role="group" aria-label="Note kind">
                  <button
                    type="button"
                    className={noteKind === "tap" ? kindButtonActive : kindButton}
                    aria-pressed={noteKind === "tap"}
                    onClick={() => setNoteKind("tap")}
                    data-testid="kind-tap"
                  >
                    <span className={kindName}>Tap</span>
                    <span className={kindHint}>press once</span>
                  </button>
                  <button
                    type="button"
                    className={noteKind === "hold" ? kindButtonActive : kindButton}
                    aria-pressed={noteKind === "hold"}
                    onClick={() => setNoteKind("hold")}
                    data-testid="kind-hold"
                  >
                    <span className={kindName}>Hold</span>
                    <span className={kindHint}>press and hold</span>
                  </button>
                </div>

                <span
                  className={selectedNote ? selectionChipActive : selectionChip}
                  data-testid="grid-selection"
                >
                  {selectedNote
                    ? `Lane ${selectedNote.block + 1} · beat ${noteBeat(selectedNote).toFixed(2)}`
                    : "No note selected"}
                </span>

                <span className={playheadChip} data-testid="playhead-readout">
                  {playheadSec.toFixed(2)}s · beat {playheadBeat.toFixed(2)}
                </span>
              </div>

              <div className={gridHint}>
                <span className={holdReadout} data-testid="hold-count">
                  {chart ? holdCount(chart) : 0} holds
                </span>
                <span className={hintText}>
                  Click an empty cell to place a {noteKind === "hold" ? "Hold" : "Tap"}. Drag a note
                  to move it.
                  {noteKind === "hold"
                    ? " Drag the blue handle at its end to change the length."
                    : " Hold makes a long note with a draggable end."}
                </span>
              </div>
            </div>

            {chart ? (
              <Suspense fallback={<p className={placeholder}>Loading the grid…</p>}>
                <NoteGrid
                  chart={chart}
                  resolution={resolution}
                  zoom={zoom}
                  selectedIndex={selectedIndex}
                  playheadSec={playheadSec}
                  onSelect={select}
                  onAddNote={handleAddNote}
                  onMoveNote={handleMoveNote}
                  onResizeHold={handleResizeHold}
                />
              </Suspense>
            ) : (
              <p className={placeholder}>
                No chart loaded. Import a chart file or fix the problems listed in the inspector.
              </p>
            )}

            {chart && chart.notes.length === 0 ? (
              <div className={emptyOverlay} aria-hidden="true">
                <span className={emptyCard}>Tap a lane to place the first note</span>
              </div>
            ) : null}
          </section>
        </div>

        <LyricsEditor
          className={lyricsPanel}
          chart={chart}
          playheadSec={playheadSec}
          onChartChange={commit}
          onSeek={handleLyricsSeek}
        />

        <aside className={side} aria-label="Editor inspector" data-testid="editor-inspector">
          <SelectionPanel
            chart={chart}
            selectedIndex={selectedIndex}
            onDeselect={handleDeselect}
            onSetKind={handleSetKind}
            onResizeHold={handleResizeSelectedHold}
            onNudge={handleNudgeSelected}
            onDelete={handleDeleteSelected}
          />
          <ValidationPanel issues={issues} />
        </aside>
      </div>
    </div>
  );
});
