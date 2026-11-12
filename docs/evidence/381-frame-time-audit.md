# #381 フレームレート: コード監査と計測の現状 (2026-11-12)

Issue #381 は「計測が修正より先」と定めている。本書は **Profiler による計測の代わりに
コードから確定できること** と、**まだ測っていないこと** を分けて記録する。
数値の裏付けが無いものを「改善した」と書かない。

## 1. Issue の前提の確認

| Issue の記述 | 現状 |
|---|---|
| `V6UiMode` の旧 UI 非表示処理が**毎フレーム**呼ばれている (`HideLegacy*`) | **該当する**。ただし呼び出し元は Game ではなく **Title / Select**。`V6TitleViewDriver.Update` が `HideLegacyV5Canvases()` と `HideLegacyTitleUi()` を毎フレーム呼ぶ |
| `HideLegacyPlayfield` / `HideLegacyUi` (Game) | 毎フレームではない。`V6GameRuntime.Start` の 1 回 |

つまり「毎フレームの `HideLegacy*`」は実在するが、**対象は Title / Select で、Game ではない**。
Issue の記述は呼び出し元を取り違えている。

### 1.1 毎フレーム呼び出しの中身 (コードから確定)

`V6UiMode.HideLegacyTitleUi` は毎回

```csharp
UnityEngine.Object.FindObjectsByType<Transform>(Include, None)
```

で **シーン全体の Transform を列挙**し、`HideLegacyV5Canvases` は毎回

```csharp
UnityEngine.Object.FindObjectsByType<GameObject>(Include, None)
```

で **シーン全体の GameObject を列挙**する。Title 画面のオブジェクト数に対して
毎フレーム 2 回の全走査 + 配列確保になる。

- `HideLegacyTitleUi` は `IsUnderV6Canvas` で `GetComponentsInParent<Canvas>(true)` も呼ぶ
  (名前が一致した要素ごと)。
- 戻り値は常に `true` で、呼び出し側は早期終了しない設計 (v5 が後から生成するため)。

**まだ修正していない。** これは表示の正しさ (二重表示の防止) に直結する経路で、
v5 が `Start` / `OnEnable` で自前の Canvas を後から生成する事実に依存している。
探索を打ち切ると v5 の再生成を取り逃して二重表示が戻る可能性があり、
**画面を見ない変更は入れない**。まず計測と、表示を確認できる実機での検証が要る。

## 2. コードから確定できたコスト (修正した)

### 2.1 `LateUpdate` が毎フレームシーン全体を探索していた

`V6GameRuntime.LateUpdate` は旧 v5 オブジェクトを探すために
`FindObjectOfType` と `FindObjectsOfType<Canvas>()` を**毎フレーム**呼んでいた。
キャッシュ変数が null のままになる条件 (v5 のオブジェクトが存在しないシーン) では、
**探索が永久に続く**。

- `FindObjectsOfType<Canvas>()` は毎回配列を確保し、シーン全体を走査する。
- 旧 v5 オブジェクトは `GameHudController` (実行順 100) の `Start` で作られるので、
  数フレームで見つかる。見つからないまま探し続ける理由が無い。

修正 (`Assets/Game/Scripts/UI/V6/V6GameRuntime.cs`):

- 探索に上限 `LegacyLookupFrameLimit = 60` を置いた (`mayLookUp`)。
- **隠し直しは毎フレーム続ける**: v5 は `loading.SetVisible(true)` で出し直すため、
  探索を止めても非表示は保つ必要がある。ここを巻き込むと二重表示が戻る。
- 子 `Graphic` の収集も初回 1 回だけにした (毎フレーム `GetComponentsInChildren` を
  呼ばない)。

### 2.2 計測していないこと (重要)

**この修正で FPS がどれだけ改善したかは測っていない。** 探索は 60 フレームで止まるため
定常状態への寄与は小さいと予想されるが、**予想を数値として書かない**。
`-fbperf` の `worst` / `gc0` で前後比較する必要がある。

## 3. まだ測っていないもの (Issue の計測タスク)

| # | タスク | 状態 |
|---|---|---|
| 1 | Mac 実機で Game / Select の Profiler (CPU / GPU / GC Alloc) | **未実施** (Profiler 未接続) |
| 2 | `HideLegacy*` のコスト単体計測 | **未実施** (全走査 2 回/フレームと分かったが、所要時間は未計測) |
| 3 | ノート数最大の曲 (Dogbite 603 ノーツ) のフレームタイム内訳 | **未実施** |
| 4 | Win64 (展示候補環境) の計測 | **未実施** (実機の操作許可が無い) |

## 3.5 追記 (2026-11-12): 実測ベースラインと、停止の層の切り分け

`script/mac-fbperf.sh` を追加し、`571bd357` で実測した。
詳細は `docs/evidence/381-fbperf-baseline-2026-11-12.md` と
`docs/evidence/395-stall-investigation-2026-11-12.md`。

| 指標 | 実測 (ツイッターランド / Mac 3420x2148) |
|---|---|
| 演奏中の平均 fps | 60.2 |
| p50 / p95 / p99 | 16.7 / 16.7 / 16.7 ms |
| gen0 GC (5 秒窓) | 5 回 |
| マネージド確保 | 3.5 KB/frame |
| 最大の停止 | 47.9 秒 (1 回) |

**この曲の定常状態は既に 60fps の上限に張り付いており、改善余地がほぼ無い。**
改善を主張できるのは、より重い曲 (#394 の Dogbite 603 ノーツ) か、
遷移・停止の側だけである。

47.9 秒の停止は **レンダリングのコストではない** ことがログから分かった:

- `video frame=4158` が停止の前後で不変 (映像は 1 フレームも進んでいない)
- `mediaTime` が 5.53 → 55.97 へ飛んでいる (時計だけ進んだ)
- `rms=0.0000` (音声は無音)

つまり「メディアのデコードが止まり、時計だけが進んだ」状態で、
#394 の対象 (ノーツ数依存の uGUI コスト) とは別の層。
Profiler の内訳はまだ取っていない。

## 4. 現在ある数値 (Mac のみ / 参考)

`-fbperf` による計測 (`docs/evidence/368-review-2026-11-12/README.md` §6):

| 指標 | 値 |
|---|---|
| 平均 fps (プレイ中) | 59〜60 |
| p50 | 16.7 ms |
| p95 | 17.6 ms |
| p99 | 17.8〜33.3 ms |
| gen0 GC (5 秒窓) | 4〜5 回 |
| 最大の停止 | 8.4 秒 (メディア準備完了時、t=20.04) |

**この数値は Mac (Apple GPU) のもので、Win64 実機の値ではない。** #381 の受入は
Win64 実機の計測を前提にしているため、この記録だけでは PASS にしない。

8.4 秒の停止は、その後のコミット `37ad42c4` でローディングの `RenderTexture` を
シーン離脱時に解放する修正を入れたが、**停止が解消したかは再計測していない**。

## 5. 次に必要なこと

1. Win64 実機で `-fbperf` を付けて Game / Select を各 60 秒計測する
   (展示機の操作許可が前提。オーナーへ依頼)。
2. 8.4 秒停止の再現確認と、Profiler での内訳特定 (候補: 映像プレーンの
   マテリアル生成 / シェーダ準備)。
3. 修正前後の比較は、**同じ端末・同じ曲・同じ `-fbperf`** で取る。

数値が揃うまで #381 を完了扱いにしない。
