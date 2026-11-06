## Tasks

- [x] 1. `draft.ts`: editor-target draft handoff (`savePublishDraft(draft, "editor")` → `/chart?draft=…`) + vitest roundtrip tests
- [x] 2. `publish-result.tsx`: failure panel Retry + Back-to-editor actions
- [x] 3. `song-publisher.tsx`: wire Retry (re-open dialog) and Back-to-editor (save draft, navigate)
- [x] 4. Verify: `npm test`, `npx tsc --noEmit`, `vp build`
- [x] 5. Playwright screenshots 1440×900 + 1280×720, layout fixes, evidence under `specs/098-publish-metadata-recovery/evidence/`

## Verification notes (browser smoke, local dev D1)

- Field-level validation: clearing Title shows "Enter the title." under the field.
- Failed publish (wrong token → 401): dialog closes, failure panel shows the message plus the API detail, inputs retained, Retry and Back-to-editor reachable and clickable.
- Retry re-opens the publish dialog with the token retained.
- Real publish against the local D1: success panel shows `who is now chart version 2. The catalog is at version 2.` (values from the `publishAndVerify` read-back).
- Layout fixes found by the smoke: the modal no longer stays open on failure (it covered the failure panel actions), and the Publish card + result panel moved inside the scrollable sidebar column (`sideScroll`) so the panel is never clipped by `overflow: hidden`.
