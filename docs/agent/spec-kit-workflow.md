# FlashBeat — Spec Kit workflow (one agent, one issue, one worktree, one Draft PR)

Status: current default practice for Issue #10 and later issues.
Authority: [`constitution/CONSTITUTION.md`](../../constitution/CONSTITUTION.md),
[`organization/profiles/release-driven-solo.md`](../../organization/profiles/release-driven-solo.md).
Release plan: `docs/architecture/stacked-pr-plan.md` (lives on branch `3`, PR #8).

This document describes **practice**. It refines the Constitution and never
replaces it.

## 1. What was installed

Spec Kit (github/spec-kit) CLI `specify` 1.3.1 was initialized **in place** on an
existing repository, following the upstream
[existing-project flow](https://github.github.com/spec-kit/how-to/existing-projects.html).

| Path | Content |
|---|---|
| `.specify/` | shared scaffolding: templates, bash + powershell scripts, workflows, integration manifests |
| `.specify/templates/overrides/` | project-local lean `spec` / `plan` / `tasks` templates (override the core templates) |
| `.specify/memory/constitution.md` | **pointer** to the project-init Constitution, not a second constitution |
| `.claude/skills/speckit-*` | Claude Code integration |
| `.agents/skills/speckit-*` | Codex CLI integration |
| `.omp/commands/speckit.*` | Oh My Pi integration (the harness this project is driven with) |

Governance files (`constitution/`, `organization/`, `skills-lock.json`) were
**not** touched. `AGENTS.md` / `CLAUDE.md` were not overwritten — `specify init`
only wrote into `.specify/`, `.claude/`, `.agents/`, `.omp/`.

Baseline before initialization was committed as tag `pre-speckit-init-10`
(SHA `25fd896b0f096bbafe848cfc7b6fb6d6730ddd16`), so the generated diff is
reviewable as a normal code review.

## 2. Reproducible setup (Mac and Windows)

The CLI is pinned and installed through the repository-controlled mise config,
so both a Mac worker and a Windows worker materialize the same version.

```sh
mise install            # installs pinned tools (bun, uv) from mise.lock
mise run specify-install # installs specify-cli==1.3.1 as a uv tool (user-local, version-pinned)
```

Then initialize (only needed once per clone; the generated files are committed):

```sh
# macOS / Linux
mise exec -- uvx --from specify-cli==1.3.1 specify init --here --force --non-interactive --integration claude --script sh
mise exec -- uvx --from specify-cli==1.3.1 specify integration install codex --script sh
mise exec -- uvx --from specify-cli==1.3.1 specify integration install omp --script sh

# Windows (PowerShell)
mise exec -- uvx --from specify-cli==1.3.1 specify init --here --force --non-interactive --integration claude --script ps
mise exec -- uvx --from specify-cli==1.3.1 specify integration install codex --script ps
mise exec -- uvx --from specify-cli==1.3.1 specify integration install omp --script ps
```

`uvx --from specify-cli==1.3.1` pins the CLI version without depending on a
Python environment layout, so the same command line works on macOS and
Windows. `mise run specify-init` runs the macOS variant.

Both script sets are committed (`.specify/scripts/bash/` and
`.specify/scripts/powershell/`), so a Windows worker does not need to re-init
before using the workflow. Re-running `init --force` refreshes the shared
script/template paths; it does **not** touch `.specify/templates/overrides/`,
`constitution/`, `organization/`, or `skills-lock.json`.

Verified re-init behavior (scratch worktree, `specify init --here --force` then
`specify integration install codex|omp`):

- `.specify/templates/overrides/` and `.specify/memory/constitution.md` are unchanged.
- `init --force` **resets `.specify/integration.json` to the single
  `--integration` key**, so the `integration install` steps must follow it —
  `mise run specify-init` does exactly that and reproduces the committed
  `integration.json` byte-for-byte.
- Only `.specify/integrations/*.manifest.json` differ afterwards, by their
  `installed_at` timestamp.

Supported integrations used here: `claude` (Claude Code), `codex` (Codex CLI),
`omp` (Oh My Pi). All three are declared multi-install safe by upstream.

## 3. One issue = one branch = one worktree = one Draft PR

```sh
# 1. worktree for the issue (branch name = issue number only)
wt switch --create 14 --base origin/12            # stacked child
wt switch --create 10 --base origin/<release>     # independent issue

# 2. publish after the first meaningful commit
git push -u origin 10
gh pr create --draft --base <release> --head 10 \
  --title "[Issue #10] <title>" --body "closes #10"

# 3. report
# 1) branch SHA  2) PR URL/base/head  3) executed tests & actual results
# 4) remaining blockers  5) next unblocked Issue
```

Rules that belong to this workflow:

- **Branch name = issue number only** (`10`, `14`, `22`). No slug suffix, no
  `feature/` prefix. The spec directory may carry a slug, the Git branch does not.
- **Draft PR base**: the active release integration branch for independent
  issues (the head of the current Draft release PR; verify with `gh pr list`,
  do not assume `release-0-1-0`); the parent issue branch for stacked children
  (`14` → `12`, `12` → `3`). Never `main`.
- **One worker / one issue / one worktree.** A worker never edits another
  issue's branch and never takes a second issue.
- **Serialized Unity ownership.** `Assets/**`, scenes and prefabs are mutated by
  exactly one owner at a time. The owner is the issue whose spec lists those
  paths as Owned Paths. Other workers use contract fixtures or mocks
  (`contracts/openapi.v1.yaml`, `contracts/schemas/chart.v1.schema.json`) until
  the owner lands. The Unity Editor is a single process: two concurrent Editor
  instances over the same `Library/` corrupt each other.
- **First meaningful commit → push + Draft PR.** Do not wait for completion.
- **Merge is not the worker's job.** Merge commit semantics only, per the
  operating model.

## 4. Lean spec / plan / tasks

Every issue gets three artifacts in `specs/<NNN>-<slug>/`:

```sh
.specify/scripts/bash/create-new-feature.sh --number 10 --short-name speckit-existing-project "..."
.specify/scripts/bash/setup-plan.sh   # creates plan.md from the override template
.specify/scripts/bash/setup-tasks.sh  # creates tasks.md from the override template
```

Windows uses the same steps with `.specify/scripts/powershell/*.ps1`.

The templates in `.specify/templates/overrides/` enforce the required content:

| Artifact | Required content |
|---|---|
| `spec.md` | objective, owned paths, exclusions, ≤3 acceptance scenarios, requirements, dependencies/gate, verification evidence, rollback |
| `plan.md` | summary, owned paths, dependencies/gate, exclusions, constitution check, verification plan, rollback |
| `tasks.md` | **≤5 tasks**, each with an exact path and a done-condition, dependency order, verification evidence, rollback |

Rules:

- **No retrospective specification.** The legacy Unity game is not re-specified.
  A spec describes only the bounded change of the current issue.
- **≤5 tasks per issue.** If it does not fit, split the issue.
- **No new governance.** Spec Kit artifacts never restate or override
  `constitution/` or `organization/`; `.specify/memory/constitution.md` points at them.
- **Specs are per issue, not per repo.** `specs/` accumulates one directory per
  issue and is the durable record of intent.

## 5. Verification

The workflow is self-checking through the same scripts the agents use:

```sh
.specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks
```

A worked example (Issue #10 itself) lives in `specs/010-speckit-existing-project/`.

## 6. Known limits

- The upstream `create-new-feature.sh` names spec directories `NNN-slug` (three
  digits) and suggests a `NNN-slug` branch name. The Git branch is created by
  the worktree flow (`wt switch --create <issue-number>`), not by that script,
  so the issue-number-only branch rule holds. The spec directory prefix is the
  zero-padded issue number.
- The PowerShell script set is CLI-generated (same CLI version) but was **not
  executed** in this environment — no `pwsh` on the macOS worker. It is
  regenerated from the same upstream assets, not hand-edited.
- `specify init --force` refreshes shared scripts/templates. Review the diff
  before committing; the project-local overrides are not part of that refresh.
