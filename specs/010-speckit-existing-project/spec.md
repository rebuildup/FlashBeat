---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Spec Kit existing-project setup and one-issue one-worktree template

**Issue**: #10 | **Branch**: `10` | **PR base**: `release-0-1-0` | **Date**: 2026-11-06 | **Status**: Draft

**Input**: "Spec Kit 既存リポジトリ導入と1 Issue=1 worktree テンプレート"

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Spec Kit is initialized **inside this existing repository** (not a fresh project),
so every later issue can produce a lean `spec.md` / `plan.md` / `tasks.md` triple
before touching code. The project-init governance stays the only authority: this
issue adds tooling and templates, it does not define a second constitution, and
it does not retrospectively specify the legacy Unity game.

## Owned Paths

- `.specify/` — shared scaffolding, lean template overrides, constitution pointer
- `.claude/skills/speckit-*`, `.agents/skills/speckit-*`, `.omp/commands/speckit.*` — agent integrations
- `.specify/scripts/bash/`, `.specify/scripts/powershell/` — Mac / Windows script sets
- `docs/agent/spec-kit-workflow.md` — the operating procedure for one-issue/one-worktree delivery
- `.github/ISSUE_TEMPLATE/spec-issue.md`, `.github/pull_request_template.md` — issue/PR templates
- `mise.toml` — pinned `uv` and the `specify-install` / `specify-init` tasks
- `specs/010-speckit-existing-project/` — this worked example

## Exclusions (explicitly out of scope)

- `constitution/`, `organization/`, `skills-lock.json` — project-init governance, owned by the governance flow
- `Assets/`, `Packages/`, `ProjectSettings/` — Unity project state, untouched
- `contracts/`, `docs/architecture/stacked-pr-plan.md` — owned by Issue #3 (branch `3`, PR #8)
- Retrospective specification of the existing game (no spec of legacy features)
- A second router, ORM, or web stack — unrelated to this issue

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — A worker prepares a bounded issue (Priority: P1)

**Given** a fresh clone with `mise` installed, **When** the worker runs the
documented setup, **Then** `specify` 1.3.1 is available and `.specify/` plus the
three agent integrations exist in the working tree.

**Independent Test**: `mise exec -- uvx --from specify-cli==1.3.1 specify --version`
→ `specify 1.3.1`, and `ls .specify .claude/skills .agents/skills .omp/commands`.

### Scenario 2 — A worker writes the lean artifacts (Priority: P1)

**Given** an issue branch, **When** the worker runs
`.specify/scripts/bash/create-new-feature.sh --number <N> --short-name <slug> "<desc>"`
followed by `setup-plan.sh`, **Then** `specs/<NNN>-<slug>/spec.md` and `plan.md`
are created from the project-local lean templates, not the generic SDD ones.

**Independent Test**: `grep -c "Owned Paths" specs/<NNN>-<slug>/spec.md` → ≥1, and
`check-prerequisites.sh --json --require-tasks --include-tasks` resolves the feature directory.

### Scenario 3 — Governance is not overwritten (Priority: P1)

**Given** the project-init governance files, **When** Spec Kit is initialized or
re-initialized, **Then** `constitution/`, `organization/` and `skills-lock.json`
are byte-identical to the baseline commit.

**Independent Test**: `git diff --stat pre-speckit-init-10 -- constitution organization skills-lock.json`
→ empty output.

### Edge Cases

- Running `specify init --here --force` again on a clone that already has `.specify/`:
  the shared script/template paths are refreshed, the project-local overrides in
  `.specify/templates/overrides/` stay untouched, and no governance file changes.
- A Windows worker with no `pwsh` on the macOS side: the PowerShell script set is
  CLI-generated and committed, but is not executable on the macOS worker.

## Requirements

- **R1**: Spec Kit MUST be initialized in place on the existing repository using the upstream existing-project flow (`specify init --here --force --integration <key>`), with a reviewable baseline recorded before initialization.
- **R2**: The lean `spec` / `plan` / `tasks` templates MUST contain objective, owned paths, exclusions, dependencies/gate, verification evidence and rollback, and the task list MUST be capped at 5 tasks.
- **R3**: The workflow MUST document branch naming as issue-number only, Draft PR base `release-0-1-0` (parent branch for stacked children), one worker per issue/worktree/PR, and serialized Unity Editor / scene ownership.
- **R4**: `.specify/memory/constitution.md` MUST point at `constitution/CONSTITUTION.md` and `organization/profiles/release-driven-solo.md` instead of restating or replacing them.
- **R5**: The setup MUST be reproducible on macOS and Windows from repository-controlled configuration (pinned `uv` + pinned `specify-cli`, `sh` and `ps` script sets).

## Dependencies & Gate

- **Depends on**: #3 (branch `3`, PR #8) — provides `contracts/` and `docs/architecture/stacked-pr-plan.md`, referenced by the workflow doc. This issue does not wait for it: it links the paths and states they live on branch `3`.
- **Blocks**: any issue that wants to produce spec/plan/tasks artifacts (#12, #14, #15, #16, #18, #19, #22 and later).
- **Gate**: `constitution/`, `organization/`, `skills-lock.json` unchanged; the worked example in `specs/010-speckit-existing-project/` resolves through `check-prerequisites.sh`; the CLI version is pinned in `mise.toml`.

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | CLI version pin | `mise exec -- uvx --from specify-cli==1.3.1 specify --version` | `specify 1.3.1` |
| V2 | Governance untouched | `git diff --stat pre-speckit-init-10 -- constitution organization skills-lock.json` | empty output |
| V3 | Integrations installed | `cat .specify/integration.json` | `installed_integrations` = claude, codex, omp |
| V4 | Worked example resolves | `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` | feature dir + `tasks.md` present |
| V5 | Lean templates active | `grep -l "Owned Paths" specs/*/spec.md specs/*/plan.md` | both files listed |

## Rollback

Revert the single merge commit of this PR, or drop branch `10`. The change is
additive (`.specify/`, agent skill/command dirs, `.github/` templates, one doc,
`mise.toml` tool pin) and touches no Unity asset, no contract file, and no
governance file, so removing it cannot affect any other issue's branch.

## Success Criteria

- **SC1**: A worker on a fresh clone can run the documented commands and obtain
  `specify` 1.3.1 with the three integrations, on macOS and on Windows.
- **SC2**: Every issue after #10 can produce a lean spec/plan/tasks triple whose
  task list is ≤5 tasks, without touching governance.
