import { ArrowLeft, ArrowRight } from "lucide-react";
import { Suspense, lazy } from "react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import type { Chart, ValidationIssue } from "@/lib/chart/types";
import { judgeNoteCount } from "@/lib/new-song/validation";

/**
 * Step 2 of the new-song flow: 譜面.
 *
 * The eight-lane editor of issue #18 runs unchanged with the YouTube video,
 * so notes are placed while listening. The chart itself lives in the parent
 * (and in `sessionStorage`), so a reload in the middle of the step loses
 * nothing. The minimum-notes rule gates the step: at least one note is
 * required, fewer than 20 only warns.
 */

/** Konva needs a canvas, so the editor is loaded on demand like in /chart. */
const ChartEditor = lazy(() =>
  import("../chart-editor/chart-editor").then((module) => ({ default: module.ChartEditor })),
);

const layout = css({
  display: "grid",
  gridTemplateRows: "minmax(0, 1fr) auto",
  height: "100%",
  minHeight: 0,
});

const editorArea = css({
  minHeight: 0,
  minWidth: 0,
});

const footer = css({
  alignItems: "center",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  px: "4",
});

const verdict = css({
  fontSize: "2xs",
  margin: "0",
});

const verdictError = css({
  color: "#ff6b6b",
});

const verdictWarn = css({
  color: "#e2b93b",
});

const verdictOk = css({
  color: "#8c8c8c",
});

const buttons = css({
  display: "flex",
  gap: "2",
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

/** The icon inside a button. */
const buttonIcon = css({
  color: "currentColor",
  marginRight: "1",
});

export interface ChartStepProps {
  chart: Chart | null;
  chartIssues: ValidationIssue[];
  videoId: string;
  /** Name of a fresh chart when the flow starts without one. */
  fallbackChartName: string;
  onChange: (chart: Chart | null) => void;
  onIssuesChange: (issues: ValidationIssue[]) => void;
  onBack: () => void;
  onAdvance: () => void;
}

export function ChartStep({
  chart,
  chartIssues,
  videoId,
  fallbackChartName,
  onChange,
  onIssuesChange,
  onBack,
  onAdvance,
}: ChartStepProps) {
  const noteCount = chart?.notes.length ?? 0;
  const verdict = judgeNoteCount(noteCount);
  const blocked = verdict?.level === "error" || chart === null || chartIssues.length > 0;

  return (
    <div className={layout}>
      <div className={editorArea}>
        <Suspense fallback={<div className={editorArea} />}>
          <ChartEditor
            key={videoId || "no-video"}
            initialChart={
              chart ?? { name: fallbackChartName, maxBlock: 8, BPM: 120, offset: 0, notes: [] }
            }
            initialYouTubeId={videoId}
            onChartChange={onChange}
            onValidationChange={onIssuesChange}
          />
        </Suspense>
      </div>

      <div className={footer}>
        <p
          className={`${verdict} ${
            verdict?.level === "error"
              ? verdictError
              : verdict?.level === "warn"
                ? verdictWarn
                : verdictOk
          }`}
          data-testid="note-count-verdict"
        >
          {verdict
            ? `${verdict.message.ja} / ${verdict.message.en}`
            : `ノート ${noteCount} 個 / ${noteCount} notes — OK`}
        </p>
        {chartIssues.length > 0 ? (
          <p className={`${verdict} ${verdictError}`}>
            譜面に問題が{chartIssues.length}件あります。修正するまで次へ進めません。 / The chart has{" "}
            {chartIssues.length} problems. Fix them to continue.
          </p>
        ) : null}
        <div className={buttons}>
          <Button className={raisedButton} size="md" onClick={onBack}>
            <ArrowLeft className={buttonIcon} size={14} aria-hidden="true" />
            戻る / Back
          </Button>
          <Button
            className={accent}
            size="md"
            data-testid="chart-advance"
            onClick={() => {
              if (!blocked) {
                onAdvance();
              }
            }}
          >
            <ArrowRight className={buttonIcon} size={14} aria-hidden="true" />
            次へ：公開 / Next: Publish
          </Button>
        </div>
      </div>
    </div>
  );
}
