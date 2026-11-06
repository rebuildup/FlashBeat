import { Suspense, lazy, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Field, Input } from "@/components/ui";
import { css } from "styled-system/css";
import { chartFileName, exportChartJson } from "@/lib/chart/export";
import { importChartJson } from "@/lib/chart/import";
import {
  addNote,
  createNote,
  dominantResolution,
  moveNote,
  removeNote,
  updateChartMeta,
} from "@/lib/chart/notes";
import { validateChart } from "@/lib/chart/schema";
import { snapBeat } from "@/lib/chart/timing";
import type { Chart, ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { EditorToolbar } from "./editor-toolbar";
import { ValidationPanel } from "./validation-panel";
import { YouTubePreview, type YouTubePreviewHandle } from "./youtube-preview";

/**
 * Konva pulls in a canvas stack that only works in the browser, so the grid
 * is loaded on demand. The editor itself stays server renderable.
 */
const NoteGrid = lazy(() => import("./note-grid").then((module) => ({ default: module.NoteGrid })));

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
  gridTemplateColumns: "minmax(0, 1fr) 20rem",
  minHeight: "0",
  overflow: "hidden",
  p: "3",
});

const gridPanel = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
  overflow: "hidden",
});

const gridHint = css({
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  fontSize: "2xs",
  paddingBottom: "2",
  paddingTop: "2",
  px: "3",
});

const side = css({
  display: "flex",
  flexDirection: "column",
  gap: "3",
  minHeight: "0",
  overflowY: "auto",
});

const sideFrame = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
});

const sideTitle = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "semibold",
  margin: "0",
});

const placeholder = css({
  alignItems: "center",
  color: "fg.muted",
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
  const [zoom, setZoom] = useState(1);
  const [playheadSec, setPlayheadSec] = useState(0);
  const [youtubeInput, setYoutubeInput] = useState(initialYouTubeId);
  const [youtubeId, setYoutubeId] = useState(initialYouTubeId);

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

  const handleAddNote = useCallback(
    (block: number, beat: number) => {
      setChart((current) => {
        if (!current) {
          return current;
        }
        const line = Math.round(snapBeat(beat, resolution) * resolution);
        return addNote(current, createNote(block, line, resolution));
      });
    },
    [resolution],
  );

  const handleMoveNote = useCallback((index: number, block: number, beat: number) => {
    setChart((current) => (current ? moveNote(current, index, block, beat) : current));
  }, []);

  const handleDeleteSelected = useCallback(() => {
    setChart((current) => {
      if (!current || selectedIndex === null) {
        return current;
      }
      return removeNote(current, selectedIndex);
    });
    setSelectedIndex(null);
  }, [selectedIndex]);

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
      if (event.key !== "Delete" && event.key !== "Backspace") {
        return;
      }

      const target = event.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA"].includes(target.tagName)) {
        return;
      }

      handleDeleteSelected();
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [handleDeleteSelected]);

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
      />

      <div className={body}>
        <section className={gridPanel}>
          <p className={gridHint}>
            Click an empty cell to add a tap note, drag a note to move it, press Delete to remove
            the selected note. The grid is drawn in beats, one column per lane.
          </p>

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
              />
            </Suspense>
          ) : (
            <p className={placeholder}>
              No chart loaded. Import a chart file or fix the problems listed on the right.
            </p>
          )}
        </section>

        <aside className={side}>
          <section className={sideFrame}>
            <h3 className={sideTitle}>Preview</h3>
            <Field.Root>
              <Field.Label>YouTube video</Field.Label>
              <Input
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
              size="sm"
              variant="outline"
              onClick={() => previewRef.current?.seekTo(0)}
              data-testid="rewind-button"
            >
              Back to start
            </Button>
          </section>

          <ValidationPanel issues={issues} />
        </aside>
      </div>
    </div>
  );
}
