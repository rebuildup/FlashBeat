import { savePublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import { ChartEditor } from "@/components/chart-editor/chart-editor";
import { loadSongSession, type SongSession } from "@/lib/song-session/load";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./chart.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };

const shell = css({
  bg: "#1b1b1b",
  display: "flex",
  flexDirection: "column",
  fontFamily: '"Noto Sans JP", "Hiragino Kaku Gothic ProN", Meiryo, system-ui, sans-serif',
  height: "100dvh",
  overflow: "hidden",
});

const topbar = css({
  alignItems: "center",
  borderBottomColor: "#2d2d2d",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  minHeight: "3rem",
  px: "3",
  py: "2",
});

const identity = css({
  color: "#b0b0b0",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
  minWidth: "0",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

const catalogLink = css({
  color: "#8c8c8c",
  fontSize: "xs",
  textDecoration: "none",
  _hover: { color: "#ffffff" },
});

/** v5 Button Primary: light raised button with dark text. */
const primaryAction = css({
  bg: "#ededed",
  color: "#1b1b1b",
  borderRadius: "8px",
  _hover: { bg: "#dedede" },
  _disabled: { opacity: 0.4 },
});

const banner = css({
  alignItems: "center",
  bg: "#4d3232",
  color: "#ff6b6b",
  display: "flex",
  fontSize: "xs",
  gap: "3",
  justifyContent: "space-between",
  padding: "2 3",
});

const statusLine = css({
  alignItems: "center",
  color: "#b0b0b0",
  display: "flex",
  flex: "1",
  fontSize: "xs",
  height: "full",
  justifyContent: "center",
  margin: "0",
});

const editorPane = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
});

export default function ChartPage({ songId, version }: Props) {
  const [session, setSession] = useState<SongSession | null>(null);
  const [editedChart, setEditedChart] = useState<Chart | null | undefined>(undefined);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const selected = songId !== null || version !== null;
  useEffect(() => {
    let active = true;
    setSession(null);
    setError("");
    setEditedChart(undefined);
    if (selected)
      loadSongSession(songId ?? "", version ?? "").then(
        (value) => {
          if (active) setSession(value);
        },
        (reason) => {
          if (active) setError(reason instanceof Error ? reason.message : String(reason));
        },
      );
    return () => {
      active = false;
    };
  }, [songId, version, selected, attempt]);
  return (
    <main className={shell}>
      <header className={topbar}>
        <a className={catalogLink} href="/">
          Catalog
        </a>
        <span className={identity} data-testid="chart-identity">
          {session
            ? `${session.song.title} · v${session.chartVersion} · ${session.chart.notes.length} notes`
            : selected
              ? "Published chart"
              : "New local draft · not published"}
        </span>
        <Button
          className={primaryAction}
          size="sm"
          disabled={(selected && !session) || editedChart === null}
          onClick={() => {
            try {
              window.location.assign(
                savePublishDraft({
                  songId,
                  version,
                  chart: editedChart ?? session?.chart ?? emptyChart,
                }),
              );
            } catch {
              setError("The local draft could not be saved. Keep this editor open and retry.");
            }
          }}
        >
          Review &amp; Publish
        </Button>
      </header>
      {error ? (
        <section className={banner} role="alert">
          {error}
          <Button size="sm" variant="outline" onClick={() => setAttempt(attempt + 1)}>
            Retry
          </Button>
        </section>
      ) : selected && !session ? (
        <p className={statusLine} role="status">
          Loading selected chart…
        </p>
      ) : (
        <div className={editorPane}>
          <ChartEditor
            key={`${songId}:${version}:${attempt}`}
            initialChart={session?.chart ?? emptyChart}
            initialYouTubeId={session?.song.youtubeId ?? ""}
            onChartChange={setEditedChart}
          />
        </div>
      )}
    </main>
  );
}
