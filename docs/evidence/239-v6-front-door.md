# Evidence — 239 [P0/v6] Title / History / Ranking UI 整合

Branch: `w/w239` / Issue: #239 (parent #217) / spec: `specs/239-v6-front-door/`

## 実装内容

### 1. Title の History / Ranking タブが出ない (監査 1)

- `Assets/Game/Scripts/UI/TitleSceneManager.cs`
  - `Start()` で `V6TitleLinks.HistoryRequested / RankingRequested` に自インスタンスのハンドラを設定 (タブはフックが設定されているときだけ描かれる既存契約を利用)。
  - `OnDestroy()` で自分が設定したフックのみ解除 (静的 Action の dangling reference 防止)。
  - `Update()` で `H` / `R` キーを poll (`KeyBindings.GetKeyDownOrInjected`)。タブの KeyCap 表記と同一のキーを poll する。
  - 遷移は `V6TitleLinks.PendingEntry` に要求を積んでから既存の `SelectScene()` (Initiate.Fade) を使う。
- `Assets/Game/Scripts/UI/SelectSceneManager.cs` `ConsumeV6EntryRequest()` (新規): `Start()` で `PendingEntry` を消費し、`History` / `Ranking` モードで初期表示する。消費後は `None` に戻す。
- `V6TitleLogic` に `HistoryTabKey`/`RankingTabKey` と対応する `HistoryTabKeyCode`/`RankingTabKeyCode` を追加し、cap 表示と poll キーを単一ソース化 (`V6TitleView.BuildTabList` と TitleSceneManager の両方から参照)。

### 2. 入口キーの不整合 (監査 2)

**決定**: タイトルは design (QJtTv) どおり History=`H` / Ranking=`R`。選曲画面内の入口は既存の `L`→History / `R`→Ranking を維持 (既存操作の保護)。spec 167 の F2/F3 は v5 向けの表記のため v6 では採用しない (キーを増やしても Select 画面の凡例に載せられないため)。

- 実装・キー操作・Title 側の凡例 (タブ KeyCap) は一致。
- **残差**: 選曲画面のフッター凡例に `L`/`R` が載っていない問題は、凡例が `V6SelectView` (Assets/Game/Scripts/UI/Select/View/, #221/#222 所有・読み取り専用) 内にあるため本 issue では修正できない。#221/#222 への調整事項 (凡例への `L: 履歴 / R: ランキング` 追加)。

### 3. ラベルと実値の不一致 (監査 3)

- `V6HistoryLogic.PlaysLabel` ("N plays") を削除し、`RecordedCountLabel` ("記録 N 曲") に置換。値は「ベスト記録のある曲数」でありプレイ回数ではない。
- プレイヤー情報パネル: 「プレイ曲」→「プレイ済み」(値は 記録曲数 / 全曲数)。
- 「順位」セル ("–位 / –人" を常時表示) は削除。総合順位を返す既存 API が無く、常時プレースホルダーの死んだ表示のため (design 差分として下記に記録)。
- プレイ情報の「HOLD」セル (常に "–") を削除。ホールド成功数の集計は既存コードに無く (Judge はホールドを先頭押下時に通常判定として加点済み、`Assets/Game/Scripts/Gameplay/Judge.cs` の `UpdateHold` / `ApplyHead`)、数値を出せない死んだセルのため。行は PERFECT / GREAT / BAD / MISS / MAX COMBO の 5 セル構成。

### 4. History の曲ランキングパネルにエラー分岐が無い (監査 4)

- `V6RankingLogic.SongRankingEmptyMessage(song, isLoading, hasError, statusMessage)` を新設。cloud 曲はデータソース (`V6SelectDataSource` — 読み取りのみ) の `IsLoading` / `HasError` / `StatusMessage` を出し分け、ローカル曲は「記録がありません」のみ (取得エラーと未記録の混同をしない)。`V6HistoryView.BuildSongRanking` の空分岐から使用。

### 5. Ranking の凡例矛盾と Online セグメント (監査 5)

- フッター凡例を「↑↓ 曲送り / Space 決定 / **S** 切替 / **F5 再試行** / Esc 戻る」に修正 (旧「Tab 切替」は実装と不一致、F5 は `HandleRankingInput` の RetryFetch に実在)。
- `V6RankingLogic.ShowOnlineSegment(song)` (cloud 曲のみ true) を追加。`V6RankingLogic.BuildScoreRanking` (view) は Online セグメントを cloud 曲のときだけ描画し、対象外の曲では表示を実効ソース Local に寄せる (`EffectiveSource()`)。セグメントの active 判定も実効ソース基準。`ToggleSource` は Online セグメントが無い曲では何もしない (見えないセグメントへの切替でキーを死なせない)。
- 空メッセージの区別は既存の `EmptyMessage` (エラー→理由 / 取得中→取得中文 / Local→記録がありません / Online→NO DATA) を実効ソースに対して適用。

### 6. PLAYERS の死んだ矢印ボタン (監査 6)

- `V6RankingView.BuildPlayers` から左右 `MakeIconButton` (onClick 未設定) とメトリックラベルを削除、`MakeIconButton` ヘルパーも使用箇所が無くなったため削除。空状態本文は `V6RankingLogic.PlayersEmptyMessage()` = 「プレイヤー別の集計には対応していません」(「NO DATA」ではなく未対応であることを明示)。

### 7. History / Ranking の設定ボタン・O キー無効 (監査 7)

- `SelectSceneManager.OnV6OpenSettings` の「Select 以外は警告して return」を削除。History / Ranking からは一度選曲モードへ戻り、既存の `v6View.OpenQuickSettings()` (クイック設定) を開く。フッターの「設定」ボタンと O キー (`KeyActions.KeyAction.Option`) の両方がこれを通る。

## 検証

### 実機プレイ: **未検証** (本タスクでは Unity Editor 起動禁止 / 展示 PC 操作禁止のため不可)

代替検証:

- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.w239 --assembly flashbeat` → **Build succeeded** (140 sources)
- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.w239 --assembly tests` → **Build succeeded** (37 sources, `Assets/Game/Tests` 全体。新しめの FlashBeat.dll を参照してコンパイルされるため、本 changeset との型整合も検証される)
- `Assets/Tests/Editor` (FlashBeat.Tests asmdef) は script の対象外のため、同一手法の一次 csproj で `dotnet build` を実行し、本変更に関わるファイル (`V6HistoryLogicTests.cs`, `V6RankingLogicTests.cs`) の型エラーが無いことを確認。`CloudCatalogTests.cs` (UnityTest 要因) と `MigrateLegacyTextJson.cs` (NoteEditor.Editor 参照) には本件と無関係の既存の参照解決エラーがあり、この環境では全体ビルドは不可 (Editor のテストランナーが正)。本 changeset 由来のエラーは無し。
- EditMode テストの実行は Supervisor が統合時に実施。

### 追加 / 更新テスト

- `Assets/Tests/Editor/V6RankingLogicTests.cs`: `ShowOnlineSegment_OnlyForCloudSongs`, `SongRankingEmpty_LocalSong_ShowsLocalState`, `SongRanking_Cloud_FetchStates` (失敗 / 取得中 / 空データの 3 分岐), `SongRanking_NullSong` を追加。
- `Assets/Tests/Editor/V6HistoryLogicTests.cs` + `Assets/Game/Tests/V6HistoryRankingTests.cs`: `PlaysLabel` のテストは誤ったラベル ("N plays") を固定していたため、`RecordedCountLabel` の期待値に差し替え (Issue の要件がラベル変更そのもののため、既存期待値の維持と両立しない)。
- `Assets/Game/Tests/V6TitleRuntimeTests.cs`: KeyCap 文字と poll 対象 KeyCode の一致 (単一ソースの不変条件) を `HistoryTabKey` / `RankingTabKey` について追加。
- 既存のレイアウト系テスト (`V6HistoryRankingLayoutTests` 10件 / `V6TitleLayoutTests` 10件) は座標・定数の固定で本変更の対象値を含まないため、変更せず。

## design 差分として記録する事項 (未確定要素の扱い)

- **PLAYERS パネル**: 総合 (プレイヤー別) 集計の REST API は現行 API に存在しない。パネル自体は design 要素として空状態のみ残し、本文で「未対応」を明示。design の左右矢印は操作先が存在しないため削除 (復活には API 設計が前提)。
- **プレイヤー情報の「順位」セル** (History): 総合順位 API が無いためセルごと削除。
- **プレイ情報の「HOLD」セル**: ホールド成功数の独立集計は既存コードに無い (ホールドは先頭押下時に通常判定として加算)。セルごと削除。
- **履歴 / ランキング画面中央の skip / play コントロール**: プレビュー再生 API がデータソースに無く、`V6SelectView` (#221 所有) と同じ装飾扱いで残置 (押しても何も起きないボタンではあるが、Select 画面と同一部品・同一扱いのため本 issue では扱わない)。**要確認**: 後日プレビュー再生を付けるか、Select 画面と一体で撤去するか。
- **Select 画面の L / R 凡例**: #221/#222 所有の `V6SelectView` のみで修正可能 (本 issue では未対応)。

## 残存リスク

- History 画面の初期選択は「記録のある先頭曲」の自動選択であり、この自動選択では `data.Select()` を通らないため、cloud 曲のオンラインランキング取得はその曲で明示的に選択し直すまで走らない (既存動作の維持。改善する場合は Rebuild 中の再入を避ける実装が必要)。表示上は取得前なので「NO DATA」ではなくローカルのベスト行 / 「記録がありません」が出るため、誤った空状態にはならない。
- Title の `H` / `R` は KeyActions の再割当て対象外の固定キー (KeyActions に項目が無い)。キー設定画面からは変更できない。
- 実機 (FHD / 2K / Mac) での文字・hitbox の確認は未実施 (上記のとおり)。フレーム配置自体は既存の 1:1 固定配置を変更していないため、レイアウト回帰のリスクは本変更では追加削除セル (順位 / HOLD / 矢印) とフッター凡例 1 項目の追加に限定される。
