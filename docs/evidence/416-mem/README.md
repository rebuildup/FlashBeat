# #416 Native 資源の寿命・再入場メモリ — macOS 実機計測証跡

計測日: 2026-11-14 / ビルド: macOS `FlashBeat.app` (Unity 6000.5.6f1)
ハーネス: `AutoTestV6Mem` (`-autotest v6mem`) + `script/mac-v6mem.sh`
(ブランチ `w/416-memprobe`。修正検証ビルドは `w/416-optionview-dispose` の
`OptionSceneManager.OnDestroy` を working tree に載せたもの)。

## 計測方法

プレイヤー引数 `-autotest v6mem -autotest-out <dir>` で以下を 1 実行で通す:

1. UI 巡回: `TitleScene → SelectScene → OptionScene → SelectScene` を
   `-autotest-rounds` 回 (`-autotest-ui` で対象シーンを絞れる)
2. プレイ再入場: `-autotest-games` 回、選曲 → Ready → GameScene → 再生 →
   ResultScene (`-autotest-songs` で曲指定)
3. 遠隔ジャケットキャッシュ: `SeedRemoteEntryForAudit` で capacity+10 件を
   注入し LRU 退避と `DestroyAll` の解放を確認

各チェック点で `GC.Collect` 後に以下を記録 (`[V6Mem] census` / `[V6Mem] types`):

- `GC.GetTotalMemory` / `Profiler.GetMonoUsedSizeLong` (マネージド)
- `Profiler.GetTotalAllocatedMemoryLong` / `GetTotalReservedMemoryLong`
- `Texture2D / Sprite / Material / RenderTexture / AudioClip / Mesh /
  GameObject` の個数と `Profiler.GetRuntimeMemorySizeLong` 合計
- 全 `UnityEngine.Object` の型別個数
- `V6JacketProvider` / `V6AssetOwner` / `V6Sprites` / `MediaCacheManager`
  の監査カウンタ

注意: `FindObjectsOfTypeAll` は**生きている**オブジェクトしか返さない。
シーン破棄済みだがマネージド側だけ残るオブジェクト (static イベント等に
掴まれたビュー) は個数系に出ない — 今回のリークはまさにその形で、
monoUsed だけが増え続けた。

## 実行と結果

| ディレクトリ | 内容 | 結果 |
|---|---|---|
| `1ead8dd9/` | 全巡回 3 周 + 2 曲プレイ (ツイッターランド / まにまに) | 下記「観測」参照 |
| `1ead8dd9-rounds8/` | UI 巡回 8 周 + 1 曲 | managed が周回ごとに約 +1.9MB 線形増加 (頭打ちなし) |
| `7ca9e80-types/` | 型別集計入り 6 周 + 1 曲 | 生存オブジェクトの個数は頭打ち → 増分は生存オブジェクトではない |
| `a404a0f-types/` | 全型個数集計 6 周 + 1 曲 | 同上を全型で確認 (差分ほぼ無し) |
| `bisect-title/` | TitleScene のみ 10 周 | **頭打ち** (3.3MB 一定) |
| `bisect-select/` | SelectScene のみ 10 周 | **頭打ち** (6.9MB 一定、初回のみ +0.6MB) |
| `bisect-option/` | OptionScene のみ 10 周 | **リーク**: 6.1MB → 22.2MB、+1.8MB/ロードで線形 |
| `fix-option-verify/` | 修正込みビルドで OptionScene 10 周 | **頭打ち** (6.1MB 一定) — 修正を実機確認 |

## 観測と原因

### リーク (修正済み → PR #460)

`V6OptionView.Create` が静的イベント `KeyBindings.Changed` に
`view.OnBindingsChanged` を購読するが、解除は `Dispose()` の中だけで、
`OptionSceneManager` は `Dispose` を呼んでいなかった。シーンを抜ける
たびに破棄済みビューのマネージド側 (UI グラフ全体) がイベントに
掴まれたまま残り、**OptionScene 1 ロードあたり約 1.8MB が単調に増加**
した。修正は `OptionSceneManager.OnDestroy` で `optionView.Dispose()`
を呼ぶ 13 行。

bisect の数値 (monoUsedMB、OptionScene のみ 10 周):

```
修正前: 6.1 → 7.9 → 9.7 → 11.4 → 13.2 → 15.0 → 16.8 → 18.6 → 20.4 → 22.2
修正後: 6.1 → 6.1 → 6.1 → 6.1 → 6.1 → 6.1 → 6.1 → 6.1 → 6.1 → 6.1
```

### 問題なしと確認できたもの

- **UI 再入場 (Title / Select)**: 生存オブジェクト個数・reserved が
  周回で頭打ち。ジャケット等のキャッシュは曲数で頭打ち。
- **ゲーム再入場 (2 曲)**: GameScene 突入時に増える RenderTexture
  (動画用) と VideoPlayer 系は Result 復帰で解放され、2 曲目で
  reserved / 個数は 1 曲目と同じ水準に収束。判定も全ノーツ完走
  (judged=130 / 76、完了)。
- **遠隔ジャケットキャッシュ**: `RemoteCacheCapacity=50` を 60 件注入で
  `evictedCounter=10`、退避された Texture2D は Unity null 判定で
  実際に破棄 (`texturesDestroyed=10`)。`DestroyAll` で残り 50 件も
  全解放 (tex2d 116→66、sprite 49→38、cached=0)。
- **MediaCacheManager**: `GetCacheSizeBytes` は TTL 30 秒の
  スナップショットで、満了時に同期再走査 (`SizeScanCount` 増加)。
  キャッシュ約 144MB / 計測中の再走査で顕著な停止は観測されず。
  大容量時のコールドパスは引き続き観察対象。

### 残課題

- `reservedMB` はゲーム初回後に 179.8→199.8MB へ一段上がるが、
  2 曲目以降は横ばい (単調増加ではない)。Unity 内部の動画デコーダ
  等の保持と見られる。長時間・多曲での再確認は #397 側で扱う。
- `V6GameHudView` の `KeyBindings.Changed` 購読が OnEnable/OnDestroy で
  非対称 (SetActive 往復で重複購読になり得る潜在的な非対称。
  現行経路では発火しない)。フォローアップ候補。
- Memory Profiler パッケージの GUI スナップショットは自動化できない
  ため、本計測は Profiler API + オブジェクト列挙で代替した。
  「生きているオブジェクト」と「マネージドにだけ残るもの」の区別は
  この 2 系統の差分で行った。
