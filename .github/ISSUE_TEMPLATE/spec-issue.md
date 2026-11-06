---
name: Implementation issue (lean spec)
about: Bounded implementation unit — one issue, one branch, one worktree, one Draft PR
title: "[Issue #NN] "
labels: ""
assignees: ""
---

<!--
Lean template. Fill every section; a section that says "n/a" must say why.
The matching spec/plan/tasks artifacts are generated under specs/<NNN>-<slug>/
from the templates in .specify/templates/overrides/.
-->

## Objective

<!-- What is observably true after this issue lands that was not true before. 1-3 sentences. -->

## Owned paths

<!-- Only the paths this issue may mutate. Unity scene/prefab ownership is exclusive and serialized. -->

- `path/`

## Exclusions (out of scope)

<!-- Things a reviewer might expect but this issue deliberately does not do. -->

-

## Acceptance scenarios

<!-- Max 3. Each independently observable. -->

1. **Given** … **When** … **Then** …
2. **Given** … **When** … **Then** …

## Tasks (max 5)

- [ ] T1 …
- [ ] T2 …

## Dependencies / gate

- Depends on: #NN (branch `NN`) — …
- Blocks: #NN
- Gate: …

## Verification evidence

<!-- What will be executed and attached to the PR, bound to the candidate SHA. -->

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | | | |

## Rollback

<!-- How this issue is undone on its own. -->

## Stack / base

- Base branch: `release-0-1-0` (or the parent issue branch for stacked children)
- Branch name: issue number only (`NN`)
