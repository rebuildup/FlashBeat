"use client";

import { css } from "styled-system/css";
import type { LyricRowPatch } from "@/lib/chart/lyrics";
import { LyricsRow, type LyricsRowView } from "./lyrics-row";

/**
 * The scrollable list of lyric lines.
 *
 * The list is the recessed #131313 surface of the editor; every line card is
 * a dedicated component so a keystroke in one line never re-renders the whole
 * inspector more than the state change requires.
 */

const list = css({
  display: "grid",
  flex: "1",
  gap: "2",
  listStyle: "none",
  margin: "0",
  minHeight: "0",
  overflowY: "auto",
  padding: "2",
  scrollbarGutter: "stable",
});

const empty = css({
  color: "#8c8c8c",
  fontSize: "xs",
  padding: "6",
  textAlign: "center",
});

const emptyStrong = css({
  color: "#ffffff",
  display: "block",
  fontSize: "sm",
  marginBottom: "1",
});

const emptyAdd = css({
  bg: "#3a3a3a",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  fontSize: "xs",
  marginTop: "3",
  paddingInline: "3",
  paddingY: "1.5",
  _hover: { bg: "#454545" },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
});

export interface LyricsTimelineProps {
  views: LyricsRowView[];
  playheadSec: number;
  canSeek: boolean;
  readOnly?: boolean;
  onPatch(id: number, patch: LyricRowPatch): void;
  onSetStartAtPlayhead(id: number, seconds: number): void;
  onSeek(seconds: number): void;
  onMove(id: number, direction: -1 | 1): void;
  onRemove(id: number): void;
  onAdd(): void;
}

export function LyricsTimeline({
  views,
  playheadSec,
  canSeek,
  readOnly = false,
  onPatch,
  onSetStartAtPlayhead,
  onSeek,
  onMove,
  onRemove,
  onAdd,
}: LyricsTimelineProps) {
  if (views.length === 0) {
    return (
      <div className={empty} data-testid="lyrics-empty">
        <span className={emptyStrong}>No lyric lines yet</span>
        Add the first line, then set its start and end from the video timeline.
        <div>
          <button
            type="button"
            className={emptyAdd}
            onClick={onAdd}
            disabled={readOnly}
            data-testid="lyrics-add-empty"
          >
            Add line
          </button>
        </div>
      </div>
    );
  }

  return (
    <ol className={list} aria-label="Lyric lines" data-testid="lyrics-list">
      {views.map((view) => (
        <LyricsRow
          key={view.row.id}
          view={view}
          total={views.length}
          playheadSec={playheadSec}
          canSeek={canSeek}
          readOnly={readOnly}
          onPatch={onPatch}
          onSetStartAtPlayhead={onSetStartAtPlayhead}
          onSeek={onSeek}
          onMove={onMove}
          onRemove={onRemove}
        />
      ))}
    </ol>
  );
}
