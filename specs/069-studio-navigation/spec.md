# Spec: Studio route navigation

Issue #69 | Branch `69` | Base `main` | 2026-11-07

## Objective
Catalog navigation opens actual editor and publish routes rather than unfinished tab panels. Search belongs to Catalog; each operation has one visible action.

## Owned paths
`web/src/app/{app-shell,sidebar,topbar,editor-panel,publish-panel}.tsx`, `web/pages/index.server.ts`. No chart loader, publisher, API, token, brand asset or deployment changes.

## Scenarios and requirements
- From Catalog activate New chart or Publish: URL becomes `/chart` or `/publish`, opening the implemented page. Back returns to Catalog; direct navigation and refresh keep destination.
- Catalog has search, level filter and one Refresh. Failed reload remains visible as an API error, never a success toast.
- Navigation uses labelled nav links and `aria-current=page`, keyboard focus remains visible. No fake tabs, roadmap copy or nested anchor/button.

## Dependencies and exclusions
#67 brand, #68 API; #71 owns exact song/version and new-draft initialization. #73 owns publish transport safety. These remain separate acceptance gates. #72 owns editor layout and return link. Visual approval requires actual pixel captures.
