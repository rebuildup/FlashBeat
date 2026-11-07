# pen デザイン実装: Unit 01 Title / Unit 02 Select

対象 Issue: #50。正本: [`design/flashbeat-ui.pen`](../design/flashbeat-ui.pen) (Unit 01 / Unit 02)。
Unity 側implemented: uGUI (Canvas + Image + TextMeshPro + LayoutGroup)、1920x1080 基準。

## 1. 実装済みの画面

| pen Unit | Unity | シーン |
|---|---|---|
| Unit 01 Title | `Canvas > BG` + `Canvas > Stage 16:9 > Edge Deco Left/Right, Bar V, Bar H, Logo, Menu, Catalog Status` | `Assets/Game/Scenes/TitleScene.unity` |
| Unit 02 Select | `Canvas > BG` + `Canvas > Stage 16:9 > Top Bar, Carousel (Card 1..5), Bottom Bar` | `Assets/Game/Scenes/SelectScene.unity` |

- `Stage 16:9` は 1920x1080 の固定 RectTransform (Canvas の中央に配置)。CanvasScaler は
  **Issue #52 の設定 (Scale With Screen Size / 1920x1080) をそのまま使う** (本 PR では
  `m_UiScaleMode` / `m_ReferenceResolution` / `m_MatchWidthOrHeight` を変更していない)。
  1280x720 実機では 0.667 倍で全体が収まる。
- 文字は `NotoSansJP-Medium SDF` (既存の日本語フォント)、色は pen の `variables` をそのまま
  使用 (`bg #1B1B1B` / `text #FFFFFF` / `text-muted #B0B0B0` / `accent #3FA9F5` /
  surface `#404040` / border `#5C5C5C` / well `#4D4D4D`)。

### Unit 01 Title (pen のノート反映)

- KEEP: ロゴ / GameStart・Option・Exit / 外周デコ / 白いバー → `Logo`, `Menu *`, `Edge Deco *`, `Bar V/H`。
- CHANGE: 各項目にアイコン + キー表記 → `Menu GameStart/Option/Exit` に play / settings / log-out
  アイコンと `KeyCap` (Space / O / E) を併記。
- NEW: カタログ状態を cloud アイコン付き → `Catalog Status` (cloud アイコン + 曲数)。
- REMOVE: Clear save data → タイトルからは削除し、**Option シーンへ移動** (下記 4 章)。

### Unit 02 Select (pen のノート反映)

- KEEP: 5枚カルーセル / Back / Ready up / ページドット → `Carousel` (Card 1..5), `Top Bar > Back`,
  `Bottom Bar > Ready Slot`, `Bottom Bar > Page Dots`。
- CHANGE: 詳細は中央カードのみ、項目名にアイコン → 中央カード (Card 3) のみに
  Stat BPM (activity) / TotalHits (target) / SongLevel (signal) / Hiscore (trophy) を表示。
  左右のカードは No. / サムネイル / 曲名 / 作曲者 / Lv のみ。
- NEW: 右上のクイック設定ボタン (sliders アイコン + Tab) → `Quick Settings Button`。
- REMOVE: 左右カードの ◁▶▷ (押せない) → 左右カードには矢印操作を置かない。

## 2. データと操作

- データモデルは `GManager` の静的フィールドのまま (Issue #52 の `GManager.ActiveSongs` /
  `HasRemoteCatalog` を使う)。`SelectSceneManager` は Issue #52 の Cloud Catalog 再取得ループと
  remote 曲スタート処理 (`CloudChartHolder` / `StartRemoteSong`) をそのまま保持し、UI 部分は
  `SongCardView` (5 枚) + 中央カードの詳細行に差し替えた。
- ページドットは 1 曲 1 個。`Page Dots` に inactive な `Dot Off` / `Dot On` テンプレートを置き、
  実行時に `GManager.totalSong` 個生成して選択中だけ塗りつぶす。
- キー操作は従来どおり (Space=決定, ←/→・A/D/S/F・J/K/L/; = 曲送り, Esc=タイトル, E=終了)。
  Tab はクイック設定の入口として Option シーンへ遷移する (4 章参照)。

## 3. 追加アセット

`Assets/Game/Art/UI/` に pen の lucide アイコンを同サイズのスプライトとして生成した
(白 64px PNG、`spritePixelsToUnits: 1` で 1px = 1 UI 単位):

- `Icons/`: play / settings / log_out / cloud / arrow_left / sliders / signal / activity /
  target / trophy / chevrons_right / skip_back / skip_forward
- `Sprites/`: `ui_round_r6` (KeyCap) / `ui_round_r8` (MenuButton) / `ui_round_r10`
  (クイック設定) / `ui_round_r24` (Status Chip のカプセル) / `ui_round_r39` (再生ボタン円) /
  `ui_dot` / `ui_ring` (ページドット)

角丸は 9-slice (`spriteBorder`) で、参照解決時も radius が保たれる。
これらのスプライトは lucide アイコンの絵意的再現であり、実アートの SVG に置き換えてよい。

## 4. 意図的な逸脱と引き継ぎ

1. **Unit 02b (クイック設定パネル) は未実装。** pen の Unit 02 は「Tab でクイック設定を開く」と
   しているが、開いた状態は別 Unit (02b) のため、`Quick Settings Button` と Tab は既存の
   OptionScene へ遷移する。02b (MainVolume / NoteSpeed / NoteTiming / 歌詞 / Flash) は別 Issue。
2. **Prefab 化していない。** 作業手順で Unity Editor を起動できなかったため、pen の
   `Prefab/*` コンポーネントはシーン階層上に同名 (KeyCap / LegendItem / MenuButton 相当 /
   StatRow 相当 / Card) として組み込んだ。Editor が使える状態になったら、この階層を
   そのまま prefab 化できる。
3. **カタログ表示の文言。** pen は「同期済み · 42曲」だが、Unity 側は
   `GManager.HasRemoteCatalog` が true のときだけ「同期済み · N曲」、それ以外は
   「ローカル · N曲」と表示する (42 は pen のプレースホルダ)。
4. **セーブデータ削除ボタン。** pen の指示に従い Title から Option へ移した (見た目は従来)。
   Option 画面そのものは Unit 08 として未改修。

## 5. 検証手順 (Windows / w22)

1. Unity 6000.5.6f1 でプロジェクトを開き、TitleScene / SelectScene を開く。
2. TitleScene を Play: タイトル・メニュー 3 項目・cloud アイコンの曲数表示・外周デコが出る。
   Space / O / E とクリックで各遷移、Esc 相当で Option へ。
3. SelectScene を Play: 5 枚のカード (中央だけ大きい) / Back / クイック設定 / ページドット /
   Ready up が出る。左右キーでカードとドットが動き、Space で GameScene へ。
4. 1280x720 と 1920x1080 のウィンドウサイズで、Stage 16:9 が収まって見えること。
