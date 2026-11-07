import { css } from "styled-system/css";
import { countErrors, countWarnings, isWarning } from "@/lib/chart/issues";
import type { ValidationIssue } from "@/lib/chart/types";

/**
 * Contract problems of the current chart.
 *
 * The list is filled from the Ajv errors of
 * `contracts/schemas/chart.v1.schema.json`, so the operator sees exactly what
 * the API would reject.
 */

const frame = css({
  bg: "#2d2d2d",
  borderRadius: "16px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
});

const title = css({
  color: "#ffffff",
  fontSize: "xs",
  fontWeight: "medium",
  margin: "0",
});

const list = css({
  bg: "#131313",
  borderRadius: "8px",
  display: "grid",
  gap: "1",
  listStyle: "none",
  margin: "0",
  maxHeight: "9rem",
  overflowY: "auto",
  padding: "2",
  width: "100%",
  maxWidth: "100%",
});

const item = css({
  alignItems: "baseline",
  display: "flex",
  gap: "2",
});

const path = css({
  color: "#8c8c8c",
  fontFamily: "mono",
  fontSize: "2xs",
  wordBreak: "break-all",
  maxWidth: "100%",
  overflowWrap: "break-word",
});

const message = css({
  color: "#ff6b6b",
  fontSize: "2xs",
  wordBreak: "break-word",
  maxWidth: "100%",
});

/**
 * A warning is a problem the chart was read with (issue #208): the chart was
 * published before the semantic rules were unified, so it can be repaired
 * instead of being dropped.
 */
const warning = css({
  color: "#e2b93b",
  fontSize: "2xs",
  wordBreak: "break-word",
  maxWidth: "100%",
});

const warningTag = css({
  color: "#e2b93b",
  fontSize: "2xs",
  fontWeight: "medium",
});

const summary = css({
  color: "#b0b0b0",
  fontSize: "2xs",
  margin: "0",
});

const clean = css({
  color: "#b0b0b0",
  fontSize: "2xs",
  margin: "0",
});

export function ValidationPanel({ issues }: { issues: ValidationIssue[] }) {
  const warnings = countWarnings(issues);
  const errors = countErrors(issues);

  return (
    <section className={frame} data-testid="validation-panel">
      <h3 className={title}>Contract check</h3>

      {issues.length === 0 ? (
        <p className={clean}>The chart satisfies the chart contract.</p>
      ) : (
        <>
          {warnings > 0 ? (
            <p className={summary} data-testid="validation-warning-summary">
              {warnings} warning{warnings === 1 ? "" : "s"} — the chart was read leniently and still
              has to be repaired before it can be published.
            </p>
          ) : null}
          {errors > 0 ? (
            <p className={summary} data-testid="validation-error-summary">
              {errors} error{errors === 1 ? "" : "s"} — the publish API refuses this chart.
            </p>
          ) : null}
          <ul className={list}>
            {issues.map((issue, index) => (
              <li key={`${issue.path}-${issue.keyword}-${index}`} className={item}>
                <span className={path}>{issue.path || "chart"}</span>
                {isWarning(issue) ? <span className={warningTag}>warning</span> : null}
                <span className={isWarning(issue) ? warning : message}>{issue.message}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
