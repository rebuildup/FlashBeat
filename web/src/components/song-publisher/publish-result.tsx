import { ArrowLeft, CircleCheckIcon, CircleXIcon, RotateCcw } from "lucide-react";
import { Button } from "@/components/ui";
import { css } from "styled-system/css";
import type { PublishResult } from "@/lib/publish/types";

/**
 * Result of the last publish attempt.
 *
 * The panel stays on screen after the attempt, so the operator can read the
 * new chart version or the reason of the rejection without opening the
 * console.
 */

const frame = css({
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
});

const success = css({
  bg: "green.solid.bg",
  borderColor: "green.solid.border",
  color: "green.solid.fg",
});

const failure = css({
  bg: "red.solid.bg",
  borderColor: "red.solid.border",
  color: "red.solid.fg",
});

const headline = css({
  alignItems: "center",
  display: "flex",
  fontSize: "xs",
  fontWeight: "semibold",
  gap: "2",
  margin: "0",
});

const detail = css({
  fontSize: "2xs",
  margin: "0",
});

const list = css({
  display: "grid",
  fontSize: "2xs",
  gap: "1",
  listStyle: "none",
  margin: "0",
  padding: "0",
});

const actions = css({
  display: "flex",
  gap: "2",
  marginTop: "1",
});

/** The icon of a result action button. */
const buttonIcon = css({
  color: "currentColor",
  flexShrink: 0,
});

export interface PublishResultPanelProps {
  result: PublishResult | null;
  /** Re-open the publish dialog with every input and the token retained. */
  onRetry?: () => void;
  /** Save the chart draft and return to the chart editor. */
  onBackToEditor?: () => void;
}

export function PublishResultPanel({ result, onRetry, onBackToEditor }: PublishResultPanelProps) {
  if (result === null) {
    return null;
  }

  if (result.ok) {
    return (
      <section className={`${frame} ${success}`} data-testid="publish-success">
        <p className={headline}>
          <CircleCheckIcon width={14} height={14} aria-hidden="true" />
          Published
        </p>
        <p className={detail}>
          {result.response.songId} is now chart version {result.response.chartVersion}. The catalog
          is at version {result.response.catalogVersion}.
        </p>
      </section>
    );
  }

  return (
    <section className={`${frame} ${failure}`} data-testid="publish-error">
      <p className={headline}>
        <CircleXIcon width={14} height={14} aria-hidden="true" />
        Not published
      </p>
      <p className={detail}>{result.failure.message}</p>
      {result.failure.details && result.failure.details.length > 0 ? (
        <ul className={list}>
          {result.failure.details.map((detail, index) => (
            <li key={`${detail}-${index}`}>{detail}</li>
          ))}
        </ul>
      ) : null}
      <p className={detail}>Nothing was published. Your inputs and the chart draft are kept.</p>
      <div className={actions}>
        {onRetry ? (
          <Button size="xs" onClick={onRetry}>
            <RotateCcw className={buttonIcon} size={14} aria-hidden="true" />
            Retry
          </Button>
        ) : null}
        {onBackToEditor ? (
          <Button size="xs" variant="outline" onClick={onBackToEditor}>
            <ArrowLeft className={buttonIcon} size={14} aria-hidden="true" />
            Back to editor
          </Button>
        ) : null}
      </div>
    </section>
  );
}
