---
description: "FlashBeat lean spec — Issue 013"
---

# Spec: yt-dlp → Unity VideoPlayer 1 曲ストリーミング完走 (S1) — ストリーム音声の実機証明

**Issue**: #13 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[S1/Win] yt-dlp+Deno→Unity VideoPlayer 1曲ストリーミング完走`

## Objective

Issue #13 は「YouTube の動画を Unity `VideoPlayer` で 1 曲完走させる」ことを求める。
`main` の実装は、**URL ストリーミングではなく yt-dlp によるローカルキャッシュ再生**へ
**意図的に設計変更**されている (Media Foundation の制約)。
映像 (`video.mp4`) は Win64 実機で再生已有的证迹がある一方、
**ストリームの音声トラック (m4a) は `Failed to recognize video file format` で準備できず**、
音は同梱 wav (`audioMode=Bundled`) で鳴っている。

本 spec の目的は **この残差 (ストリーム音声の実機証明)** と、
Issue 本文の陳腐化した要求 (**「download-to-local fallback なし」**) の扱いを出発点として、
**実装済みの項目と未充足の項目を分離して** 残作業を 1 つの作業単位に固定することにある。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| ローカルキャッシュ方式の維持 | **main の実装 (yt-dlp でダウンロードしてローカル再生) を維持する。** Issue 本文の「no download-to-local fallback」は 2026-11-06 時点の想定であり、`YouTubeStreamResolver.cs:74-80` に理由 (Media Foundation `MFCreateSourceReaderFromURL` が `googlevideo.com` を `0x80070005` で拒否、Unity の `VideoPlayer` はカスタム HTTP ヘッダを送れない) が記載済み。**この逸脱は Issue の陳腐化であり、差し戻さない** |
| 音声トラック (m4a) の扱い | 現状 `music.m4a` は `VideoPlayer` に渡できず準備失敗する。**解禁手段は 3 候補** — (1) combined format (video+audio) を itag で優先選択し単一 mp4 に畳む、(2) 音声のみ m4a を別 yt-dlp で取得し **`AudioSource`** に渡す (`MusicManager.cs` の split 経路は既に存在)、(3) ffmpeg で結合してから再生。**どれを採用するかは Worker が実測で選ぶ** (|U8 参照) |
| 判定の証明基準 | 「映像が再生した」「判定が N まで進んだ」は **ストリーム音声が鳴ったことを保証しない**。合格の判定は **`Player.log` の `audioMode=Stream` (または同値) 行 + ストリーム由来の `rms` 値** である。`audioMode=Bundled` は **不合格** |
| 同期の扱い | `MediaTimeline.cs` / `PlaybackRecovery.cs` / `SongEndStateMachine.cs` の **既存クロック** を維持。stall 越しに `Time.time` を累積しない (`MusicManager.cs:926` の `StallFailSeconds = 30f` 監視) |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** macOS Player / EditMode 緑 / `PlaybackClockTests.cs` 緑は **Win64 の代用にならない** (`docs/evidence/index.md` §1.6) |
| 成果物の同一性 | 候補 ZIP は `.zip.sha256` sidecar で照合する。**sha256 一致は「ZIP が Windows で動く」ことの証明ではない** (`docs/operations/win64-candidate-acceptance-2026-11-10.md` §1) |
| Issue T1 の扱い | 「download-to-local fallback なし」を受入条件として **維持しない**。main の設計変更を **正式に受け入れる** 形で Issue の受入条件を書き換える (cc が Issue を更新するのは cc 側。**本 spec では書き換え案を提示するのみ**) |

## 非目標 (Out of scope)

- 新ゲーム UI・認証・編集ツール・オフラインメディア・新しい note type (Issue 本文 "No scope")。
- `web/**` / API の変更 (Issue 本文 "No web/ or API changes")。
- Invidious 経路の复活 (公開インスタンス 13 ホストが 0/13 で JSON を返さない。`YouTubeStreamResolver.cs:67-91` に理由記載 / `MusicManager.cs:557` `DisableInvidiousPlayer` で除去済)。
- 曲エンド状態機械 (#226) と判定キュー (#182) の改修。
- **`Assets/**` のコード変更** (本 spec はドキュメントのみ)。

## Acceptance criteria

1. Win64 実機で 1 曲完走し、**ストリームの音声トラック** が鳴ることが `Player.log` で証明される
   (`audioMode=Stream` 行 + 同梱 wav ではない `rms` 値)。
2. `mediaMode` / `audioMode` の判定根拠が Player.log に 1 行で残り、**「何が鳴っているか」を推測で埋められない**。
3. 映像 (video track) と音声 (audio track) の **同期** が観測でき、note の描画位置・判定タイミングが
   音源の時刻に一致している (同期誤差の測定値が記録に残ること)。
4. 起動遅延 (，选曲から音が出るまでの実測秒数) が記録に残ること。
5. バッファリング / ストール時に `Time.time` の累積が再開せず、判定がずれない (EditMode の既存
   クロック契約 + 実機での observational 確認)。
6. 同じ run で **select → 再生 → result → retry** が通ること (retry 後に再計測可能)。
7. 証明済みの Win64 artifact が **SHA と chart でタグ付け**されて保存される。
8. Issue 本文の「download-to-local fallback なし」は **main の設計変更として正式に受け入れ**、
   受入条件の書き換え案が cc に提示される (本 spec はドキュメントのみ。Issue の更新は cc が行う)。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。

### 充足 (コード)

| Issue タスク | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| T2: VideoPlayer Prepare→Play、音声出力、fail/retry UI、**固定 8 秒待ちの除去**、Invidious fallback 除去 | `Assets/Game/Scripts/Gameplay/MusicManager.cs:43` `PrepareTimeout = 20f` / `:44` `FallbackStartTimeout = 30f` / `:48` `StallFailSeconds = 30f` (固定 8 秒待ちなし) / `:282`・`:557` `DisableInvidiousPlayer` / `:420` `PlaybackRecovery.DecideAudioSource(combined, videoReady, audioReady, Music != null)` / `Assets/Game/Scripts/Gameplay/PlaybackRecovery.cs:62` `DecideAudioSource` / `:86` `NeedsSelectReturn` | 充足 (コード) |
| T3: note 描画/判定を映像時刻に同期、buffering で pause、**stall 越しに `Time.time` を累積しない**、既存 note JSON 変換を維持 | `Assets/Game/Scripts/Gameplay/MediaTimeline.cs:27` `JudgeStartTime` / `:33` `MediaTimeAt` / `:39` `JudgeLag` / `:49` `MediaLag` / `:61` `IsUsableDuration` / `:71` `HasSongEnded` / `:101` `FallbackMediaTime` / `:111` `ShouldResync` / `MusicManager.cs:926` 30 秒 stall 監視 (issue #226) / `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs` | 充足 (コード) / **実機同期誤差は未測定** |
| T5: SHA と chart をタグ付けした runnable Win build を保存 | `docs/evidence/index.md` §1 S1 行 fingerprint `b8c0acde7a318bc3f8d7929025743638042a3b0e2dec7836e418a2d80c60830f` / `release/check/stage-manifest.json` の S1 エントリ (`status: "unproven"` だが fingerprint は存在) | 充足 (成果物の **存在**) / **S1 としての承認は未取得** |
| No scope: 新 UI / auth / 編集ツール / オフライン / 新 note type | 該当実装の追加なし | 充足 (規律) |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | T1: 実 yt-dlp/Deno probe、`-F` で combined A/V 形式を選択、**download-to-local fallback なし** | **未充足 (意図的な逸脱)** | 実装は **ローカルキャッシュへダウンロード** して再生する。`Assets/Game/Scripts/Gameplay/YouTubeStreamResolver.cs:74-80` (Media Foundation の制約を記録するコメント) / `Assets/Game/Scripts/Gameplay/YouTubeMediaCache.cs:50` (class コメント) / `:293` `YouTubeStreamResolver.DownloadAsync(videoId, selector, template, cancellationToken)` / `:91` `DownloadOverride` (テスト用フック) / `:285-290` 拡張子は `video.mp4` / `audio.m4a`  且つ VideoPlayer がコンテナを識別できるよう命名が必須のコメント |
| 2 | **ストリーム音声 (m4a) の再生** | **未充足** | `docs/evidence/win64-2026-11-06-release-gate/README-gen3.md` (Run D) — 映像 `prepared=True` / `video[playing=True]`、音 `rms=0.44…0.56 peak=0.82`、**`audioMode=Bundled`**、**m4a は `Failed to recognize video file format`**。`release/check/stage-manifest.json:33` にも同文言が記録されている |
| 3 | T4: **Win64 実ストリーミング select→result→retry** と測定値 (起動遅延・判定同期誤差) | **部分充足** | EditMode テストは存在 (`Assets/Tests/Editor/PlaybackClockTests.cs` / `MediaTimelineTests.cs` / `Assets/Game/Tests/` の再生系)。実 Win64 の select→result→retry は `docs/evidence/index.md` §1.5 第 3 世代で到達済み。**起動遅延 14.1 秒** の記録は §1.5 にあるが、**判定同期誤差の数値は未記載** |
| 4 | 音声トラックの itag / 分割 / 結合の選択ロジックの実測 | **未検証** | `MusicManager.cs` に combined (video+audio 1 ファイル) と split (video-only + audio-only) の 2 経路がある (`:335` `bool combined = plan.Combined` / `:338` merged 失敗時の video-only フォールバック / `:348` `plan.HasAudioFile && !combined` / `:383` split の sound player 準備 / `:420` `DecideAudioSource`)。**どちらが Win64 で `audioMode=Stream` に到達するかを実測した記録が無い** |
| 4b | split 経路の A/V ドリフト補正と無音フォールバック | **充足 (コード)** | `MusicManager.cs:939-952` `MediaTimeline.ShouldResync(pictureTime, soundTime)` で映像基準に音声を re-seek / `:955-975` 3 秒時点で rms を測り `rms < 0.001` なら同梱 wav へ `SwitchToBundledAudio` (同梱 wav が無いときは `MediaFailure.AudioSilent`)。**この無音フォールバックが `audioMode=Bundled` へ落ちる主因と推定される** |
| 5 | Deno の解決 (YouTube の JS challenge に必要) | **未検証 (コードは充足)** | `YouTubeStreamResolver.cs` に `ResolveDenoPath()` / ffmpeg 探索がある。**実 Win64 で Deno が見つかり yt-dlp が成功した記録は `docs/evidence/` に無い** |

### ブロッカー

- **Windows 実機 (展示 PC)** が要る。cc は展示 PC を操作しない。
- **YouTube 側の到達性** (age gate / bot 検出 / 地域制限) が要る。macOS 側でも yt-dlp の解決は可能だが、**Media Foundation の制約は Windows 固有** なので、音声トラックの判定は Win64 でしか出ない。
- **展示 PC の sqqa環境** (MSE / コーデック) が `m4a` を扱えるかは展示 PC の管理者に確認が要る (`docs/operations/win64-candidate-acceptance-2026-11-10.md` §4 の「コーデック導入は展示PC の管理者に確認」)。

### 参考

- `Assets/Game/Scripts/Gameplay/MediaCacheManager.cs:17` — `MediaCacheManager` (`:34` `GetCachedVideoIds` / `:52` `GetCachedSongCount` / `:58` `GetCacheSizeBytes` / `:76` `Clear` / `:93` `ClearAll`)。
- `docs/audit/open-issues-audit.md` §4 **U8** (ストリーム音声の実証) が本 Issue の実装作業_units を指している。
