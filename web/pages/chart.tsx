import { savePublishDraft } from "@/lib/publish/draft";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import { ChartEditor, type ChartEditorHandle } from "@/components/chart-editor/chart-editor";
import { loadSongSession, type SongSession } from "@/lib/song-session/load";
import { StudioShell } from "@/app/app-shell";
import {
  pageActions,
  pageBackLink,
  pageHeader,
  pageHeaderIdentity,
  pageTitle,
  pageSubtitle,
  primaryCta,
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

/** Permission boundary of the publish path, visible before pressing it. */
const tokenHint = css({
  color: "fg.subtle",
  flexBasis: { base: "100%", md: "auto" },
  fontSize: "2xs",
  minWidth: "0",
  whiteSpace: { base: "normal", md: "nowrap" },
});

/** Demoted secondary link to the source video (issue #215 review). */
const youtubeLink = css({
  color: "fg.muted",
  fontSize: "xs",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: { color: "fg.default" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "colorPalette.solid.bg",
    outlineOffset: "2px",
  },
});

/**
 * Start the editor's YouTube preview — the only owner of playback (#216) —
 * through the editor's imperative handle, then bring the preview panel into
 * view and focus it, so the visitor sees the song start without leaving the
 * Studio.
 *
 * The panel is the focus target, not the player iframe: the YouTube API
 * replaces its iframe when playback starts, which drops the focus. The panel
 * is rendered by the editor and stays in place, so keyboard users keep a
 * stable anchor.
 */
function tryPreview(editor: ChartEditorHandle | null): void {
  editor?.play();
  const panel = document.querySelector<HTMLElement>('[data-testid="editor-preview-panel"]');
  if (!panel) return;
  panel.scrollIntoView({ block: "center" });
  panel.tabIndex = -1;
  panel.focus();
}

export default function ChartPage({ songId, version }: Props) {
  const editorRef = useRef<ChartEditorHandle | null>(null);
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
          {/* Issue #215 review: the visitor's primary CTA stays inside the
              Studio — it brings the editor's YouTube preview (the only owner
              of playback, #216) into view and focuses its Play control. The
              staff publish path is demoted to the outline variant. */}
          {session?.song.youtubeId ? (
            <>
              <Button
                className={primaryCta}
                size="sm"
                data-testid="try-song-button"
                onClick={() => tryPreview(editorRef.current)}
              >
                再生して試す / Try it
              </Button>
              <a
                className={youtubeLink}
                href={`https://youtu.be/${encodeURIComponent(session.song.youtubeId)}`}
                rel="noreferrer"
                target="_blank"
                data-testid="open-youtube-link"
              >
                YouTube で開く
              </a>
            </>
          ) : null}
          <Button
            variant="outline"
            colorPalette="blue"
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
            ref={editorRef}
            onChartChange={setEditedChart}
          />
        </div>
      )}
    </StudioShell>
  );
}
