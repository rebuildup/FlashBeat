# Plan: Publish states

**Issue**: #149 | **Branch**: `149` | **Base**: `expo-web`
**Spec**: [spec.md](spec.md)

## Summary
Keep SongPublisher unchanged. Render loading/errors through a centered bounded card with real BrandHeader, Pen semantic surfaces, one primary retry and an explicit return to Catalog. Do not modify API or browser storage.

## Owned Paths
`web/pages/publish.tsx`; `specs/149-publish-state/**`.

## Dependencies & Gate
PR #144 needs to remain the parent. Web checks, keyboard E2E and actual before/after screenshots before approval. Sole Cloudflare deployment owner only.

## Exclusions
Do not introduce a new framework or global layout primitive.

## Constitution Check
Project-init governance and exclusive file ownership maintained; no external side effect or data migration.

## Verification Plan
Run Web type-check, build, tests, then browser screenshot+interaction verification (including invalid chart version and draft parse failure) at SHA.

## Rollback
Revert ticket PR.
