---
description: "起動時・ゲーム開始前の重い読み込みを非同期化し、ローディング表示を出す"
---

# Spec: 非同期読み込みとローディング表示

**Issue**: #113 | **Branch**: `113` | **PR base**: `release-0-1-3` | **Date**: 2026-11-07 | **Status**: Draft

**Input**: "[Game/UX] 起動時・ゲーム開始前の重い読み込みを非同期化し、ローディング表示を出す" (Refs #41)

## Objective

シーン遷移とゲーム開始前の重い同期処理 (シーンロード、譜面解析・ノーツ生成、
音源の Resources.Load、メディア準備) を非同期化し、進捗が見えるローディング
表示を出す。固まって見える状態をなくす。

## Owned Paths

- `Assets/ThirdParty/Simple Scene Fade Load System/Scripts/Fader.cs` / `Initiate.cs`
- `Assets/Game/Scripts/UI/LoadingOverlay.cs` / `LoadingOverlayLogic.cs`
- `Assets/Game/Scripts/Gameplay/NotesManager.cs` / `TimeBudgetBatcher.cs`
- `Assets/Game/Scripts/Gameplay/MusicManager.cs` (読み込み関連部分のみ)
- `Assets/Game/Scripts/UI/OpeningSceneManager.cs`
- `Assets/Tests/Editor/` の追加テスト、`specs/113-async-loading/`

対象外: シーンファイル、Select/Title/Result/Option 各マネージャ、KeyGuide/KeyConfig/Judge。

## Requirements

1. **シーン遷移**: `Fader` は `SceneManager.LoadSceneAsync` + `allowSceneActivation=false`
   で読み、暗転完了後にアクティベートする。`Initiate.Fade` の公開シグネチャと
   全呼び出し site (~12 箇所) の挙動は不変。二重起動を防御する。
2. **LoadingOverlay**: コード生成 uGUI (DontDestroyOnLoad シングルトン、最前面
   sortingOrder、ScaleWithScreenSize 1920x1080 / match 0.5)。250ms 超の読み込みで
   表示、スピナー (unscaled 時間駆動)、進捗バー (`AsyncOperation.progress` 0..0.9
   を 0..100% に写像)、日本語文言 + 画面名、表示後最短 400ms、フェードアウト。
   1024x768..2560x1440 で欠けない。API: `Show(label)` / `SetProgress(float)` / `Hide()`。
3. **譜面読み込み**: `NotesManager` は JSON 解析 → ノーツ生成を 1 フレーム
   8ms 以下のバッチ (実測適応) に分割し、進捗を Overlay に出す。`MusicManager`
   はメディア待ちに加えて「ノーツ準備完了」も待ってから Space 開始を許可する。
   音源は `Resources.LoadAsync`。
4. **メディア待ち**: #93 の stage/failure 情報を Overlay の段階文言
   (動画を取得中… / 準備中…) に使う。
5. **Opening**: 最初のフレームを止める要因 (オープニング動画の初回準備) を
   最初の描画後に移動し、Overlay を出す。挙動は不変。
6. **[Perf] ログ**: シーンロード開始/終了、譜面解析・生成 (件数+ms)、メディア
   準備完了に `[Perf] <name> <ms>ms` 形式で出す (Windows ビルドの Player.log 計測用)。
7. **テスト**: バッチスケジューラ、進捗写像、表示遅延/最短表示の純粋ロジックを
   NUnit で検証する。

## Acceptance

- 同期 `SceneManager.LoadScene` / 同期 `Resources.Load` (音源) / 同期ノーツ一括生成が
  コードから消えている。
- 純粋ロジックの NUnit テストが通る (Unity Editor 上で実行、Worker は実行不可 → Supervisor)。
- `[Perf]` ログが Player.log から計測できる。
