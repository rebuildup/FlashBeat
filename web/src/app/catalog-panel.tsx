import { CirclePlay, RotateCcw, X } from "lucide-react";
import { Link } from "@void/react";
import { routeUrl } from "void/routes";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import { formatDuration } from "./catalog-view";
import {
  Badge,
  actionLink,
  actionLinkSubtle,
  keyCap,
  metaLine,
  metaText,
  num,
  numWide,
  rowActions,
  visuallyHidden,
} from "./list";
import { SongThumbnail } from "./song-thumbnail";
import type { SongSummary } from "./types";
import {
  StateBlock,
  panelBodyFlush,
  panelFrame,
  panelHeader,
  panelSubtitle,
  panelTitle,
} from "./panel";

export type CatalogStatus = "loading" | "ready" | "empty" | "error" | "offline";

/**
 * Catalog list (issue #70) — the song library of a chart-authoring tool.
 *
 * Pen v5 translation: recessed list well (`panelFrame` = `well`), one
 * `Prefab/SongRow`-shaped row per song (title 500 over muted artist, level in
 * a `Prefab/Tag` chip, right-aligned tabular numerics, hairline separators).
 * The whole row is the primary action (Open chart → `/chart?songId&version`);
 * Publish (`/publish?songId&version`) and the YouTube check link sit above the
 * stretched link. Keyboard model: `/` focuses search (topbar), rows are native
 * links (Tab + Enter), ↑/↓ move row focus (app-shell delegates the list here).
 */

const COLUMNS = {
  base: "minmax(0, 1fr) auto",
  md: "minmax(14rem, 1fr) 3rem 4.5rem 4.5rem 3.5rem 12.5rem",
  lg: "minmax(14rem, 1fr) 3rem 4.5rem 4rem 4.5rem 3.5rem 12.5rem",
};

const card = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  /**
   * Issue #515: the table's fixed tracks need ~832px plus padding, so below
   * `xl` (1280px) it cannot fit next to the 13rem rail — the card layout is
   * the readable form for the whole md–lg band, not just phones.
   */
  display: { base: "flex", xl: "none" },
  flexDirection: "column",
  gap: "2",
  minHeight: "0",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
  _last: { borderBottomWidth: "0" },
});

const cardHeader = css({
  alignItems: "flex-start",
  display: "flex",
  gap: "2",
  justifyContent: "space-between",
  minWidth: "0",
});

const cardTitleRow = css({
  alignItems: "center",
  display: "flex",
  gap: "3",
  minWidth: "0",
  /**
   * The stretched link inside the card is absolutely positioned (issue #74):
   * without a positioned ancestor it resolves against the viewport and the
   * whole page ends up covered by a 390px-wide invisible link.
   */
  position: "relative",
});

const cardTitle = css({
  color: "fg.default",
  /** Issue #802: row titles use the shared `rowTitle` step (16px medium). */
  textStyle: "rowTitle",
  minWidth: "0",
  overflow: "hidden",
  /**
   * A long, unbroken title (no spaces) must break inside the card instead of
   * pushing the catalog list into a horizontal scroll (issue #74).
   */
  overflowWrap: "anywhere",
  textOverflow: "ellipsis",
  whiteSpace: "normal",
  wordBreak: "break-word",
  display: "-webkit-box",
  WebkitLineClamp: "2",
  boxOrient: "vertical",
});

const cardMeta = css({
  color: "fg.muted",
  display: "flex",
  flexWrap: "wrap",
  fontSize: "sm",
  gap: "2",
  lineHeight: "1.5",
});

const cardActions = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  justifyContent: "flex-end",
  marginTop: "1",
  pt: "2",
  borderTopColor: "border",
  borderTopWidth: "1px",
});

const cardContainer = css({
  display: { base: "flex", xl: "none" },
  flexDirection: "column",
});

const tableContainer = css({
  display: { base: "none", xl: "block" },
});

const header = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  columnGap: "4",
  display: { base: "none", xl: "grid" },
  fontSize: "xs",
  gridColumn: "1 / -1",
  gridTemplateColumns: COLUMNS,
  paddingBottom: "2",
  paddingTop: "1",
});

const headerNum = css({
  textAlign: "right",
});

const headerTime = css({
  display: "none",
  lg: { display: "block" },
});

const list = css({
  display: "grid",
  gridTemplateColumns: COLUMNS,
});

const row = css({
  alignItems: "center",
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  columnGap: "4",
  display: "grid",
  gridColumn: "1 / -1",
  gridTemplateColumns: COLUMNS,
  /** Issue #802: 4rem rows — the 3rem thumbnail plus breathing room. */
  minHeight: "4rem",
  paddingBottom: "2",
  paddingTop: "2",
  position: "relative",
  _last: {
    borderBottomWidth: "0",
  },
  _hover: {
    bg: "gray.a3",
  },
  _focusWithin: {
    bg: "gray.a3",
  },
});

const rowLink = css({
  alignItems: "center",
  borderRadius: "l2",
  color: "inherit",
  display: "flex",
  gap: "3",
  inset: "0",
  outline: "none",
  paddingLeft: "4",
  position: "absolute",
  textDecoration: "none",
  zIndex: "0",
  width: "100%",
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "-2px",
  },
});

/**
 * The card layout has no table row to stretch over, so the title is an
 * ordinary link next to the thumbnail (issue #74). An absolute link here
 * would cover the thumbnail — and, without a positioned ancestor, the whole
 * viewport.
 */
const cardTitleLink = css({
  inset: "auto",
  /**
   * The link is a flex item of `cardTitleRow`; without `min-width: 0` its
   * automatic minimum size is the width of the longest word, so a long title
   * widens the card (and the catalog list) beyond the viewport (issue #74).
   */
  minWidth: "0",
  paddingLeft: "0",
  position: "static",
  width: "auto",
});

const rowBody = css({
  alignItems: "center",
  display: "flex",
  gap: "3",
  minWidth: "0",
  paddingInline: "4",
  pointerEvents: "none",
  py: "2",
  width: "100%",
});

const rowText = css({
  alignItems: "flex-start",
  display: "flex",
  flexDirection: "column",
  gap: "1",
  minWidth: "0",
  justifyContent: "center",
});

const titleText = css({
  color: "fg.default",
  /** Issue #802: row titles use the shared `rowTitle` step (16px medium). */
  textStyle: "rowTitle",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "normal",
  display: "-webkit-box",
  WebkitLineClamp: "2",
  boxOrient: "vertical",
  width: "100%",
});

/** Fixed-width level chip inside the shared neutral Badge. */
const levelBadge = css({
  fontVariantNumeric: "tabular-nums",
  minWidth: "2rem",
});

const version = css({
  color: "fg.muted",
  display: { base: "none", md: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  whiteSpace: "nowrap",
});

const levelCell = css({
  display: { base: "none", md: "block" },
});

/** Right inset of the actions cell, aligned with the row body's padding. */
const actionsCell = css({
  paddingRight: "4",
});

/**
 * Issue #802 review: the publish requirement used to repeat as a badge on
 * every row — pure noise once the list has more than a couple of songs.
 * It is stated once, above the list, instead.
 */
const publishNote = css({
  color: "fg.subtle",
  fontSize: "xs",
  gridColumn: "1 / -1",
  /**
   * Issue #802 review: the note sits above the column header as a caption
   * strip. Its inline padding matches the row text (`rowBody` px-4 plus the
   * link inset), so it no longer reads as an orphaned line under the headers.
   */
  paddingBlock: "2",
  paddingInline: "4",
});

const legend = css({
  alignItems: "center",
  borderTopColor: "border",
  borderTopWidth: "1px",
  color: "fg.subtle",
  display: { base: "none", xl: "flex" },
  flexWrap: "wrap",
  fontSize: "xs",
  gap: "3",
  paddingInline: "4",
  paddingY: "2",
});

const accent = css({ colorPalette: "blue" });

/** The icon of an action button or link. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

const stateWrap = css({
  px: { base: "4", md: "6" },
  py: "6",
});

/**
 * The editor URL of a catalog row. `routeUrl` keeps the destination inside
 * the typed route map, and Void's `Link` turns the click into a client-side
 * visit (issue #801) — no document reload.
 */
function chartHref(song: SongSummary) {
  return routeUrl("/chart", {
    query: { songId: song.id, version: song.chartVersion },
  });
}

export function CatalogPanel({
  status,
  songs,
  total,
  errorMessage,
  onRetry,
  hasFilters,
  onClearFilters,
  onListKeyDown,
}: {
  status: CatalogStatus;
  songs: SongSummary[];
  /** Songs in the loaded catalog before filtering — shown as "N published · M shown". */
  total: number;
  errorMessage?: string;
  onRetry: () => void;
  /** A search query or level filter is active — a ready list of 0 rows is "no match", not "empty". */
  hasFilters: boolean;
  onClearFilters: () => void;
  /** Arrow-key row navigation owned by the shell (rows are native links). */
  onListKeyDown?: React.KeyboardEventHandler;
}) {
  const noFilterMatch = status === "ready" && songs.length === 0 && hasFilters;
  const showLegend = status === "ready" && songs.length > 0;
  const subtitle =
    status === "loading"
      ? "曲を読み込み中…"
      : status === "offline"
        ? "オフライン"
        : hasFilters
          ? `${total}曲が公開中 · ${songs.length}件を表示`
          : `${total}曲が公開中`;

  return (
    <section className={panelFrame}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>カタログ</h2>
          <p className={panelSubtitle}>{subtitle}</p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={onRetry}
          loading={status === "loading"}
          disabled={status === "loading"}
        >
          <RotateCcw className={buttonIcon} size={14} aria-hidden="true" />
          更新
        </Button>
      </header>

      <div className={panelBodyFlush}>
        {status !== "ready" || songs.length === 0 ? (
          <div className={stateWrap}>
            {status === "loading" ? (
              <StateBlock
                title="カタログを読み込み中"
                description="FlashBeat APIから公開済みカタログを読み込んでいます。"
              />
            ) : null}

            {status === "offline" ? (
              <StateBlock
                title="オフラインです"
                description="カタログはこのサイトのFlashBeat APIから読み込まれます。再接続してから再試行してください。"
                action={
                  <Button variant="outline" size="sm" onClick={onRetry}>
                    <RotateCcw className={buttonIcon} size={14} aria-hidden="true" />
                    再試行
                  </Button>
                }
              />
            ) : null}

            {status === "error" ? (
              <StateBlock
                title="カタログを取得できません"
                description={errorMessage ?? "カタログの取得に失敗しました。"}
                action={
                  <Button className={accent} size="sm" onClick={onRetry}>
                    <RotateCcw className={buttonIcon} size={14} aria-hidden="true" />
                    再試行
                  </Button>
                }
              />
            ) : null}

            {status === "empty" ? (
              <StateBlock
                title="まだ公開された曲がありません"
                description="譜面をFlashBeat APIに公開すると、ここにすぐ表示されます。"
              />
            ) : null}

            {noFilterMatch ? (
              <StateBlock
                title="条件に一致する曲がありません"
                description="検索クエリまたはレベルフィルタを解除すると、公開済みの曲が表示されます。"
                action={
                  <Button variant="outline" size="sm" onClick={onClearFilters}>
                    <X className={buttonIcon} size={14} aria-hidden="true" />
                    フィルタを解除
                  </Button>
                }
              />
            ) : null}
          </div>
        ) : null}

        {status === "ready" && songs.length > 0 ? (
          <>
            {/* カード表示の帯 (xl 未満) でも ↑/↓ の行移動を効かせる。
                ハンドラは data-row-link を拾うので両レイアウトで共通。 */}
            <div className={cardContainer} onKeyDown={onListKeyDown}>
              <p className={publishNote} data-testid="publish-requirement">
                公開はスタッフ（要トークン）
              </p>
              {songs.map((song) => (
                <div key={song.id} className={card}>
                  <div className={cardHeader}>
                    <span className={cardTitleRow}>
                      <SongThumbnail title={song.title} youtubeId={song.youtubeId} />
                      <Link
                        className={`${rowLink} ${cardTitleLink}`}
                        href={chartHref(song)}
                        data-row-link
                      >
                        <span className={cardTitle}>{song.title}</span>
                      </Link>
                    </span>
                    <Badge className={levelBadge}>{song.level}</Badge>
                  </div>
                  <div className={cardMeta}>
                    <span>{song.artist}</span>
                    <span>{song.bpm} BPM</span>
                    <span>{formatDuration(song.durationSec)}</span>
                    <span>{song.noteCount}ノート</span>
                    <span>v{song.chartVersion}</span>
                  </div>
                  <div className={cardActions}>
                    <Link className={actionLink} href={chartHref(song)}>
                      譜面を開く
                    </Link>
                    <a
                      aria-label={`${song.title} をYouTubeで開く`}
                      className={actionLinkSubtle}
                      href={`https://youtu.be/${encodeURIComponent(song.youtubeId)}`}
                      rel="noreferrer"
                      target="_blank"
                    >
                      <CirclePlay size={14} aria-hidden="true" />
                    </a>
                  </div>
                </div>
              ))}
            </div>
            <div className={tableContainer}>
              <div className={list} data-testid="catalog-list" onKeyDown={onListKeyDown}>
                <p className={publishNote} data-testid="publish-requirement">
                  公開はスタッフ（要トークン）
                </p>
                <div className={header} aria-hidden="true">
                  <span>曲</span>
                  <span>Lv</span>
                  <span className={headerNum}>BPM</span>
                  <span className={`${headerNum} ${headerTime}`}>時間</span>
                  <span className={headerNum}>ノート</span>
                  <span>Ver</span>
                  <span />
                </div>
                {songs.map((song) => (
                  <div key={song.id} className={`row ${row}`}>
                    <Link className={rowLink} href={chartHref(song)} data-row-link>
                      <span className={visuallyHidden}>
                        {`譜面を開く: ${song.title} — ${song.artist}、レベル${song.level}、バージョン${song.chartVersion}`}
                      </span>
                    </Link>

                    <span className={rowBody} data-testid="catalog-row-body">
                      <SongThumbnail title={song.title} youtubeId={song.youtubeId} />
                      <span className={rowText}>
                        <span className={titleText}>{song.title}</span>
                        <span className={metaText}>{song.artist}</span>
                        <span className={metaLine}>
                          <span>Lv {song.level}</span>
                          <span>·</span>
                          <span>{song.bpm} BPM</span>
                          <span>·</span>
                          <span>{formatDuration(song.durationSec)}</span>
                          <span>·</span>
                          <span>{song.noteCount}ノート</span>
                          <span>·</span>
                          <span>v{song.chartVersion}</span>
                        </span>
                      </span>
                    </span>

                    <span className={levelCell}>
                      <Badge className={levelBadge}>{song.level}</Badge>
                    </span>
                    <span className={num}>{song.bpm}</span>
                    <span className={numWide}>{formatDuration(song.durationSec)}</span>
                    <span className={num}>{song.noteCount}</span>
                    <span className={version}>v{song.chartVersion}</span>

                    <span className={`${rowActions} ${actionsCell}`}>
                      <a
                        aria-label={`${song.title} をYouTubeで開く`}
                        className={actionLinkSubtle}
                        href={`https://youtu.be/${encodeURIComponent(song.youtubeId)}`}
                        rel="noreferrer"
                        target="_blank"
                      >
                        <CirclePlay size={14} aria-hidden="true" />
                      </a>
                    </span>
                  </div>
                ))}
              </div>
            </div>
          </>
        ) : null}
      </div>

      {showLegend ? (
        <footer className={legend} aria-hidden="true">
          <span>
            <kbd className={keyCap}>/</kbd> 検索
          </span>
          <span>
            <kbd className={keyCap}>↑</kbd>
            <kbd className={keyCap}>↓</kbd> 移動
          </span>
          <span>
            <kbd className={keyCap}>Enter</kbd> 譜面を開く
          </span>
        </footer>
      ) : null}
    </section>
  );
}
