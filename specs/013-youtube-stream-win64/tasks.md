---
description: "FlashBeat lean tasks — Issue 013"
---

# Tasks: yt-dlp → Unity VideoPlayer 1 曲ストリーミング完走 (Issue #13)

**Issue**: #13 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 実 yt-dlp / Deno probe と combined A/V 形式の選択が `main` に実装されていることを確認し、
  ローカルキャッシュ再生という **設計変更** を含む現状を記録する。
  — **done when**: 実装箇所と設計変更の理由が file:line で記録されていること
  — **実測**: `Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs:74-80` (Media Foundation の
  制約 `0x80070005` / カスタム HTTP ヘッダ不可を理由にローカル再生するコメント) /
  `:92` `public static class YouTubeStreamResolver` / `:111` `ResolveYtDlpPath()` /
  `:166` `ResolveDenoPath()` / `:67-91` Invidious を採用しない理由 (0/13 ホスト) /
  `Assets/Game/Scripts/Gameplay/YouTubeMediaCache.cs:50` / `:91` `DownloadOverride` (テスト用) /
  `:293` `DownloadAsync` 呼び出し / `:285-290` 拡張子 `video.mp4` / `audio.m4a` が必須である理由。
  **Issue 本文の「download-to-local fallback なし」は main では充足されていない (意図的な逸脱)。**

- [x] **T2** VideoPlayer Prepare→Play、fail/retry UI、固定 8 秒待ちの除去、Invidious fallback の除去が
  実装済であることを確認する。
  — **done when**: 各項目に file:line が付いていること
  — **実測**: `Assets/Game/Scripts/Gameplay/MusicManager.cs:43` `PrepareTimeout = 20f` /
  `:44` `FallbackStartTimeout = 30f` / `:48` `StallFailSeconds = 30f` /
  `:282` + `:557` `DisableInvidiousPlayer` / `:620` `PrepareLocalVideo` /
  `:420` `PlaybackRecovery.DecideAudioSource` /
  `Assets/Game/Scripts/Gameplay/PlaybackRecovery.cs:62` `DecideAudioSource` /
  `:86` `NeedsSelectReturn` / `:127` `FailureLog` / `:134` `StartHint`。

- [x] **T3** note 描画 / 判定のクロックが映像時刻に同期し、stall 越しに `Time.time` を累積しないことが
  コードで固定されていることを確認する。
  — **done when**: クロック関数の file:line と既存 EditMode テストが特定できること
  — **実測**: `Assets/Game/Scripts/Gameplay/MediaTimeline.cs:27` `JudgeStartTime` /
  `:33` `MediaTimeAt` / `:39` `JudgeLag` / `:49` `MediaLag` / `:61` `IsUsableDuration` /
  `:71` `HasSongEnded` / `:86` `FallbackSongEnd` / `:101` `FallbackMediaTime` /
  `:111` `ShouldResync` / `:122` `NotePositionZ` /
  `MusicManager.cs:926` 30 秒 stall 監視 /
  `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs` (曲終了状態機械 #226) /
  テスト `Assets/Tests/Editor/PlaybackClockTests.cs` / `MediaTimelineTests.cs`。
  **実機での判定同期誤差の測定値は未取得 (T6)。**

- [ ] **T4** split 経路の音声 (`audio.m4a`) が Win64 で準備失敗する理由と、
  combined 化 / `AudioSource` 化 / ffmpeg 結合の 3 候補から採用方式を実測で選ぶ (段 1)。
  — **done when**: 採用方式と、その根拠となる実測記録があること
  — **備考**: 現状の無音フォールバックは `MusicManager.cs:955-975` (3 秒時点で rms 測定、
  `rms < 0.001` なら `SwitchToBundledAudio`)。**これが `audioMode=Bundled` へ落ちる主因**。
  採用候補は `plan.md` §「実装順序」参照。**推奨は combined 化 (追加依存ゼロ)**。

- [ ] **T5** 採用方式を純粋ロジックとして抽出し、EditMode テストで固定する (段 1)。
  — **done when**: `script/compile-check.sh --assembly flashbeat` と `--assembly tests` が通ること
  — **備考**: 抽出は `YouTubeStreamResolver` / `YouTubeMediaCache` / `MusicManager` の
  うち itag → combined/split 判定の部分。**既存クラスは読むだけ・変更は最小**。

- [ ] **T6** Win64 実機で 1 曲完走し、**ストリームの音声トラックが鳴る**ことを証明する (段 2)。
  — **done when**: Player.log に `audioMode=Stream` (または `StreamSeparate`) の行と、
  **同梱 wav ではない** ストリーム由来の rms 値が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  `audioMode=Bundled` は **不合格**。手順は
  `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3.3 / §3.4。
  映像 `prepared=True` だけでは合格にしない (既に証明済み)。

- [ ] **T7** 起動遅延と判定同期誤差の測定値を取得する (段 2)。
  — **done when**: 起動遅延 (秒) と判定同期誤差の数値が記録に残ること
  — **備考**: 起動遅延 14.1 秒の記録は `docs/evidence/index.md` §1.5 に既にあるが、
  **同期誤差の数値は未記載**。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T8** select → 再生 → result → retry を通しで観測し、retry 後に再計測できることを確認する (段 2)。
  — **done when**: 1 ランの操作ログと Player.log が揃うこと
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T9** 証明済みの Win64 artifact を SHA と chart 付きで保存する。
  — **done when**: fingerprint (SHA-256) と chart 名が記録に残ること
  — **備考**: 既存の第 3 世代の fingerprint `b8c0acde…` / code state `b77b63c` は
  `release/check/stage-manifest.json` に存在する。**新しい証明の追加は cc 側の統合 PR で行う**
  (`release/check/**` と `docs/evidence/**` は本 spec の所有範囲外)。

- [ ] **T10** Issue #13 の受入条件 T1 「download-to-local fallback なし」を、
  main の設計変更として **正式に受け入れる** 形で書き換える案を cc に提示する。
  — **done when**: 書き換え案が `spec.md` にあり、cc が Issue を更新する準備ができていること
  — **備考**: **本 spec は Issue を更新しない** (cc が Issue コメントを投稿する)。
  書き換え案は `spec.md` §Decisions の「Issue T1 の扱い」に記載済み。

- [ ] **T11** macOS 実機での完走を (副次的に) 取得し、Win64 の代替ではないことを明記する。
  — **done when**: macOS の観測記録があり、**Win64 の代用ではない**旨が明記されていること
  — **備考**: `docs/evidence/index.md` §1.6 が「macOS Player 起動は Win64 の代用にならない」と明記。
  **本 Issue の合格条件にはしない。**
