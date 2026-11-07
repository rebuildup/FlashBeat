import { savePublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import { ChartEditor } from "@/components/chart-editor/chart-editor";
import { loadSongSession, type SongSession } from "@/lib/song-session/load";
import { StudioShell } from "@/app/app-shell";
import {
  pageActions,
  pageBackLink,
  pageHeader,
  pageHeaderIdentity,
  pageTitle,
  pageSubtitle,
} from "@/app/panel";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./chart.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };

const banner = css({
  alignItems: "center",
  bg: "red.subtle.bg",
  color: "red.subtle.fg",
  colorPalette: "red",
  display: "flex",
  fontSize: "xs",
  gap: "3",
  justifyContent: "space-between",
  padding: "2 3",
  flexShrink: "0",
});

const statusLine = css({
  alignItems: "center",
  color: "fg.muted",
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

/** v5 primary CTA: the same solid palette action the publish screen uses. */
const primaryAction = css({
  colorPalette: "blue",
  bg: "colorPalette.solid.bg",
  color: "colorPalette.solid.fg",
  _hover: { opacity: "0.9" },
  _disabled: { opacity: "0.4" },
});

/** Permission boundary of the publish path, visible before pressing it. */
const tokenHint = css({
  color: "fg.subtle",
  fontSize: "2xs",
  whiteSpace: "nowrap",
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
    <StudioShell scroll={false}>
      <header className={pageHeader}>
        <a className={pageBackLink} href="/">
          ← 曲を選ぶ / Back to songs
        </a>
        <div className={pageHeaderIdentity}>
          <h1 className={pageTitle}>{selected ? "譜面を見る・試す" : "新しい譜面"}</h1>
          <p className={pageSubtitle} data-testid="chart-identity">
            {session
              ? `${session.song.title} · v${session.chartVersion} · ${session.chart.notes.length} notes`
              : selected
                ? "Published chart"
                : "New local draft · not published"}
          </p>
          <p className={pageSubtitle} data-testid="chart-guidance">
            {selected
              ? "再生して試してみてください。左のリンクで曲選びに戻れます / Play it; the link on the left goes back to the songs."
              : "編集して「公開に進む」へ。戻っても入力は消えません / Edit, then continue; going back keeps your input."}
          </p>
        </div>
        <div className={pageActions}>
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
            公開に進む / Review &amp; Publish
          </Button>
          <span className={tokenHint} data-testid="publish-token-hint">
            公開にはスタッフのadminトークンが必要です / Staff admin token required
          </span>
        </div>
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
            initialIssues={session?.issues}
            onChartChange={setEditedChart}
          />
        </div>
      )}
    </StudioShell>
  );
}
