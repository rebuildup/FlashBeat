import { CircleCheckIcon, CircleXIcon } from "lucide-react";
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

export function PublishResultPanel({ result }: { result: PublishResult | null }) {
  if (result === null) {
    return null;
  }

  if (result.ok) {
    return (
      <section className={`${frame} ${success}`} data-testid="publish-success">
        <p className={headline}>
          <CircleCheckIcon width={14} height={14} />
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
        <CircleXIcon width={14} height={14} />
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
    </section>
  );
}
