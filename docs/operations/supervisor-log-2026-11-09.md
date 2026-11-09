# 監督ログ 2026-11-09 (開発継続のための引き継ぎ記録)

このファイルは **cc (開発監督) の作業 Continuity 用**です。
Issue の受入条件の記録は各 Issue のコメントに置，但因为 **Worker の作業が
API 上限で中断されたときの再開手順** をここに集約します。

対象 Issue: #250 (全 Issue 完遂と展示) / #233 / #182 / #118 / #41

---

## 0. 現在の main

| 項目 | 値 |
|---|---|
| `origin/main` | `d8c1b39` (PR #251 merge 時点) |
| EditMode スイート (main で実測) | **1073 / 1073 成功** (Unity 6000.5.6f1 / macOS batchmode) |

基準となる実測値はこの 1073 です。Worker 申告の数値は cc が各 PR ごとに独立に再実測しています。

---

## 1. 統合キュー (レビュー → merge 待ち)

| PR | Issue | 内容 | cc 側の検証 | 独立レビュー |
|---|---|---|---|---|
| #252 | #225 | NoteEditor 曲切替時に未保存確認 | EditMode **1086/1086** | 済 (中断で投稿前、再開中) |
| #253 | #41/#35 | Win64 パッケージを macOS pwsh でも実行可能 | `pwsh` で実実行 (224 entries / 138 DLL / song-free) | 未了 |
| #254 | #41 | 展示 runbook に候補ビルドと受入記録表 | 文書のみ | 未了 |
| #255 | #118 | 更新フローの受入条件固定 + 純関数化 | EditMode **1118/1118** (Worker 申告) | **済** (blocking 0 / should-fix 5 → 全件解消済) |
| #256 | #182 | 判定キューの受入条件裏付け + **実バグ修正** | EditMode **1080/1080** | 再開中 |
| #257 | #74/#75 | Web Studio 狭幅 (390px) 横スクロール解消 | `npm test` / visual gate 再実測中 | 未了 |

### PR #255 で既に把握している残点 (統合時に要確認)

- v6 Title にはリテラルの「アップデートあり」が出ない
  (`HideLegacyUpdateNotice()` が旧ピルを隠すため二重表示は起きない)。
  → **#239 の v6 Title 担当に連絡済み**。Title 側から更新通知を操作できる配線が必要。

---

## 2. 未コミット変更が残っている worktree (中断した Worker の作業)

**重要**: Worker が API 上限で中断されると、**作業内容は worktree に未コミットで残ります**。
再開時は `git -C <worktree> status --porcelain` と `git diff` で把握してから続けてください
(推測で書き直さないこと)。

| worktree | ブランチ | 未コミット | 中断した Worker の作業 |
|---|---|---|---|
| `FlashBeat.wpf` | `w/w233-playfield` | `AutoTestV6.cs`, `MediaTimeline.cs` | #233 プレイフィールド (確認窓 1.6 s / レーン構造 / 密区間の撮影) |
| `FlashBeat.wend` | `w/w233-songend-harness` | `Judge.cs`, `MusicManager.cs`, `MediaTimeline.cs`(未追跡) | #233/#226 「曲終了 → Result 到達」の実機ハーネス |
| `FlashBeat.w233` | `w/w233-loadingoverlay` | `GameUiExclusivity.cs` | #233 旧 UI 排他 / 歌詞二重表示 |
| `FlashBeat.w241` | `w/w241fix` | `OptionSceneManager.cs`, `V6OptionView.cs` | #241 v6 Option |
| `FlashBeat.w56` | `w/w56fix` | `.agents/skills/*` (staged) | #56 AI 開発環境 |
| `FlashBeat.waudit` | `w/audit-open-issues` | — | 全 open Issue 監査 (`docs/audit/open-issues-audit.md`) |

---

## 3. シーン所有権 (単一所有者ルール)

`Assets/Game/Scenes/GameScene.unity` は **同時に複数 Worker が触る**ため、
原則 **どの Worker も編集しない**こと。シーン変更が不可避なら
**理由を報告するだけ**にして、cc が統合時に調整します。

| 領域 | 担当 |
|---|---|
| `GameScene.unity` | 旧 UI 排他 Worker (w233-loadingoverlay) が優先。プレイフィールド / 曲終了 Worker は**コード側で完結**させること |
| `TitleScene.unity` | v6 Title Worker (#239) |
| `ResultScene.unity` | v6 Result Worker (#240) |
| `OptionScene.unity` | v6 Option Worker (#241) |
| `NoteEditor.unity` | 別 (NoteEditor は build scenes list に入らず展示ビルドに影響しない) |

---

## 4. 作業の中断に対する cc の方針

API の Token 上限で Worker が中断されるため、**cc は次のことを指示しています**:

1. **15 分程度の小さい単位で commit する** (大きな塊で固めない)。
2. **成果物 (docs / evidence) を先に書き出す**。中断しても証跡は残る。
3. 対象 Issue へのコメント投稿は **cc が行う** (Worker は PR まででよい)。
4. Worker の完了申告は **cc が main の実コードと突き合わせて検証するまで受け入れない**
   (「Worker の自己申告や Issue の Closed だけを実装完了の証拠にしない」)。

---

## 5. オーナー待ちの操作 (cc では解けないもの)

| # | 操作 | 影響 |
|---|---|---|
| 1 | GitHub Actions Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` を登録 | Win64 CI が緑になる (现在是 Personal `.ulf` 拒否で必ず失敗) |
| 2 | Windows 実機で 1 曲完走 (Title→Select→Game→Result→Retry) | #233 / #242 の受入条件。展示 PC は cc が触らない |
| 3 | 展示候補の GitHub Release 化 | **Windows 実機受入後に限る** (未検証の配布は公開しない) |

運用手順は [`exhibit-runbook.md`](exhibit-runbook.md) に、
候補ビルドの詳細は [`../evidence/win64-build-from-macos.md`](../evidence/win64-build-from-macos.md) にあります。

---

## 6. 監査で判明した、重要な事実 (Issue の open/closed と実装の対応)

**open な Issue 数 = 未実装件数ではありません。** 次の 3 件は**実装が既に main にあり**、
Issue だけが open・コメント無しの状態でした。全 open Issue の分類は
`docs/audit/open-issues-audit.md` (作成中) にあります。

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
| 版 | `0.1.14-win64-d8c1b39` |
| ZIP | `FlashBeat-0.1.14-win64-d8c1b39.zip` (274,826,784 bytes / 224 entries / Managed DLL 138) |
| SHA-256 | `3ac76d0d7a9e30f1063d733fbd17028c853f6c4b8fc4950de51761b9b81abfb3` |
| 置き場 | `/Users/rebui/work/FlashBeat.macverify/dist2/` |
| 状態 | **未検証** (Windows 実機未完走 / Releases 未 publish) |
| 注意 | ZIP は modification time を含むため**バイト単位で再現しない**。再パッケージすると SHA-256 は変わる。完全性検証は同梱の `.sha256` sidecar と 1 対 1 |
