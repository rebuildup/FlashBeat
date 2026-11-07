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
  fontSize: "xs",
  wordBreak: "break-all",
  maxWidth: "100%",
  overflowWrap: "break-word",
});

const message = css({
  color: "#ff6b6b",
  fontSize: "xs",
  wordBreak: "break-word",
  maxWidth: "100%",
});

const clean = css({
  color: "#b0b0b0",
  fontSize: "xs",
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
