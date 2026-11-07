# Tasks
- [x] T1 web/src/lib/new-song/: slug suggestion + catalog uniqueness, oEmbed fill, sessionStorage draft, bilingual validation + note-count rule, iframe-api loader, fixture studio API (publish + read-back).
- [x] T2 web/src/components/new-song/: stepper, song-info step, chart step (editor reuse), publish step, success view, flow root; `/new-song` page.
- [x] T3 web/src/app/sidebar.tsx: New song entry; aria-current from the URL.
- [x] T4 web/src/lib/new-song/__tests__/: slug, oembed, draft, validation, fixture API (vitest); tsc + vp build.
- [x] T5 Real-browser screenshots 1440×900 and 1280×720 of every step (incl. failure recovery and sidebar entry), reviewed and fixed (fixture read-back, empty-slug guard).

Dependencies: none outside web/; chart-editor and publish lib are consumed, not modified. No live write authorized; production behavior of the flow itself unverified (dev-server + fixture only).
