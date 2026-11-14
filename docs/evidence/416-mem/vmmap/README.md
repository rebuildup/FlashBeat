# #416 Native メモリ内訳 — vmmap 実測 (macOS)

Unity Memory Profiler パッケージは未導入のため、プロセス外計測 (`/usr/bin/vmmap -summary`) で
Native 側のリージョン内訳を取得した。目的は「シーン巡回・モード遷移で Native footprint が
単調増加しないこと」と「メモリの支配的リージョンが何か」の確認。

## 方法

- ビルド: `Builds/macOS/FlashBeat.app` (main 系、v6mem ハーネス入り)
- 起動: `-autotest v6mem -autotest-rounds 6 -autotest-games 0 -autotest-songs "ツイッターランド"`
  (UI シーン巡回 + History/Ranking サブモード往復 + キャッシュ/遠隔プローブ。ゲームプレイなし)
- 計測: プレイ中の pid に対し 15 秒間隔で `vmmap -summary <pid>` を 8 回実行
- 生データ: `vmmap/sample-1.txt` 〜 `sample-8.txt`

サンプルと census の対応 (Player.log の `[V6Mem] census` 直前ラベル):

| sample | 直前 census | scene |
|---|---|---|
| 1 | selectscene-0 | SelectScene |
| 2 | selectscene-1 | SelectScene |
| 3 | optionscene-2 | OptionScene |
| 4 | optionscene-3 | OptionScene |
| 5 | titlescene-5 | TitleScene |
| 6-8 | submode history/ranking/selectback ループ | SelectScene |

## 結果

**Physical footprint は全サンプルで 991〜1000 MB に収まり、単調増加なし。**

| sample | footprint | IOAccelerator (graphics) resident | IOSurface resident | Untagged resident | Malloc Small resident |
|---|---|---|---|---|---|
| 1 | 1.0G | 168.6M | 113.3M | 77.4M | 81.8M |
| 2 | 1.0G | ~168M | 113.3M | 84.3M | ~81M |
| 3 | 998.3M | ~155M | 113.3M | 83.9M | ~27M |
| 4 | 991.4M | 155.0M | 113.3M | 54.7M | 27.3M |
| 5 | 1.0G | ~153M | 113.3M | 53.4M | ~20M |
| 6 | 997.0M | ~153M | 113.3M | 49.5M | ~20M |
| 7 | 995.0M | ~153M | 113.3M | 58.6M | ~20M |
| 8 | 996.1M | 153.2M | 113.3M | 38.7M | 20.2M |

(カラムは vmmap -summary の SIZE 列のうち resident 相当。`Malloc Small` は圧縮・退避で
resident がサンプル間で変動するが、footprint 合計は安定。)

## 内訳の解釈

- **IOAccelerator (graphics)**: 仮想 ~723-739MB、resident ~153-169MB — GPU/Metal 関連。
  動画再生パイプラインと RenderTexture (`TempBuffer 3420x2148` 等、census の rt=190MB と整合)
  が支配的。
- **IOSurface**: resident 113.3M 一定 — デコード済みフレーム/テクスチャの共有メモリ。
- **Untagged**: Unity ネイティブ (mono/エンジン) 領域。resident 38-84M で変動するが累積傾向なし。
  `Untagged (media)` は 10MB 固定。
- **Malloc ゾーン**: Small resident は初期 81.8M → 20M 台へ低下 (起動直後の一時確保が
  圧縮・解放されたもの)。Large は 2.3M 固定。
- **__TEXT/__LINKEDIT**: コードマッピング主体で resident は実行ページ分のみ。

## 結論と限界

- UI シーン巡回・History/Ranking サブモード往復・Option/Title 再入場を通じて
  **Native footprint は ~1.0GB で安定**。リーク兆候 (単調増加) は観測されない。
- managed 側 (monoUsed ~7MB) と合わせ、#416 の「再入場・シーン破棄で資源が戻る」条件と整合。
- **限界**: vmmap はリージョン粒度までで、個別オブジェクト単位の帰属はできない。
  Unity Memory Profiler (`com.unity.memoryprofiler`) によるヒープスナップショット比較は
  パッケージ未導入のため未実施 — 導入する場合は manifest 追加 + 開発ビルドでの
  `TakeSnapshot` が必要。オブジェクト単位の内訳が必要になった時点で別途実施する。
- 本計測は macOS のみ。Win64 での同等計測は未実施 (Windows 実機待ち)。
