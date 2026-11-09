import { useCallback, useRef, useState } from "react";
import { ArrowLeft, CircleX, CloudUpload, LoaderCircle } from "lucide-react";
import { Button, Field, Input } from "@/components/ui";
import { css } from "styled-system/css";
import type { Chart, ValidationIssue } from "@/lib/chart/types";
import { parseYouTubeId } from "@/lib/chart/youtube";
import { publishAndVerify } from "@/lib/publish/client";
import { buildPublishRequest } from "@/lib/publish/validation";
import type { PublishResult, SongPublishResponse } from "@/lib/publish/types";
import type { PublishTransport } from "@/lib/publish/transport";
import type { NewSongInfo } from "@/lib/new-song/draft";

/**
 * Step 3 of the new-song flow: 公開.
 *
 * One `PUT /api/v1/admin/songs/{songId}` publishes metadata and chart. The
 * staff token is typed at runtime, lives in this tab's memory only and is
 * never written to the URL, storage or the bundle. A failure keeps every
 * input — the step re-renders with the token, the summary and the chart
 * intact, so the operator only fixes what the API complained about.
 */

const layout = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: { base: "1fr", md: "minmax(0, 1fr) 24rem" },
  height: "100%",
  minHeight: 0,
  overflowY: "auto",
  p: "4",
});

const card = css({
  bg: "#2d2d2d",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  padding: "4",
});

const cardTitle = css({
  color: "#ffffff",
  fontSize: "sm",
  fontWeight: "semibold",
  margin: "0",
});

const summaryList = css({
  display: "grid",
  fontSize: "xs",
  gap: "1",
  gridTemplateColumns: "auto minmax(0, 1fr)",
  margin: "0",
});

const summaryKey = css({
  color: "#8c8c8c",
  fontSize: "2xs",
  margin: "0",
});

const summaryValue = css({
  color: "#ffffff",
  fontSize: "2xs",
  margin: "0",
  overflowWrap: "anywhere",
});

const actions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "space-between",
});

const raisedButton = css({
  bg: "#3a3a3a",
  color: "#ffffff",
  borderRadius: "8px",
  borderColor: "transparent",
  _hover: { bg: "#454545" },
  _disabled: { opacity: 0.4 },
});

const accent = css({ colorPalette: "blue" });

/** The icon inside a button or a status line. */
const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
  verticalAlign: "middle",
});

const failure = css({
  bg: "red.solid.bg",
  borderColor: "red.solid.border",
  borderRadius: "l2",
  borderWidth: "1px",
  color: "red.solid.fg",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
});

const failureList = css({
  display: "grid",
  fontSize: "2xs",
  gap: "1",
  listStyle: "none",
  margin: "0",
  padding: "0",
});

export interface PublishStepProps {
  info: NewSongInfo;
  chart: Chart;
  chartIssues: ValidationIssue[];
  transport: PublishTransport;
  token: string;
  onTokenChange: (token: string) => void;
  onBack: () => void;
  /** Called with the verified response; the parent shows the success view. */
  onPublished: (response: SongPublishResponse) => void;
}

export function PublishStep({
  info,
  chart,
  chartIssues,
  transport,
  token,
  onTokenChange,
  onBack,
  onPublished,
}: PublishStepProps) {
  const [publishing, setPublishing] = useState(false);
  const [result, setResult] = useState<PublishResult | null>(null);
  const inFlight = useRef(false);

  const youtubeId = parseYouTubeId(info.youtube) ?? "";
  const summary = [
    { key: "曲ID / Song id", value: info.songId.trim() },
    { key: "曲名 / Title", value: info.title.trim() },
    { key: "アーティスト / Artist", value: info.artist.trim() || "—" },
    { key: "動画 / Video", value: youtubeId },
    { key: "レベル / Level", value: String(info.level) },
    { key: "再生時間 / Duration", value: `${info.durationSec} 秒 / s` },
    { key: "譜面 / Chart", value: `${chart.notes.length} ノート / notes · ${chart.BPM} BPM` },
  ];

  const handlePublish = useCallback(async () => {
    if (inFlight.current) return;

    const built = buildPublishRequest(info, chart);
    if (!built.ok) {
      setResult({
        ok: false,
        failure: {
          code: "invalid",
          message:
            "曲情報に不備があります。以下の項目を修正してください。 / The song info has problems. Fix the fields below.",
          details: built.issues.map((issue) => `${issue.field}: ${issue.message}`),
        },
      });
      return;
    }

    inFlight.current = true;
    setPublishing(true);

    const outcome = await publishAndVerify({
      baseUrl: transport.baseUrl,
      songId: info.songId.trim(),
      token,
      request: built.request,
      fetchImpl: transport.fetch,
    });

    inFlight.current = false;
    setPublishing(false);
    setResult(outcome);

    if (outcome.ok) {
      onPublished(outcome.response);
    }
  }, [chart, info, onPublished, token, transport]);

  return (
    <div className={layout}>
      <section className={card}>
        <h2 className={cardTitle}>公開内容の確認 / Review what will be published</h2>
        <dl className={summaryList}>
          {summary.map((row) => (
            <SummaryRow key={row.key} k={row.key} v={row.value} />
          ))}
        </dl>
        {chartIssues.length > 0 ? (
          <p className={`${failureList}`} style={{ color: "#ff6b6b" }}>
            譜面に問題が{chartIssues.length}件あります。戻って修正してください。 / The chart has{" "}
            {chartIssues.length} problems. Go back and fix them.
          </p>
        ) : null}
      </section>

      <section className={card}>
        <h2 className={cardTitle}>公開 / Publish</h2>
        <Field.Root>
          <Field.Label>スタッフトークン / Staff token</Field.Label>
          <Input
            type="password"
            autoComplete="off"
            placeholder="FLASHBEAT_ADMIN_TOKEN"
            value={token}
            onChange={(event) => onTokenChange(event.currentTarget.value)}
          />
          <Field.HelperText>
            実行時だけ入力します。URLや保存領域には書き込まれません。 / Typed at runtime only; never
            written to the URL or any storage.
          </Field.HelperText>
        </Field.Root>

        <p className={`${summaryKey}`} data-testid="transport-mode">
          {transport.mode === "fixture"
            ? "テスト用API（fixture）に送信します。 / Sending to the fixture API."
            : transport.baseUrl
              ? `API: ${transport.baseUrl}`
              : "このサイトのAPIに送信します。 / Sending to this site's API."}
        </p>

        {result !== null && !result.ok ? (
          <section className={failure} data-testid="publish-error">
            <p style={{ fontWeight: "semibold", margin: "0" }}>
              <CircleX className={buttonIcon} size={14} aria-hidden="true" />
              公開できませんでした / Not published
            </p>
            <p style={{ fontSize: "2xs", margin: "0" }}>{result.failure.message}</p>
            {result.failure.details && result.failure.details.length > 0 ? (
              <ul className={failureList}>
                {result.failure.details.map((detail, index) => (
                  <li key={`${detail}-${index}`}>{detail}</li>
                ))}
              </ul>
            ) : null}
            <p style={{ fontSize: "2xs", margin: "0" }}>
              入力と譜面はすべて保持されています。 / Everything you entered is kept.
            </p>
          </section>
        ) : null}

        <div className={actions}>
          <Button className={raisedButton} size="md" disabled={publishing} onClick={onBack}>
            <ArrowLeft className={buttonIcon} size={14} aria-hidden="true" />
            戻る / Back
          </Button>
          <Button className={accent} size="md" disabled={publishing} onClick={handlePublish}>
            {publishing ? (
              <>
                <LoaderCircle className={buttonIcon} size={14} aria-hidden="true" />
                公開中… / Publishing…
              </>
            ) : (
              <>
                <CloudUpload className={buttonIcon} size={14} aria-hidden="true" />
                公開する / Publish
              </>
            )}
          </Button>
        </div>
      </section>
    </div>
  );
}

function SummaryRow({ k, v }: { k: string; v: string }) {
  return (
    <>
      <dt className={summaryKey}>{k}</dt>
      <dd className={summaryValue}>{v}</dd>
    </>
  );
}
