# Plan: Studio route navigation

Retain Void/React/Panda/Park UI and current catalog client. Replace Ark tab semantics with real anchors because destinations have independent URLs. Remove unused placeholder components. Keep catalog operations in CatalogPanel and Topbar search/filter only.

Design audit: fake panels and developer roadmap obscure real work, duplicate Refresh/Publish compete with navigation, refresh success was announced before response. Apply rebuildup/design-skills navigation-design (links for route changes, current location, focus), and Damir Sokolovsky/heydamir unslop (CC BY 4.0): quiet navigation and task-specific copy. Pen v5 variables inspected: accent #3FA9F5, accent-tint #2B4457, accent-light #9AD3FA. Use these for current/focus state without a separate palette redesign.

Direction: FlashBeat chart authors; precise and rhythmic; eight-lane music workspace; chart carries detail; route links keep chrome quiet. No additional approval wait: user explicitly requested implementation.

Verification: npm test, TypeScript and production build. Browser captures and keyboard interactions when available; record limits accurately. No production deployment.
