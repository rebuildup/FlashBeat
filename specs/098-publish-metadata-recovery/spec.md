# Spec: #98

When a catalog song is selected (`/publish?songId=…&version=…`), the Publish form initializes title/artist/level/durationSec/youtubeId from the published song and stays editable. Validation messages are specific and shown per field. A failed publish keeps every input and the chart draft, shows an actionable error and offers Retry (re-open the publish dialog) and Back-to-editor (chart draft saved to `sessionStorage`, navigate to `/chart?draft=…`). Success shows the verified `songId`/`chartVersion`/`catalogVersion` from the read-back. The staff token stays runtime-only (component state; never persisted, never in a URL). No live writes, no deploys.

## Decisions

- Metadata initialization from `initialSong` landed with #73; #98 verifies it and completes the recovery flow (Retry + Back-to-editor), which #73 did not ship.
- Back-to-editor reuses `savePublishDraft` storage with a `target` argument returning a `/chart?draft=…` URL, so `chart.tsx` can restore with the existing `readPublishDraft`. `pages/chart.tsx` is outside this task's ownership — the restore wiring there is reported, not implemented.
