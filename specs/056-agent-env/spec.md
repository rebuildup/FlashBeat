# Spec: agent environment rework (#56)

Issue #56 | Branch `56` | Base `release-0-1-3` | 2026-11-06

## Objective
One authority for agent environment: vendored skills at current upstream revisions, `AGENTS.md` as the sole routing entry, no in-repo supervisor/router/constitution.

## Owned Paths
`.agents/skills/`, `.claude/skills/`, `skills-lock.json`, `AGENTS.md`, `scripts/agent-routing/` (removal), `scripts/worktree/`, `specs/056-agent-env/`. No `Assets/`, `web/`, `.github/`, `tools/`, `docs/operations/` changes.

## Requirements
R1: Vendored skills match `skills-lock.json` hashes; source repos pinned; Linear and pack-router skills not adopted (AGENTS.md mapping table).
R2: `AGENTS.md` is the only routing/contract document; project-specific rules live there, not in global config or private memory.
R3: Workers launch only via the existing `agent <harness> <provider>` router (`~/.dotfiles/config/agents.toml` is the single route authority); no second supervisor, router, or constitution.
R4: Worktree entry is native `git worktree add` with `GIT_LFS_SKIP_SMUDGE=1`; `wt` is not used for agent worktrees.
R5: Merges to trunk follow ADR-0027/ADR-0012; merge commits only.

## Dependencies & Gate
Upstream `rebuildup/project-init` and `rebuildup/writing-skills` revisions. Gate: `mise run skills-check` (vendored vs lock hash check) must pass.

## Verification Evidence
`mise run skills-check` PASS; worktree shell syntax PASS; diff check PASS; results in PR #65 comments.

## Rollback
Revert the PR #65 merge commit on trunk.

## Success Criteria
Fresh clone + `mise install --locked` + `mise run skills-check` passes; AGENTS.md contains no reference to removed in-repo routing; no duplicate authority documents.
