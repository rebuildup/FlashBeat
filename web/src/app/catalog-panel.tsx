import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import type { SongSummary } from "./types";
import { StateBlock, panelBody, panelFrame, panelHeader, panelSubtitle, panelTitle } from "./panel";

export type CatalogStatus = "loading" | "ready" | "empty" | "error";

const table = css({
  display: "grid",
  gap: "0",
  gridTemplateColumns: "minmax(0, 2.4fr) 4rem 5rem 5rem 5rem",
});

const tableHead = css({
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  color: "fg.subtle",
  fontSize: "2xs",
  letterSpacing: "0.06em",
  paddingBottom: "2",
  paddingTop: "2",
  textTransform: "uppercase",
});

const tableRow = css({
  alignItems: "center",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "grid",
  gap: "0",
  gridColumn: "1 / -1",
  gridTemplateColumns: "minmax(0, 2.4fr) 4rem 5rem 5rem 5rem",
  paddingBottom: "3",
  paddingTop: "3",
  _last: {
    borderBottomWidth: "0",
  },
});

const cell = css({
  color: "fg.muted",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
});

const songTitle = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "medium",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const songArtist = css({
  color: "fg.subtle",
  fontSize: "2xs",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const levelBadge = css({
  bg: "colorPalette.subtle.bg",
  borderRadius: "l1",
  color: "colorPalette.subtle.fg",
  display: "inline-flex",
  fontSize: "2xs",
  fontWeight: "medium",
  justifyContent: "center",
  paddingX: "2",
  paddingY: "0.5",
  width: "fit-content",
});

const accent = css({ colorPalette: "teal" });

export function CatalogPanel({
  status,
  songs,
  errorMessage,
  onRetry,
}: {
  status: CatalogStatus;
  songs: SongSummary[];
  errorMessage?: string;
  onRetry: () => void;
}) {
  return (
    <section className={panelFrame}>
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

        {status === "ready" ? (
          <div className={table}>
            <div className={`${tableHead} ${tableRow}`}>
              <span>Song</span>
              <span>Level</span>
              <span>BPM</span>
              <span>Notes</span>
              <span>Version</span>
            </div>
            {songs.map((song) => (
              <div key={song.id} className={tableRow}>
                <span>
                  <span className={songTitle}>{song.title}</span>
                  <br />
                  <span className={songArtist}>{song.artist}</span>
                </span>
                <span>
                  <span className={levelBadge}>{song.level}</span>
                </span>
                <span className={cell}>{song.bpm}</span>
                <span className={cell}>{song.noteCount}</span>
                <span className={cell}>v{song.chartVersion}</span>
              </div>
            ))}
          </div>
        ) : null}
      </div>
    </section>
  );
}
