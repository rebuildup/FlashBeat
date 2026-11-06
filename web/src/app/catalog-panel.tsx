import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import type { SongSummary } from "./types";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

export type CatalogStatus = "loading" | "ready" | "empty" | "error";

/**
 * Catalog list (issue #70).
 *
 * Pen v5 `SongRow` translation: one row = one link to the real chart
 * (`/chart?songId&version`), title at weight 500 over a muted artist line,
 * numeric columns tabular and aligned, hairline separators, recessed panel
 * surface (`panelFrame` = Pen `well`). Full content width — the width is for
 * scanning work, not a capped card.
 */

const COLUMNS = "minmax(16rem, 1fr) 4.5rem 5.5rem 6rem 4.5rem 7rem";

const header = css({
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  columnGap: "1.25rem",
  display: "grid",
  fontSize: "xs",
  gridColumn: "1 / -1",
  gridTemplateColumns: COLUMNS,
  paddingBottom: "2",
  paddingTop: "1",
});

const headerNum = css({
  textAlign: "right",
});

const list = css({
  display: "grid",
  gridTemplateColumns: COLUMNS,
});

const row = css({
  alignItems: "center",
  borderBottomColor: "border",
  borderBottomWidth: "1px",
  color: "inherit",
  columnGap: "1.25rem",
  display: "grid",
  gridColumn: "1 / -1",
  gridTemplateColumns: COLUMNS,
  minHeight: "3.5rem",
  outline: "none",
  paddingBottom: "2",
  paddingTop: "2",
  textDecoration: "none",
  _hover: {
    bg: "gray.a3",
  },
  _focusVisible: {
    bg: "gray.a3",
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "-2px",
  },
  _last: {
    borderBottomWidth: "0",
  },
});

const titleCell = css({
  alignItems: "center",
  display: "flex",
  gap: "3",
  minWidth: "0",
  paddingRight: "3",
});

const titleText = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "medium",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const artistText = css({
  color: "fg.muted",
  fontSize: "xs",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const levelBadge = css({
  bg: "blue.subtle.bg",
  borderRadius: "l1",
  color: "blue.subtle.fg",
  display: "inline-flex",
  fontVariantNumeric: "tabular-nums",
  fontSize: "xs",
  fontWeight: "medium",
  justifyContent: "center",
  justifySelf: "start",
  minWidth: "2.25rem",
  paddingX: "2",
  paddingY: "0.5",
});

const num = css({
  color: "fg.muted",
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
});

const version = css({
  color: "fg.muted",
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  justifySelf: "start",
});

const openHint = css({
  color: "fg.subtle",
  fontSize: "xs",
  justifySelf: "end",
});

const rowHover = css({
  _hover: {
    "& .open-hint": { color: "blue.11" },
  },
});

const accent = css({ colorPalette: "blue" });

export function CatalogPanel({
  status,
  songs,
  errorMessage,
  onRetry,
  hasFilters,
  onClearFilters,
}: {
  status: CatalogStatus;
  songs: SongSummary[];
  errorMessage?: string;
  onRetry: () => void;
  /** A search query or level filter is active — a ready list of 0 rows is "no match", not "empty". */
  hasFilters: boolean;
  onClearFilters: () => void;
}) {
  const noFilterMatch = status === "ready" && songs.length === 0 && hasFilters;

  return (
    <section className={`${panelFrame} ${rowHover}`}>
      <header className={panelHeader}>
        <div>
          <h2 className={panelTitle}>Catalog</h2>
          <p className={panelSubtitle}>
            {status === "loading"
              ? "Loading songs…"
              : `${songs.length} song${songs.length === 1 ? "" : "s"} published`}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={onRetry} disabled={status === "loading"}>
          Refresh
        </Button>
      </header>

      <div className={panelBody}>
        {status === "loading" ? (
          <StateBlock
            title="Loading catalog"
            description="Reading the published catalog from the FlashBeat API."
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

        {status === "ready" && songs.length > 0 ? (
          <div className={list}>
            <div className={header} aria-hidden="true">
              <span>Song</span>
              <span>Lv</span>
              <span className={headerNum}>BPM</span>
              <span className={headerNum}>Notes</span>
              <span>Ver</span>
              <span />
            </div>
            {songs.map((song) => (
              <a
                key={song.id}
                className={row}
                href={`/chart?songId=${encodeURIComponent(song.id)}&version=${song.chartVersion}`}
              >
                <span className={titleCell}>
                  <span className={css({ minWidth: "0" })}>
                    <span className={titleText}>{song.title}</span>
                    <br />
                    <span className={artistText}>{song.artist}</span>
                  </span>
                </span>
                <span>
                  <span className={levelBadge}>{song.level}</span>
                </span>
                <span className={num}>{song.bpm}</span>
                <span className={num}>{song.noteCount}</span>
                <span className={version}>v{song.chartVersion}</span>
                <span className={`open-hint ${openHint}`}>Open</span>
              </a>
            ))}
          </div>
        ) : null}
      </div>
    </section>
  );
}
