# Evidence: 240 — v6 Result のローカル登録契約の復元

- Issue: #240
- 作業ディレクトリ: `/Users/rebui/work/FlashBeat.w240` / branch `w/w240`
- 作業者: 実装ワーカー (Unity Editor 起動なし)
- **2026-11-09 追記**: 本実装 (commit `bf51eaf`) は `origin/main` に
  cherry-pick 済み (`8f2642f` "(cherry picked from commit bf51eaf)")。
  `git rebase origin/main` の結果 "skipped previously applied commit" により
  **実装部分は main に既に入っている**ことが確認できた。
  このブランチが持つ差分は、本ファイルと `specs/240-result-registration/tasks.md`
  の **実測証跡** (EditMode 実行結果 / negative control) のみ。

## 1. 実装

### 1.1 純ロジック (`Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs`)

既存の `V6ResultLogic` に「登録の方式」を純関数として追加した。

| メンバ | 内容 |
|---|---|
| `LocalPlayerNamePrefsKey` | `"V5PlayerName"` (v5 の `ResultView.PlayerNameKey` と同じ) |
| `LocalNameMaxLength` | `8` (v5 の `PlayerNameMax` と同じ) |
| `LocalNameDefault` | `"YOU"` (v5 の空入力時と同じ) |
| `IsSubmittable(isRemote, remoteId, chartVersion)` | リモート曲 + 妥当な songId + `chartVersion >= 1` のときだけ true |
| `RegistrationMode(...)` | `Online` / `Local` を返す |
| `NormalizeLocalName(raw)` | v5 の `ResultView.NormalizeName` を使い (大文字化 + A-Z0-9 + 最大8文字)、空なら `"YOU"` |
| `LocalSaveMessage(savedName)` | `"この端末に「<名前>」を保存しました"` (オンライン語彙を含まない) |
| `InitialName(mode, onlineNickname, localName)` | ローカルは端末内の保存名、オンラインはオンラインの保存名 (無ければ端末内の保存名) |
| `ResolveChartVersion(pinned, live)` | pinned > 0 なら pinned、そうでなければ live |

オンライン側の既存純ロジック (`ClassifySubmitResult` / `SubmitStatusMessage` /
`ValidateNicknameInput`) は **変更していない**。よって 201 / 429 duplicate /
オフライン / その他 HTTP の 4 区分はそのまま維持される。

### 1.2 保存先 (`Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs` 新規)

- `enum V6RegistrationMode { Online, Local }`
- `V6LocalNameStore.Load() / Save(name) / Clear()` — PlayerPrefs キー
  `"V5PlayerName"` の読み書きのみ。ロジックは持たない。

### 1.3 画面配線 (`Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs`)

- `OpenModal()`: モーダルを開いた時点で
  `CurrentRegistrationMode()` で方式を確定し、初期値を
  `V6ResultLogic.InitialName(mode, RankingClient.LoadNickname(), V6LocalNameStore.Load())`
  で決める。入力ヒントも方式で切り替える (ローカル: 「最大8文字 (A-Z 0-9)」)。
- `TrySubmit()`: ローカル方式なら **通信せず** `SaveLocalName()` だけを行う。
  オンライン方式は既存の検証・送信フローそのまま。
- `SubmitCoroutine()`:
  - 送信に使う譜面版は `ResolveChartVersion(pinned, live)`。
  - 送信不可 (ローカル曲 / 譜面未取得) のときは、旧実装の `OnSubmitResult(null)`
    (= オンラインの「オフライン」判定) を呼ばず、`SaveLocalName()` に落とす。
- `SaveLocalName()`: v5 と同じ規則で正規化 → `V6LocalNameStore.Save()` →
  入力欄を正規化済みの保存名にそろえ → ローカル結果メッセージを表示。
  「オフライン」「送信」「HTTP」は出ない。
- 入力: 上限文字数を方式で切替 (ローカル 8 / オンライン 16)。ローカルでは
  v5 と同じく A-Z0-9 以外の文字を受け付けない。
- `SongOf()`: `GManager.GetSong(id)` を優先。`ResultScene` / `GameScene` では
  `PlaySessionSnapshot` の固定 (pin) した曲・譜面版が返る。
- `StartRankingFetch()`: 取得に使う譜面版も pinned 版にそろえる。
- 古いコメント (旧 `:615` 付近「見た目と開閉のみ (入力機能は未実装)」) を、
  実装 内容 (入力 / 送信 / ローカル保存 / 結果表示) に合う文言に更新。

### 1.4 譜面版 (chartVersion) の pin 元について

brief は「`V6SelectDataSource.CommitReady` が pin する既存値」と記すが、
実装を確認した結果、固定は **別経路** で行われる。

- `V6SelectDataSource.CommitReady()` は選択の確定 (曲行の決定・遷移の開始) のみで、
  譜面版の固定はしない。
- 実際の固定は既存の開始経路
  `SelectSceneManager` → `GManager.ResetSession()` → `GManager.BeginPlaySession()`
  → `PlaySessionSnapshot.Begin(row, chartVersion, chartJson)` で行われる。
  リモート曲では実際に取得した版 (`CloudChartHolder` の保持版)、ローカル曲では
  行の版で固定される。
- `PlaySessionSnapshot.PinnedSongFor(id)` は `ResultScene` / `GameScene` でのみ
  固定を優先する (`SnapshotPreferredScene`)。本実装は `GManager.GetSong()` 経由で
  この固定版を読むため、結果画面では **実際にプレイした譜面版** が使われる。

## 2. テスト

`Assets/Game/Tests/V6ResultRegistrationTests.cs` (新規, asmdef `FlashBeat.V5CanvasTests`)

| テスト | 検証内容 |
|---|---|
| `ModeOf_LocalSong_IsLocal` | ローカル曲 → `Local` |
| `ModeOf_RemoteSongWithChart_IsOnline` | リモート曲 + 版あり → `Online` |
| `ModeOf_RemoteSongWithoutChartVersion_IsLocal` | 譜面未取得は送信不可 → `Local` |
| `ModeOf_RemoteSongWithInvalidSongId_IsLocal` | 不正 songId → `Local` |
| `IsSubmittable_MatchesModeOf` | 判定の整合 |
| `LocalSave_WritesV5PlayerNameKey` | キーが `"V5PlayerName"` で、PlayerPrefs に書かれる |
| `LocalSave_RoundTripsThroughStore` | Store 経由の読み書き |
| `LocalSave_NormalizesLikeV5` | v5 と同じ正規化 (大文字化 / A-Z0-9 / 最大8 / 空は YOU) |
| `LocalSave_MaxLengthIsEight` | 上限 8 文字 |
| `LocalMessage_HasNoOnlineVocabulary` | ローカル結果に「オフライン」「送信」「HTTP」が無い |
| `LocalMessage_DefaultNameIsUsedWhenMissing` | 未保存時の既定名 |
| `InitialName_*` (4 件) | 初期値の決め方 (ローカル / オンライン / フォールバック / 空) |
| `ResolveChartVersion_*` (2 件) | pinned 優先 / 非固定なら live |
| `OnlineSubmit_KeepsFourWayDistinction` | オンライン 4 区分 (201 / 429 / オフライン / 422) が保たれる |

**ネットワーク通信は実施していない。** テストは純ロジック
(`V6ResultLogic`) と PlayerPrefs の読み書きだけを触り、
`RankingClient.PostScore` / `RankingClient.GetLeaderboard` は呼ばない。
PlayerPrefs の "V5PlayerName" は `[SetUp]` で退避し、`[TearDown]` で復元する。

実測: **18 件** (2026-11-09 の EditMode 実行で全件 `Passed`)。
内訳は §3.2 のテスト名表を参照。negative control の実測は §3.4。

## 3. 検証結果

### 3.1 compile-check (実施)

```
$ ./script/compile-check.sh --project /Users/rebui/work/FlashBeat.w240 --assembly flashbeat
compile-check: 141 sources -> /Users/rebui/work/FlashBeat.w240/FlashBeat.compilecheck.csproj
Build succeeded.

$ ./script/compile-check.sh --project /Users/rebui/work/FlashBeat.w240 --assembly tests
compile-check: 38 sources -> /Users/rebui/work/FlashBeat.w240/FlashBeat.compilecheck.csproj
Build succeeded.
```

Unity Editor は起動していない (他作業者が `Library` を所有)。
(2026-11-09 追記: §3.2 / §3.4 の EditMode 実行のためには Unity Editor を起動した。
この compile-check の記録は 2026-11-08 時点のもの。)

### 3.2 EditMode テストの実行 — **実施済** (2026-11-09)

`Assets/Game/Tests/` は asmdef `FlashBeat.V5CanvasTests`
(`includePlatforms: ["Editor"]` / `defineConstraints: ["UNITY_INCLUDE_TESTS"]` /
`precompiledReferences: ["nunit.framework.dll"]`) に属するため、**EditMode スイートに
含まれる**。#240 の新規テストも実際に実行された (TestResults-w240.xml で確認)。

```
$ /Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity -batchmode \
  -projectPath /Users/rebui/work/FlashBeat.w240 \
  -runTests -testPlatform editmode \
  -testResults /Users/rebui/work/FlashBeat.w240/TestResults-w240.xml \
  -logFile /Users/rebui/work/FlashBeat.w240/unity-w240.log
```

| 指標 | 実測値 (main 基準) | 実測値 (モーダル配置テスト追加後) |
|---|---|---|
| 対象 commit | `ab8c336` (= `origin/main`, PR #255 merge 済) | `2c163a0` (本ブランチ) |
| total | **1118** | **1120** |
| passed | **1118** | **1120** |
| failed | **0** | **0** |
| skipped / inconclusive | 0 / 0 | 0 / 0 |
| 実行時間 (`test-run` の `duration`) | 3.046 s | 3.748 s |

基準値 (`origin/main` = 1118 / 1118) からの増減は、main 基準の実行では 0、
本ブランチでは **+2** (今回追加したモーダル配置テスト 2 件)。
#240 のテストが EditMode スイートに含まれており、既存テストの期待値も
変更していない (`ClassifySubmitResult` / `SubmitStatusMessage` /
`ValidateNicknameInput` は無改変)。

#### #240 の新規テスト 18 件 (すべて `Passed`)

`FlashBeat.UI.V6.Result.Tests.V6ResultRegistrationTests`:

| テスト名 | 結果 |
|---|---|
| `ModeOf_LocalSong_IsLocal` | Passed |
| `ModeOf_RemoteSongWithChart_IsOnline` | Passed |
| `ModeOf_RemoteSongWithoutChartVersion_IsLocal` | Passed |
| `ModeOf_RemoteSongWithInvalidSongId_IsLocal` | Passed |
| `IsSubmittable_MatchesRegistrationMode` | Passed |
| `LocalSave_WritesV5PlayerNameKey` | Passed |
| `LocalSave_RoundTripsThroughStore` | Passed |
| `LocalSave_NormalizesLikeV5` | Passed |
| `LocalSave_MaxLengthIsEight` | Passed |
| `LocalMessage_HasNoOnlineVocabulary` | Passed |
| `LocalMessage_DefaultNameIsUsedWhenMissing` | Passed |
| `InitialName_LocalMode_UsesSavedLocalName` | Passed |
| `InitialName_OnlineMode_PrefersOnlineNickname` | Passed |
| `InitialName_OnlineMode_FallsBackToLocalName` | Passed |
| `InitialName_EmptyWhenNothingSaved` | Passed |
| `ResolveChartVersion_PrefersPinnedVersion` | Passed |
| `ResolveChartVersion_FallsBackToLiveVersionWhenNotPinned` | Passed |
| `OnlineSubmit_KeepsFourWayDistinction` | Passed |

#### 追加した登録モーダル配置テスト 2 件 (すべて `Passed`)

`FlashBeat.V5CanvasTests.V6ResultFramePlacementTests`
(asmdef は `Assets/Game/Tests/V6CanvasTests/FlashBeat.V6CanvasTests.asmdef`):

| テスト名 | 結果 | 検証内容 |
|---|---|---|
| `ResultModalDialog_FitsInsideSharedFrame` | Passed | 登録モーダル Dialog (840x510 @ 444,231) が共通フレーム (1728x972) 内に収まる |
| `ResultModalDialog_IsFullyVisibleAtDesignResolutions` | Passed | FHD 1920x1080 / 2K 2560x1440 / Mac 2560x1664 の 3 解像度で Dialog の矩形が画面内に収まる (クリップされない) |

### 3.3 実機での画面確認 — **未検証**

- 実 Win64 / 展示 PC での画面確認: **未検証** (展示 PC 操作は禁止)。
- 実オンライン送信の確認: **未検証** (実 API 呼び出しは禁止)。
- 代替検証: 上記の EditMode テスト (純ロジック + PlayerPrefs) と compile-check による
  静的検証。実画面での文言・導線の確認は残課題。

### 3.4 negative control — 新規テストが実際に契約を検査していることの実測

「新規テストが緑のまま (false green) では無意味」なので、`V6ResultRegistration` の
ロジックを **意図的に壊して** 新規テストが fail することを確認した。

- 壊した箇所: `Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs` の
  `V6LocalNameStore.Save()` が `V6ResultLogic.LocalPlayerNamePrefsKey`
  (`"V5PlayerName"`) ではなく **別キー** (`"NEGATIVE_CONTROL_BROKEN_KEY"`) へ書くように変更。
  (= Issue #240 の契約「v5 と同じ PlayerPrefs キー」そのものを壊す。
  変更は一時的で、実行後に必ず元に戻した。)

| 指標 | 実測値 (壊したまま) |
|---|---|
| total | 1118 |
| passed | **1116** |
| failed | **2** |
| 失敗したテスト | `V6ResultRegistrationTests.LocalSave_WritesV5PlayerNameKey`<br>`V6ResultRegistrationTests.LocalSave_RoundTripsThroughStore` |
| 失敗メッセージ | `Expected string length 7 but was 0` / `Expected string length 6 but was 0` |

壊したキーが `Load()` 側 (`"V5PlayerName"`) ではなく Save 側だけが変わったため、
「`"V5PlayerName"` に書けているか」と「Store 経由で読み書きできるか」の 2 件が
落ち、**契約に紐づく 2 件だけが fail する**ことを確認できた (他の 1116 件は緑)。
壊していない他の #240 テスト (方式判定 / 初期値 / pinned 版 / オンライン 4 区分) は
影響を受けないことも確認できた。

**復元後の再実行**: 壊した 1 行を元に戻したうえで EditMode スイートを再実行し、
**1118 / 1118 に戻る**ことを確認した (§3.2 と同じ実測値)。

## 4. Issue #240 の受入条件ごとの確認結果

Issue #240 の受入条件: 「実結果と表示が一致し、既存の保存・登録の意味を保って
操作できる。閉じるだけの登録、偽の成功、二重入力がない。3解像度で結果・ボタン・
モーダルに重なりやクリップがない。新しいゲーム集計やサーバー機能を必須にしない。」

| # | 受入条件 | 確認方法 | 結果 |
|---|---|---|---|
| 1 | 実結果と表示が一致する | `V6ResultView` は GManager / PlaySessionSnapshot の実値のみを採る (design の固定値を描画しない)。`V6ResultLogic` の採点・ゲージ・差分計算は既存 `ResultView` / `SelectViewLogic` の閾値と一致 | **コードレビュー + 既存 EditMode テスト** (`V6ResultLogicTests` 11 件 / `V6ResultSubmitTests` 10 件 すべて Passed) |
| 2 | 既存の保存・登録の意味を保つ | ローカル保存は v5 と同じ PlayerPrefs キー `"V5PlayerName"` / 同じ正規化 / 同じ上限 8 文字。オンライン送信は `RankingClient` の既存経路 (201 / 429 / オフライン / その他 HTTP) | **合格** — `V6ResultRegistrationTests` 18 件 Passed。negative control で保存キーを壊すと 2 件が fail (§3.4) |
| 3 | 閉じるだけの登録をしない | `TrySubmit()` は Local 方式で `SaveLocalName()` を呼び、Online 方式で `RankingClient.PostScore` を呼ぶ。`OpenModal`/`CloseModal` だけでは保存しない | **合格** (コードレビュー + 純ロジックテスト) |
| 4 | 偽の成功を出すがない | 送信不可 (ローカル曲 / 譜面未取得) は旧実装の `OnSubmitResult(null)` (=「オフライン」判定) を呼ばず、ローカル保存へ落とす。ローカル結果メッセージにオンライン語彙 (オフライン / 送信 / HTTP) は含まれない | **合格** — `LocalMessage_HasNoOnlineVocabulary` Passed |
| 5 | 二重入力がない | モーダル表示中は `modalOpen` で背景の Retry / 遷移入力を早期 return する。旧 `ResultRankingOverlay` は `SuspendLegacyOverlay` で停止 | **合格** (コードレビュー。実操作は未検証) |
| 6 | 3解像度で重なり・クリップがない | 共通フレームは 1:1 固定解像度 (`FrameOrigin`)。Main / 右カラム / フッター / 曲情報バンド / ランキング見出し / **登録モーダル** の各矩形がフレーム内・画面内に収まることを EditMode で検証 | **幾何は合格** (配置テスト 8 件 Passed)。**実画面のスクリーンショットは未検証** |
| 7 | 新しいゲーム集計・サーバー機能を必須にしない | 追加したのはローカル名の保存と表示の切替のみ。`GManager` / `Judge` / 履歴ストア / REST API への変更なし | **合格** (差分は UI 層と PlayerPrefs のみ) |

## 5. 残存リスク

1. **実画面での文言・導線未確認 (中)**
   ローカル曲でモーダルを開き Register を押すと「端末に保存」メッセージが出て
   入力欄が保存名になる、という導線はコード上は成立しているが、実画面では
   未確認。ローカル曲では見出しを v5 と同じ「この端末の名前」、ボタンを
   「Save」に切り替え、入力ヒントも「最大8文字 (A-Z 0-9)」に 変更する。
   design (05b 登録モーダル) の既定文言「スコアを登録」「Register」は
   オンライン曲でのみ使う。design 側にもローカル用の文言定義が無いので、
   文言の最終確定は Owner の目視確認が必要。
   (配置の幾何は 2026-11-09 の追記で EditMode 検証済み — §3.2 の
   `ResultModalDialog_*` 2 件。**文言と導線**の見た目は依然として未検証。)
2. **ローカル曲で登録モーダルを開きに行く導線 (中)**
   v6 Result の Enter / 「Op Register」ボタンから開くモーダルは、
   ローカル曲でも同じ操作で到達する (v5 の `ResultView` と同じ位置づけ)。
   ローカル曲に専用導線が必要なら design 変更が別途必要。
3. **オンライン送信の success 経路は `"V5PlayerName"` を書かない (低)**
   オンライン側で登録した名前は RankingClient 自身の PlayerPrefs キー
   `"RankingNickname"` に保存される (既存挙動)。v5 と同じ `"V5PlayerName"` は
   **ローカル方式の保存時のみ** 書く。両方を書く仕様に変更すると
   v5 の名前 (`A-Z0-9` 8 文字) とオンラインの nick (16 文字Unicode可) の
   形式が衝突するため、現状の一方向分離を採った。
4. **譜面版の pin はシーン依存 (低)**
   `PlaySessionSnapshot.PinnedSongFor` は `ResultScene` / `GameScene` でのみ
   固定を優先する。Result 画面外 (選曲など) で同じロジックを呼ぶと live 版に
   なるが、本変更の用途は Result 画面のみ。
5. **実 EditMode テスト — 2026-11-09 に実行済 (解消)**
   `origin/main` (`ab8c336`) で EditMode スイート全体を実行し **1118 / 1118**。
   #240 の新規テスト 18 件もすべて `Passed` (§3.2)。
   negative control で `V6ResultRegistration` の保存キーを壊したときは
   契約に紐づく 2 件が fail することを確認済み (§3.4)。
   残る未検証は「実画面での文言・導線」(項目 1) のみ。
6. **`Assets/Game/Tests` 内の新規テストの asmdef は既存 `FlashBeat.V5CanvasTests` を共有 (低)**
   別 asmdef を新設していないため、統合時のテスト実行で他テストと同一バイナリで
   走る (他テストと干渉しないことを推奨)。
   2026-11-09 の実測で、同一バイナリでの実行は干渉なし (1118 / 1118) を確認した。

## 6. 変更ファイル

| ファイル | 変更 |
|---|---|
| `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs` | 純ロジック追加 |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs` (+ `.meta`) | 新規 (enum + PlayerPrefs store) |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` | 方式判定・ローカル保存・pin 版の配線、古いコメント修正 |
| `Assets/Game/Tests/V6ResultRegistrationTests.cs` (+ `.meta`) | 新規 EditMode テスト |
| `specs/240-result-registration/{spec,plan,tasks}.md` | 新規 |
| `docs/evidence/240-result-registration.md` | 新規 (本ファイル) |

### 2026-11-09 追記 (本ブランチの差分)

| ファイル | 変更 |
|---|---|
| `docs/evidence/240-result-registration.md` | §3.2 に EditMode 実測値 (main 1118 / 1118、本ブランチ 1120 / 1120) と #240 のテスト名 18 件 + 配置テスト 2 件、§3.4 に negative control の実測、§4 に受入条件ごとの確認結果を追記 |
| `specs/240-result-registration/tasks.md` | 「EditMode テストの実実行」を完了に更新 |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs` | 登録モーダル Dialog の幾何 (`ModalDialogW/H/X/Y`) と `ModalDialogTopLeft()` を追加 (値は変えない) |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` | `BuildModal` が上記定数を使うように変更 (値は変えない) |
| `Assets/Game/Tests/V6CanvasTests/V6ResultFramePlacementTests.cs` | モーダル配置のテスト 2 件を追加 |
