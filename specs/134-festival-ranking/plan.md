# Plan

1. `Assets/Game/Scripts/UI/RankingClient.cs` — static client, pure helpers + UnityWebRequest coroutines.
2. `Assets/Game/Scripts/UI/RankingPanel.cs` — code-built UI (canvas, top-10 panel, submit modal, hint).
3. `Assets/Game/Scripts/UI/ResultRankingOverlay.cs` — bootstrap, scene gating, input state machine,
   scene suspension (KeyConfigUI pattern), submit flow.
4. `GManager.maxCombo` + `Judge` maintenance + `ResultSceneManager.Retry` reset.
5. `Assets/Tests/Editor/RankingClientTests.cs` — NUnit for the pure parts.
6. Verify: stub-compile the new code against a hand-written UnityEngine/TMPro/NUnit surface
   (dotnet csc) to catch syntax/typing errors; manual re-read. Unity run is the Supervisor's.
7. Port to `134-patch` (from `origin/109-patch`), same files + same counter edits, push, comment SHA on #134.
