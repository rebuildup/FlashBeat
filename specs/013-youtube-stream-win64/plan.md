---
description: "FlashBeat lean implementation plan — Issue 013"
---

# Plan: yt-dlp → Unity VideoPlayer 1 曲ストリーミング完走 (Issue #13)

**Issue**: #13 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

main の現状 (ローカルキャッシュ再生) を踏まえた、**残差「ストリーム音声の証明」** の流れ。

```
[macOS / cc — 準備]                          [Windows 実機 (展示 PC) — オーナー実施]
 yt-dlp (PATH / mise / Python /                FlashBeat.exe (引数なし)
   Assets/StreamingAssets/yt-dlp/)              ├─ Title → Select → Game
   + Deno (--js-runtimes deno:<path>)           │    ↓
   ↓                                           │  MusicManager
 YouTubeStreamResolver                          │    ├─ ResolveYtDlpPath() / ResolveDenoPath()
   ↓  -F を解析して combined / split を選ぶ      │    ├─ YouTubeMediaCache.EnsureAsync(videoId)
   ↓  combined 失敗 → video-only へ fallback    │    │    └─ yt-dlp → cache/video.mp4 (+ cache/audio.m4a)
   ↓                                           │    ├─ PrepareLocalVideo(path, combined)
 YouTubeMediaCache (cache/ 配下)                │    │    └─ VideoPlayer prepared=True / playing=True
   ├─ combined : video.mp4 (映像+音声)          │    ├─ split : streamAudioPlayer (audio.m4a)
   │    → AudioSource は使わない                │    │    └─ Windows のデコーダが m4a を受け付けない
   └─ split    : video.mp4 + audio.m4a          │    │       → prepared=False
                → AudioSource に渡す             │    │       ("Failed to recognize video file format")
                                                  │    ├─ MusicManager.cs:420 DecideAudioSource
                                                  │    │    → audioMode = Stream / StreamSeparate / Bundled
                                                  │    ├─ MusicManager.cs:955-975 3 秒後に rms 測定
                                                  │    │    → rms<0.001 なら SwitchToBundledAudio (fallback)
                                                  │    ├─ MusicManager.cs:939-952 A/V ドリフト → 映像基準へ再 seek
                                                  │    └─ MediaTimeline / SongEndStateMachine で判定
                                                  ↓
                                            Result → Retry
                                            Player.log に
                                            "[MusicManager] media ready: … audioMode=…" 行

判定 (合格条件)
  audioMode=Stream / StreamSeparate かつ  同梱 wav ではない rms  ⇒ 合格
  audioMode=Bundled                                    ⇒ 不合格 (実測で.Fatal ている)
```

## 実装順序

本 Issue は **実装作業と証跡作業の 2 段** 構成。段 1 は Worker (macOS)、段 2 はオーナー (Windows)。

### 段 1 — 残差の切り分け (Worker / macOS, コード変更可)

1. **再現 observations の確認** (既存の記録を読むだけ):
   - `docs/evidence/win64-2026-11-06-release-gate/README-gen3.md` (Run D) — `audioMode=Bundled` と m4a の失敗メッセージ。
   - `release/check/stage-manifest.json:33` — 同じ文言。
2. **候補の切り分け**: `MusicManager.cs` の combined / split 経路が実機でどちらに落ちるかを読解し、
   どの itag _selector が `audio.m4a` を取得しているかを特定する
   (`YouTubeStreamResolver.cs` の `-F` 解析部)。
3. **実装の選択** (3 候補のいずれか 1 つ):
   - (A) combined format を優先選択し **単一 mp4** に畳む (音声が映像トラックに入る → `VideoPlayer` だけで鳴る)。
   - (B) 音声のみ m4a を別取得し `AudioSource` に渡す (split 経路の `streamAudioPlayer` を mp4 対応コンテナに変える)。
   - (C) ffmpeg で結合してから再生 (追加依存。Windows 側に ffmpeg が必要 → 展示 PC の管理者に要確認)。
   - **推奨は (A)**: 追加依存ゼロで、Media Foundation の制約 (単一コンテナ) をそのまま回避できる。
4. **EditMode / .NET ハーネスで固定**: 選択ロジック (itag → combined/split の判定) を純粋関数として抽出し、
   `Assets/Tests/Editor/` にテストを追加する。
5. **compile-check**: `script/compile-check.sh --assembly flashbeat` / `--assembly tests` を通す。

### 段 2 — 実機での証明 (オーナー / Windows 実機)

6. 候補 ZIP を `script/win64-build-mac.sh` で作製し、`.zip.sha256` を照合する。
7. 展示 PC で `FlashBeat.exe` を **引数なし** 起動 → 1 曲完走 → `audioMode` 行と rms 値を採取。
8. 起動遅延と同期誤差の測定値を記録する。
9. 採用方式に合わせて Issue の受入条件 T1 を書き換える (cc が行う)。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC)** | 段 2 の全項目がブロック | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** 手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 |
| B2 | **Media Foundation の URL リーダー制約** (`0x80070005`) | URL ストリーム再生は成立しない | main のローカルキャッシュ方式を採用 (設計変更として正式受入)。Issue の T1 を書き換える |
| B3 | **Windows のデコーダが m4a を受け付けない** | split 経路の音声が準備失敗 | combined 化の検討 (候補 A) / 対応コンテナへの変換 / ffmpeg。**実測で決める** |
| B4 | **YouTube 側の bot 検出 / age gate / 地域制限** | yt-dlp の解決自体が失敗しうる | 別 videoId での retry。`MusicManager.cs` に retry 経路がある |
| B5 | **展示 PC のコーデック環境** | m4a / mp4 の復号可否 | 展示 PC の管理者に確認 (`docs/operations/win64-candidate-acceptance-2026-11-10.md` §4) |
| B6 | **Issue の T1 が陳腐化している** | そのままでは受入不能 | 書き換え案は本 spec に提示済み。**Issue の更新は cc が行う** (cc は Issue コメントの投稿も行う) |

## テスト戦略

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| 純粋ロジック | itag 選択 / combined-split 判定を抽出した関数の EditMode テスト | **可** (Worker が実装) |
| クロック | `Assets/Tests/Editor/PlaybackClockTests.cs` / `MediaTimelineTests.cs` (既存) | **可**。ただし **緑は実機合格の証明ではない** |
| 実機 | Win64 で `audioMode=Stream` + ストリーム由来 rms | **本 Issue の受入そのもの**。オーナー実施 |
| 静音フォールバック | `MusicManager.cs:955-975` の rms 判定は実機でしか機能しない | 実機のみ |

**macOS Player での完走は Win64 の代用にならない** (`docs/evidence/index.md` §1.6)。
**EditMode 緑 ≠ 実機確認** (`docs/audit/open-issues-audit.md` §0)。
