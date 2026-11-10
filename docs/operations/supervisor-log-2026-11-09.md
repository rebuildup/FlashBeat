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


---

# 追記 2026-11-10 (午前の統合)

## 0. 現在の main

| 項目 | 値 |
|---|---|
| `origin/main` | `877bde7` (PR #270 merge 時点) |
| EditMode スイート (main で実測) | **1185 / 1185 成功** (`/tmp/testresults-buildfix.xml` / PR #270 のブランチ) |
| Web テスト (main で実測) | **403 / 403 成功** (PR #257 のブランチ / `web/`) |

## 1. 今日 main へ取り込んだ PR

| PR | Issue | 内容 |
|---|---|---|
| #256 | #182 | 判定キューの受入条件固定 (Issue #182 → **Closed**) |
| #257 | #74 / #75 | Web の狭幅横スクロールを解消 (`npm run visual` exit 0 / clipped 0) |
| #264 | #241 | v6 Option「データ初期化」の保存範囲統一 + 譜面/設定 JSON の Resources 混在バグ修正 |
| #265 | #30 | 合意済み仕様と未確定項目 (D1〜D6) の切り分け (E 一部解消) |
| #266 | #59 | 4 機能 (歌詞同期 / キーガイド / HOLD / HUD) の受入記録 |
| #267 | #56 | agent 環境 (vendored Skills / skills-check / CI) の整備 (Issue #56 → **Closed**) |
| #268 | — | `YuGothB SDF.asset` を LFS ポインタとして保存 (`git lfs pull` の失敗を解消) |
| #270 | — | 退避メディア復元が例外でバッチ全体が失敗していたのを修正 (配布 ZIP が作れなかった) |

## 2. 実行中の Worker (2026-11-10 時点)

| Worker | 対象 | 状態 |
|---|---|---|
| PR #263 review | #233 プレイフィールド (+ #182 の確定数) | レビュー中 |
| PR #258 | #233 曲終了 → Result 実機 (macOS) ハーネス | macOS ビルド中 |
| #98 | Web の publish → chart ドラフト復元 | 実装中 |
| A 級 Web 検証 | #12 / #19 / #60 / #68 / #71 / #73 / #135 | 検証中 |
| 新規 Worker | #222 (Ready 連打ガード + カタログ回帰テスト) / #226 (正負 noteTiming 回帰テスト) | 実装中 |
| 新規 Worker | #118 (v6 Title の「アップデートあり」表示) | 実装中 |

## 3. 配布ビルド経路の復旧 (今日やったこと)

`script/win64-build-mac.sh` は **Unity が起動する前に止まっていた**。原因 2 件:

1. `git lfs pull` が `YuGothB SDF.asset` で失敗 (実体なのに LFS 対象と指定) → PR #268 で修正
2. **ビルド成功後** の退避メディア復元が `Directory.Move` で例外 → バッチ全体が非 0 終了
   → PR #270 で「移動先があれば上書きマージ」「復元に失敗してもビルドは落とさない」に修正

復旧後、main (`877bde7`) から Win64 候補が **実際に作れた** (下の §4)。
それまでは配布 ZIP が 1 つも生成されていなかった。

## 4. 展示候補 (Win64 / macOS ホストでクロスコンパイル)

| 項目 | 値 |
|---|---|
| ソース SHA | `877bde7a75571a620bd19683c9d43994a37f1529` (PR #270 merge) |
| ビルド | `bash script/win64-build-mac.sh origin/main /Users/rebui/work/dist/win64` |
| 出力 | `0.1.0+877bde7-win64-877bde7.zip` (274,818,257 bytes) + `.zip.sha256` + `.build-info.txt` |
| 状態 | **未検証** (Windows 実機未完走 / Releases 未 publish) |
| 注意 | 版の基準は `FLASHBEAT_VERSION_BASE` で決まる。現行 Release は `v0.1.21-win64-d581c4c` なので、**次の配布は `FLASHBEAT_VERSION_BASE=0.1.22`** で作る (同じ版で別 SHA を作ると自動更新が効かない) |

## 5. オーナー待ち (cc では解けないもの) — 2026-11-10 時点

| # | 操作 | 影響 |
|---|---|---|
| 1 | GitHub Actions Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` を登録 | Win64 CI が緑になる |
| 2 | Windows 実機で 1 曲完走 + 3 解像度の実全画面 | #233 / #242 / #88 / #96 / #59 の残り |
| 3 | 展示候補の GitHub Release 化 | **Windows 実機受入後に限る** |
| 4 | #30 の D1〜D6 (歌詞形状 / 歌詞表示 / キーガイド / pen.dev 正本 / `scoringVersion` / 既存ふりがな) | 他 Issue は代替既定値で進めている |

**展示 PC について**: 展示用の PC として扱っており、**cc は許可なく操作・ビルド・上書きしない**。
上の #2 はすべてオーナーの手動作業として手順書を `docs/operations/` に用意する。

---

## 追記 2026-11-10 (午後 — #233 実機ハーネス / #222 #226 / #118)

基準点: `origin/main` = `25c645f` (PR #272 merge 直後)

### 6. この節で main に取り込んだ PR

| PR | 内容 | Issue | 検証 |
|---|---|---|---|
| #258 | 曲終了 → Result の **実機 (macOS) ハーネス** (`-autotest v6songend`)。製品コード変更は getter 3 本 + `Judge` のキー注入 1 行のみ | #233 | **1193 / 1193** (cc が独立再実行) + 実機 29/29 断言 |
| #272 | Ready 連打の遷移ガード + カタログ回帰テスト + **負の noteTiming 回帰テスト** (+ 一度も動いていなかった `MenuKeyActionWiringTests` の meta 復旧) | #222 / #226 | **1203 / 1203** (cc が独立再実行) |
| #273 | v6 タイトルに「アップデートあり」の明示表示 (pure ロジック + バナー描画) | #118 | 検証中 (cc が独立再実行) |

### 7. レビューで発見した実バグ (PR #263)

PR #263 (`w/w233-playfield`) を独立レビューしたところ **NEEDS WORK**。2 件のブロッカー:

1. **白い板**: 映像プレーン (28.7×15.84) のマテリアルの `_MainTex` が `{fileID: 0}` で白になる。
   `VideoPicture.Activate` が `isPrepared` .Flags の后才呼ばれるため、**ダウンロード中と
   prepare 失敗中 (Windows は #13 で恒常的に失敗) に画面が真っ白**。
2. **根本原因の記載が誤り**: 「平面が inactive で親だけ有効化した」は実行時 no-op。
   実際の原因は VideoPlayer に target renderer / render texture が設定されていなかったこと。
   証拠文書と 1 本のテストが誤ったモデルを固定していた。

→ 修正 PR を起票し、**Windows 実機の視認は #233 の未解決問題としてそのまま残す**。

### 8. 実行中の Worker (2026-11-10 午後)

| 対象 | 作業 | 状態 |
|---|---|---|
| PR #263 修正 | B1 / B2 / S1〜S7 (白い板 / 原因記載 / 1.538 解像度でのレーン欠け / リソースリーク / `IsDrawn` 誤検出 / z スケール 1 回確定) | 実行中 |
| #98 | publish → chart ドラフト復元 | 実行中 |
| #226 残 | duration 端点 Tap/HOLD・動画長<duration・フレーム落ち・ランキング送信の回帰テスト | 実行中 |
| A グレード Web | #74 / #75 の受入条件個別確認 | 実行中 |

### 9. 環境メモ

- ディスクが 100% (ENOSPC) になり Unity の import が落ちていた。**使い捨ての detached worktree
  (`rv252` / `rv253` / `rv254` / `rv256` / `rv257` / `unitytest`) を削除して 35 GiB を確保**した。
  作業中 worker の worktree は削除していない。

---

# 追記 2026-11-10 (午後 — 統合の締め / HOLD 到達不能の発見)

基準点: `origin/main` = `5276327` (PR #277 merge 直後)

## 0. 現在の main

| 項目 | 値 |
|---|---|
| `origin/main` | `5276327` (PR #277 = evidence §7 merged) |
| EditMode スイート (main) | **1203 / 1203** (PR #272 の実測値。main と同等) |
| Web テスト (main) | **404 / 404** (PR #275 の実測値) |

## 1. この節で main に取り込んだ PR

| PR | 内容 | Issue |
|---|---|---|
| #272 | Ready 連打の遷移ガード + カタログ回帰テスト + 負の noteTiming 回帰テスト | #222 / #226 |
| #273 | v6 Title に「アップデートあり」の明示表示 | #118 |
| #274 / #276 | 監査台帳 (§1 / §4 U1〜U11 / §7) の更新 | 台帳 |
| #275 | publish → chart ドラフト復元 (Web) | #98 |
| #277 | evidence §7 (macOS 実機 1 曲完走 + Win64 候補 0.1.22) | 台帳 |

## 2. HOLD が実機で到達不能であることを発見 (Issue #278 新規起票)

`Assets/Game/Resources/*.json` の **全 27 曲**で `notes[].type` は **1 (Tap) のみ**。
`LongNote.TYPE_HOLD = 2` のノーツは **0 個**。

- HOLD の実装 (`LongNote.cs` / `NotesManager.LoadRoutine` / `Notes.ApplyHoldLength`) は main にあり、
  EditMode テストもある。**実装不足ではない。内容的到達性が 0**。
- そのため Issue #59 の受入「HOLD の実機演奏」は **実行不能** のまま残っていた。
- 対策 (A): テスト用 HOLD 譜面 + macOS 実機ハーネス (カタログは増やさない。譜面差し替えは
  既存の `PlaySessionSnapshot` シームを使う) → **Worker 実行中**。
- 対策 (B): 実曲 (チュートリアル等) に HOLD を追加 → **谱面内容の方針決定 = オーナー判断**。

## 3. 実行中の Worker (2026-11-10 午後後半)

| 対象 | 作業 | 状態 |
|---|---|---|
| PR #263 修正 | B1 / B2 / S1〜S7 | 実行中 (origin/main を merge 済み) |
| #226 残受入条件 | duration 端点 / 動画長 / フレーム落ち / ランキング送信の回帰テスト | 実行中 |
| #167 仕様タスク検証 | 仕様に基づく個別検証 | 実行中 |
| A グレード Web | #12 / #19 / #60 / #68 / #71 / #73 / #135 の受入確認 | 実行中 |
| #278 (新規) | HOLD 譜面 + 実機ハーネス | 実行中 |

## 4. 次の統合順

1. PR #263 の修正が緑 → 独立再実行 → merge
2. **その後に** main から Win64 候補を再ビルド (`FLASHBEAT_VERSION_BASE=0.1.22`)。
   現行候補 (0.1.22+0bab549-win64-0bab549, SHA-256 `a23c8c4a…`) は **#263 以前の SHA** なので
   **再ビルドが必要**。
3. evidence §7.2 / §7.3 と #233 / #242 のコメントを新しい SHA で更新する。

## 5. オーナー待ち (cc では解けないもの) — 2026-11-10 午後

| # | 操作 | 影響 |
|---|---|---|
| 1 | GitHub Actions Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` を登録 | Win64 CI が緑になる |
| 2 | Windows 実機で 1 曲完走 + 3 解像度の実全画面 | #233 / #242 / #88 / #96 / #59 の残り |
| 3 | 展示候補の GitHub Release 化 | Windows 実機受入後に限る |
| 4 | #30 の D1〜D6 | 他 Issue は代替既定値で進行中 |
| 5 | **#278 対策 (B) の可否** (実曲に HOLD を入れるか) | 対策 (A) は cc が実装する |

---

## 6. 2026-11-10 (午後〜夜) — 統合の続き

### 6.1 この節で main に取り込んだ PR

| PR | 内容 | Issue |
|---|---|---|
| #291 | HOLD 実機検証用の譜面差し替え + ハーネス (macOS 実機で 3 段階判定) | #278 |
| #293 | カタログ取得の abort / 古い応答の棄却 + 契約テスト 21 件 | #68 |
| #294 | カタログ取得に `cache: "no-store"` を明示 | #68 |
| #295 | #60 の受入記録 (実行 SHA / PR URL / tasks.md / before-after 画素証跡) | #60 |
| #296 | #60 の記録の訂正 (teal ハードコードは main で解消済みだった) | #60 |
| #292 | 台帳の更新 (#263 merge / #58 の macOS 証跡 / #278 の土台) | 台帳 |

### 6.2 Close した Issue (受入条件を個別判定した上で)

| Issue | 判定 | 根拠 |
|---|---|---|
| #278 | 受入条件 1 / 2 充足 | PR #291 + macOS 実機 run (`docs/evidence/278-hold/`)。**Windows 実機での HOLD 演奏は未検証として残す**。キー押下は注入シーム (`KeyBindings.InjectKeyHold`) |
| #68 | 受入条件 7 / 7 充足 | PR #293 + #294。実 API の生レスポンスを取得して `parseSongs` が受理することを実測 |
| #60 | 受入条件 5 / 5 充足 | PR #295 + #296。実行 SHA `2bd7302` / before SHA `54505c2` / PR #90 URL / 画素証跡 20 ファイル |

### 6.3 実機での観測 (macOS 実ディスプレイ 2560×1664 全画面)

| 対象 | 結果 |
|---|---|
| #233 の再撮影 (`-autotest v6`) | `RESULT pass=29 fail=0`。映像 / レーン境界 8 本 / 判定ライン / ノーツ / 歌詞パネル を画像で確認。**Windows 実機の視認は未検証** |
| #278 の HOLD | `RESULT pass=41 fail=0` (`holdClear=3 holdBroken=0`)。修正前 run は `fail=2` (**負の対照**) |
| #58 の選曲 → 完走 | 2 曲を完走 (`pass=31 fail=0` / `judged == settled == chartNotes`) |

### 6.4 Win64 展示候補の再生成

| 項目 | 値 |
|---|---|
| ZIP | `0.1.22+36a3572-win64-36a3572.zip` (274,842,010 bytes / 223 files) |
| SHA-256 | `d3a605f297ff43e4fa59436fc5da34e9c49d846d2a07aa808e43a78b711791a7` |
| ソース SHA | `36a3572cf75de73c45ef90d6d643ee248522df2a` (`origin/main` — PR #296 merge 直後) |
| ビルド | `FLASHBEAT_VERSION_BASE=0.1.22 bash script/win64-build-mac.sh origin/main /Users/rebui/work/dist/win64` |

**同じ出力ディレクトリの `0.1.0+877bde7` / `0.1.22+0bab549` / `0.1.22+e45c494` は
ソース SHA が古く (後続の PR を含まない) 使用禁止。** 手順は
`docs/operations/win64-candidate-acceptance-2026-11-10.md`。

### 6.5 保全した (統合不要だった) Worker の差分

`w/w233-loadingoverlay-preserve` (`bd2f80f`) は中断された Worker の未コミット差分の退避。
中身は `GameUiExclusivity.cs` の旧ロード表示の所有判定だが、**`main` は別 ((1) `GameLoadingRoute.UsesLegacyOverlay` を正本にし、(2) 歌詞専用の
`LegacyLyricObjectNames` に分離) で実装済み**。→ **再実装不要。統合も不要。**
(教訓: 未コミット差分の保全ブランチは、main の実装と突き合わせてから統合を決める)

### 6.6 オーナー待ち (cc では解けないもの) — 2026-11-10 夜

| # | 操作 | 影響 |
|---|---|---|
| 1 | GitHub Actions Secrets に `UNITY_EMAIL` / `UNITY_PASSWORD` を登録 | Win64 CI が緑になる |
| 2 | Windows 実機で 1 曲完走 + 3 解像度の実全画面 | #233 / #242 / #88 / #96 / #59 の残り |
| 3 | Windows 実機での HOLD 演奏 (本物のキー押下) | #278 の受入条件 3 |
| 4 | 展示候補 `0.1.22+36a3572` の GitHub Release 化 | Windows 実機受入後に限る |
| 5 | #30 の D1〜D6 | 他 Issue は代替既定値で進行中 |
| 6 | #278 対策 (B) の可否 (実曲に HOLD を入れるか) | 対策 (A) は main に実装済み |
