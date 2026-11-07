# Plan: exhibition web line

1. Merge `origin/135` into `expo-web`; resolve conflicts keeping both sides' behaviour
   (keyconfig lookup from 109-patch + key-guide HUD from 135; issue-#125 update-result functions kept).
2. Merge `origin/133`; keep `Math.fround` score model intact.
3. Verify `contracts/openapi.v1.yaml` documents scores + leaderboard endpoints (133 already does).
4. Run `cd web && npm test` and `cd web && npx vp build`; fix merge-caused failures only.
5. Push `expo-web`, open Draft PR against `109-patch`.
