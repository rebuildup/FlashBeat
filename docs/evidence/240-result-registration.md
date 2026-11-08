# Evidence: 240 — v6 Result のローカル登録契約の復元

- Issue: #240
- 作業ディレクトリ: `/Users/rebui/work/FlashBeat.w240` / branch `w/w240`
- 作業者: 実装ワーカー (Unity Editor 起動なし)

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

### 3.2 EditMode テストの実行 — **未検証**

Unity Editor の起動が禁止されているため、本ワーカーでは EditMode テストを
**実行していない**。実行は Supervisor が統合時に行う。
追加したテストは既存の `V6ResultSubmitTests` (同じ `Assets/Game/Tests/`) と
asmdef を共有しており、既存テストの期待値は変更していない
(`ClassifySubmitResult` / `SubmitStatusMessage` / `ValidateNicknameInput` は無改変)。

### 3.3 実機での画面確認 — **未検証**

- 実 Win64 / 展示 PC での画面確認: **未検証** (展示 PC 操作は禁止)。
- 実オンライン送信の確認: **未検証** (実 API 呼び出しは禁止)。
- 代替検証: 上記の EditMode テスト (純ロジック + PlayerPrefs) と compile-check による
  静的検証。実画面での文言・導線の確認は残課題。

## 4. 残存リスク

1. **実画面での文言・導線未確認 (中)**
   ローカル曲でモーダルを開き Register を押すと「端末に保存」メッセージが出て
   入力欄が保存名になる、という導線はコード上は成立しているが、実画面では
   未確認。ローカル曲では見出しを v5 と同じ「この端末の名前」、ボタンを
   「Save」に切り替え、入力ヒントも「最大8文字 (A-Z 0-9)」に 変更する。
   design (05b 登録モーダル) の既定文言「スコアを登録」「Register」は
   オンライン曲でのみ使う。design 側にもローカル用の文言定義が無いので、
   文言の最終確定は Owner の目視確認が必要。
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
5. **実 EditMode テスト未実行 (中)**
   Supervisor による統合時の実行で失敗すれば本 issue の完了判定に影響する。
6. **`Assets/Game/Tests` 内の新規テストの asmdef は既存 `FlashBeat.V5CanvasTests` を共有 (低)**
   別 asmdef を新設していないため、統合時のテスト実行で他テストと同一バイナリで
   走る (他テストと干渉しないことを推奨)。

## 5. 変更ファイル

| ファイル | 変更 |
|---|---|
| `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs` | 純ロジック追加 |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultRegistration.cs` (+ `.meta`) | 新規 (enum + PlayerPrefs store) |
| `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` | 方式判定・ローカル保存・pin 版の配線、古いコメント修正 |
| `Assets/Game/Tests/V6ResultRegistrationTests.cs` (+ `.meta`) | 新規 EditMode テスト |
| `specs/240-result-registration/{spec,plan,tasks}.md` | 新規 |
| `docs/evidence/240-result-registration.md` | 新規 (本ファイル) |
