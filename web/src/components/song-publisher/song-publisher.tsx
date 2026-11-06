import type { SongSummary } from "@/app/types";
import { useCallback, useMemo, useRef, useState } from "react";
import { ChartEditor } from "@/components/chart-editor/chart-editor";
import { Button, Toaster, toaster } from "@/components/ui";
import { css } from "styled-system/css";
import type { Chart, ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { publishAndVerify } from "@/lib/publish/client";
import { savePublishDraft } from "@/lib/publish/draft";
import { resolvePublishTransport } from "@/lib/publish/transport";
import type { PublishResult } from "@/lib/publish/types";
import {
  buildPublishRequest,
  validateSongMetadata,
  type SongMetadataDraft,
  type SongMetadataField,
} from "@/lib/publish/validation";
import { PublishDialog, type PublishSummary } from "./publish-dialog";
import { PublishResultPanel } from "./publish-result";
import { SongMetadataForm } from "./song-metadata-form";

/**
 * Publish screen of the Web Studio.
 *
 * The screen holds the metadata of one song, the chart of that song and the
 * one request that publishes both. The chart comes from the editor of issue
 * #18, the request goes to `PUT /api/v1/admin/songs/{songId}` of the
 * contract.
 */

const root = css({
  display: "grid",
  gridTemplateRows: "auto minmax(0, 1fr)",
  height: "100%",
  minHeight: 0,
  overflow: "hidden",
});

const header = css({
  alignItems: "center",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
});

const headerTitles = css({
  display: "grid",
  gap: "1",
});

const title = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "semibold",
  margin: "0",
});

const subtitle = css({
  color: "fg.muted",
  fontSize: "xs",
  margin: "0",
});

const backLink = css({
  color: "fg.muted",
  fontSize: "xs",
  textDecoration: "none",
  _hover: {
    color: "fg.default",
  },
});

const body = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: "minmax(0, 1fr) 21rem",
  minHeight: 0,
  overflow: "hidden",
  p: "3",
});

const editorArea = css({
  display: "grid",
  gridTemplateRows: "minmax(0, 1fr)",
  minHeight: 0,
  minWidth: 0,
  overflow: "hidden",
});

const side = css({
  display: "flex",
  flexDirection: "column",
  gap: "3",
  minHeight: 0,
  overflow: "hidden",
});

const sideScroll = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "3",
  minHeight: 0,
  overflowY: "auto",
});

const card = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  padding: "3",
});

const cardTitle = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "semibold",
  margin: "0",
});

const facts = css({
  display: "grid",
  gap: "1",
  gridTemplateColumns: "auto minmax(0, 1fr)",
  margin: "0",
});

const factKey = css({
  color: "fg.muted",
  fontSize: "2xs",
  margin: "0",
});

const factValue = css({
  color: "fg.default",
  fontSize: "2xs",
  margin: "0",
  overflowWrap: "anywhere",
});

const warning = css({
  color: "fg.error",
  fontSize: "2xs",
  margin: "0",
});

const badge = css({
  bg: "gray.surface.bg.hover",
  borderRadius: "full",
  color: "fg.muted",
  fontSize: "2xs",
  paddingBottom: "0.5",
  paddingTop: "0.5",
  px: "2",
  whiteSpace: "nowrap",
});

const accent = css({ colorPalette: "teal" });

const NO_TOUCHED: Record<SongMetadataField, boolean> = {
  songId: false,
  title: false,
  artist: false,
  youtube: false,
  level: false,
  durationSec: false,
};

export interface SongPublisherProps {
  /** Chart the screen opens with. */
  initialChart: Chart;
  /** YouTube video of the preview. */
  initialYouTubeId: string;
  /**
   * Origin of the publish API.
   *
   * Empty means the same-origin live API. A value switches the screen to another
   * endpoint of the contract.
   */
  apiOrigin?: string;
  initialSong?: SongSummary;
  initialVersion?: number;
}

export function SongPublisher({ initialChart, initialYouTubeId, apiOrigin, initialSong, initialVersion }: SongPublisherProps) {
  const [draft, setDraft] = useState<SongMetadataDraft>(() => ({
    songId: initialSong?.id ?? "",
    title: initialSong?.title ?? initialChart.name,
    artist: initialSong?.artist ?? "",
    youtube: initialYouTubeId,
    level: initialSong?.level ?? 10,
    durationSec: initialSong?.durationSec ?? Number.NaN,
  }));
  const [titleEdited, setTitleEdited] = useState(Boolean(initialSong));
  const [chart, setChart] = useState<Chart | null>(initialChart);
  const [chartIssues, setChartIssues] = useState<ValidationIssue[]>([]);
  const [touched, setTouched] = useState<Record<SongMetadataField, boolean>>(NO_TOUCHED);
  const [attempted, setAttempted] = useState(false);
  const [token, setToken] = useState("");
  const [publishing, setPublishing] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [result, setResult] = useState<PublishResult | null>(null);

  const inFlight = useRef(false);
  const transport = useMemo(() => resolvePublishTransport(apiOrigin), [apiOrigin]);

  // The title follows the name of the chart until the operator writes one.
  const effectiveDraft = useMemo<SongMetadataDraft>(
    () => ({
      ...draft,
      title: titleEdited ? draft.title : (chart?.name ?? draft.title),
    }),
    [chart, draft, titleEdited],
  );

  const issues = useMemo(() => validateSongMetadata(effectiveDraft), [effectiveDraft]);

  const visibleIssues = useMemo(
    () => issues.filter((issue) => attempted || touched[issue.field]),
    [attempted, issues, touched],
  );

  const chartReady = chart !== null && chartIssues.length === 0;

  const summary = useMemo<PublishSummary>(
    () => ({
      songId: effectiveDraft.songId.trim(),
      title: effectiveDraft.title.trim(),
      artist: effectiveDraft.artist.trim(),
      youtubeId: parseYouTubeId(effectiveDraft.youtube) ?? "",
      level: effectiveDraft.level,
      durationSec: effectiveDraft.durationSec,
      bpm: chart?.BPM ?? 0,
      noteCount: chart?.notes.length ?? 0,
    }),
    [chart, effectiveDraft],
  );

  const handleChange = useCallback((patch: Partial<SongMetadataDraft>) => {
    if (patch.title !== undefined) {
      setTitleEdited(true);
    }
    setResult(null);
    setDraft((current) => ({ ...current, ...patch }));
  }, []);

  const handleFieldBlur = useCallback((field: SongMetadataField) => {
    setTouched((current) => ({ ...current, [field]: true }));
  }, []);

  const handleChartChange = useCallback((next: Chart | null) => {
    setResult(null);
    setChart(next);
  }, []);

  const handleChartIssues = useCallback((next: ValidationIssue[]) => {
    setChartIssues(next);
  }, []);

  const handleReview = useCallback(() => {
    setAttempted(true);
    setResult(null);

    if (issues.length === 0 && chartReady) {
      setDialogOpen(true);
    }
  }, [chartReady, issues.length]);

  const handlePublish = useCallback(async () => {
    if (inFlight.current) return;
    if (chart === null) {
      setResult({
        ok: false,
        failure: { code: "invalid", message: "There is no chart to publish." },
      });
      return;
    }

    const built = buildPublishRequest(effectiveDraft, chart);
    if (!built.ok) {
      setAttempted(true);
      setResult({
        ok: false,
        failure: {
          code: "invalid",
          message: "The song is not ready to publish.",
          details: built.issues.map((issue) => `${issue.field}: ${issue.message}`),
        },
      });
      return;
    }

    inFlight.current = true;
    setPublishing(true);

    const outcome = await publishAndVerify({
      baseUrl: transport.baseUrl,
      songId: effectiveDraft.songId.trim(),
      token,
      request: built.request,
      fetchImpl: transport.fetch,
    });

    inFlight.current = false;
    setPublishing(false);
    setResult(outcome);

    if (outcome.ok) {
      setDialogOpen(false);
      toaster.create({
        title: "Published",
        description: `${outcome.response.songId} is chart version ${outcome.response.chartVersion}, the catalog is at version ${outcome.response.catalogVersion}.`,
        type: "success",
      });
      return;
    }

    toaster.create({
      title: "Not published",
      description: outcome.failure.message,
      type: "error",
    });
  }, [chart, effectiveDraft, token, transport]);

  const handleBackToEditor = useCallback(() => {
    const url = savePublishDraft(
      {
        songId: initialSong?.id ?? null,
        version: initialVersion !== undefined ? String(initialVersion) : null,
        chart: chart ?? initialChart,
      },
      "editor",
    );
    window.location.assign(url);
  }, [chart, initialChart, initialSong, initialVersion]);

  return (
    <div className={root}>
      <header className={header}>
        <div className={headerTitles}>
          <a className={backLink} href="/">
            ← Web Studio
          </a>
          <h1 className={title}>Publish a song</h1>
          <p className={subtitle}>{initialVersion ? `Editing published v${initialVersion} · draft preserved on failure` : "New unpublished draft"}</p>
        </div>

        <span className={badge}>
          {transport.mode === "fixture"
            ? "Fixture API"
            : transport.baseUrl
              ? `Live API ${transport.baseUrl}`
              : "Live API (this origin)"}
        </span>
      </header>

      <div className={body}>
        <section className={editorArea}>
          <ChartEditor
            initialChart={initialChart}
            initialYouTubeId={initialYouTubeId}
            onChartChange={handleChartChange}
            onValidationChange={handleChartIssues}
          />
        </section>

        <aside className={side}>
          <div className={sideScroll}>
            <section className={card}>
              <h2 className={cardTitle}>Song metadata</h2>
              <SongMetadataForm
                draft={effectiveDraft}
                onChange={handleChange}
                onFieldBlur={handleFieldBlur}
                issues={visibleIssues}
              />
            </section>
          </div>

          <section className={card}>
            <h2 className={cardTitle}>Publish</h2>

            <dl className={facts}>
              <dt className={factKey}>Chart</dt>
              <dd className={factValue}>
                {summary.noteCount} notes at {summary.bpm} BPM
              </dd>

              <dt className={factKey}>Song id</dt>
              <dd className={factValue}>{summary.songId || "—"}</dd>
            </dl>

            {chartIssues.length > 0 ? (
              <p className={warning}>
                The chart has {chartIssues.length} contract problems. Fix them before publishing.
              </p>
            ) : null}

            <Button className={accent} size="md" disabled={publishing} onClick={handleReview}>
              Review publish
            </Button>
          </section>

          <PublishResultPanel result={result} onRetry={handleReview} onBackToEditor={handleBackToEditor} />
        </aside>
      </div>

      <PublishDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        summary={summary}
        token={token}
        onTokenChange={setToken}
        publishing={publishing}
        error={result !== null && !result.ok ? result.failure.message : undefined}
        onPublish={handlePublish}
      />

      <Toaster />
    </div>
  );
}
