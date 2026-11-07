import { CirclePlay } from "lucide-react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import { formatDuration } from "./catalog-view";
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
  display: { base: "flex", md: "none" },
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
});

const cardTitle = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "medium",
  lineHeight: "1.3",
  minWidth: "0",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "normal",
  display: "-webkit-box",
  WebkitLineClamp: "2",
  boxOrient: "vertical",
});

const cardMeta = css({
  color: "fg.muted",
  display: "flex",
  flexWrap: "wrap",
  fontSize: "xs",
  gap: "2",
  lineHeight: "1.5",
});

const cardActions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "flex-end",
  marginTop: "1",
  pt: "2",
  borderTopColor: "border",
  borderTopWidth: "1px",
});

const cardContainer = css({
  display: { base: "flex", md: "none" },
  flexDirection: "column",
});

const tableContainer = css({
  display: { base: "none", md: "block" },
});

const header = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  columnGap: "4",
  display: { base: "none", md: "grid" },
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
  minHeight: "3.5rem",
  paddingBottom: "2",
  paddingTop: "2",
  position: "relative",
  _last: {
    borderBottomWidth: "0",
  },
  _hover: {
    bg: "gray.a3",
    "& [data-open-hint]": { color: "blue.11" },
  },
  _focusWithin: {
    bg: "gray.a3",
    "& [data-open-hint]": { color: "blue.11" },
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

const openHint = css({
  color: "fg.subtle",
  display: { base: "none", md: "block" },
  fontSize: "xs",
  marginLeft: "auto",
  paddingRight: "4",
  whiteSpace: "nowrap",
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
  fontSize: "sm",
  fontWeight: "medium",
  lineHeight: "1.3",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "normal",
  display: "-webkit-box",
  WebkitLineClamp: "2",
  boxOrient: "vertical",
  width: "100%",
});

const artistText = css({
  color: "fg.muted",
  fontSize: "xs",
  lineHeight: "1.3",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
  width: "100%",
});

const levelBadge = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.default",
  display: "inline-flex",
  fontVariantNumeric: "tabular-nums",
  fontSize: "xs",
  fontWeight: "medium",
  justifyContent: "center",
  minWidth: "2rem",
  paddingX: "2",
  paddingY: "0.5",
});

const num = css({
  color: "fg.muted",
  display: { base: "none", md: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
  whiteSpace: "nowrap",
});

const version = css({
  color: "fg.muted",
  display: { base: "none", md: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  whiteSpace: "nowrap",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "flex-end",
  paddingRight: "4",
  zIndex: "1",
  minWidth: "0",
});

const actionLink = css({
  alignItems: "center",
  borderRadius: "l1",
  color: "fg.muted",
  display: "inline-flex",
  fontSize: "xs",
  fontWeight: "medium",
  gap: "1",
  minHeight: "8",
  paddingInline: "2",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: {
    bg: "gray.a3",
    color: "fg.default",
  },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "1px",
  },
});

const youtubeAction = css({
  color: "fg.subtle",
  _hover: {
    color: "fg.default",
  },
});

const levelCell = css({
  display: { base: "none", md: "block" },
});

const metaLine = css({
  color: "fg.subtle",
  display: { base: "flex", md: "none" },
  flexWrap: "wrap",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
  gap: "1",
  lineHeight: "1.3",
});

const metaValue = css({
  color: "fg.muted",
});

const legend = css({
  alignItems: "center",
  borderTopColor: "border",
  borderTopWidth: "1px",
  color: "fg.subtle",
  display: { base: "none", md: "flex" },
  flexWrap: "wrap",
  fontSize: "xs",
  gap: "3",
  paddingInline: "4",
  paddingY: "2",
});

const keyCap = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.default",
  display: "inline-block",
  fontFamily: "inherit",
  fontSize: "xs",
  lineHeight: "1.2",
  minWidth: "4",
  padding: "0.5 1.5",
  textAlign: "center",
});

/** Invisible only to sighted users; screen readers read the full row name. */
const visuallyHidden = css({
  clip: "rect(0 0 0 0)",
  clipPath: "inset(50%)",
  height: "1px",
  overflow: "hidden",
  position: "absolute",
  whiteSpace: "nowrap",
  width: "1px",
});

const accent = css({ colorPalette: "blue" });

const stateWrap = css({
  px: { base: "4", md: "6" },
  py: "6",
});

function chartHref(song: SongSummary): string {
  return `/chart?songId=${encodeURIComponent(song.id)}&version=${song.chartVersion}`;
}

function publishHref(song: SongSummary): string {
  return `/publish?songId=${encodeURIComponent(song.id)}&version=${song.chartVersion}`;
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
      ? "Loading songs…"
      : status === "offline"
        ? "Offline — the catalog needs a connection"
        : status === "error"
          ? "Unable to load catalog"
          : hasFilters
            ? `${total} song${total === 1 ? "" : "s"} published · ${songs.length} shown`
            : `${total} song${total === 1 ? "" : "s"} published`;
  return (
    <section className={panelFrame}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>Catalog</h2>
          <p className={panelSubtitle}>{subtitle}</p>
        </div>
        <Button variant="outline" size="sm" onClick={onRetry} disabled={status === "loading"}>
          Refresh
        </Button>
      </header>

      <div className={panelBodyFlush}>
        {status !== "ready" || songs.length === 0 ? (
          <div className={stateWrap}>
            {status === "loading" ? (
              <StateBlock
                title="Loading catalog"
                description="Reading the published catalog from the FlashBeat API."
              />
            ) : null}

            {status === "offline" ? (
              <StateBlock
                title="You are offline"
                description="The catalog is read from the FlashBeat API on this site. Reconnect, then retry."
                action={
                  <Button variant="outline" size="sm" onClick={onRetry}>
                    Retry
                  </Button>
                }
              />
            ) : null}

            {status === "error" ? (
              <StateBlock
                title="Catalog unavailable"
                description={errorMessage ?? "The catalog request failed."}
                action={
                  <Button className={accent} size="sm" onClick={onRetry}>
                    Try again
                  </Button>
                }
              />
            ) : null}

            {status === "empty" ? (
              <StateBlock
                title="No songs published yet"
                description="Publish a chart to the FlashBeat API and it shows up here immediately."
              />
            ) : null}

            {noFilterMatch ? (
              <StateBlock
                title="No song matches the filters"
                description="Clear the search query or level filter to see the published songs."
                action={
                  <Button variant="outline" size="sm" onClick={onClearFilters}>
                    Clear filters
                  </Button>
                }
              />
            ) : null}
          </div>
        ) : null}

        {status === "ready" && songs.length > 0 ? (
          <>
            <div className={cardContainer}>
              {songs.map((song) => (
                <div key={song.id} className={card}>
                  <div className={cardHeader}>
                    <span className={cardTitleRow}>
                      <SongThumbnail title={song.title} youtubeId={song.youtubeId} />
                      <a className={rowLink} href={chartHref(song)} data-row-link>
                        <span className={cardTitle}>{song.title}</span>
                      </a>
                    </span>
                    <span className={levelBadge}>{song.level}</span>
                  </div>
                  <div className={cardMeta}>
                    <span>{song.artist}</span>
                    <span>{song.bpm} BPM</span>
                    <span>{formatDuration(song.durationSec)}</span>
                    <span>{song.noteCount} notes</span>
                    <span>v{song.chartVersion}</span>
                  </div>
                  <div className={cardActions}>
                    <a className={actionLink} href={chartHref(song)}>
                      Open chart
                    </a>
                    <a className={actionLink} href={publishHref(song)}>
                      Publish
                    </a>
                    <a
                      aria-label={`${song.title} on YouTube`}
                      className={`${actionLink} ${youtubeAction}`}
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
                <div className={header} aria-hidden="true">
                  <span>Song</span>
                  <span>Lv</span>
                  <span className={headerNum}>BPM</span>
                  <span className={`${headerNum} ${headerTime}`}>Time</span>
                  <span className={headerNum}>Notes</span>
                  <span>Ver</span>
                  <span />
                </div>
                {songs.map((song) => (
                  <div key={song.id} className={`row ${row}`}>
                    <a className={rowLink} href={chartHref(song)} data-row-link>
                      <span className={visuallyHidden}>
                        {`Open chart: ${song.title} — ${song.artist}, level ${song.level}, version ${song.chartVersion}`}
                      </span>
                    </a>

                    <span className={rowBody}>
                      <SongThumbnail title={song.title} youtubeId={song.youtubeId} />
                      <span className={rowText}>
                        <span className={titleText}>{song.title}</span>
                        <span className={artistText}>{song.artist}</span>
                        <span className={metaLine}>
                          <span>Lv {song.level}</span>
                          <span>·</span>
                          <span>{song.bpm} BPM</span>
                          <span>·</span>
                          <span>{formatDuration(song.durationSec)}</span>
                          <span>·</span>
                          <span>{song.noteCount} notes</span>
                          <span>·</span>
                          <span>v{song.chartVersion}</span>
                        </span>
                      </span>
                    </span>

                    <span className={rowBody}>
                      <span className={titleText}>{song.title}</span>
                      <span className={artistText}>{song.artist}</span>
                      <span className={metaLine}>
                        <span>Lv {song.level}</span>
                        <span>·</span>
                        <span>{song.bpm} BPM</span>
                        <span>·</span>
                        <span>{formatDuration(song.durationSec)}</span>
                        <span>·</span>
                        <span>{song.noteCount} notes</span>
                        <span>·</span>
                        <span>v{song.chartVersion}</span>
                      </span>
                    </span>

                    <span className={levelCell}>
                      <span className={levelBadge}>{song.level}</span>
                    </span>
                    <span className={num}>{song.bpm}</span>
                    <span className={num}>{formatDuration(song.durationSec)}</span>
                    <span className={num}>{song.noteCount}</span>
                    <span className={version}>v{song.chartVersion}</span>

                    <span className={actions}>
                      <span className={openHint} aria-hidden="true" data-open-hint>
                        Open chart
                      </span>
                      <a className={actionLink} href={publishHref(song)}>
                        Publish
                      </a>
                      <a
                        aria-label={`${song.title} on YouTube`}
                        className={`${actionLink} ${youtubeAction}`}
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
            <kbd className={keyCap}>/</kbd> Search
          </span>
          <span>
            <kbd className={keyCap}>↑</kbd>
            <kbd className={keyCap}>↓</kbd> Move
          </span>
          <span>
            <kbd className={keyCap}>Enter</kbd> Open chart
          </span>
        </footer>
      ) : null}
    </section>
  );
}
