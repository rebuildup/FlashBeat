---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: [FEATURE / ISSUE TITLE]

**Issue**: #<issue-number> | **Branch**: `<issue-number>` | **Base**: `release-0-1-0`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [ ] **T1** [P] [description with exact path] — [done when: observable condition]
- [ ] **T2** [description with exact path] — [done when: observable condition]
- [ ] **T3** [description with exact path] — [done when: observable condition]
- [ ] **T4** [description with exact path] — [done when: observable condition]
- [ ] **T5** [description with exact path] — [done when: observable condition]

## Dependencies & Execution Order

1. [T1] → [T2] → [T3] …
2. [T4] can start in parallel with [T1] because it touches `<different path>`

**Blocking dependency**: [issue-number / external system, and the fallback used while it is unavailable]

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | [check] | [exact command] | [criterion] |

## Rollback

[Exact per-issue undo action (revert merge commit / drop branch), and the reason
it is independent of other issues' branches.]

## Notes

- Scope discipline: implement only the tasks above; new ideas become follow-up Issues.
- The Unity Editor / scene / prefab mutation is serialized: only the issue that
  owns the scene may run the Editor.
