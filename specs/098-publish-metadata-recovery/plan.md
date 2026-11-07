# Plan: #98

1. `web/src/lib/publish/draft.ts`: `savePublishDraft(draft, target)` — `"editor"` returns `/chart?draft=…&songId=…&version=…`, same storage/prefix so `readPublishDraft` restores it.
2. `web/src/components/song-publisher/publish-result.tsx`: failure panel gains Retry and Back-to-editor buttons (`onRetry` / `onBackToEditor` props); success panel unchanged (already shows verified songId/chartVersion/catalogVersion).
3. `web/src/components/song-publisher/song-publisher.tsx`: wire `onRetry` (re-open the publish dialog, inputs/token retained) and `onBackToEditor` (save current chart + song/version target, navigate).
4. Vitest tests in `web/src/lib/publish/__tests__/draft.test.ts` (editor-target URL + roundtrip); existing component-level behavior covered by lib tests only (no jsdom in the locked stack).
5. Verify: `npm test`, `npx tsc --noEmit`, `vp build`; Playwright screenshots of `/publish` at 1440×900 and 1280×720 into `specs/098-publish-metadata-recovery/evidence/`.
