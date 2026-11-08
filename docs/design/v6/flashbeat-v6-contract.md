# FlashBeat v6 デザイン契約 (Worker A / Issue #218)

> ⚠ **これは旧配置のミラー**。正本は [`docs/ui/v6-screen-contract.md`](../../ui/v6-screen-contract.md) (Issue #218 の Owned Path)。
> 差分は正本側のみ更新する。

他ワーカーがそのままコードへ落とせるデザイン契約。正本は
`feature/pencil-ui` @ `19f08b31a3cf269bd0921543d6bc4f8ee3b6b876` の `design/flashbeat-ui.pen` (JSON, version 2.22)。
機械可読な全量は同梱の **`flashbeat-v6-nodes.json`** (解決済みノードツリー + 変数) を参照。
本書の座標は、特記なければ各ノードのローカル座標 (親フレーム基準)。

## 1. 抽出方法 (実施記録)

1. **正本の特定**: `feature/pencil-ui` @ `19f08b31a3cf269bd0921543d6bc4f8ee3b6b876`、`design/flashbeat-ui.pen` (約 1.58 MB、JSON、version 2.22)。`git show` ダンプ (`/tmp/fb6.pen`) で読み取り専用参照。v5 資産 (`design/README.md`, `ui-size-profiles.json`) は参照しない。
2. **CLI 疎通**: `design/package.json` → `npm install` → `@pen.dev/cli 0.3.14`。
   - `pen status` → `Method CLI Key (PEN_CLI_KEY) / Status ● Active` (Infisical 経由)。
   - `pen interactive --in flashbeat-ui.pen --out /tmp/fb6-inspect.pen` (headless) → 正常オープン、`get_app_state()` 応答、`execute()` で全ノード走査可能。
3. **JSON 解析**: `.pen` はそのまま JSON。`ref` ノードは `ref: "<baseId>"` + `descendants: { "<childId>": patch }`。
   patch は (a) プロパティ上書き (`{"content": "選曲"}`) か (b) ノード全体置換 (`type`+`children` を含む)。
   解決手順: base フレームをクローンし、`descendants` を id 単位で適用する。**共通フレーム `rw7zx` だけを読んで設計判断しない** (各画面は descendants で上書きされる)。
4. **スクリーンショット**: 各対象ノードだけを含む一時 `.pen` を `/tmp` に生成し、`pen --in <node>.pen --export <out>.png` (prompt 不要 = AI 実行なし) で PNG 化。アセットの相対パス解決のため、`.pen` と同階層に `assets/` への symlink を配置。
   ジャケット画像は LFS 実体が必要なため、`feature/pencil-ui` を sparse-checkout した一時 worktree (`/tmp/fb6-pen`) から取得。

## 2. 対象ノード (v6)

| ID | 名前 | 種類 | サイズ | 内容 |
|---|---|---|---|---|
| `rw7zx` | `Frame/Shared 1728×972` | frame | 1728×972 | 共通フレーム (全 v6 画面の `ref` 先) |
| `OENRv` | `02 Select · v6` | ref→`rw7zx` | 1728×972 | 選曲画面 |
| `AqFtx` | `02b Select · v6 · 該当なし` | ref→`rw7zx` | 1728×972 | 選曲 (空状態) |
| `wSjAm` | `02c Select · v6 · 設定` | ref→`rw7zx` | 1728×972 | 選曲 (設定) |
| `q9JZcH` | `03 History · v6` | ref→`rw7zx` | 1728×972 | 履歴 |
| `S1TZD` | `04 Ranking · v6` | ref→`rw7zx` | 1728×972 | ランキング |
| `nrfw8` | `07 Game · v6 · プレイフィールド (共通フレーム)` | ref→`rw7zx` | 1728×972 | ゲーム画面 (共通フレーム) |
| `uDNC0` | `07b Game · v6 · ポーズ` | ref→`rw7zx` | 1728×972 | ポーズ |
| `g4Q3F` | `07c Game · v6 · 曲ローディング` | ref→`rw7zx` | 1728×972 | 曲ローディング |
| `Uc9v6` | `Game · HUD · FHD 1920×1080` | frame | 1920×1080 | HUD (FHD) |
| `E51yo` | `Game · HUD · 2K 2560×1440` | frame | 2560×1440 | HUD (2K) |
| `QrSwA` | `Game · HUD · MacBook Air M5 2560×1664` | frame | 2560×1664 | HUD (Mac) |
| `dRztp` | `Panel/選択中の曲 (プレイヤー)` | frame | 536×780 | 選択中の曲パネル |
| `o0zh7` | `Components v6` | frame | 5400×1100 | コンポーネント v6 |
| `kyOJx` | `Video (楽曲映像)` | frame | 1728×972 | 楽曲映像 (背景) |

`kyOJx` の中身: `#00000040` 塗り + `#00000040` 減光オーバーレイ。ゲーム画面の背景。
`o0zh7` の子: `rw7zx` (共通フレーム本体)、`dRztp` (選択中の曲パネル)、`HZ55h` (Dialog)、`PyPE1` (Component/スコアゲージ)、`HcP8N` (Component/歌詞表示枠)。

## 3. トークン (色 / フォント)

`.pen` の `variables` (全 61 個)。**実コードは `Assets/Game/Scripts/UI/V5/V5Tokens.cs` に同値が既にある** — v6 で新色トークンの追加は無し。`V5Tokens` を流用 (または薄い `V6Tokens` エイリアス) して両スタックの整合を保つ。

| カテゴリ | トークン | 値 |
|---|---|---|
| ベース | `bg` `text` `text-muted` `text-dim` `overlay` `deep` `well` `base` | `#1B1B1B` `#FFFFFF` `#B0B0B0` `#8C8C8C` `#222222` `#1A1A1A` `#131313` `#2D2D2D` |
| アクセント | `accent` `accent-tint` `accent-light` | `#3FA9F5` `#2B4457` `#9AD3FA` |
| 危険 | `danger` `danger-tint` `danger-light` | `#FF6B6B` `#4D3232` `#FFA8A8` |
| 警告 | `warn` `warn-tint` `warn-light` | `#E9B949` `#4A4029` `#F4D88A` |
| 成功 | `ok` `ok-tint` `ok-light` | `#4CC38A` `#2B4A3C` `#8FE0B8` |
| 突出グレー | `g1`…`g19` | `#1A1A1A` `#202020` `#262626` `#2A2A2A` `#2D2D2D` `#313131` `#353535` `#393939` `#3E3E3E` `#444444` `#4A4A4A` `#515151` `#5A5A5A` `#666666` `#7A7A7A` `#949494` `#B0B0B0` `#CFCFCF` `#FFFFFF` |
| 沈みグレー | `d1`…`d4` | `#222222` `#1B1B1B` `#151515` `#0F0F0F` |
| 浮きグレー | `u1`…`u5` | `#363636` `#404040` `#4B4B4B` `#585858` `#696969` |
| キー面 | `k0`…`k6` | `#131313` `#1B1B1B` `#222222` `#2D2D2D` `#3A3A3A` `#454545` `#525252` |
| ランク | `r1`…`r3` | `#373737` `#3E3E3E` `#494949` |
| フォント | `font-ui` `font-num` | `Noto Sans JP` (両方) |

⚠ `g` ランプは線形ではない (g16–g19 が急に明るい: `#949494` `#B0B0B0` `#CFCFCF` `#FFFFFF`)。補間で近似しないこと。正確な値は `flashbeat-v6-nodes.json` を参照。

## 4. 共有フレーム `rw7zx` (1728×972) — ゾーン構造

外周: stroke `#EDEDED33` sw2。

| ゾーン | xy | wh | 内容 |
|---|---|---|---|
| ヘッダー | (24,0) | 1680×96 | 左: 戻る 56×56 (y20) + タイトル (72,20) fs38 w700。右: 曲情報ゾーン (240,0) 300×96 + 状態ゾーン (540,0) 300×96 |
| メイン | (24,96) | 1680×780 | 6 列 280×780 (x=0,280,560,840,1120,1400)。各画面がここを差し替え |
| フッター | (24,876) | 1680×96 | 左: キー凡例 (0,30) gap16 3 個 (KeyCap (fill,36) `#3A3A3A` r4 [0,10] fs22 + Label fs22 `$text-muted`) + 凡例ゾーン (445,30) 240×36。右: 補助操作 (358,20) 197×56 → Button Secondary + 主操作 (567,20) 273×56 → Button Primary |
| モーダル | (0,0) | 1728×972 | **`enabled: false` (既定非表示)**。Scrim `#00000099` + Dialog (444,231) 840×510 `$k1` r16 |

共通ボタン:
- **Button Primary**: 高さ 56, `#EDEDED` r8 gap12 padding[0,24]: icon 32×32 `#1B1B1B` / Label fs30 w500 `#1B1B1B` / KeyCap `#00000026` r4 [0,8] (Key fs22 `#1B1B1B`)
- **Button Secondary**: 同構造, `#3A3A3A` 面 / Label fs30 w500 `$text` / icon・Key `$text-muted`
- **KeyCap (汎用)**: 高さ 36, `#3A3A3A` r4 padding[0,10], Key fs22 `$text-muted`
- **Dialog (曲名入力, 05b)**: Head (28,28) fs30 w700 / Name Field (28,112) 784×84 `$k0` r8 (Value fs40 w700 `$text-dim`) / Input Hints (28,208) 784×32 gap24 (KeyCap 104×32 + Hint fs16) / Summary (28,264) 784×140 `$k2` r8 (Thumb 88×50 r4 / Title fs26 w500 / Artist fs18 `$text-muted` / Grade 64×40 `$k4` r4 (fs24 w500) / Divider 752×1 `#FFFFFF1F` / Record 行 fs16–20 / Score Group (400,26) 288×52 gap14: Label fs16 `$text-dim` + Value fs34 w700 `$text`) / Buttons (28,432) 784×50 gap16 (Skip 198×50 `$k4` r8 / Register 252×50 `#EDEDED` r8、各 icon 24 + Label fs26 + KeyCap)

## 5. Select v6 (`OENRv`, 1728×972) — 3 カラム

`rw7zx` + パッチ: 全ゾーンの枠線を `#00000000` に、右上の曲情報を Search へ置換、主操作を Op·Ready へ置換、メインを 3 カラムへ置換。

```
メイン (24,96) 1680×780
├─ 曲リスト (列1-2)   (0,0)   560×780  #131313CC r16
│   Scroll Up   (210,0)   140×44  $k4 r8 (icon 24×24 $text-muted)
│   曲行        (16, 48+76k)  504×72  #1B1B1B r8 gap12 padding[0,12]  ← 選択行のみ $k6 (#525252)
│     Level 44×44 $k4 r22 (fs22 w500) / Thumb 96×54 r4 (jacket) /
│     Texts (縦 gap2): Title fs26 w500 / Composer fs20 $text-muted / Grade 56×36 $k4 r4 (fs22 w500)
│   Scroll Down (210,732) 140×44
│   ドットインジケータ (536,136) 縦 gap4: 26 個、アクティブ 10×10 $accent、非アクティブ 8×8 #FFFFFF26 (r5)
├─ 選択中の曲 (列3-4) (572,0) 536×780 → ref `dRztp` (下記 §7)
└─ Ranking (列5-6)  (1120,0) 560×780
    BEST (16,24) 528×112 縦 gap2 padding[0,16]: Label fs22 $text-dim / Score fs56 w700 lineHeight1 / Grade 72×64 $k6 r8 (fs34 w500)
    SCORE RANKING (16,152) 528×300 $k2 r8:
      Head (8,8) 496×36: Title fs18 $text-dim + Source タグ $k4 r4 [0,8] (fs18 $text-muted)
      行 (8, 92+52k) 496×44 $k1 r8 gap12 padding[0,12] (2 行目のみ $k6):
        Pos 36×36 $k4 r18 (fs22) / Name w140 fs22 $text / Value w180 fs22 $text 右寄せ / Grade 56×36 $k4 r4
    PLAYERS (16,468) 528×288 $k2 r8:
      Head (8,8) 496×36: Title fs18 $text-dim + chevron 32×32 $k4 r8 ×2 + Metric fs18 w500 $text
      行 (8, 60+52k) 496×44 $k1 r8 (4 行目のみ $k6):
        Pos / Name / Value (Ranking と同寸) + Plays w56 fs20 $text-dim 右寄せ
```

フッターのキー凡例 (Select): 決定 / 曲送り / 検索 / 絞り込み (gap24)。ヘッダー右: Search (fill,48) `$overlay` r8 gap8 [0,12] (icon 20×20 `$text-dim` + Placeholder fs22 `$text-dim` + KeyCap) + Count (140,_) 右寄せ fs22 `$text-muted` + Filter 48×48 `$k4` r8。

## 6. ゲームプレイフィールド (`nrfw8`) — 8 レーンの遠近法

`rw7zx` + パッチ: ヘッダー / フッターを `enabled: false` に、メインをゲームプレイへ置換。

**レーン線 = レーン中心線** (確定):
- 8 本のパス `Lane 0`…`Lane 7` (0,0) 1728×972, stroke `#FFFFFF2E` sw2。
- 剤線 x (y=792): **339, 489, 639, 789, 939, 1089, 1239, 1389** (150 px 間隔) / 上端 x (y=0): **801…927** (18 px 間隔)。
- **判定**: 8 本の線は 8 レーンの**中心線**。根拠:
  1. 各パスのノード名が `Lane 0`…`Lane 7` (分離線という名前ではない)。
  2. サンプルノート 8 個の中心 x が、レーン中心線の y 補間値と一致 (誤差 ≤ 1.2 px)。例: ノート (752,640) 中心 800.5 ↔ Lane 3 中心 y=640 で 801.7。
  3. 対称性: レーン幅 150 px と仮定するとフィールドは下端 x 264–1464 (1200 px) / 上端 x 792–936 (144 px) で、どちらもフレーム中心 x=864 に対して対称。境界線解釈 (8 線で 7 レーン) は成立しない。
  4. 既存エンジンのレーン配置も中心基準 (`NotesManager.cs`: `Instantiate(noteObj, new Vector3(note.block - 3.5f, 0.55f, z))`、レーン幅 1、中心 -3.5…+3.5) で、中心線モデルと一致。

| 要素 | 幾何 | 塗り |
|---|---|---|
| Judge Line | (0,792) 1728×4 | `#EDEDED` |
| Judge Line Glow | (0,796) 1728×2 | `#FFFFFF4D` |
| Note (サンプル 8 個) | 例 (715,120) 30×4、(752,640) 97×13 | `#EDEDED` |

ノートは高さ 4 px (最遠) → 13 px (判定線手前) で、遠近法でスケール。実装は y に応じて幅も補間 (中心線上のレーン幅: y=792 で 150 px → y=0 で 18 px)。

## 7. 選択中の曲パネル (`dRztp`, 536×780) `$k2` r16

| 部位 | xy | wh | 内容 |
|---|---|---|---|
| Hero | (12,24) | 512×288 | r8, ジャケット (cover) |
| Meta | (12,332) | 512×100 | 縦 gap4 中央: Title fs48 w700 lineHeight1.05 / Artist 行 gap10 (Tag `$k4` r4 [0,10] fs20 + Artist fs26 `$text-muted` lineHeight1.05) |
| Stats | (12,452) | 512×196 | `$k1` r8, 2×2 (各 256×98, gap8 padding[0,16]): BPM / NOTES (上段), LEVEL / BEST (下段), Label fs22 `$text-dim` + Value fs32 w500。Divider 縦 (256,0) 1×196 / 横 (0,98) 512×1 `#FFFFFF1F` |
| Controls | (12,668) | 512×88 | gap20 中央: skip-back 56×56 `$k4` r28 (icon 32) / play 72×72 `#EDEDED` r36 (icon 36 `#1B1B1B`) / skip-forward 56×56 `$k4` r28 |

## 8. ゲーム HUD — 3 解像度

背景: `assets/song-video-frame.jpg` (cover) + Video Dim `#00000080` (全画面)。3 フレームとも 13 子で構成同じ。

### FHD (`Uc9v6`, 1920×1080)

| 部位 | xy | wh | 内容 |
|---|---|---|---|
| Video Dim | (0,0) | 1920×1080 | `#00000080` |
| Top Shade | (0,0) | 1920×260 | `#000000A6` |
| Progress Bar | (0,0) | 1920×8 | `#000000AA` + Fill 806×8 `#EDEDED` |
| Grade | (48,48) | — | `$text` fs95 w700 lineHeight1 |
| Score Gauge | (188,48) | 600×36 | → 下記ゲージ詳細 |
| Score Label | (188,78) | — | fs22 `$text-muted` |
| Score Value | (188,100) | — | fs38 w500 `$text` |
| Menu | (1692,48) | 180×48 | `$k4` r8 gap10: Label fs22 `$text` + KeyCap `#00000040` r4 [0,10] (Key fs22 `$text-muted`) |
| Combo | (48,430) | 600×220 | Label fs22 `$text-muted` 中央 / Value fs180 w700 `$text` 中央 (y24) |
| Combo Status | (1512,460) | 360×160 | Row (0,96) 280×44: No Miss fs22 `$accent-light` (0,8) + FC チップ 150×40 `#3A3220` r4 (fs22 `$warn-light`)。Track (0,52) 280×8 `$k0` r4 + Fill 232×8 `$text` r4 |
| Combo Bonus | (1512,460) | 360×90 | Label/Value fs22 (Value `$warn-light` 右寄せ) y0 / Next Bonus y44 — **Combo Status と同座標・排他** |
| 判定行 | (0,963) | 1920×44 | レーン毎の判定テキスト fs30 `$text` 中央 (サンプル: レーン 3 x623 / レーン 6 x1155, w140) |
| Key Guide | (0,1011) | 1920×36 | 8 KeyCap 56×36 `#3A3A3A` r4 [0,10] (Key fs24 `$text-muted`) x = 310, 487, 665, 842, 1020, 1197, 1375, 1552 |

### 2K (`E51yo`, 2560×1440) と Mac (`QrSwA`, 2560×1664)

| 部位 | 2K | Mac |
|---|---|---|
| Top Shade | 2560×347 | 2560×347 |
| Progress Bar | 2560×11, Fill 1075×11 | 同 2K |
| Grade | (64,64) fs125 | 同 2K |
| Score Gauge | (251,64) 800×48 (Track 800×24, Seg 160/53/93, Fill 699×14, Best 5×24 x737) | 同 2K |
| Score Label / Value | (251,104) fs30 / (251,133) fs50 | 同 2K |
| Menu | (2256,64) 240×64 (fs30) | 同 2K |
| Combo | (64,573) 800×293, Value fs240 | Mac: (64,685) |
| Combo Status | (2016,613) 480×213 (Row y128 373×59, FC 200×53) | Mac: (2016,725) |
| Combo Bonus | (2016,613) 480×120 | Mac: (2016,725) |
| 判定行 | y1283 h59, fs40 (x880/1491, w187) | Mac: y1507 (x852/1519) |
| Key Guide | y1347 h48, KeyCap 75×48 (Key fs32) x = 529, 733, 936, 1140, 1343, 1547, 1750, 1954 | Mac: y1571, x = 464, 686, 908, 1130, 1353, 1575, 1797, 2019 |

⚠ **Key Guide と Combo Status の間隔はレーン間隔 (150 px × スケール) と一致しない** (FHD: KeyCap 間隔 ≈ 177.4、レーン 166.7; 2K: 203.6 vs 222.2; Mac: 222 ≒ 222.2)。FHD/2K は画面中央基準の等間隔配置 (FHD: 310→1608 で中心 959 ≒ 960)。実装でレーンに追従させるか画面中央の等間隔にするかは **未確定** — ゲームプレイ的にはレーン中心への追従を推奨 (キー入力はレーンに対応するため)。判断したら PR に書くこと。

### スコアゲージ (`PyPE1` / `cNEBa`, 600×36)

Track 600×18 `$k0` r2、セグメント (y2, h14): **C(0,120) B(122,120) A(244,120) S(366,120) SS(488,40) +(530,70) `#3A3220`**、Fill 524×14 `$text` r2、Best マーカー (553,0) 4×18 `$warn`。ラベル (y18, fs16 `$text-muted` 中央): C/B/A/S/SS/+。

## 9. ポーズ (`uDNC0`) / 曲ローディング (`g4Q3F`) — P1 (簡易)

### ポーズ (`uDNC0`)

`rw7zx` + パッチ: フレーム塗り `#000000CC` (暗転)、タイトルを「FlashBeat」へ、右上の曲情報を Thumb 64×36 + Song 320×80 (Title fs30 w500 / Sub fs22 `$text-muted`) + Divider 1×44 `#FFFFFF26` + Stat (LEVEL/NOTES/曲長 100×80: Label fs18 `$text-dim` / Value fs30 w500) へ置換、キー凡例を 4 個 (選択/変更/タブ切替/再開) へ置換、主操作を 選曲へ 280×56 + リトライ 220×56 `$k4` r8 / 再開 266×56 `#EDEDED` r8 へ置換、メインを タブ+設定+プレビュー へ置換:
- タブ (438,0) 824×48 gap8: Play 200×48 `$k6` r8 (fs24 w500 `$text`) / Display / Sound / Data 200×48 `$k2` r8 (fs24 `$text-muted`)
- 設定 (304,68) 512×688 `$k2` r16: Head (8,8) 496×36 (Title fs18 `$text-dim`); 行 (8, 60+76k) 496×56 `$k1` r8 gap12 [0,12]: Label w200 fs24 `$text` / Value w120 fs24 右寄せ / Prev・Next 40×40 `$k4` r8 (icon 24 `$text-muted`) — NoteSpeed (y60) / NoteTiming (y136) / Key Layout (y212) / キー設定 (y288, `$k6` 行、Prev/Next 無し)
- プレビュー (864,68) 512×688 `$k2` r16: Head (8,8) (Title fs18 `$text-dim`); Game Preview (24,60) 464×280 `$k0` r8: 8 レーンパス `#FFFFFF24` sw1 / ノート 8 個 / Judge Line (0,240) 464×3 `#EDEDED` / レーンラベル (6,246) 40 fs18 `$text-muted` 中央; キー一覧 (8,356) 34×40 `$k4` r4 (fs20 `$text` lineHeight1 中央): 1–0, -, ^ (y356) / \, Q–[ (y400) / A–; (y444) / ], Z–_ (y488); Divider (24,544) 464×1 `#FFFFFF1F` / Desc Title (24,564) fs32 w500 / Desc (24,614) 464 fs24 lineHeight1.4

### 曲ローディング (`g4Q3F`)

`rw7zx` + パッチ: タイトルゾーンを 0×0 へ、右上の曲情報を Jacket 56×56 r4 + Texts (縦 gap2: Song Title fs22 `$text` / Song Artist fs16 `$text-muted`) へ置換、フッターの決定/曲送りを 0×0 へ、補助・主操作を `enabled: false` へ。メインは共通のまま (空 6 列)。

⚠ **未確認 (デザイン側に要確認)**: ローディング進捗の表現 (バー / スピナ / テキスト) がデザインに無い。メインゾーンは空。実装者は進捗表現を決める必要がある (既存 `MusicManager` のロード状態表示に合わせるのが妥)。

## 10. 歌詞表示枠 (`HcP8N`, 1120×220) `#00000099` r8

縦 gap6 padding[16,24] 中央: Furigana fs22 `$text-dim` / Lyrics fs44 `$text` 中央 / Romaji fs20 `$text-dim` / Start Prompt gap10 (KeyCap (fill,36) + Label fs22 `$text-muted`)。

## 11. スクリーンショット

`docs/design/v6/screens/` 配下 (ノード ID + 名前):

| ファイル | ノード |
|---|---|
| `OENRv-select-v6.png` | Select v6 |
| `AqFtx-select-v6-empty.png` | Select (空状態) |
| `wSjAm-select-v6-settings.png` | Select (設定) |
| `nrfw8-game-playfield.png` | Game プレイフィールド (背景 `#1B1B1B` 合成。レーン線 `#FFFFFF2E` は実ゲームでは楽曲映像の上に重なるため、透過のままだと可読性が無い) |
| `Uc9v6-hud-fhd.png` | HUD FHD |
| `E51yo-hud-2k.png` | HUD 2K |
| `QrSwA-hud-mac.png` | HUD Mac |
| `dRztp-panel-selected-song.png` | 選択中の曲パネル |
| `uDNC0-pause.png` | ポーズ |
| `g4Q3F-song-loading.png` | 曲ローディング |
| `o0zh7-components-v6.png` | Components v6 |

## 12. 一次抽出との差分 (確定事項)

- スコアゲージは**セグメント式** (C/B/A/S/SS/+ + Best マーカー)。一次抽出では「(ref cNEBa)」のみだった。
- Combo Bonus と Combo Status は**同座標 (1512,460)・排他**。
- 判定行のサンプルはレーン 3 と 6 のみ (実装は 8 レーン全て)。
- `nrfw8` はヘッダー / フッター `enabled: false` (ゲーム中は描画しない)。
- `g4Q3F` はローディング進捗の表現が無い (メイン空)。
- Key Guide は **ASDFJKL;** 表記 (Key Layout 設定の既定値。実装は `KeyBindings` 設定に追従)。
- 選択行のハイライトは `$k6` (`#525252`) — 曲リスト 4 行目 / ランキング 2 行目 / プレイヤー 4 行目。
