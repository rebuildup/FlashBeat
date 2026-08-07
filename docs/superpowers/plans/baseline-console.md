# Baseline Unity Console State

Captured: 2026-08-06
Task: Task 1 (FlashBeat refactor plan) — baseline capture
Source: `mcp__UnityMCP__read_console(action="get", types=["error","warning"], count=30, format="detailed")`

## Summary

- **Total entries retrieved:** 14
- **Errors:** 0
- **Warnings:** 14

## Warnings (14)

All warnings are pre-existing, non-blocking, and unrelated to C# compilation:

| # | Source | Message |
|---|---|---|
| 1 | MCP-FOR-UNITY (`.Library/PackageCache/com.coplaydev.unity-mcp@.../Editor/Helpers/McpLog.cs:45`) | `[WebSocket] Unexpected receive error: WebSocket is not initialised` |
| 2-12 | TMP (`./Library/PackageCache/com.unity.ugui@67707a67a4ab/Runtime/TMP/TextMeshProUGUI.cs:2012`) | Japanese characters (U+305D, U+308C, U+306A, U+308A, U+306B, U+6587, U+5B57, U+6570, U+306E, U+591A, U+3044, U+5217) not found in `[LiberationSans SDF]` font asset — replaced with U+25A1 in text object `[問題]` |
| 13 | Unity AI Assistant (`./Library/PackageCache/com.unity.ai.assistant@.../Modules/Unity.AI.Toolkit.Accounts/Services/States/ApiAccessibleState.cs:46`) | `Account API did not become accessible within 30 seconds. This may be due to network issues or editor focus.` |

## Interpretation

- No compile errors. Project compiles cleanly.
- All warnings are benign and pre-existing:
  - MCP WebSocket warning is a transport-layer blip from the MCP package itself.
  - TMP font warnings reflect missing Japanese glyphs in `LiberationSans SDF` (the project ships `NotoSansJP-Medium SDF.asset` and `YuGothB SDF.asset` for Japanese rendering — these are configured per-scene via TextMeshPro components; the warnings appear only in contexts that still reference the Latin font).
  - AI Assistant account warning is unrelated to gameplay code.

## Baseline established

Future refactor tasks should not regress compile state. Any new error entries added after this date indicate a regression introduced during refactor.