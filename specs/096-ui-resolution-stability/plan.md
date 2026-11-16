# Plan: #96 low-resolution responsive reachability (2026-11-16)

Spec: [spec.md](spec.md). Source observation: PR #521, `docs/evidence/96-screen-clip-2026-11-16/`. Previous observation-only plan is superseded; earlier evidence remains in Git history.

## Architecture

- Viewport/profile selection is still centralized in `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs` and shared layout layer. On large viewports preserve its fixed design frame.
- On small viewports use a bounded responsive view strategy (reposition top/bottom action regions, allow reachable scrolling/compact arrangement). Do not treat negative `Origin` as a usable location for interactive widgets.
- Extend the existing `AutoTestV6Screens` screen clip audit with an explicit essential-control inventory, visibility/reachability and a fail-gate in tests; retain informational logs for nonessential decoration. Tests must model viewport bounds and ScrollRect/Mask ancestors.
- Keep UI ownership boundaries: #96 Title/Result/Option/Opening; #523 Select/Game/Pause. Shared viewport contract may be implemented separately before individual screens, with conflict-free ownership of common paths.
- Make the response strategy deterministic and testable without Windows hardware. Use an available Unity batch EditMode compile at the **batch boundary** (not each screen), plus independent code review.

## Implementation sequence

1. Model contract and small/large viewport geometry in shared code and tests. Expected failing low-res cases are regression fixtures (1024×768, 1280×720, 1366×768, 1600×900).
2. Adapt Title/Result/Option interaction regions; preserve v6 FHD/2K/Mac design. Opening is separate and modified only if a deterministic failing test proves a defect.
3. Coordinate #523 for Select/Game/Pause action reachability under the same contract. Do not change their Owned Paths from this Issue without coordination.
4. Promote essential-control reachability assertions from report-only to failing automated tests. Check keyboard/pointer bindings and all screen states against the functional inventory.
5. Batch Unity compile + related EditMode + independent review. Merge, record source SHA, verified scope, and any unobserved hardware behavior. No device waiting, no owner screenshot gate.

## Verification

`verify.yml` checks repo integrity only. Existing EditMode tests (`V6ScreenAudit`, `V6UiProfileTests`, transition tests) provide geometry/functional evidence once augmented. A screenshot is an optional diagnostic, not proof of clickability; actual Windows panel is not required for development acceptance. Release candidate full EditMode and builds follow `docs/operations/development-convergence.md`.

## Rollback

共通 profile と #96 固有 UI は別 PR/commit に分離して revert 可能にする。#96 の変更を戻す前に #523 の shared helper 呼出箇所を列挙し、旧 API 互換がない場合は #523 owner と共通契約を調整する。revert 後に Title/Result/Option と Select/Game/Pause の到達性 regression を回し、再発する既知低解像度 defect は未完了として残す。手動の Windows ゲートへ差し戻さない。
