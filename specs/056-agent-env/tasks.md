# Tasks: #56

Issue #56 | Branch `56` | Base `release-0-1-3`

- [x] T1 Re-vendor 27 skills at pinned revisions into `.agents/skills/` + `.claude/skills/`; update `skills-lock.json`.
- [x] T2 Rewrite `AGENTS.md` as sole routing entry (native worktree + `GIT_LFS_SKIP_SMUDGE=1`, dotfiles `agent` router, no second supervisor).
- [x] T3 Remove in-repo dispatch/runtime/route config (`scripts/agent-routing/`); keep `.specify/memory/constitution.md` as pointer only.
- [x] T4 Run `mise run skills-check` and diff checks; post evidence to PR #65.
- [x] T5 Merge `origin/release-0-1-3` into `56` (42bdb01, conflict 0); re-verify operating-model review still holds.

Verification: `mise run skills-check` PASS; merge conflict 0; evidence in PR #65 comments.
