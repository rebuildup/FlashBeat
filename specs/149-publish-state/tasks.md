# Tasks: Publish states

**Issue**: #149 | **Branch**: `149` | **Base**: `expo-web`

- [x] **T1** Create branded loading/error state hierarchy in `web/pages/publish.tsx` with Pen v5 surfaces and official BrandHeader.
- [x] **T2** Add accessible Retry/Catalog controls with focus and readable error text in `web/pages/publish.tsx`.
- [x] **T3** Preserve existing SongPublisher arguments, state, and no-mock API behavior in `web/pages/publish.tsx`.
- [x] **T4** Run Web checks (`npx tsc --noEmit`, `npx vp build`, `npm test`) and smoke errors.
- [x] **T5** Capture actual browser pixels at 1440×900, 1280×720 and 390×844, review against design-skills/unslop with SHA evidence.

## Dependencies
PR #144, issue #149. No overlapping ownership.

## Rollback
Revert #150 ticket PR.

## Evidence
See `evidence.md` (tsc/vp build/vitest 220/220 + browser screenshots at 1440x900 / 1280x720 / 390x844).
