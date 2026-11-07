import type { ValidationIssue } from "./types";

/**
 * Severity of the validation issues the editor shows.
 *
 * `validate.ts` marks a tolerated problem with the `warning.` prefix on its
 * `keyword`, so a chart that was read leniently (issue #208) keeps its
 * problems visible without pretending that the publish path accepts them.
 */
export const WARNING_PREFIX = "warning.";

/** True when the issue is a warning and not a hard error. */
export function isWarning(issue: ValidationIssue): boolean {
  return issue.keyword.startsWith(WARNING_PREFIX);
}

/** The same issue, marked as a warning. */
export function asWarning(issue: ValidationIssue): ValidationIssue {
  return isWarning(issue) ? issue : { ...issue, keyword: `${WARNING_PREFIX}${issue.keyword}` };
}

/** Number of warnings in the list. */
export function countWarnings(issues: ValidationIssue[]): number {
  return issues.filter((issue) => isWarning(issue)).length;
}

/** Number of hard errors in the list. */
export function countErrors(issues: ValidationIssue[]): number {
  return issues.length - countWarnings(issues);
}

/**
 * Identity of a problem: where it is and which rule it broke.
 *
 * The message is not part of it: a hold that is moved changes the beats in
 * the message, but it is the same problem at the same place.
 */
function issueKey(issue: ValidationIssue): string {
  const keyword = isWarning(issue) ? issue.keyword.slice(WARNING_PREFIX.length) : issue.keyword;
  return `${issue.path}|${keyword}`;
}

/**
 * Merge the live validation of a chart with the warnings it was loaded with.
 *
 * The editor validates the chart it shows on every edit, and it validates it
 * **strictly** — the publish API refuses a chart with a broken relation, so
 * the editor must not be more forgiving than the API. A chart that was read
 * leniently (issue #208) is the exception: it was published before the rules
 * were unified, so its problems are shown as warnings. They stay warnings
 * until the operator repairs them; a problem the operator creates while
 * editing is a hard error again.
 *
 * A warning that the operator has already repaired is not shown any more:
 * the live validation does not report it any more.
 */
export function mergeLoadWarnings(
  live: ValidationIssue[],
  loaded: ValidationIssue[],
): ValidationIssue[] {
  if (loaded.length === 0) {
    return live;
  }
  const warned = new Set(loaded.map(issueKey));
  return live.map((issue) => (warned.has(issueKey(issue)) ? asWarning(issue) : issue));
}
