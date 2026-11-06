# Tasks: #98

- [ ] 1. `draft.ts`: editor-target draft handoff (`savePublishDraft(draft, "editor")` → `/chart?draft=…`) + vitest roundtrip tests
- [ ] 2. `publish-result.tsx`: failure panel Retry + Back-to-editor actions
- [ ] 3. `song-publisher.tsx`: wire Retry (re-open dialog) and Back-to-editor (save draft, navigate)
- [ ] 4. Verify: `npm test`, `npx tsc --noEmit`, `vp build`
- [ ] 5. Playwright screenshots 1440×900 + 1280×720, layout fixes, evidence under `specs/098-publish-metadata-recovery/evidence/`
