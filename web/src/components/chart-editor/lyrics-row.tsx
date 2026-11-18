"use client";

import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { css } from "styled-system/css";
import {
  formatTimecode,
  formatTimeInput,
  parseTimeInput,
  type LyricInterval,
  type LyricRow,
  type LyricRowIssues,
  type LyricRowPatch,
} from "@/lib/chart/lyrics";

/**
 * One lyric line of the editor.
 *
 * Times are edited as text so the operator can clear a field, select all,
 * paste a value or type with a Japanese IME (full-width digits are accepted).
 * The value is committed on blur or Enter, never on every keystroke, so an
 * intermediate empty field does not collapse the timeline.
 *
 * Layout follows design/flashbeat-ui.pen v5: the row is a recessed #131313
 * well on the #2D2D2D panel, the active line is outlined with the #3FA9F5
 * focus colour and controls are #3A3A3A raised buttons.
 */

const rowBox = css({
  bg: "#1b1b1b",
  borderColor: "#2d2d2d",
  borderWidth: "1px",
  borderRadius: "10px",
  display: "grid",
  gap: "2",
  padding: "2.5",
});

const rowActive = css({
  bg: "#1f2326",
  borderColor: "#3fa9f5",
  boxShadow: "0 0 0 1px #3fa9f5 inset",
});

const head = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  minWidth: "0",
});

const lineNumber = css({
  color: "#8c8c8c",
  fontFamily: "mono",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
  minWidth: "1.25rem",
});

const range = css({
  color: "#b0b0b0",
  fontFamily: "mono",
  fontSize: "2xs",
  fontVariantNumeric: "tabular-nums",
});

const badge = css({
  borderRadius: "4px",
  fontSize: "2xs",
  lineHeight: "1.4",
  paddingInline: "1",
});

const nowBadge = css({
  bg: "#3fa9f5",
  color: "#101010",
  fontWeight: "semibold",
});

const legacyBadge = css({
  bg: "#3a3222",
  color: "#d6b25e",
});

const actions = css({
  display: "flex",
  gap: "1",
  marginLeft: "auto",
});

const action = css({
  bg: "#3a3a3a",
  borderColor: "transparent",
  borderRadius: "6px",
  color: "#ffffff",
  cursor: "pointer",
  fontSize: "2xs",
  lineHeight: "1.4",
  padding: "1 1.5",
  _disabled: { cursor: "default", opacity: 0.4 },
  _focusVisible: { outline: "2px solid #3fa9f5", outlineOffset: "1px" },
  _hover: { bg: "#454545" },
});

const actionDanger = css({
  _hover: { bg: "#5a2b2b", color: "#ffb4b4" },
});

const timeGrid = css({
  display: "grid",
  gap: "2",
  gridTemplateColumns: "1fr 1fr",
});

const textGrid = css({
  display: "grid",
  gap: "2",
});

const field = css({
  display: "grid",
  gap: "1",
  minWidth: "0",
});

const label = css({
  color: "#8c8c8c",
  fontSize: "2xs",
});

const input = css({
  bg: "#222222",
  borderColor: "transparent",
  borderWidth: "1px",
  borderRadius: "6px",
  color: "#ffffff",
  fontFamily: "inherit",
  fontSize: "xs",
  minWidth: "0",
  paddingBottom: "1.5",
  paddingInline: "2",
  paddingTop: "1.5",
  width: "100%",
  _disabled: { color: "#8c8c8c" },
  _focusVisible: {
    borderColor: "#3fa9f5",
    outline: "2px solid #3fa9f5",
    outlineOffset: "1px",
  },
  _placeholder: { color: "#6b6b6b" },
});

const inputInvalid = css({
  borderColor: "#f56b6b",
});

const timeInput = css({
  fontFamily: "mono",
  fontVariantNumeric: "tabular-nums",
});

const errorText = css({
  color: "#f56b6b",
  fontSize: "2xs",
});

const derivedNote = css({
  color: "#8c8c8c",
  fontSize: "2xs",
});

export interface LyricsRowView {
  row: LyricRow;
  index: number;
  interval: LyricInterval;
  issues: LyricRowIssues;
  active: boolean;
}

export interface LyricsRowProps {
  view: LyricsRowView;
  total: number;
  playheadSec: number;
  canSeek: boolean;
  readOnly?: boolean;
  onPatch(id: number, patch: LyricRowPatch): void;
  onSetStartAtPlayhead(id: number, seconds: number): void;
  onSeek(seconds: number): void;
  onMove(id: number, direction: -1 | 1): void;
  onRemove(id: number): void;
}

type TimeField = "start" | "end";

export function LyricsRow({
  view,
  total,
  playheadSec,
  canSeek,
  readOnly = false,
  onPatch,
  onSetStartAtPlayhead,
  onSeek,
  onMove,
  onRemove,
}: LyricsRowProps) {
  const { row, index, interval, issues, active } = view;
  const [startDraft, setStartDraft] = useState(() => formatTimeInput(row.startTime));
  const [endDraft, setEndDraft] = useState(() => formatTimeInput(row.endTime));
  const [parseErrors, setParseErrors] = useState<Partial<Record<TimeField, string>>>({});
  const startFocused = useRef(false);
  const endFocused = useRef(false);

  // Values changed elsewhere (playhead shortcut, a fresh import) refresh the
  // field while it is not being typed in.
  useEffect(() => {
    if (!startFocused.current) {
      setStartDraft(formatTimeInput(row.startTime));
    }
  }, [row.startTime]);

  useEffect(() => {
    if (!endFocused.current) {
      setEndDraft(formatTimeInput(row.endTime));
    }
  }, [row.endTime]);

  const commitTime = (field: TimeField) => {
    const draft = field === "start" ? startDraft : endDraft;
    const parsed = parseTimeInput(draft);
    if (parsed === undefined) {
      setParseErrors((current) => ({
        ...current,
        [field]: "秒数（12.5）またはタイムコード（0:12.5）で入力してください",
      }));
      return;
    }

    setParseErrors((current) => ({ ...current, [field]: undefined }));

    // Focusing a field and leaving it again changes nothing, so the chart
    // must not change either: a legacy song keeps its raw times (and stays
    // byte identical) when the operator only tabs through the list.
    const current = field === "start" ? row.startTime : row.endTime;
    if (parsed === current) {
      return;
    }

    onPatch(row.id, field === "start" ? { startTime: parsed } : { endTime: parsed });
  };

  const onTimeKeyDown = (field: TimeField) => (event: KeyboardEvent<HTMLInputElement>) => {
    // Enter during IME composition belongs to the IME, not to the field.
    if (event.nativeEvent.isComposing) {
      return;
    }
    if (event.key === "Enter") {
      event.preventDefault();
      commitTime(field);
    }
    if (event.key === "Escape") {
      event.preventDefault();
      if (field === "start") {
        setStartDraft(formatTimeInput(row.startTime));
      } else {
        setEndDraft(formatTimeInput(row.endTime));
      }
      setParseErrors((current) => ({ ...current, [field]: undefined }));
    }
  };

  const startError = parseErrors.start ?? issues.startTime;
  const endError = parseErrors.end ?? issues.endTime;

  const rangeLabel = `${formatTimecode(interval.start ?? 0)} - ${formatTimecode(interval.end ?? 0)}`;
  const headingId = `lyric-line-${row.id}`;

  return (
    <li
      className={`${rowBox} ${active ? rowActive : ""}`}
      aria-current={active ? "true" : undefined}
      aria-labelledby={headingId}
      data-testid={`lyrics-row-${index}`}
      data-active={active ? "true" : "false"}
    >
      <div className={head}>
        <span className={lineNumber} id={headingId}>
          {String(index + 1).padStart(2, "0")}
        </span>
        {active ? <span className={`${badge} ${nowBadge}`}>現在</span> : null}
        {interval.derived && row.endTime !== null ? (
          <span
            className={`${badge} ${legacyBadge}`}
            title="保存された終了が開始より後ではありません。Unityは次の行の開始または+4秒を使います。"
          >
            従来の終了
          </span>
        ) : null}
        <span className={range}>{rangeLabel}</span>

        <div className={actions}>
          <button
            type="button"
            className={action}
            onClick={() => {
              if (interval.start !== null) {
                onSeek(interval.start);
              }
            }}
            disabled={readOnly || !canSeek || interval.start === null}
            aria-label={`${index + 1}行目の位置へ移動`}
          >
            再生位置へ
          </button>
          <button
            type="button"
            className={action}
            onClick={() => onSetStartAtPlayhead(row.id, playheadSec)}
            disabled={readOnly}
            aria-label={`${index + 1}行目の開始をプレイヘッドに設定`}
          >
            @ 再生位置
          </button>
          <button
            type="button"
            className={action}
            onClick={() => onMove(row.id, -1)}
            disabled={readOnly || index === 0}
            aria-label={`${index + 1}行目を上へ`}
          >
            ↑
          </button>
          <button
            type="button"
            className={action}
            onClick={() => onMove(row.id, 1)}
            disabled={readOnly || index === total - 1}
            aria-label={`${index + 1}行目を下へ`}
          >
            ↓
          </button>
          <button
            type="button"
            className={`${action} ${actionDanger}`}
            onClick={() => onRemove(row.id)}
            disabled={readOnly}
            aria-label={`${index + 1}行目を削除`}
          >
            ✕
          </button>
        </div>
      </div>

      <div className={timeGrid}>
        <label className={field}>
          <span className={label}>開始（秒）</span>
          <input
            className={`${input} ${timeInput} ${startError ? inputInvalid : ""}`}
            value={startDraft}
            inputMode="decimal"
            autoComplete="off"
            spellCheck={false}
            disabled={readOnly}
            aria-label={`${index + 1}行目の開始時刻`}
            aria-invalid={startError ? true : undefined}
            data-testid={`lyrics-start-${index}`}
            onFocus={() => {
              startFocused.current = true;
            }}
            onBlur={() => {
              startFocused.current = false;
              commitTime("start");
            }}
            onChange={(event) => setStartDraft(event.currentTarget.value)}
            onKeyDown={onTimeKeyDown("start")}
          />
          {startError ? <span className={errorText}>{startError}</span> : null}
        </label>

        <label className={field}>
          <span className={label}>終了（秒）</span>
          <input
            className={`${input} ${timeInput} ${endError ? inputInvalid : ""}`}
            value={endDraft}
            inputMode="decimal"
            autoComplete="off"
            spellCheck={false}
            disabled={readOnly}
            placeholder="自動"
            aria-label={`${index + 1}行目の終了時刻`}
            aria-invalid={endError ? true : undefined}
            data-testid={`lyrics-end-${index}`}
            onFocus={() => {
              endFocused.current = true;
            }}
            onBlur={() => {
              endFocused.current = false;
              commitTime("end");
            }}
            onChange={(event) => setEndDraft(event.currentTarget.value)}
            onKeyDown={onTimeKeyDown("end")}
          />
          {endError ? <span className={errorText}>{endError}</span> : null}
          {!endError && interval.derived && row.endTime === null ? (
            <span className={derivedNote}>終了は次の行または+4秒</span>
          ) : null}
        </label>
      </div>

      <div className={textGrid}>
        <label className={field}>
          <span className={label}>本文</span>
          <input
            className={input}
            value={row.mondai}
            lang="ja"
            autoComplete="off"
            disabled={readOnly}
            placeholder="歌詞行（空にするとパネルが非表示）"
            aria-label={`${index + 1}行目の本文`}
            data-testid={`lyrics-text-${index}`}
            onChange={(event) => onPatch(row.id, { mondai: event.currentTarget.value })}
          />
        </label>

        <label className={field}>
          <span className={label}>フリガナ</span>
          <input
            className={input}
            value={row.furigana}
            lang="ja"
            autoComplete="off"
            disabled={readOnly}
            aria-label={`${index + 1}行目のフリガナ`}
            data-testid={`lyrics-furigana-${index}`}
            onChange={(event) => onPatch(row.id, { furigana: event.currentTarget.value })}
          />
        </label>

        <label className={field}>
          <span className={label}>ローマ字</span>
          <input
            className={input}
            value={row.romaji}
            autoComplete="off"
            spellCheck={false}
            disabled={readOnly}
            aria-label={`${index + 1}行目のローマ字`}
            data-testid={`lyrics-romaji-${index}`}
            onChange={(event) => onPatch(row.id, { romaji: event.currentTarget.value })}
          />
        </label>
      </div>
    </li>
  );
}
