import { readPublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui";
import { SongPublisher } from "@/components/song-publisher/song-publisher";
import { loadSongSession, type SongSession } from "@/lib/song-session/load";
import { StudioShell } from "@/app/app-shell";
import {
  pageBackLink,
  panelHeader,
  panelTitle,
  stateBlock,
  stateDescription,
  stateTitle,
} from "@/app/panel";
import { css } from "styled-system/css";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./publish.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };

/** The publisher fills the work area; it owns its header and inner scroll. */
const publisherPane = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
});

const content = css({
  alignItems: "center",
  display: "flex",
  flex: "1",
  flexDirection: "column",
  justifyContent: "center",
  minHeight: "0",
  overflowY: "auto",
  px: { base: "4", md: "8" },
  py: "8",
});

const card = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l3",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "5",
  maxWidth: "34rem",
  p: { base: "5", md: "8" },
  width: "full",
});

const overline = css({
  color: "fg.subtle",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
  letterSpacing: "0.04em",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
});

/** v5 primary CTA: the same solid palette action the chart editor uses. */
const retryButton = css({
  colorPalette: "blue",
  bg: "colorPalette.solid.bg",
  color: "colorPalette.solid.fg",
  minHeight: "10",
  _hover: { opacity: "0.9" },
  _focusVisible: { outline: "2px solid", outlineOffset: "2px" },
});

/** A loading or failed page keeps the Studio identity and the recovery path. */
function PublishState({
  kind,
  message,
  onRetry,
}: {
  kind: "loading" | "error";
  message?: string;
  onRetry: () => void;
}) {
  const failed = kind === "error";
  return (
    <StudioShell>
      <div className={content}>
        <section className={card} role={failed ? "alert" : "status"} aria-busy={!failed}>
          <header className={panelHeader}>
            <h1 className={panelTitle}>
              {failed ? "譜面を読み込めませんでした" : "譜面を読み込み中"}
            </h1>
            <span className={overline}>Publish</span>
          </header>
          <div className={stateBlock}>
            <p className={stateTitle}>
              {failed ? "読み込みに失敗しました" : "公開対象を確認しています"}
            </p>
            <p className={stateDescription}>
              {failed
                ? (message ?? "接続を確認して再試行してください。")
                : "公開対象の楽曲と譜面バージョンを確認しています。"}
            </p>
          </div>
          <div className={actions}>
            {failed ? (
              <Button className={retryButton} size="sm" onClick={onRetry}>
                再試行
              </Button>
            ) : null}
            <a className={pageBackLink} href="/">
              ← Catalog に戻る
            </a>
          </div>
        </section>
      </div>
    </StudioShell>
  );
}

export default function PublishPage({ songId, version, draftId }: Props) {
  const [session, setSession] = useState<SongSession | null>(null);
  const [localChart, setLocalChart] = useState<Chart | null>(null);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  const selected = songId !== null || version !== null;

  useEffect(() => {
    let active = true;
    setSession(null);
    setError("");
    setLocalChart(null);

    try {
      setLocalChart(draftId ? readPublishDraft(draftId, songId, version) : null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
      return;
    }

    if (selected) {
      loadSongSession(songId ?? "", version ?? "").then(
        (value) => {
          if (active) setSession(value);
        },
        (reason) => {
          if (active) setError(reason instanceof Error ? reason.message : String(reason));
        },
      );
    }

    return () => {
      active = false;
    };
  }, [songId, version, draftId, selected, attempt]);

  if (error) {
    return (
      <PublishState kind="error" message={error} onRetry={() => setAttempt((value) => value + 1)} />
    );
  }

  if (selected && !session) {
    return <PublishState kind="loading" onRetry={() => setAttempt((value) => value + 1)} />;
  }

  return (
    <StudioShell scroll={false}>
      <div className={publisherPane}>
        <SongPublisher
          key={`${songId}:${version}:${attempt}`}
          initialChart={localChart ?? session?.chart ?? emptyChart}
          initialYouTubeId={session?.song.youtubeId ?? ""}
          initialSong={session?.song}
          initialVersion={session?.chartVersion}
        />
      </div>
    </StudioShell>
  );
}
