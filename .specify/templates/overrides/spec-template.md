---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: [FEATURE / ISSUE TITLE]

**Issue**: #<issue-number> | **Branch**: `<issue-number>` | **PR base**: `release-0-1-0` | **Date**: [DATE] | **Status**: Draft

**Input**: `$ARGUMENTS`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

[1–3 sentences. The observable outcome this issue delivers, not the implementation
plan. State what is true after the issue lands that was not true before.]

## Owned Paths

[Only the paths this issue may mutate. Another issue touching the same path is a
coordination conflict, not a merge conflict.]

- `path/to/dir/` — [what changes here and why it belongs to this issue]

## Exclusions (explicitly out of scope)

- [thing a reviewer would reasonably expect, but this issue does NOT do]
- [any path/behavior that must stay untouched by this issue]

## User Scenarios & Testing *(mandatory)*

[Maximum 3 scenarios. Each is independently observable. Do not describe the
existing game; describe only the behavior this issue adds or changes.]

### Scenario 1 — [title] (Priority: P1)

**Given** [precondition], **When** [action], **Then** [observable result]

**Independent Test**: [the exact command, scene, or manual step that proves it]

### Scenario 2 — [title] (Priority: P2) *(only if needed)*

**Given** [precondition], **When** [action], **Then** [observable result]

**Independent Test**: [exact check]

### Edge Cases

- [boundary or failure condition, and the expected behavior]

## Requirements

- **R1**: [imperative, testable requirement]
- **R2**: [imperative, testable requirement]

## Dependencies & Gate

- **Depends on**: #<issue-number> (branch `<n>`) — [what is needed and how to work without it if it is not merged]
- **Blocks**: #<issue-number> *(if any)*
- **Gate**: [what must be true before this Draft PR may be integrated: test run, artifact, review]

## Verification Evidence

[Evidence that will be attached to the PR, bound to the candidate SHA.]

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | [e.g. EditMode tests] | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode` | [pass/fail criterion] |

## Rollback

[How this issue is undone on its own, without touching other issues' work.
Usually: revert the merge commit, or delete the branch — state which and why it
is safe because of the Owned Paths boundary.]

## Success Criteria

- **SC1**: [measurable outcome tied to a scenario above]
