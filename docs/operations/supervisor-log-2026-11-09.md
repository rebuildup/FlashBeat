# 監督ログ 2026-11-09 (開発継続のための引き継ぎ記録)

このファイルは **cc (開発監督) の作業 Continuity 用**です。
Issue の受入条件の記録は各 Issue のコメントに置、**ただし Worker の作業が
API 上限で中断されたときの再開手順** はここに集約します。

対象 Issue: #250 (全 Issue 完遂と展示) / #233 / #182 / #118 / #41

---

## 0. 現在の main

| 項目 | 値 |
|---|---|
| `origin/main` | `45c00db` (PR #253 merge 時点) |
| EditMode スイート (main で実測) | **1118 / 1118 成功** (Unity 6000.5.6f1 / macOS batchmode) |

基準となる実測値はこの 1118 です。Worker 申告の数値は cc が各 PR ごとに独立に再実測しています。

---

## 1. 統合キュー (レビュー → merge 待ち)

| PR | Issue | 内容 | cc 側の検証 | 独立レビュー |
|---|---|---|---|---|
| #252 | #225 | NoteEditor 曲切替時に未保存確認 | cc 再実測待ち (Worker 申告 EditMode **1114/1114**) | 済 (blocking 3 → Worker が `3a95037` で解消) |
| #253 | #35 | Win64 パッケージを macOS pwsh でも実行可能 / 再現可能化 | `pwsh` で **2 回実行して SHA-256 一致を再実測** (224 entries / 138 DLL / song-free / Burst 0 / sidecar OK) | 済 (should-fix 5 → Worker が `13a5855` で解消) → **merged (`45c00db`)** |
| #254 | #41 | 展示 runbook に候補ビルドと受入記録表 | 文書のみ | 済 (blocking 4 → 本 PR で解消) → **merge 待ち** |
| #255 | #118 | 更新フローの受入条件固定 + 純関数化 | EditMode **1118/1118** | 済 → **merged** |
| #256 | #182 | 判定キューの受入条件裏付け + **実バグ修正** | cc 再実測待ち | 済 (should-fix 3 → Worker が対応中) |
| #257 | #74/#75 | Web Studio 狭幅 (390px) 横スクロール解消 | `npm test` 399/399 + visual gate PASS (cc 再実測) | レビュー中 |
| #258 | #233 | 曲終了 → Result 到達の実機ハーネス | Draft | 済 (blocking 4 → 要修正。S1 は #233 プレイフィールド側と同一原因) |
| #259 | CI | macOS ビルド CI の runner / インストーラ修正 | 2 回 dispatch で 5 ステップ進行 → ライセンス検査で 22 秒失敗 (正しいオーナーアクション) | 済 (blocking 0 / should-fix 2 → `eeac115` で F1/F2/F3 解消) |
| #260 | #240 | v6 Result のローカル登録契約の実測証跡 | 文書のみ | blocking 0 / should-fix 2 残 → 要修正 |

### 統合順の制約

- **#253 → #254 の順**で解消済み。#253 を `45c00db` で merge したため、
  #254 の runbook が参照する
  [`../evidence/win64-build-from-macos.md`](../evidence/win64-build-from-macos.md) は
  **main に実在します** (`git cat-file -e origin/main:docs/evidence/win64-build-from-macos.md`
  で確認済み)。dead link にはなっていません。

### PR #255 で既に把握している残点 (統合時に要確認)

- v6 Title にはリテラルの「アップデートあり」が出ない
  (`HideLegacyUpdateNotice()` が旧ピルを隠すため二重表示は起きない)。
  → **#239 の v6 Title 担当に連絡済み**。Title 側から更新通知を操作できる配線が必要。

---

## 2. worktree ごとの未コミット / 未 push 状態

**重要**: Worker が API 上限で中断されると、**作業内容は worktree に未コミットで残ります**。
再開時は `git -C <worktree> status --porcelain` と `git diff` で把握してから続けてください
(推測で書き直さないこと)。

**パスの区切りはハイフン**です (`FlashBeat-w233` が実体。`FlashBeat.w233` は存在しません)。
区切り記号はハイフンに統一してください。

| worktree (絶対パス) | ブランチ | 状態 (2026-11-09 19:45 JST 実測) | 内容 |
|---|---|---|---|
| `/Users/rebui/work/FlashBeat.wpf` | `w/w233-playfield` | `origin/main` より 6 behind / 6 ahead。**未コミット**: `Assets/Tests/Editor/PlaybackClockTests.cs`, `Assets/Tests/Editor/PlayfieldGeometryTests.cs` (+ 未追跡 `unity-run.out`, `unity-wpf.log`) | #233 プレイフィールド (3D レーン復元 / 撮影窓 / z スケール統一) |
| `/Users/rebui/work/FlashBeat.wend` | `w/w233-songend-harness` | 4 behind / 2 ahead / clean | #233/#226 曲終了ハーネス |
| `/Users/rebui/work/FlashBeat-w233` | `w/w233-loadingoverlay` | 7 behind / 0 ahead。**未コミット**: `Assets/Game/Scripts/UI/V5/GameUiExclusivity.cs` | #233 旧 UI 排他 / 歌詞二重表示 |
| `/Users/rebui/work/FlashBeat.w241` | `w/w241fix` | 0 behind / 0 ahead。**未コミット**: `OptionSceneManager.cs`, `V6OptionView.cs` 修正 + 未追跡 `Assets/Tests/Editor/V6OptionWiringTests.cs` (+ `.meta`) | #241 v6 Option |
| `/Users/rebui/work/FlashBeat.w56` | `w/w56fix` | 4 behind / 0 ahead。**staged のみ**: `.agents/skills/*` ほか (`mise.toml`, `AGENTS.md`, `skills-lock.json` も変更) | #56 AI 開発環境 |
| `/Users/rebui/work/FlashBeat.waudit` | `w/audit-open-issues` | 0 behind / 6 ahead / clean | 全 open Issue 監査 (`docs/audit/open-issues-audit.md` は **このブランチにのみ存在**。未 merge) |
| `/Users/rebui/work/FlashBeat.w253fix` | `w/win64-mac-package-v2` | 4 behind / 8 ahead / clean | #253 (PR 反映済み) |
| `/Users/rebui/work/FlashBeat.wdocs` | `w/docs-exhibit-runbook` | 4 behind / 3 ahead / clean | #254 (PR 反映済み) |

この表は **2026-11-09 19:45 JST 時点のスナップショット**です。Worker が並行動作しているため
実際の状態は必ず `git -C <worktree> status --porcelain` と
`git -C <worktree> rev-list --left-right --count origin/main...HEAD` で再確認してから
判断してください (表の数値をそのまま信用しない)。

---

## 3. シーン所有権 (単一所有者ルール)

`Assets/Game/Scenes/GameScene.unity` は **同時に複数 Worker が触る**ため、
原則 **どの Worker も編集しない**こと。シーン変更が不可避なら
**理由を報告するだけ**にして、cc が統合時に調整します。

| 領域 | 担当 |
|---|---|
| `GameScene.unity` | 旧 UI 排他 Worker (`w/w233-loadingoverlay`) が優先。プレイフィールド / 曲終了 Worker は**コード側で完結**させること |
| `TitleScene.unity` | v6 Title Worker (#239) |
| `ResultScene.unity` | v6 Result Worker (#240) |
| `OptionScene.unity` | v6 Option Worker (#241) |
| `NoteEditor.unity` | 別 (NoteEditor は build scenes list に入らず展示ビルドに影響しない) |

### #233 で建立的 2 つの独立 defect (2026-11-09 に cc が実測で特定)

1. **3D レーンの z スケール** — カメラ (pos 0, 4.9, -0.8 / euler 54.8, 0, 0 / FOV 60 / far 1000)
   の可視地面は z ∈ [-0.76, 8.83] (far clip ではなく FOV が制約)。noteSpeed 10 では
   画面上に出るのは約 0.9 秒分。→ `w/w233-playfield` の `MediaTimeline.VisibleDepthZ` /
   `ZScaleFor` で対応中。
2. **映像プレーンが非表示** — `GameScene.unity` の曲別映像プレーン "1"〜"29" が
   **すべて `m_IsActive: 0`** (`a156da3` / `f17979a` / HEAD のいずれでも 0 =  longstanding
   regression)。`MusicManager` は `YPlayer[songID]` のみ active にする
   (`MusicManager.cs:183`) で、`VPlayer[songID]` のプレーン GameObject は一度も active に
   しない (`MusicManager.cs:185` は代入のみ、`AttachVideoPicture()` は物料配線だけ)。
   → **コード側で** active 化すること。シーンは触らない。

---

## 4. 作業の中断に対する cc の方針

API の Token 上限で Worker が中断されるため、**cc は次のことを指示しています**:

1. **15 分程度の小さい単位で commit する** (大きな塊で固めない)。
2. **成果物 (docs / evidence) を先に書き出す**。中断しても証跡は残る。
3. 対象 Issue へのコメント投稿は **cc が行う** (Worker は PR まででよい)。
4. Worker の完了申告は **cc が main の実コードと突き合わせて検証するまで受け入れない**
   (「Worker の自己申告や Issue の Closed だけを実装完了の証拠にしない」)。
5. **変異テスト (mutation test) を必須にする** — ガードを意図的に壊して
   「そのテストが落ちる」ことを確認する。落ちなければ core が未検証。

---

## 5. オーナー待ちの操作 (cc では解けないもの)

| # | 操作 | 影響 |
|---|---|---|
| 1 | GitHub Actions Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` を登録 | Win64 / macOS の両 CI が緑になる。**現状は Personal `.ulf` 拒否で必ず失敗** (macOS は runner image / インストーラを直して「正しい失敗」に到達済み。Win64 は 5GB 取得前に落とす precheck 済み) |
| 2 | Windows 実機で 1 曲完走 (Title→Select→Game→Result→Retry) | #233 / #242 の受入条件。**展示 PC は cc が触らない** (メモリ方針) |
| 3 | 3 解像度 (FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664) を **実ディスプレイ全画面** で確認 | 窓リサイズによる擬似全画面は検証にならない (メモリ方針) |
| 4 | 展示候補の GitHub Release 化 | **Windows 実機受入後に限る** (未検証の配布は公開しない)。公開版数は `0.1.22` が次の候補 (Latest は `v0.1.21`) |

運用手順は [`exhibit-runbook.md`](exhibit-runbook.md) に、
候補ビルドの詳細は [`../evidence/win64-build-from-macos.md`](../evidence/win64-build-from-macos.md) にあります
(このファイルは PR #253 で追加されます)。

---

## 6. 監査で判明した、重要な事実 (Issue の open/closed と実装の対応)

**open な Issue 数 = 未実装件数ではありません。** 次の 3 件は**実装が既に main にあり**、
Issue だけが open・コメント無しの状態でした。全 open Issue 47 件の分類は
`docs/audit/open-issues-audit.md` (ブランチ `w/audit-open-issues`、**未 merge**) にあります。

| Issue | 実装 (main 済み) |
|---|---|
| #182 | `ad20d21` / `6d9a4ae` / `f953eea` — `JudgeQueue.cs` + `Judge.cs` |
| #226 | `cfcd59f` — `SongEndStateMachine.cs` / `MediaTimeline.HasSongEnded` |
| #118 | `UpdateFlowModel` / `UpdateChecker.RequestDownload` / `RequestApply` (実コードに `OnApplicationQuit` は 0 件) |

**Issue の状態を見て「未実装」と判断しないこと。** 着手前に必ず
`git merge-base --is-ancestor <sha> origin/main` と `grep` で実装の所在を確かめてください。

---

## 7. 展示候補ビルド (2026-11-09)

| 項目 | 値 |
|---|---|
| 候補 ID (ZIP 内部のラベル) | `0.1.14-win64-d8c1b39` |
| 内部版数 (`Application.version`) | `0.1.14` (`ProjectSettings.bundleVersion` = `0.1.14+d8c1b39`) |
| ZIP | `FlashBeat-0.1.14-win64-d8c1b39.zip` (274,826,784 bytes / 224 entries / Managed DLL 138) |
| SHA-256 | `3ac76d0d7a9e30f1063d733fbd17028c853f6c4b8fc4950de51761b9b81abfb3` |
| 置き場 | `/Users/rebui/work/FlashBeat.macverify/dist2/` (**`dist2/` のみが正**) |
| 状態 | **未検証** (Windows 実機未完走 / Releases 未 publish) |
| 更新チャンネル | `preview` (prerelease も更新候補に含める設定)。**展示用は `stable` が必要** |
| 公開時の版数 | `0.1.22` (Latest `v0.1.21` の次。同じ版数・別 SHA の公開は禁止) |

### 注意

- **同名 ZIP が 2 つあります。** `dist3/` にも `FlashBeat-0.1.14-win64-d8c1b39.zip` が
  あって SHA-256 が異なります (`eb337474…` / 274,826,785 bytes)。原因は旧パッケージャが
  `version.json` の `createdAt` に実行時刻を埋め込んでいたこと。**PR #253 で再現可能化
  済み** (2 回ビルドして SHA-256 一致を実測) なので、再ビルドし直して SHA-256 を
  再実測してから使ってください。
- **この ZIP は再現できません** — 未コミットの作業ツリー差分
  (`scriptingDefineSymbols` に `SENTIS_ANALYTICS_ENABLED` を追加) を含んだ状態で
  ビルドされています。クリーンチェックアウトからは同じバイナリになりません。
- 完全性検証は同梱の `.sha256` sidecar と 1 対 1。

### 再現性のパッケージャ実測 (2026-11-09 19:47 JST / PR #253 merge 後の head で再実行)

cc が PR head (`13a5855`) の `tools/release/package-win64.ps1` を **別出力先で 2 回**
実行しました。入力は `/Users/rebui/work/FlashBeat.macverify/Builds/Windows`、
`-CommitDate 2026-11-09T19:00:00+09:00` (ISO 8601) を固定。

```
-rw-r--r--  274824992  /tmp/v253a.x3Hl0f/FlashBeat-0.1.14-win64-d8c1b39.zip
-rw-r--r--  274824992  /tmp/v253b.VFONe1/FlashBeat-0.1.14-win64-d8c1b39.zip
281c457d8789c1a8de7089cb4c1e98d596b3697cb85b462fc8737fbc83342fe9  /tmp/v253a.x3Hl0f/...zip
281c457d8789c1a8de7089cb4c1e98d596b3697cb85b462fc8737fbc83342fe9  /tmp/v253b.VFONe1/...zip
```

**同一 SHA-256**。`version.json` の `createdAt` は実行時刻ではなく
**ビルド元 commit の日時** (`2026-11-09T10:00:00Z`) になっていることも確認しました。

| 検査 | 結果 |
|---|---|
| エントリ数 | 224 |
| Managed DLL | 138 |
| メディア (`*.wav` / `*.mp4` / `Musics/` / `UnityYoutube`) | **0 件** |
| `BurstDebugInformation` | **0 件** |
| `.sha256` sidecar の `shasum -c` | OK |
| `unzip -t` | No errors detected |

この SHA (`281c457d…`) は上表の候補 (`3ac76d0d…`) とは異なります — 上表は旧パッケージャ
の産物で、`-CommitDate` を固定していない実行だったためです。**実際の配布候補は
`281c457d…` の再ビルド成果物を版数 `0.1.22` チャンネル `stable` で作り直してから
使ってください** (上表の `3ac76d0d…` は参考値としてのみ)。
