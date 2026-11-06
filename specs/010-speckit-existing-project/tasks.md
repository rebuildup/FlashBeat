---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: Spec Kit existing-project setup and one-issue one-worktree template

**Issue**: #10 | **Branch**: `10` | **Base**: `release-0-1-0`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [ ] **T1** [P] Install Spec Kit 1.3.1 in place with the upstream existing-project flow and record the pre-init baseline tag `pre-speckit-init-10` (`.specify/`, `.claude/skills/speckit-*`, `.agents/skills/speckit-*`, `.omp/commands/speckit.*`, `.specify/scripts/bash/`, `.specify/scripts/powershell/`) — done when `specify init --here --force --non-interactive --integration claude --script sh` plus `specify integration install codex|omp` succeed and `git diff` shows no change to tracked pre-existing files.
- [ ] **T2** Add the lean project-local template overrides and the constitution pointer (`.specify/templates/overrides/spec-template.md`, `.specify/templates/overrides/plan-template.md`, `.specify/templates/overrides/tasks-template.md`, `.specify/memory/constitution.md`) — done when each template carries objective, owned paths, exclusions, dependencies/gate, verification evidence and rollback, the task template caps at 5 tasks, and the constitution file only links `constitution/` and `organization/`.
- [ ] **T3** [P] Write the delivery procedure and the issue/PR templates (`.github/ISSUE_TEMPLATE/spec-issue.md`, `.github/pull_request_template.md`, `docs/agent/spec-kit-workflow.md`) — done when the doc states branch = issue number only, Draft PR base `release-0-1-0` (parent branch for stacked children), one worker per issue/worktree/PR, serialized Unity Editor/scene ownership, and the Mac/Windows setup commands.
- [ ] **T4** [P] Pin the toolchain for reproducible setup (`mise.toml`) — done when `mise install` provides the pinned `uv` and `mise run specify-install` / `specify-init` materialize `specify` 1.3.1 on macOS and Windows.
- [ ] **T5** Run the minimal example through the workflow and publish (`.specify/scripts/bash/create-new-feature.sh`, `setup-plan.sh`, `setup-tasks.sh`, `specs/010-speckit-existing-project/`) — done when `check-prerequisites.sh --json --require-tasks --include-tasks` resolves the feature directory and the Draft PR exists with base `release-0-1-0`.

## Dependencies & Execution Order

1. T1 → T2 → T5 (T2 needs the scaffolding that T1 creates; T5 needs both)
2. T3 and T4 run in parallel with T1/T2 — they touch different paths (`.github/`, `docs/`, `mise.toml` vs `.specify/`)
3. T5 last: it publishes the branch and the Draft PR

**Blocking dependency**: #3 (branch `3`, PR #8) for `contracts/` and `docs/architecture/stacked-pr-plan.md`. This issue links those paths instead of vendoring them, so it is not blocked by #3.

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | CLI version + integrations | `mise exec -- uvx --from specify-cli==1.3.1 specify --version` ; `cat .specify/integration.json` | `specify 1.3.1` ; claude, codex, omp listed |
| T2 | Governance untouched + overrides active | `git diff --stat pre-speckit-init-10 -- constitution organization skills-lock.json` ; `grep -l "Owned Paths" specs/010-speckit-existing-project/spec.md` | empty diff ; spec.md listed |
| T3 | Procedure completeness | `grep -E "release-0-1-0|issue number only" docs/agent/spec-kit-workflow.md` | both matched |
| T4 | Reproducible install | `mise install && mise run specify-install` | pinned tools installed, `specify` 1.3.1 available |
| T5 | Worked example resolves | `.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` | feature dir with spec.md, plan.md, tasks.md |

## Rollback

Revert the merge commit of the Draft PR, or delete branch `10`. All touched paths
are additive (`.specify/`, `.claude/`, `.agents/`, `.omp/`, `.github/`,
`docs/agent/`, `mise.toml`, `specs/010-*/`), no Unity asset and no governance
file is modified, so the rollback is independent of every other issue branch.

## Notes

- Scope discipline: implement only the tasks above; new ideas become follow-up Issues.
- The Unity Editor / scene / prefab mutation is serialized: only the issue that
  owns the scene may run the Editor. This issue owns none, so it runs in parallel
  with #9, #11 and #12.
