# ADR-0030: Web guidance skill evaluation, request-guard consolidation, and Studio navigation IA

- Status: Accepted
- Date: 2026-11-21
- Resolves: Issue #741 (modern-web-guidance evaluation + web structure audit), Issue #742 (Studio information hierarchy)
- Related: ADR-0001 section 6 (skill adoption policy), `web/AGENTS.md`

## Context

Issue #741 asks whether `GoogleChrome/modern-web-guidance` should be adopted
under the `skill-selection` policy, and whether the Void/React code of `web/`
has structural problems worth fixing. Issue #742 asks for a material
information-hierarchy redesign of the Web Studio screens.

Evaluation of `GoogleChrome/modern-web-guidance` (repo inspected 2026-11,
Apache-2.0, maintained by the Chrome team, ~23k stars):

- Its `SKILL.md` description declares the skill **mandatory for every HTML/CSS
  and client-side JS task** ("Execute FIRST … DO NOT skip"). That is exactly
  the broad, entry-grabbing trigger `skill-selection` rejects: a Skill must
  fire only for the situation it fits, and the entry point belongs to the
  root contract alone.
- Its workflow delegates to an `npx modern-web-guidance` CLI fetched at agent
  runtime. The vendoring mechanism (`evals/policy-evaluation/vendor-skills.mts`)
  pins file hashes of the Skill directory, not the CLI version, so adopting it
  would introduce an unpinned external dependency into every web task.
- `web/AGENTS.md` already fixes the stack, commands and ownership; the
  skill's generic guidance overlaps that contract without adding a distinct
  responsibility.

The code audit found two real duplications: the "latest request wins +
abort" pattern existed in three variants (`app-shell.tsx`,
`dashboard-panel.tsx`, `songs-panel.tsx` — the last without abort), and the
blank-chart literal `{ name, maxBlock: 8, BPM: 120, offset: 0, notes: [] }`
existed in three places (`pages/chart.tsx`, `pages/publish.tsx`,
`src/components/new-song/chart-step.tsx`).

For #742, the staff rail listed monitoring pages (dashboard, archive) before
the production flow, so the most frequent operator path — register → edit →
publish — was visually last.

## Decision

1. **`modern-web-guidance` is not installed as a Skill.** Its guidance remains
   a reference for web work (accessibility, forms, UX patterns); when a
   concrete web-platform question arises, its guides may be consulted via the
   upstream CLI or repository. The rejection and its reasons are recorded in
   `web/AGENTS.md` ("Web guidance skill") so the evaluation is not repeated.
   Upstream feedback (`candidates.tsv`) is a project-init change, out of scope
   here.
2. **One request guard.** `src/app/request-guard.ts` now owns
   `useLatestRequest()` (abort previous, issue id, `isCurrent()`) and
   `isAbortError()`. `app-shell.tsx`, `dashboard-panel.tsx` and
   `songs-panel.tsx`
   share it; `loadAdminSongs` gained the same `signal` option the other
   dashboard reads already had. `isAbortError` moved out of `catalog.ts`,
   which no longer exports a generic fetch helper.
3. **One blank chart.** `src/lib/chart/empty.ts` exports
   `createEmptyChart(name)`; the two pages and the new-song chart step use it.
4. **Navigation mirrors the workflow.** The staff rail is grouped as
   制作 (新曲を登録 → 譜面を編集 → 公開する) then 運営 (ダッシュボード →
   楽曲アーカイブ), with visible subgroup labels on every viewport width.
   Labels, routes and the admin-token badge are unchanged; only order and
   grouping changed. The per-screen action hierarchy is specified in
   `docs/design/web-studio-ia.md`.

## Consequences

- No new dependency, no lock change, no vendored file: `skills-lock.json` and
  `mise run skills-check` are unaffected.
- Behavior is preserved: same abort semantics (songs-panel now also aborts on
  unmount, matching the other panels), same routes, same test ids.
- The IA spec in `docs/design/web-studio-ia.md` is the contract future Studio
  UI work is reviewed against; browser-level visual verification of the new
  grouping is recorded as unperformed where it could not run.

## Re-evaluate

- If `modern-web-guidance` ships a scoped (non-mandatory) Skill variant, or
  the vendoring mechanism can pin the CLI it calls, re-run `skill-selection`.
- If a fourth panel needs the request guard, keep using `useLatestRequest`;
  do not re-inline the pattern.
