# FlashBeat Unity プレイヤー 横断監査 (2026-11-12)

親 Issue: **#397**。子 Issue: **#398〜#403**。関連: #377 / #381 / #385 / #393〜#396。

この文書は **何が分かっていて、何がまだ分かっていないか** を分けて残すための正本。
Issue 本文の一次監査 (main `571bd357`) を出発点に、今回の作業で
コードを読み直して確認・更新した結果をここへ集約する。

> 数値の裏付けが無いものを「改善した」と書かない。
> 未計測は未計測と書く。旧計測値や修正済みのキャッシュを「未実装」と扱わない。

---

## 0. この監査の現在地 (サマリ)

| 区分 | 状態 |
| --- | --- |
| 資源・画面・責務マトリクス (§2) | **作成済み** |
| 静的コード監査 (読みで確定した構造) | **完了** (§3) |
| 実行時計測 (Profiler / `-fbperf`) | **未完了** (§5)。Mac の baseline と計測手順はある。差分計測は各子 Issue で継続 |
| #398 ジャケット/サムネイルの所有統一 | **実装・EditMode 検証済み** (§6) |
| #401 角丸/アイコン/フォントの共通化 | **実装・EditMode 検証済み** (§6) |
| #403 キャッシュ容量走査 | **実装・EditMode 検証済み** (§6) |
| #402 定常フレームの書戻し抑止 | **実装・EditMode 検証済み** (§6)。Profiler での rebuild 回数は未測定 |
| #400 v5/v6 二重生成の分離 | **実装・EditMode 検証済み** (§6) |
| #399 全面 Rebuild → 差分更新 | **Select のみ実装・検証済み** (§6)。History/Ranking/Option/Result は継続 |

「#397 完了」はこの表の全行が実装・検証・統合まで終わったときだけ宣言する。

---

## 1. 対象範囲

起動 → Opening → Title → Select → Ready → Loading → Game → Pause → Result →
History / Ranking / Option → 再プレイ の全経路。

対象は **Unity プレイヤー (クライアント)**。Web Studio / festival API の性能は対象外。

---

## 2. 資源・画面・責務マトリクス

「誰が持ち、いつまで生き、何で無効化されるか」を 1 行ずつ。

### 2.1 画像 / Sprite

| 資源 | 生成元 | 所有者 | 寿命 | 無効化 | 参照元 |
| --- | --- | --- | --- | --- | --- |
| ジャケット (ローカル) | `V6JacketProvider.Local` | **provider** (元は View ごと) | プロセス | `V6ContentCaches.InvalidateAll` | Select / Result / History / Ranking |
| ジャケット (crop) | `V6JacketProvider.SquareCrop` | **provider** | プロセス | 同上 | Loading |
| YouTube サムネ | `V6JacketProvider.RequestRemote` | **provider** (元は Pause が生成放置) | プロセス + 負キャッシュ 60 秒 | `DestroyAll` | Pause / Result |
| 角丸 9-slice | `V6Sprites.Rounded` | **共有 (V6AssetOwner)** | プロセス | テストのみ | v5 / v6 全部品 |
| アイコン | `V6Sprites.Icon` | **共有 (参照のみ、実体は Unity)** | プロセス | なし (不変) | v6 全部品 |
| ロゴ | `V6Sprites.FromTexture` | **共有** | プロセス | テストのみ | Title |
| v5 デコ (edge / logo) | `V5Decor.LoadSprite` → `V6Sprites.FromTexture` | **共有** | プロセス | テストのみ | Title / Opening |
| ローディング背景 (1×2) | `V6LoadingView` (`Sprite.Create`) | **View** | View 寿命 | `Dispose` | Loading |
| 動画テクスチャ (RT) | `V6LoadingView` | **View** | View 寿命 | `Dispose` (Release + Destroy) | Loading |

### 2.2 音声 / 動画 / データ

| 資源 | 取得 | 所有者 | キャッシュ |
| --- | --- | --- | --- |
| バンドル音声 | `Resources.LoadAsync<AudioClip>` | Unity | Unity の資源キャッシュ |
| YouTube メディア | `YouTubeMediaCache` | ファイルキャッシュ | 完了印 + 再取得制御 (既存) |
| 譜面メタ / 存在 | `V6SelectDataSource` | 静的辞書 | プロセス (#377) |
| メディア容量 | `MediaCacheManager.GetCacheSizeBytes` | 毎回全走査 | **無し** (#403) |
| 雲カタログ | `CloudCatalog.LoadConfig` | 呼び出し側 | 無し (#403) |

### 2.3 画面ごとの更新責務

| 画面 | 毎フレーム更新 | 全再構築の契機 | 備考 |
| --- | --- | --- | --- |
| Title | `V6TitleViewDriver.Update` → 旧 UI 隠し | なし | #396 で全走査を削減済み |
| Select | `V6SelectViewDriver.Update` | **`data.Changed` のたび `Rebuild`** | #399 |
| History / Ranking | `V6…View.Update` | **`Refresh` / `Rebuild` で全子破棄** | #399 |
| Option | なし | **`Refresh` で全ゾーン再構築** | #399 |
| Game HUD | `V6GameHudView.Update` → `Refresh` | 解像度変更で `Rebuild` | #393 / #402 |
| Playfield | `V6PlayfieldView.Update` | サイズ変更で `ApplyLayout` | フラッシュのみ時間依存 |
| Pause | `V6PauseView.Update` | 開くたび `RefreshHeader` | ジャケットは provider 経由 |
| Loading | `V6LoadingView.Update` | `ApplySong` | crop は provider 共有 |
| Result | ドライバ `Update` | `RefreshRanking` で全行再生成 | #399 |

---

## 3. 静的監査で確定したこと (読みで言い切れる)

### 3.1 既に正しいもの (再報告しない)

- `V6SelectDataSource` の譜面メタ / 存在キャッシュと `V6ContentCaches.InvalidateAll`
  の入口 1 本化 (#377) は**生きている**。Select 再訪時の再読込は 0 回。
- `MusicManager` のバンドル音声は既に `Resources.LoadAsync`。
- `YouTubeMediaCache` の完了印・再取得制御は既存で正しい。
- `V6LoadingView.Dispose` は RenderTexture を Release + Destroy する (#395 の修正)。

### 3.2 確定していた欠陥 (今回の修正対象)

| # | 事実 | 影響 | 担当 |
| --- | --- | --- | --- |
| A | `V6HistoryView` / `V6RankingView` が各々 `Dictionary<string,Sprite>` + `Resources.Load<Texture2D>` + `Sprite.Create` | 同じ画像が画面数だけ実体化。View を破棄しても Sprite は残る | #398 |
| B | `V6PauseView.FetchJacket` が独自に `UnityWebRequestTexture` + `Sprite.Create`、Texture/Sprite の所有者なし | ポーズを開くたび native 増加。Result と同時に開くと HTTP 重複 | #398 |
| C | `V6JacketResources.ClearCache` は辞書 Clear のみ (Sprite.Create の実体は放置) | 無効化しても native が減らない | #398 |
| D | `V5Ui.RoundedSprite` と `V6Controls.RoundedSprite` が同じ生成を別辞書にキャッシュ | 同半径でテクスチャ 2 枚 | #401 |
| E | `V6Icons.LoadSprite` が毎回 `Resources.Load<Sprite>` | 呼び出し回数だけ解決コスト | #401 |
| F | `V5GameUi.LoadSprite` / `V5Decor.LoadSprite` / `V6TitleView.BuildMain` が個別 `Sprite.Create` | 画面再入場で実体増加 | #401 |
| G | `MediaCacheManager.GetCacheSizeBytes` が毎回 `GetFiles(..., AllDirectories)` | Option を開くたび同期 I/O + 配列確保 | #403 |

### 3.3 確定しているが「まだ直していない」もの

- **Select の全再構築**: `OnDataChanged` が毎回 `Rebuild()`。カーソル移動 1 回で
  ツリー全体を作り直す (#399)。
- **v5 Canvas の二重生成**: `GameHudController.Start` が v6 有効でも
  `V5ReadyCanvas` / `V5OverlayCanvas` / `V5PauseCanvas` を作り、
  `V6GameRuntime` が LateUpdate で隠す (#400)。
- **定常フレームの同値書戻し**: `V6GameHudView.UpdateLaneGrids` が固定配置の
  `anchoredPosition` を毎フレーム設定。`SetAlpha` も変化なしで代入 (#402)。

### 3.4 監査で新たに気づいた点

- `V6GameRuntime.LoadJacket` は `song.id` をキーに `Texture2D` をキャッシュするが、
  これは `V6JacketProvider.LocalTexture` と同じ解決。二重のキャッシュだが
  値は同じ Texture を指すため実害は小さい。**統合は #398 の範囲で行う**
  (キーが `song.id` で一致するので provider へ寄せられる)。
- `ResultSceneManager.FetchThumbnail` は v6 有効時も走る。v6 Result は
  `V6JacketResources` でローカル画像を出すため、**遠隔取得は無駄になり得る**。
  #398 の provider に寄せて dedupe の対象にする (挙動は変えない)。

---

## 4. 共通化の設計判断

### 4.1 共通化する (データ/資源の同一性が同じ)

| 対象 | 判断 |
| --- | --- |
| ジャケット画像の取得・所有 | **する**。同じ曲/同じ映像 ID は同じ実体。画面差は表示 variant で表す |
| 角丸 9-slice の生成と実体 | **する**。半径だけで決まり、色は Image.color で付く |
| アイコンの解決 | **する**。不変アセット |
| 原 Texture → 全面 Sprite | **する**。生成条件が同じなら同じ実体 |
| Native の寿命管理 | **する**。`V6AssetOwner` / `V6AssetScope` に集約 |

### 4.2 共通化しない (意味・寿命・アルゴリズムが違う)

| 対象 | 理由 |
| --- | --- |
| v5 の角丸半径と v6 の角丸半径 | 単位が違う (FHD 実 px / design px)。**生成アルゴリズムと実体だけ**共有し、半径の解釈は呼び出し側に残す |
| 画面固有の crop (Loading 1:1 / Select 16:9) | 見た目の仕様。原 Texture は共有し、crop 矩形を variant として明示する |
| ジャケット無しの空枠 | 画面ごとのデザイン (色 $k2 / $k3)。View の判断 |
| v5 の旧 Canvas 生成 | v6 と寿命・入力経路が違う。#400 で「状態検出」と「描画」を分ける |
| 雲カタログとローカル表 | 更新契機が違う (#377 の判断を維持) |

---

## 5. 計測

### 5.1 現状の数値 (Mac / 参考)

| 指標 | 値 | 出典 |
| --- | --- | --- |
| 平均 fps (Game 定常) | 59〜60 | `docs/evidence/368-review-2026-11-12/README.md` §6 |
| p50 / p95 / p99 | 16.7 / 17.6 / 17.8〜33.3 ms | 同上 |
| gen0 GC (5 秒窓) | 4〜5 回 | 同上 |
| 最大停止 | 8.4 秒 (メディア準備完了時) | 同上 / #395 |

**Win64 実機の値ではない。** #381 / #397 の受入は Win64 実機を前提にするため、
この記録だけで PASS にしない。

### 5.2 計測手順 (再現可能な形)

```sh
# 定常フレーム (GameScene)。before/after は同じ端末・同じ曲・同じ秒数で取る。
script/mac-fbperf.sh <app> <outdir> <song> <seconds>

# EditMode テスト (コンパイル + 論理の正しさ)
Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
# 注意: -quit を付けるとテストが走らず exit 0 になる。付けるなら結果 XML を必ず読む。
```

`-fbperf` が出す値: `avgFps / p50 / p95 / p99 / worst / gc0 / gcKBPerFrame`。
GPU 時間と Canvas rebuild の内訳は Profiler (UI Details / Canvas.BuildBatch) が必要で、
`-fbperf` だけでは分からない。

### 5.3 この監査で追加した計測点 (コード内カウンタ)

画面を見ずに「再読込・再生成が増えていない」ことを確かめるためのもの。

| カウンタ | 意味 | 場所 |
| --- | --- | --- |
| `V6JacketProvider.LocalLoadCount` | ローカル画像を実際に読んだ回数 | #398 |
| `V6JacketProvider.RemoteFetchCount` | 遠隔 HTTP を実際に開始した回数 | #398 |
| `V6JacketProvider.RemoteDedupCount` | 同時要求を 1 本にまとめた回数 | #398 |
| `V6JacketProvider.NegativeCacheHitCount` | 失敗直後の再試行を抑止した回数 | #398 |
| `V6JacketProvider.CropCreateCount` | crop Sprite を実際に作った回数 | #398 |
| `V6AssetOwner.SharedCreateCount` | 共有アセットを実際に生成した回数 | #401 |
| `V6Sprites.CreateCount` | 同上 (角丸/アイコン/Texture 経由) | #401 |

これらは EditMode テストで「2 回目は増えない」ことを固定している (§6)。

---

## 6. 実施済みの改善

### 6.1 #398: ジャケット / サムネイルの取得・所有を一本化

追加:
- `Assets/Game/Scripts/UI/V6/Common/V6AssetOwner.cs`
  - `V6AssetOwner.Shared<T>(key, create)`: プロセス共有アセット。null は固定しない。
  - `V6AssetScope`: View が作った native をまとめて破棄する束。
    Dispose 後の `Track` はリークさせずその場で破棄する。
- `Assets/Game/Scripts/UI/V6/Common/V6JacketProvider.cs`
  - `Local(songId,title)`: ローカル優先。無い曲は負キャッシュ。
  - `Crop` / `SquareCrop`: **原 Texture + 矩形** をキーに共有 (再デコードしない)。
  - `RequestRemote(videoId, onReady)`: 同時要求 dedupe + 失敗の負キャッシュ
    (60 秒) + 同時接続 2 本。結果は待っている全員へ配る。

変更:
- `V6HistoryView` / `V6RankingView`: 独自 `jacketCache` を削除し provider へ。
- `V6PauseView`: 独自 UnityWebRequest を削除し provider へ。
- `V6LoadingView`: `SquareCrop` を使用。`Dispose` で provider 所有の Sprite を
  Destroy しない (他画面が白くなるため)。
- `V6JacketResources`: 対応表 (パス解決) だけを残し、実体は provider。
  既存の計測プロパティ (`CachedSpriteCount` / `TextureLoadCount`) は provider を指す。
- `V6ContentCaches.InvalidateAll`: provider の無効化も呼ぶ。

検証 (EditMode):
- `Assets/Tests/Editor/JacketProviderTests.cs` — 同一曲で同一実体 / 負キャッシュ /
  crop 共有 / cover 矩形が Loading の規則と一致 / 空 ID で HTTP を起こさない /
  View が独自辞書へ戻らないこと。
- `Assets/Tests/Editor/AssetOwnerTests.cs` — 共有は 1 回だけ生成 / null を固定しない /
  scope は自分のものだけ破棄 / Dispose 後 Track がリークしない / Release で所有移譲。

### 6.2 #401: 角丸 / アイコン / Texture → Sprite の重複解消

追加:
- `Assets/Game/Scripts/UI/V6/Common/V6Sprites.cs`
  - `Rounded(radius)`: 全画面で 1 実体。`Apply(false, true)` で CPU コピーも捨てる。
  - `Icon(name)`: 名前ごとに 1 回だけ解決。
  - `FromTexture(tex, name)`: 原 Texture と名前で共有。
  - `RoundedRadiusOf(sprite)`: 見た目の契約 (半径) を実装非依存に検査する。

変更:
- `V6Controls.RoundedSprite` / `V5Ui.RoundedSprite` → `V6Sprites.Rounded`。
- `V6Icons.LoadSprite` → `V6Sprites.Icon`。
- `V5GameUi.LoadSprite` / `V5Decor.LoadSprite` / `V6TitleView` のロゴ → `V6Sprites.FromTexture`。

**性能効果はまだ測っていない。** 効果は「同じ半径のテクスチャが 2 枚 → 1 枚」
「Title/History/Ranking を往復しても実体が増えない」という構造の改善であり、
フレーム時間への寄与は小さいと予想する (§5.3 のカウンタで確認可能)。

### 6.3 #403 (一部): キャッシュ容量の全走査

`MediaCacheManager.GetCacheSizeBytes` は呼ぶたび `Directory.GetFiles(..., AllDirectories)`
で全ファイルを列挙し `FileInfo` を作っていた。変更:

- 同じ root の結果をスナップショットとして保持し、通常の表示ではディスクを舐めない。
- 無効化はキャッシュを書き換えた側が行う (Clear / ClearAll / ダウンロード完了)。
- `ScanSizeBytes` に「常にディスクを読む」計測経路を分離。
- `SizeScanCount` で実際に舐めた回数を観測できる。

**鮮度の契約**: 外部がファイルを増やした場合、完了した側が `InvalidateSize` を
呼ぶ。呼ばなければ表示は古いまま。誤った値を素早く返すより規約を明示する。

### 6.4 #402: 定常フレームの同値書戻しを止める

`V6LayoutWrites` を追加し、`sizeDelta` / `anchoredPosition` / `alpha` / `active`
の同値代入を避ける。HUD の固定値書き込みと、プレイフィールドのレーンフラッシュを
これ経由にした。`UpdateLaneGrids` は解像度・scale・Playfield フラグ・
カメラの `hasChanged` が変わったときだけ計算する。

**時間依存の表示は止めていない** (歌詞・進捗・スコア・判定・ポップ)。
EditMode テストがその構造を固定する。

**未計測**: Canvas rebuild が実際に何回減ったかは Profiler で測る必要がある。
「同値代入をしない」規則の実装であって、性能改善の証明ではない。

### 6.5 #400: 状態検出と v5 UI 生成の分離

追加 `GamePlayPhaseService` (UI を生成しない):
- 再生パイプラインの状態判定 (`HudPlayPhase`)
- 旧 HUD の非表示 / Raycaster 無効化 / 歌詞復帰
- VideoPlayer のエラー購読

`GameHudController` は v5 の View 生成と表示制御だけを持つ。v6 有効時は
Ready / Loading / Error を作らない。v6 ポーズが無効なときだけ v5 ポーズを作る
(未移植分の役割維持)。

`V6GameRuntime.LateUpdate` から、生成されない `V5ReadyCanvas` /
`V5OverlayCanvas` の毎フレーム探索と隠し直しを削除。

追加修正: `GameSceneManager.HasV5PauseMenu` が v5 ポーズの無いシーンで
毎フレーム `FindFirstObjectByType` を続けていた。v6 構成では探索しない。

### 6.6 #399 (Select のみ): 変動ゾーンだけの差分更新

`OnDataChanged` / `Refresh` が `RefreshVolatile` を通り、変動する 4 ゾーン
(Header / Song List / Selected Song Panel / Ranking Column・Settings Column)
だけを外して作り直す。フッター (凡例・設定ボタン) は状態で変わらないので保持する。

骨格の意味が変わる操作 (クイック設定の開閉・解像度変更) だけ `Rebuild` に落とす。

計測点: `RebuildCountForAudit` (画面全体) と `IncrementalUpdateCountForAudit`
(差分)。EditMode テストが「選択変更で全体 Rebuild が 0 回」を固定する。

**History / Ranking / Option / Result は未移行** (§7)。

---

## 7. 未着手 / 継続 (子 Issue)

| Issue | 状態 | 次にやること |
| --- | --- | --- |
| **#399** Select/History/Ranking/Option/Result の差分更新 | **Select 完了 / 他は未着手** | History / Ranking の `Refresh` は `Rebuild` のままだが、ゾーン単位で外しているので次の一段は「状態で変わらないゾーン (Header/Footer) を保持」。Option は設定変更で全ゾーン再構築。Result は `RefreshRanking` が全行再生成 |
| **#400** v5 Canvas の二重生成 | **実装済み** | v6 無効 (v5) 経路の回帰確認。Ready→Loading→Play→Pause→Resume→Result/Retry の実機確認は未実施 |
| **#402** 定常フレームの書戻し | **実装済み / 未計測** | Profiler (UI Details / Canvas.BuildBatch) で rebuild 回数の before/after を取る。効果が無ければそのまま記録する |
| **#403** 容量走査・Native 寿命 | **容量は実装済み / Native 寿命は未着手** | `renderer.material` のインスタンス化、VideoPlayer / RT / AudioClip の寿命を再入場で実測する |

### 7.1 #399 の残り (History / Ranking / Option / Result)

`V6HistoryView.Refresh` / `V6RankingView.Refresh` は `Rebuild` で
Header / Main / Footer の子を作り直す。Select と同じ「変動ゾーンだけ外す」形へ
段階的に寄せられるが、**画面往復のたびに作り直す操作が無い**ため
Select ほど頻度は高くない。優先度は Select より下。

`V6OptionView.Refresh` は設定変更で全ゾーンを構築し直す。設定変更は
ユーザー操作のたびなので、値の差分反映に寄せる余地がある。

`V6ResultView.RefreshRanking` は行の全子を再生成する。Result は 1 回の
プレイに 1 回なので、頻度は低い。

### 7.2 未計測のまま残っていること

- Canvas rebuild / layout の実回数 (Profiler 未接続)
- 8.4 秒停止の再現と内訳 (#395)
- Win64 実機のすべての数値

---

## 8. 守る条件の確認

- v6 デザイン・入力・判定精度・音声映像同期を退化させない。
  今回の変更はいずれも「同じ画像を同じ見た目で使い回す」もので、座標・色・
  crop 規則は変えていない (crop は Loading の規則と一致することをテストで固定)。
- 展示候補 `b6860a80`、stable 公開物、リリースブランチは変更していない。
- Win64 実機が無いため、Mac の計測・コード改善・EditMode 検証を継続する。
  **Win64 未検証を PASS と書かない。**
