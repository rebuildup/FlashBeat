# Tasks
- [x] T1 web/src/lib/publish/transport.ts: explicit test-only fixture opt-in.
- [x] T2 web/pages/publish.* and song-publisher.tsx: selected metadata/version and empty new draft.
- [x] T3 web/src/lib/publish/client.ts: verify persisted identity/chart after write; preserve errors.
- [x] T4 web/src/lib/publish/__tests__/: transport/readback regression and typecheck.
- [ ] T5 production E2E and actual browser pixels (supervisor gate).

Dependencies: #71 loader before T2. T1/T2 precede T4. No live write authorized for this worker; production and visual acceptance remain unverified. Revert this ticket commit independently.

Evidence: publish Vitest 6 files / 34 tests PASS; TypeScript noEmit PASS after Void/Panda codegen; build pending. Chart page handoff additionally owned by explicit supervisor assignment. Design methods: design-skills interaction-state (pending/reentry/errors), unslop and signs (quiet chrome and action clarity), inspected Pen source. Actual browser pixels remain unverified.
