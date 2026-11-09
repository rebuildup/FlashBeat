"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { css } from "styled-system/css";
import {
  activeLyricIndex,
  addLyricRow,
  buildLyricsArrays,
  formatTimecode,
  lyricsIssues,
  moveLyricRow,
  readLyrics,
  removeLyricRow,
  rowInterval,
  rowIssues,
  setLyricStart,
  sortLyricRows,
  summarizeLyrics,
  updateLyricRow,
  type LyricsDocument,
} from "@/lib/chart/lyrics";
import type { Chart } from "@/lib/chart/types";
import type { LyricsRowView } from "./lyrics-row";
import { LyricsTimeline } from "./lyrics-timeline";

/**
 * Lyrics panel of the chart editor (issue #165).
 *
 * It owns the `chart.lyrics` block only. Notes, BPM and the YouTube preview
 * stay with the chart editor; this panel receives the current playhead and
 * asks for a seek when a line is clicked. Keeping the whole lyrics workflow in
 * these three components is what lets #165 and the long-note work (#164) touch
 * the editor without editing the same file.
 *
 * Saving rule (mirrors `Assets/Game/Scripts/UI/LyricsTimeline.cs`):
 * - reading never rewrites the chart; an untouched lyrics block is handed
 *   back byte for byte (that is how `Who`'s reversed `endTime` survives);
 * - a text-only edit keeps the raw times of every line;
 * - a line whose times were edited is repaired on save (end = next start or
 *   start + 4s) and every array is written with one entry per line.
 */

const root = css({
  bg: "#131313",
  borderColor: "#222222",
  borderWidth: "1px",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
  overflow: "hidden",
});

const header = css({
  borderBottomColor: "#222222",
  borderBottomWidth: "1px",
  display: "grid",
  gap: "2",
  padding: "3",
});

const titleRow = css({
  alignItems: "baseline",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  justifyContent: "space-between",
});

const title = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const subtitle = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  margin: "0",
});

const count = css({
  color: "#b0b0b0",
  fontFamily: "mono",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
});

const toolbar = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
});

const button = css({
  bg: "#3a3a3a",
  borderColor: "transparent",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  fontSize: "2xs",
  lineHeight: "1.4",
  padding: "1 2",
  _disabled: { cursor: "default", opacity: 0.4 },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
  _hover: { bg: "#454545" },
});

const buttonPrimary = css({
  bg: "#ededed",
  color: "#1b1b1b",
  _hover: { bg: "#dedede" },
});

const playhead = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  marginLeft: "auto",
});

const playheadValue = css({
  color: "#ffffff",
  fontFamily: "mono",
  fontVariantNumeric: "tabular-nums",
});

const issues = css({
  display: "grid",
  gap: "1",
  listStyle: "none",
  margin: "0",
  maxHeight: "7rem",
  overflowY: "auto",
  padding: "0",
});

const issueWarning = css({
  color: "#d6b25e",
  fontSize: "2xs",
  margin: "0",
});

const issueError = css({
  color: "#f56b6b",
  fontSize: "2xs",
  margin: "0",
});

const placeholder = css({
  color: "#8c8c8c",
  fontSize: "xs",
  padding: "6",
  textAlign: "center",
});

export interface LyricsEditorProps {
  /** Chart whose `lyrics` block is edited. */
  chart: Chart | null;
  /** Current YouTube preview position in seconds. */
  playheadSec?: number;
  /** Called with the chart after every lyrics edit. */
  onChartChange?(chart: Chart): void;
  /** Seek the preview, used by the per-line "Seek" button. */
  onSeek?(seconds: number): void;
  /** Disable while publishing. */
  readOnly?: boolean;
  className?: string;
}

export function LyricsEditor({
  chart,
  playheadSec = 0,
  onChartChange,
  onSeek,
  readOnly = false,
  className,
}: LyricsEditorProps) {
  const [doc, setDoc] = useState<LyricsDocument>(() => readLyrics(chart?.lyrics));

  const chartRef = useRef(chart);
  chartRef.current = chart;
  const chartChangeRef = useRef(onChartChange);
  chartChangeRef.current = onChartChange;
  const lastSourceRef = useRef(chart?.lyrics);

  // A different chart (import, song switch) replaces the doc; an edit
  // we produced ourselves is recognised by its lyrics object identity.
  useEffect(() => {
    if (lastSourceRef.current === chart?.lyrics) {
      return;
    }
    lastSourceRef.current = chart?.lyrics;
    setDoc(readLyrics(chart?.lyrics));
  }, [chart?.lyrics]);

  const emit = useCallback((next: LyricsDocument) => {
    setDoc(next);

    const current = chartRef.current;
    if (!current) {
      return;
    }

    const lyrics = buildLyricsArrays(next);
    lastSourceRef.current = lyrics;
    chartChangeRef.current?.({ ...current, lyrics });
  }, []);

  const summary = useMemo(() => summarizeLyrics(doc), [doc]);
  const problems = useMemo(() => lyricsIssues(doc), [doc]);
  const activeIndex = useMemo(() => activeLyricIndex(doc, playheadSec), [doc, playheadSec]);

  const views = useMemo<LyricsRowView[]>(
    () =>
      doc.rows.map((row, index) => ({
        row,
        index,
        interval: rowInterval(doc, index),
        issues: rowIssues(doc, index),
        active: index === activeIndex,
      })),
    [doc, activeIndex],
  );

  const handlePatch = useCallback(
    (id: number, patch: Parameters<typeof updateLyricRow>[2]) => {
      emit(updateLyricRow(doc, id, patch));
    },
    [doc, emit],
  );

  const handleSetStart = useCallback(
    (id: number, seconds: number) => {
      emit(setLyricStart(doc, id, seconds));
    },
    [doc, emit],
  );

  const handleAdd = useCallback(() => {
    const afterId = activeIndex === null ? undefined : doc.rows[activeIndex]?.id;
    emit(addLyricRow(doc, afterId));
  }, [activeIndex, doc, emit]);

  const handleMove = useCallback(
    (id: number, direction: -1 | 1) => {
      emit(moveLyricRow(doc, id, direction));
    },
    [doc, emit],
  );

  const handleRemove = useCallback(
    (id: number) => {
      emit(removeLyricRow(doc, id));
    },
    [doc, emit],
  );

  const handleSort = useCallback(() => {
    emit(sortLyricRows(doc));
  }, [doc, emit]);

  const handleSeek = useCallback(
    (seconds: number) => {
      onSeek?.(seconds);
    },
    [onSeek],
  );

  return (
    <section className={`${root} ${className ?? ""}`} aria-label="Lyrics editor">
      <div className={header}>
        <div className={titleRow}>
          <div>
            <h3 className={title}>Lyrics</h3>
            {chart ? <p className={subtitle}>{chart.name}</p> : null}
          </div>
          <span className={count} data-testid="lyrics-count">
            {summary.lines} {summary.lines === 1 ? "line" : "lines"}
          </span>
        </div>

        <div className={toolbar}>
          <button
            type="button"
            className={`${button} ${buttonPrimary}`}
            onClick={handleAdd}
            disabled={readOnly || !chart}
            data-testid="lyrics-add"
          >
            Add line
          </button>
          <button
            type="button"
            className={button}
            onClick={handleSort}
            disabled={readOnly || summary.lines < 2}
            data-testid="lyrics-sort"
          >
            Sort by time
          </button>
          {activeIndex !== null ? (
            <span className={playhead} data-testid="lyrics-now">
              Line {activeIndex + 1} at{" "}
              <span className={playheadValue}>{formatTimecode(playheadSec)}</span>
            </span>
          ) : (
            <span className={playhead}>
              Preview <span className={playheadValue}>{formatTimecode(playheadSec)}</span>
            </span>
          )}
        </div>

        {problems.length > 0 ? (
          <ul className={issues} data-testid="lyrics-issues" aria-live="polite">
            {problems.map((problem) => (
              <li
                key={problem.message}
                className={problem.severity === "error" ? issueError : issueWarning}
              >
                {problem.message}
              </li>
            ))}
          </ul>
        ) : null}
      </div>

      {chart ? (
        <LyricsTimeline
          views={views}
          playheadSec={playheadSec}
          canSeek={Boolean(onSeek)}
          readOnly={readOnly}
          onPatch={handlePatch}
          onSetStartAtPlayhead={handleSetStart}
          onSeek={handleSeek}
          onMove={handleMove}
          onRemove={handleRemove}
          onAdd={handleAdd}
        />
      ) : (
        <p className={placeholder} data-testid="lyrics-no-chart">
          Open a chart to edit its lyrics.
        </p>
      )}
    </section>
  );
}
