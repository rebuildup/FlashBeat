# Spec: Branded Publish loading and error recovery

**Issue**: #149 | **Branch**: `docs/149-publish-state-evidence` | **PR base**: `main` | **Status**: Verified (evidence in `evidence.md`)

## Objective
Replace unstyled Publish loading/error route output with a task-oriented FlashBeat panel that keeps branding, readable status, accessible retry and Catalog escape.

## Owned Paths
- `web/pages/publish.tsx`
- `specs/149-publish-state/**`

## Exclusions
Do not change `SongPublisher`, publish API, Cloudflare, data contract, Unity, route shape, or other Web pages.

## User Scenarios & Testing
### Scenario 1 (P1)
Given a broken published chart or network error, when opening Publish, then the user sees a branded error, a retry and Catalog link that are keyboard-operable.
Independent test: browser direct-load `/publish?songId=missing&version=999` and inspect error, Tab/Enter.

### Scenario 2 (P1)
Given an existing chart, when Publish loads its session, then a readable loading status is shown without a blank raw document or missing branding.
Independent test: throttle network and take screenshot.

### Edge Cases
Transient failure after route navigation, old draft storage error, narrow browser width; existing `SongPublisher` may not lose draft data.

## Requirements
- R1: Use the existing FlashBeat real app icon and Pen v5 Panda/Park theme.
- R2: Keep current retry/session/publish semantics, never fake a success.
- R3: Test responsive viewport and 200% zoom, no traps or unreachable buttons.

## Dependencies & Gate
Depends on PR #144 (Web exhibition branch `expo-web`). Blocks visual gate #75 only for this subpath. Final gate: Web TS/build, Playwright screenshots, actual keyboard navigation and release deploy approval.

## Verification Evidence
`cd web && npx tsc --noEmit && npx vp build && npm test`, screenshots at 1440x900/1280x720/390x844 (current candidate SHA). Evidence recorded in [`evidence.md`](evidence.md): `npx tsc --noEmit` exit 0, `npx vp build` exit 0, `npx vitest run` 28 files / 220 tests passed; browser screenshots at 1440x900 / 1280x720 / 390x844 (loading + error) with SHA-256 evidence, captured against the local dev server on candidate `fbe5810`.

## Rollback
Revert this one PR, no persisted data affected.
