# FlashBeat Spec Kit Constitution (pointer, not a second authority)

> **This file does not define project governance.** It points Spec Kit at the
> governance that already exists in this repository.
>
> - Constitutional authority: [`constitution/CONSTITUTION.md`](../../constitution/CONSTITUTION.md)
> - Current operating model: [`organization/profiles/release-driven-solo.md`](../../organization/profiles/release-driven-solo.md)
> - Delivery plan for the current release: `docs/architecture/stacked-pr-plan.md` (branch `3`)
>
> Those files are owned by the project-init governance flow. Spec Kit commands
> MUST NOT rewrite them. If a Spec Kit workflow step suggests changing them,
> stop and report to the governance owner instead.

## Core Principles (refinement of the authority above)

### I. One Issue = One Branch = One Worktree = One Draft PR

Every implementation unit is a GitHub Issue with a numeric branch name equal to
the issue number, developed in its own worktree, published as a Draft PR.
Branch naming is the issue number only (`10`, not `10-feature-name`).
Draft PR base is the parent branch named by the delivery plan
(`release-0-1-0` for independent issues, the parent issue branch for stacked
children).

### II. Lean Artifacts

Each issue produces exactly three artifacts: `spec.md`, `plan.md`, `tasks.md`
under `specs/<NNN>-<slug>/`, using the project-local template overrides in
`.specify/templates/overrides/`. A spec states objective, owned paths,
exclusions, dependencies/gate, verification evidence, and rollback. A plan
states the approach and the constitution check. A task list has **at most 5
tasks**. The legacy game is never retrospectively re-specified; only the
bounded change is specified.

### III. Exclusive Mutable Ownership

Owned paths in the spec are exclusive for the lifetime of the branch. The Unity
Editor, scene files, and prefabs are mutated by exactly one owner at a time
(serialized gate), and every other worker uses a contract fixture or mock until
the owner lands.

### IV. Evidence Bound to the SHA

A verification result is evidence only when it is bound to the candidate commit
SHA and states what was actually executed. "Ready" or "green" without the
executed command and its actual result is not evidence.

### V. Independent Rollback

Every issue must be undoable on its own (revert its merge commit or drop its
branch) without touching other issues' work. This is what makes parallel
workers safe.

## Governance

Spec Kit artifacts refine the Constitution; they never override it. Constitution
amendments require the project-init governance flow, not a Spec Kit command.

**Version**: 1.0.0 | **Ratified**: 2026-11-06 | **Last Amended**: 2026-11-06
