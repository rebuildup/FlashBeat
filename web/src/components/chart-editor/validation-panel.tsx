import { css } from "styled-system/css";
import type { ValidationIssue } from "@/lib/chart/types";

/**
 * Contract problems of the current chart.
 *
 * The list is filled from the Ajv errors of
 * `contracts/schemas/chart.v1.schema.json`, so the operator sees exactly what
 * the API would reject.
 */

const frame = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flexDirection: "column",
  gap: "2",
  padding: "3",
});

const title = css({
  color: "fg.default",
  fontSize: "xs",
  fontWeight: "semibold",
  margin: "0",
});

const list = css({
  display: "grid",
  gap: "1",
  listStyle: "none",
  margin: "0",
  maxHeight: "9rem",
  overflowY: "auto",
  padding: "0",
});

const item = css({
  alignItems: "baseline",
  display: "flex",
  gap: "2",
});

const path = css({
  color: "fg.subtle",
  fontFamily: "mono",
  fontSize: "2xs",
  wordBreak: "break-all",
});

const message = css({
  color: "fg.error",
  fontSize: "2xs",
});

const clean = css({
  color: "fg.muted",
  fontSize: "2xs",
  margin: "0",
});

export function ValidationPanel({ issues }: { issues: ValidationIssue[] }) {
  return (
    <section className={frame} data-testid="validation-panel">
      <h3 className={title}>Contract check</h3>

      {issues.length === 0 ? (
        <p className={clean}>The chart satisfies the chart contract.</p>
      ) : (
        <ul className={list}>
          {issues.map((issue, index) => (
            <li key={`${issue.path}-${issue.keyword}-${index}`} className={item}>
              <span className={path}>{issue.path || "chart"}</span>
              <span className={message}>{issue.message}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
