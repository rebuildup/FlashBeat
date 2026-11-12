# #396 Title/Select の毎フレーム旧UI全探索を解消 — 実装

## 1. 問題 (確認済み)

`571bd357` の時点で、Title と Select のドライバが毎フレーム全走査していた。

| 関数 | 毎フレームの処理 |
|---|---|
| `V6UiMode.HideLegacyTitleUi` | `FindObjectsByType<Transform>(Include)` = シーン全体の Transform を列挙 + 配列確保 |
| `V6UiMode.HideLegacyV5Canvases` | `FindObjectsByType<GameObject>(Include)` = シーン全体の GameObject を列挙 + 配列確保 |

呼び出し元: `V6TitleViewDriver.Update` が両方、`V6SelectViewDriver.Update` が後者。

## 2. 調査で分かったこと (重要)

**`HideLegacyTitleUi` の走査は、現行コードでは何も見つけない。**

- `TitleSceneManager` のクラスコメントに「v5 の TitleView (コード生成) は
  どこからも作られていない」と明記されている。
- `grep TitleView.Build` しても定義しか出ない (呼び出しが無い)。
- 旧 UI は **シーン配置の Canvas** だけで、それは `TitleSceneManager.BuildV6Title` が
  v6 構築時に 1 回隠している。

つまり毎フレームの全 Transform 列挙は、**存在しないオブジェクトを探し続けていた**。
これは Issue #396 の記述 (「旧v5側が Start/OnEnable で遅延生成する」) より強い結論で、
実際には遅延生成自体が起きていない。

## 3. 修正

### 3.1 v5 Canvas を生成時に登録する

`V5GameUi.NewCanvas` は v5 の全 Canvas が通る唯一のファクトリなので、ここで参照を登録する。

- `V5GameUi.CreatedCanvases` (参照の一覧、コピーしない)
- `V5GameUi.CanvasGeneration` (生成した回数)

### 3.2 隠す側は登録参照だけを見る

`HideLegacyV5Canvases()` は `CreatedCanvases` を走査するだけ。シーン全列挙も
配列確保もしない。破棄済みの Canvas は Unity の偽 null として飛ばす。

### 3.3 Title の走査は「v5 が生成したとき」だけ

`HideLegacyTitleUi()` は、`CanvasGeneration` が変わったとき (= v5 が何か作ったとき) と、
保険の定期走査 (`SafetyScanIntervalFrames = 120` フレーム ≈ 2 秒に 1 回) のときだけ走る。

走査自体を消さないのは、v5 TitleView を再び使う変更が入ったときに
二重表示を黙って作らないため。

### 3.4 起動時の 1 回だけ全走査

`HideLegacyV5CanvasesOnce()` を追加し、Title / Select の `Start` で 1 回呼ぶ。
登録を通らない旧オブジェクト (過去ビルドの残骸) への保険。

## 4. 回帰テスト

| テスト | 固定すること |
|---|---|
| `HidesTheCanvasesV5Created` | v5 が作った Canvas を隠す |
| `KeepsV6AndNonRegisteredObjects` | v6 と、ファクトリを通らない名前だけのオブジェクトは触らない |
| `LateCreatedCanvasIsHiddenOnTheNextCall` | 後から生成された Canvas も次の呼び出しで隠れる |
| `TheOnceVariantStillSweepsByPrefix` | 起動時の保険は接頭辞で走査する |
| `DestroyedCanvasesDoNotThrow` | 破棄済みの登録で例外を出さない |
| `HideLegacyTitleUi_DoesNotScanWhenNothingChanged` | 変化が無いフレームでは走査しない |
| `HideLegacyTitleUi_HidesLegacyObjectsWhenV5CreatesACanvas` | v5 生成後は走査して隠す |
| `HideLegacyTitleUi_ScansPeriodicallyAsASafetyNet` | 保険の間隔が毎フレームに近づかない |

`HideLegacyTitleUi_DoesNotScanWhenNothingChanged` は、ゲートを外すと **FAIL する**ことを
確認した (走査を毎回やると、後から置いた素の GameObject が隠れてしまう)。

既存テストのうち「V5 で始まる名前なら何でも隠す」を前提にしていたものは、
新しい契約 (v5 のファクトリが作ったものだけ隠す) に合わせて書き換えた。
これは仕様変更ではなく、**実際に隠す対象を正しくした**結果である。

## 5. 計測

Title / Select の FPS は `script/mac-fbperf.sh` では取れない (GameScene の窓しか
集計しない)。`-fbperf` は全シーンの窓を出すので、生ログから Title / Select の
`event=window` 行を読めば比較できる。

**未計測。** 修正後のビルドで Title / Select を往復し、
`[PerfProbe] event=window scene=TitleScene` / `scene=SelectScene` の
`gc0` と `gcKBPerFrame` を before/after で比べる。

## 6. 実装時に踏んだ回帰 (重要)

最初の実装は **v6 の Canvas まで「旧 v5」として登録してしまい**、ゲーム中に
v6 の HUD Canvas を無効化した。

- `V5GameUi.NewCanvas` は v5 専用ではなく、v6 も同じファクトリを使う
  (`V6GameHudCanvas` / `V6GamePauseCanvas` / `V6 Result Canvas` / `V6 Select Canvas` など)。
- 生成時に無条件で登録したため、`HideLegacyV5Canvases()` が v6 の HUD を
  `SetActive(false)` していた。
- 実測では GameScene が **avgFps 7〜8 / p50 109〜113ms** まで落ちた
  (修正前は 60.2 / 16.7ms)。

**修正**: 名前が `V6` で始まる Canvas は登録しない。

```csharp
if (name != null && !name.StartsWith("V6"))
{
    createdCanvases.Add(canvas);
}
```

回帰テスト `V6CanvasesMadeThroughTheFactoryAreNeverHidden` を追加し、
ガードを外すと FAIL することを確認した。

### この回帰から得たこと

1. **「v5 のファクトリ」は v5 専用ではない。** 共有ファクトリで「v5 だけ」を
   選別する条件を、名前以外に持っていないことが原因だった。
2. 性能改善の変更は、**測って初めて正しさが分かる**。テストが緑でも
   (この回帰はテストを追加する前は緑だった)、実機の数値が壊れていた。
3. 8.4fps を「環境負荷のせい」と片付ける前に、同じ負荷帯で before を測り直した
   ことで、**自分の変更が原因**だと分かった。負荷の記録は必須。
