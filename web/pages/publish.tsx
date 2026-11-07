import { readPublishDraft } from "@/lib/publish/draft";
import { useEffect, useState } from "react";
import { BrandHeader } from "@/components/brand";
import { Button } from "@/components/ui";
import { SongPublisher } from "@/components/song-publisher/song-publisher";
import { loadSongSession } from "@/lib/song-session/load";
import { css } from "styled-system/css";
import type { Chart } from "@/lib/chart/types";
import type { Props } from "./publish.server";
import "@/styles/app.css";

const emptyChart: Chart = { name: "Untitled draft", maxBlock: 8, BPM: 120, offset: 0, notes: [] };

const page = css({
  alignItems: "center",
  bg: "canvas",
  color: "fg.default",
  display: "flex",
  justifyContent: "center",
  minHeight: "100dvh",
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

const header = css({
  alignItems: "center",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  justifyContent: "space-between",
  paddingBottom: "4",
});

const overline = css({
  color: "fg.subtle",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
  letterSpacing: "0.04em",
});

const title = css({
  color: "fg.default",
  fontSize: { base: "xl", md: "2xl" },
  fontWeight: "semibold",
  lineHeight: "1.3",
  margin: "0",
});

const description = css({
  color: "fg.muted",
  fontSize: "sm",
  lineHeight: "1.6",
  margin: "0",
  overflowWrap: "anywhere",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
});

const retryButton = css({
  colorPalette: "blue",
  bg: "colorPalette.solid.bg",
  color: "colorPalette.solid.fg",
  minHeight: "10",
  _hover: { opacity: 0.9 },
  _focusVisible: { outline: "2px solid", outlineOffset: "2px" },
});

const catalogLink = css({
  alignItems: "center",
  borderColor: "gray.outline.border",
  borderRadius: "l2",
  borderWidth: "1px",
  color: "fg.default",
  display: "inline-flex",
  fontSize: "sm",
  fontWeight: "medium",
  minHeight: "10",
  paddingInline: "4",
  textDecoration: "none",
  _focusVisible: { outline: "2px solid", outlineOffset: "2px" },
  _hover: { bg: "gray.outline.bg.hover" },
});

/** A loading or failed page retains the actual Studio identity and recovery path. */
function PublishState({
  kind,
  message,
  onRetry,
}: {
  kind: "loading" | "error";
  message?: string;
  onRetry: () => void;
}) {
  const error = kind === "error";
  return (
    <main className={page} aria-busy={!error}>
      <section className={card} role={error ? "alert" : "status"}>
        <header className={header}>
          <BrandHeader />
          <span className={overline}>Publish</span>
        </header>
        <div className={css({ display: "flex", flexDirection: "column", gap: "2" })}>
          <h1 className={title}>{error ? "譜面を読み込めませんでした" : "譜面を読み込み中"}</h1>
          <p className={description}>
            {error
              ? message ?? "読み込みに失敗しました。接続を確認して再試行してください。"
              : "公開対象の楽曲と譜面バージョンを確認しています。"}
          </p>
        </div>
        <div className={actions}>
          {error ? (
            <Button className={retryButton} size="sm" onClick={onRetry}>
              再試行
            </Button>
          ) : null}
          <a className={catalogLink} href="/">
            Catalog に戻る
          </a>
        </div>
      </section>
    </main>
  );
}

export default function PublishPage({ songId, version, draftId }: Props) {
  const [session, setSession] = useState<Awaited<ReturnType<typeof loadSongSession>> | null>(null);
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
        (value) => { if (active) setSession(value); },
        (reason) => { if (active) setError(reason instanceof Error ? reason.message : String(reason)); },
      );
    }

    return () => { active = false; };
  }, [songId, version, draftId, selected, attempt]);

  if (error) {
    return <PublishState kind="error" message={error} onRetry={() => setAttempt((value) => value + 1)} />;
  }

  if (selected && !session) {
    return <PublishState kind="loading" onRetry={() => setAttempt((value) => value + 1)} />;
  }

  return (
    <div style={{ height: "100dvh", overflow: "hidden" }}>
      <SongPublisher
        key={`${songId}:${version}:${attempt}`}
        initialChart={localChart ?? session?.chart ?? emptyChart}
        initialYouTubeId={session?.song.youtubeId ?? ""}
        initialSong={session?.song}
        initialVersion={session?.chartVersion}
      />
    </div>
  );
}
