# Tasks: #96 low-resolution UI reachability (2026-11-16)

Spec: [spec.md](spec.md). Plan: [plan.md](plan.md). No physical-device acceptance gate. Prior T1–T10 observation-only task list superseded; PR #520/#521 evidence already exists.

- [ ] **T1 Shared geometry**: Document essential control reachability at 1024×768, 1280×720, 1366×768, 1600×900 and reference FHD/2K/Mac. Own `Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs` or a dedicated companion helper and `Assets/Game/Tests/V6CanvasTests/*`; test frame and viewport boundaries without changing large-screen scale.
- [ ] **T2 Title/Result/Option**: Keep key/pointer click targets and required text within visible or reachable regions; responsive layout only. Own corresponding `Assets/Game/Scripts/UI/V6/{Title,Result,Option}/**` and relevant tests. Do not change unrelated gameplay logic.
- [ ] **T3 Related scenes coordination**: Record #88 handoff for Select/Game/Pause and any shared viewport adapter. Do not mark all-screen reachability PASS until those paths have automated coverage.
- [ ] **T4 Hard failing regressions**: Extend `AutoTestV6Screens` / `V6ScreenAudit` to fail for essential outside/non-reachable controls, with correct ScrollRect/Mask handling and tests for expected visible state. Historical PR #521 results were report-only, not acceptance.
- [ ] **T5 Verify and integrate**: One batched Unity compile + relevant EditMode and independent review, then merge to development target; capture SHA, commands, failures, and limitations in PR. Avoid Win64/macOS manual game startup or multi-resolution photo collection as a prerequisite.

Done = T1/T2/T3/T4 validated and T5 completed. Do not close because `AuditScreenClip` emitted logs or because no test runner was available; **actual fix remains outstanding**.
