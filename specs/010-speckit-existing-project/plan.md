---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: Spec Kit existing-project setup and one-issue one-worktree template

**Issue**: #10 | **Branch**: `10` | **Base**: `release-0-1-0` | **Date**: 2026-11-06
**Spec**: [`specs/010-speckit-existing-project/spec.md`](spec.md)

## Summary

Install Spec Kit in place with the upstream existing-project flow, keep the
project-init governance untouched, and replace the generic SDD templates with
project-local lean overrides that encode the delivery rules this project
already runs (one issue = one branch = one worktree = one Draft PR). The
delivery procedure is written down in `docs/agent/spec-kit-workflow.md`, the
issue/PR templates mirror the lean fields, and this issue's own artifacts are
the worked example.

## Owned Paths

```text
.specify/                                  # scaffolding, overrides, scripts (sh + ps)
.claude/skills/speckit-*/                   # Claude Code integration
.agents/skills/speckit-*/                   # Codex CLI integration
.omp/commands/speckit.*                     # Oh My Pi integration
docs/agent/spec-kit-workflow.md             # operating procedure
.github/ISSUE_TEMPLATE/spec-issue.md        # lean issue template
.github/pull_request_template.md            # evidence-carrying PR template
mise.toml                                   # pinned uv + specify tasks
specs/010-speckit-existing-project/         # worked example
```

**Structure Decision**: Spec Kit's own directory layout is kept as upstream
defines it (`.specify/`), because the CLI resolves templates and scripts from
there; the project-specific content lives in `.specify/templates/overrides/`,
the documented project-local override location, so a future
`specify init --force` cannot wipe it.

## Dependencies & Gate

- **Depends on**: #3 (branch `3`, PR #8) — supplies `contracts/` and `docs/architecture/stacked-pr-plan.md`. This branch links those paths but does not vendor them; it works before #3 merges.
- **Blocks**: every issue that produces spec/plan/tasks artifacts.
- **Gate**: governance byte-identical to the pre-init baseline; worked example resolves through `check-prerequisites.sh`; CLI version pinned in `mise.toml`.

## Exclusions

- Backfilling specs for legacy game features.
- Any Unity asset, contract file, or governance edit.
- Adding the git extension of Spec Kit: branch/worktree management stays with the worktrunk flow (`wt switch --create <issue-number>`), which already owns that rule.

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) and [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md) are unchanged by this issue and remain the top-level contract. `.specify/memory/constitution.md` points at them instead of restating them.
- Mutable Ownership Safety: the issue owns only additive paths listed above; no Unity scene, prefab, or shared mutable state is touched, so it can run in parallel with #9, #11, #12.
- Evidence Integrity: each verification row in the spec names an executed command and its actual result; the baseline tag `pre-speckit-init-10` fixes the pre-init identity.
- Canonical Consistency: `.specify/memory/constitution.md` is a pointer, not a competing constitution; the upstream delivery plan on branch `3` remains the single source for the PR DAG.
- Violations: none.

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | CLI version | `mise exec -- uvx --from specify-cli==1.3.1 specify --version` | stdout `specify 1.3.1` |
| V2 | Governance untouched | `git diff --stat pre-speckit-init-10 -- constitution organization skills-lock.json` | empty diff |
| V3 | Integrations | `cat .specify/integration.json` | claude, codex, omp listed |
| V4 | Worked example | `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` | feature dir + tasks.md present |
| V5 | Lean templates | `grep -l "Owned Paths" specs/010-speckit-existing-project/spec.md specs/010-speckit-existing-project/plan.md` | both files listed |

## Rollback

Revert this PR's merge commit (or drop branch `10`). The change is additive and
does not modify shared mutable state, so the revert cannot affect other issues'
branches or the Unity project.
