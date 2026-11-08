import { useCallback, useEffect, useMemo, useState } from "react";
import { css } from "styled-system/css";
import type { ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { createFixtureStudioFetch } from "@/lib/new-song/fixture-api";
import {
  clearNewSongDraft,
  emptyNewSongDraft,
  loadNewSongDraft,
  saveNewSongDraft,
  type NewSongDraft,
  type NewSongInfo,
  type NewSongStep,
} from "@/lib/new-song/draft";
import { resolvePublishTransport, type PublishTransport } from "@/lib/publish/transport";
import type { SongPublishResponse } from "@/lib/publish/types";
import { ChartStep } from "./chart-step";
import { PublishStep } from "./publish-step";
import { PublishSuccessView } from "./publish-success";
import { SongInfoStep } from "./song-info-step";
import { Stepper } from "./stepper";

/**
 * The new-song flow (issue #135).
 *
 * Three guided steps — 曲情報 → 譜面 → 公開 — for registering a YouTube song
 * with a chart at the exhibition booth. The draft (info + chart + step
 * position) lives in `sessionStorage`, so a reload loses nothing; the staff
 * token lives in memory only. A successful publish is shown only after the
 * API answered and the published data was read back.
 */

const root = css({
  bg: "#1b1b1b",
  color: "#ffffff",
  display: "grid",
  fontFamily: '"Noto Sans JP", "Hiragino Kaku Gothic ProN", Meiryo, system-ui, sans-serif',
  gridTemplateRows: "auto auto minmax(0, 1fr)",
  height: "100dvh",
  overflow: "hidden",
});

const header = css({
  alignItems: "center",
  borderBottomColor: "#2d2d2d",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  minHeight: "3rem",
  px: "4",
  py: "2",
});

const headerTitles = css({
  display: "grid",
  gap: "1",
});

const backLink = css({
  color: "#8c8c8c",
  fontSize: "xs",
  textDecoration: "none",
  _hover: { color: "#ffffff" },
});

const title = css({
  color: "#ffffff",
  fontSize: "sm",
  fontWeight: "semibold",
  margin: "0",
});

const badge = css({
  bg: "#2d2d2d",
  borderRadius: "full",
  color: "#8c8c8c",
  fontSize: "2xs",
  paddingBlock: "0.5",
  paddingInline: "2",
  whiteSpace: "nowrap",
});

const goal = css({
  color: "fg.subtle",
  fontSize: "xs",
  margin: "0",
});

const stepperBar = css({
  borderBottomColor: "#2d2d2d",
  borderBottomWidth: "1px",
  display: "flex",
  justifyContent: "center",
  paddingBlock: "2",
  px: "4",
});

const stepScroll = css({
  minHeight: 0,
  overflowY: "auto",
  px: "4",
  py: "4",
});

const stepFrame = css({
  bg: "#2d2d2d",
  borderRadius: "16px",
  margin: "0 auto",
  maxWidth: "44rem",
  padding: "4",
});

export interface NewSongFlowProps {
  /**
   * Origin of the publish API. Empty means the same origin; the value
   * `fixture` selects the in-repo fixture transport (dev only).
   */
  apiOrigin?: string;
}

export function NewSongFlow({ apiOrigin }: NewSongFlowProps) {
  const [draft, setDraft] = useState<NewSongDraft>(() => loadNewSongDraft() ?? emptyNewSongDraft());
  const [chartIssues, setChartIssues] = useState<ValidationIssue[]>([]);
  const [takenIds, setTakenIds] = useState<ReadonlySet<string> | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [token, setToken] = useState("");
  const [published, setPublished] = useState<{
    response: SongPublishResponse;
    noteCount: number;
  } | null>(null);

  // The fixture transport is a review aid: it only exists in development and
  // serves the read side too, so publishAndVerify can read its own write.
  const transport = useMemo<PublishTransport>(() => {
    if (import.meta.env.DEV && (apiOrigin ?? "").trim() === "fixture") {
      return { mode: "fixture", baseUrl: "", fetch: createFixtureStudioFetch() };
    }
    try {
      return resolvePublishTransport(apiOrigin);
    } catch {
      return resolvePublishTransport("");
    }
  }, [apiOrigin]);

  useEffect(() => {
    saveNewSongDraft(draft);
  }, [draft]);

  const refreshCatalog = useCallback(async () => {
    try {
      const response = await transport.fetch(`${transport.baseUrl}/api/v1/catalog`, {
        cache: "no-store",
        headers: { accept: "application/json" },
      });
      if (!response.ok) {
        throw new Error(String(response.status));
      }
      const body = (await response.json()) as { songs?: Array<{ id?: unknown }> };
      const ids = new Set<string>();
      for (const song of body.songs ?? []) {
        if (typeof song.id === "string") {
          ids.add(song.id);
        }
      }
      setTakenIds(ids);
      setCatalogError(null);
    } catch (error) {
      setTakenIds(null);
      setCatalogError(error instanceof Error ? error.message : String(error));
    }
  }, [transport]);

  useEffect(() => {
    void refreshCatalog();
  }, [refreshCatalog]);

  const handleInfoChange = useCallback((patch: Partial<NewSongInfo>) => {
    setDraft((current) => ({ ...current, info: { ...current.info, ...patch } }));
  }, []);

  const handleChartChange = useCallback((chart: NewSongDraft["chart"]) => {
    setDraft((current) => ({ ...current, chart }));
  }, []);

  const goTo = useCallback((step: NewSongStep) => {
    setDraft((current) => ({
      ...current,
      step,
      maxStep: step > current.maxStep ? step : current.maxStep,
    }));
  }, []);

  const handlePublished = useCallback(
    (response: SongPublishResponse) => {
      setPublished({ response, noteCount: draft.chart?.notes.length ?? 0 });
      clearNewSongDraft();
    },
    [draft.chart],
  );

  const handleRegisterAnother = useCallback(() => {
    setPublished(null);
    setDraft(emptyNewSongDraft());
  }, []);

  const videoId = useMemo(() => parseYouTubeId(draft.info.youtube) ?? "", [draft.info.youtube]);

  if (published !== null) {
    return (
      <div className={root}>
        <Header transport={transport} />
        <div className={stepScroll}>
          <PublishSuccessView
            response={published.response}
            noteCount={published.noteCount}
            apiBaseUrl={transport.baseUrl}
            onRegisterAnother={handleRegisterAnother}
          />
        </div>
      </div>
    );
  }

  return (
    <div className={root}>
      <Header transport={transport} />

      <div className={stepperBar}>
        <Stepper current={draft.step} maxReached={draft.maxStep} onSelect={goTo} />
      </div>

      {draft.step === 1 ? (
        <div className={stepScroll}>
          <div className={stepFrame} data-testid="new-song-step-1">
            <SongInfoStep
              info={draft.info}
              takenIds={takenIds}
              catalogError={catalogError}
              onChange={handleInfoChange}
              onAdvance={() => goTo(2)}
            />
          </div>
        </div>
      ) : null}

      {draft.step === 2 ? (
        <div data-testid="new-song-step-2">
          <ChartStep
            chart={draft.chart}
            chartIssues={chartIssues}
            videoId={videoId}
            fallbackChartName={draft.info.title.trim() || "Untitled draft"}
            onChange={handleChartChange}
            onIssuesChange={setChartIssues}
            onBack={() => goTo(1)}
            onAdvance={() => goTo(3)}
          />
        </div>
      ) : null}

      {draft.step === 3 && draft.chart !== null ? (
        <div className={stepScroll} data-testid="new-song-step-3">
          <PublishStep
            info={draft.info}
            chart={draft.chart}
            chartIssues={chartIssues}
            transport={transport}
            token={token}
            onTokenChange={setToken}
            onBack={() => goTo(2)}
            onPublished={handlePublished}
          />
        </div>
      ) : null}
    </div>
  );
}

function Header({ transport }: { transport: PublishTransport }) {
  return (
    <header className={header}>
      <div className={headerTitles}>
        <a className={backLink} href="/">
          ← 曲を選ぶ / Back to songs
        </a>
        <h1 className={title}>新曲を登録 / New song</h1>
        {/* Issue #215: the goal and the permission boundary are stated up
            front, before the operator reaches the publish step. */}
        <p className={goal} data-testid="new-song-goal">
          新しい曲を登録して公開します。公開にはスタッフのadminトークンが必要です。入力は保存され、戻っても消えません。
        </p>
      </div>
      <span className={badge} data-testid="new-song-transport">
        {transport.mode === "fixture"
          ? "Fixture API"
          : transport.baseUrl
            ? `Live API ${transport.baseUrl}`
            : "Live API (this origin)"}
      </span>
    </header>
  );
}
