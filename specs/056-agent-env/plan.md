# Plan: agent environment rework (#56)

Issue #56 | Branch `56` | Base `release-0-1-3`

## Approach
1. Re-vendor skills with `bunx skills add <source> --skill <name> --agent claude-code --agent codex --copy -y` from pinned upstream revisions; regenerate `skills-lock.json` (27 skills: project-init 19, writing-skills 2, mattpocock 3, design-skills 1, cloudflare 2).
2. Rewrite `AGENTS.md` as the single dispatcher: project facts, canonical state, bootstrap/validation, Issue→branch→Draft PR protocol, delegation via dotfiles `agent` CLI, native worktree rules, recovery.
3. Remove in-repo dispatch/runtime/route configuration (`scripts/agent-routing/`); `.specify/memory/constitution.md` stays as a pointer to `constitution/CONSTITUTION.md`.
4. Merge `origin/release-0-1-3` into `56` on each trunk advance; keep trunk content for non-agent-environment files.

## Risks
- Upstream skill drift → pinned by lock hashes, checked by `mise run skills-check`.
- Duplicate authority (second constitution/router) → review against real constitution, `organization/profiles/release-driven-solo.md`, `~/.dotfiles/config/agents.toml`, and `rebuildup/project-init` HEAD before landing.
