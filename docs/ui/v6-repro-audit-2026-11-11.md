# v6 UI 再現品質の再評価 (2026-11-11)

対象: `feature/pencil-ui` @ `19f08b31a3cf269bd0921543d6bc4f8ee3b6b876` の
`design/flashbeat-ui.pen` を正本とし、v6 UI の再現が画面・情報・操作として成立しているかを
再評価した記録。**正本 `.pen` は変更していない** (Pen.app で開いたのは `/tmp/fbpen/` の複製)。

- 正本画像: Pencil MCP `Export` で各ノードを PNG 化 (`scale: 1`)。`design/exports/v6/screens/`
  の既存書き出しと SSIM 0.9952 で一致することを確認済み (書き出しは現行 `.pen` と同一)。
- 実機画像: PR #353 の 16 枚 (2560×1440 / 1920×1080) と非公開の連続録画。
- 比較は「ピクセル一致」ではなく、**領域・情報・階層・操作**で行った。動的な数値
  (SCORE / COMBO / ランキング順位) は値の一致を求めない。

## 0. 結論

再現は「概ね似ている」段階で止まっており、**情報の有無と土台の契約が設計と一致していない**。
個別の座標ズレではなく、次の 3 つが共通原因である。

1. **契約が Result / Title / Option / Pause を定義していない**。`docs/ui/v6-screen-contract.md`
   は Select と Game HUD しか画面別幾何を持たず、他画面は共有フレーム §4 の「6 列 280」を
   そのまま流用して実装された。Result の 5 列設計はこの流用で壊れた (§4)。
2. **デザインの `enabled: false` / 表示ゾーンがコードに写されていない**。Title は
   デザインで非表示のフッター右ゾーンを描き、逆に表示されているタブ行と凡例を消している。
3. **共通部品が画面ごとに写経されている**。戻るボタンの配置、ジャケット解決、キー凡例、
   空状態が各 View に重複し、片方だけ直る/壊れる構造になっている。

したがって修正は「画面ごとの座標直し」ではなく、**共通契約 (戻る/ヘッダー、表示状態、
ジャケット、空状態) を先に直し、各画面へ伝播**させる。3D プレイフィールドは
オーナー承認済みの意図的例外として変更しない (`V6UiMode.Playfield = false` を維持)。

## 1. 画面別の一致 / 不一致

| 画面 | 一致している点 | 不一致 (根拠) |
|---|---|---|
| **Title** (`QJtTv`) | ロゴの位置・大きさ、ヘッダー左上の戻る領域、背景アニメ | ① タブ行 (`FHcxk` 4 個: GameStart/History/Ranking/Option) が無い (`V6UiMode.TitleTabs=false`)。② 左下凡例 (`QQ4jM`) が無い (`TitleLegend=false`)。③ デザインで `enabled:false` のフッター右ゾーン (`T8HuQS`) の 設定/START を描いている。④ ヘッダー右が設計の「更新して再起動 F3 + v1.1.0 → v1.2.0」ではなく「自己ベスト/曲名/アーティスト/版」。⑤ デザインで非表示のタイトル文字 (`ci867`) を表示。⑥ Exit は設計 162×56 + "Exit" ラベル、実装は 56×56 + 縦積み。 |
| **Select** (`OENRv`) | 3 カラム構成、中央パネル (ジャケット/曲名/BPM/NOTES/LEVEL/BEST/再生 3 ボタン)、右上検索、右ランキングの枠 | ① **戻るボタンが設計 (24,20) に無い**。`HeaderBackButton()` は (0,20,56,56) を返すが、`BuildHeader` が `At()` を呼んでおらずヘッダー中央 (≈x 812) に落ちる。② リスト先頭行が設計 y=144 ではなく約 4 行下から始まる (スクロール領域の原点)。③ 凡例のキーが設計 (A-Z 検索 / Tab 絞り込み) と違う (Tab 検索 / F 絞り込み)。④ 設計に無い "1 / 28" を表示。⑤ ランキング 2 枚が「データがありません」。 |
| **Select 空状態** (`AqFtx`) | — | 未撮影。空状態の文言・行テンプレートの扱いが未定義 (§7-C6)。 |
| **Select 設定** (`wSjAm`) | — | 未撮影。 |
| **History** (`q9JZcH`) | View/Logic は存在 | 実機未撮影。Title タブから到達できない (SelectScene 内にのみ生成)。 |
| **Ranking** (`S1TZD`) | View/Logic は存在 | 同上。列・行テンプレートの一致は未確認。 |
| **Loading** (`g4Q3F`) | パネル位置 | ジャケットが出ない (§4-②)。実機未撮影。 |
| **Game HUD** (`Uc9v6`/`E51yo`/`QrSwA`) | グレード / ゲージ / SCORE / COMBO / 下部キー / メニュー | ① メニューに設計の Esc キャップが無い。② 右側の `+ COMBO BONUS` / `NO MISS` / `FULL COMBO` ブロックが無い (`V6UiMode.VerdictRow=false` と関連)。③ 歌詞枠の幅・位置が設計 `HcP8N` と一致するか未検証。3D プレイフィールドは意図的例外。 |
| **Pause** (`uDNC0`) | 左パネル/右キー割当の骨格、LEVEL/NOTES/曲長、フッター右 3 ボタンの並び | ① 背後の HUD (SCORE/ゲージ/COMBO/キー) が透けたまま。② 左パネルに **Key Layout と キー設定 (F2) の行が無い** (設計は 4 行)。③ 曲情報 (ジャケット+曲名+アーティスト・BPM) がタブに隠れる。④ フッターの "Backspace" / "リトライ" が折り返して重なる。⑤ 凡例キーが設計と違う。 |
| **Result** (`zcSHh`) | グレード/SCORE/前回最高、ゲージ、判定 5 列、曲情報、右 2 パネル、フッター 3 ボタン | ① **メインが 5 列設計 (x=164, w=1400) ではなく 6 列 (x=24, w=1680)**。右パネルは設計 x=1020 に対し x=1176。② 曲情報のジャケットが空枠。③ ランキングが "NO DATA"。④ グレードの表現 (設計は巨大ウォーターマーク)。 |
| **Result 登録モーダル** (`lqHaN`) | Dialog 840×510、Head/Name/Summary/Buttons | 実機未撮影 (キー設定ダイアログのみ撮影済み)。 |
| **Option Play/Display/Sound/Data** (`B6If6Z`/`EzLfb`/`z7joSy`/`DQHHp`) | タブ位置、左右パネル位置と幅、設定行、右パネルの説明文 (選択行に対応) | ① レーン図が設計の平面 8 レーン (細線 + ノート) ではなく、灰色の 3D コーン状の帯。② キーキャップ行が設計の均等 4 行ではなく、行ごとに減る左詰め。③ 右パネル下部の見出し (設計は太字「キー設定」) が無い。 |

**未検証 (実機画像が無い)**: History / Ranking / Loading / Select 空状態 / Select 設定 /
Result 登録モーダル / 1920×1080 と Mac でのレイアウト。これらは修正後に新ビルドで撮影する
(既存の PASS を設計受入の代用にしない)。

## 2. オーナー指摘の再現結果

| 指摘 | 再現 | 原因 (コード) |
|---|---|---|
| Esc ボタンが本来の位置から大きく外れている | **再現**。設計 (24,20) に対し、Select はヘッダー中央 ≈x812 に描画 | `V6SelectView.BuildHeader` が `HeaderBackButton()` を取得しても `At(back, …)` を呼んでいない。`V5Ui.Panel` は既定で親中央に置くため中央へ落ちる |
| 表示されるはずのない枠が描画されている | **再現**。Title のフッター右ゾーンは正本で `enabled:false` なのに 設定/START を描画。Title のヘッダータイトルも正本で非表示 | `V6TitleView.BuildFooter` が表示状態を見ずに生成。`V6UiMode` に「正本の enabled」を表す概念が無い |
| Result は 5 列設計なのに 6 列で実装 | **再現**。正本 `zcSHh`: Main (164,96) 1400×780 / 5 列 280、右パネル (856,24) 528。実装: Main (24,96) 1680 / 6 列、右パネル x=1152 | `V6ResultLogic.MainX/MainW/RankX` が共有フレーム値をそのまま使用。コード注釈は「旧実装は 164/1400 に狭めていた」と、正しい設計を回帰として扱っている |
| 曲サムネイルが表示されない | **再現** (Result / Loading)。Select は表示される | `V6ResultView.JacketResourcePathOf` と `V6LoadingViewLogic` が拡張子付き (`V6/select/jacket/01-rosen.jpg`) を `Resources.Load` に渡す。`Resources.Load` は拡張子不可。Select だけ `ResourcePathWithoutExtension` を通している |
| Title の情報が設計と違う | **再現** (§1)。タブ/凡例の欠落 + 非設計情報の追加 + 非表示要素の表示 | `V6UiMode.TitleTabs/TitleLegend=false` (2026-11-08 のオーナー指示) と、フッター右ボタンによる「二重表示」の回避が根拠。ただし正本ではフッター右が非表示・タブ/凡例が表示なので、**入れ替わっている** |
| Option のレーン表示・配置が大きく崩れている | **再現**。3D コーン状の帯 + 左詰めのキー行 | レーン図の描画が独自実装。キー行は実 `KeyBindings` から生成するが、行の折返し幅を設計の 13 キャップ均等にしていない |
| その他の設計/情報/操作の不一致 | **再現** (§1) | 共通原因 C1–C6 (§3) |

## 3. 共通原因

- **C1 契約の欠落**: `docs/ui/v6-screen-contract.md` に Title/Result/Option/Pause/History/
  Ranking の節が無い。§4 の「各画面がここ (Main 6 列) を差し替え」という但し書きが、
  実装では「Main をそのまま使う」と読まれた。
- **C2 戻る/ヘッダーの共通部品が無い**: Title は `V6TitleLogic.BackButtonPos`、Select は
  `HeaderBackButton()` + 手動 `At()`。後者だけ配置漏れ。Result/Pause/Option も別実装。
- **C3 表示状態 (enabled) の契約が無い**: 正本の `enabled:false` をコードに写す仕組みがなく、
  Title のフッター右とタイトル文字が「なんとなく」描かれている。
- **C4 ジャケット解決の三重実装**: `V6SelectDataSource` / `V6ResultView` / `V6LoadingViewLogic`
  が同じ対応表を持ち、拡張子の扱いだけが違う。
- **C5 v5/v6 の二重生成**: Pause で旧 HUD を隠しきれていない。Title は毎フレーム名前一致で
  旧 UI を隠す方式で、取りこぼしが起きやすい。
- **C6 空/ローディング/失敗状態が設計に無い**: 各画面が "NO DATA" / "データがありません" /
  "ランキングのデータがありません" を別々の位置・高さで出し、パネルの構成が変わる。

## 4. 今すぐ直す項目と依存関係

| 順 | 項目 | 依存 | 受入 |
|---|---|---|---|
| 1 | **ジャケット解決の一本化** (`V6JacketResources` + 拡張子除去 + 3 画面で共用) | なし | EditMode: パスに拡張子が無い / 全曲ロード可。Result・Loading の実機画像 |
| 2 | **戻るボタン共通化** (設計 (24,20) 56×56 を 1 箇所で配置) と Select の配置漏れ修正 | なし | Select 実機画像で Esc が左上 |
| 3 | **Result 5 列化** (Main 164/1400、右パネル 856/528、判定・曲情報を 840 幅で維持) | 1 | EditMode: 定数と配置。Result 実機画像 |
| 4 | **Title の表示状態を正本に合わせる** (タブ/凡例 ON、フッター右/ヘッダータイトル OFF) | 2 | Title 実機画像 |
| 5 | **Option レーン図とキー行** (平面 8 レーン、13 キャップ均等 4 行、見出し) | なし | Option 4 タブの実機画像 |
| 6 | **Pause** (旧 HUD を隠す、Key Layout/キー設定行、曲情報、フッター折返し) | 2,4 | Pause 実機画像 |
| 7 | **Game HUD** (メニューの Esc、COMBO BONUS ブロック、歌詞枠) | なし | プレイ中画像 |
| 8 | **空状態の共通化** (ランキング/履歴/検索 0 件) | 1,3 | 空状態の実機画像 |
| 9 | History/Ranking/Loading/登録モーダルの実機撮影と差分修正 | 1–8 | 各画面画像 |

## 5. Issue の扱い

再利用 (既存):
- #217 v6 並列移植 (umbrella) / #221 Select / #222 Select 結線 / #224 Pause・Loading /
  #239 Title・History・Ranking / #240 Result / #241 Option / #242 実機受入 / #167 描画だけの UI。

新規 (共通土台と、画面 Issue に属さない横断欠陥):
- **共通フレーム契約の再定義** — 戻るボタン、正本 `enabled`、v5 混在、重複生成。
- **ジャケット解決の一本化** — 3 箇所の対応表と拡張子バグ (Result/Loading)。
- **Result 5 列の回帰** — 6 列化と、正しい設計を回帰と書いた注釈。
- **Title の表示状態逆転** — タブ/凡例 OFF・非表示ゾーン ON。

画面別の修正は #221/#224/#239/#240/#241 にぶら下げ、重複 Issue は作らない。

## 6. 並列分担と排他

| 担当 | 所有ファイル | 備考 |
|---|---|---|
| 共通土台 | `Common/V6JacketResources.cs`, `Common/V6Frame.cs`, `V6UiMode.cs`, `docs/ui/v6-screen-contract.md` | レイアウト/シーン/プレハブの排他所有 |
| Title | `Title/*`, `TitleSceneManager.cs` | 共通土台の変更後に着手 |
| Select | `Select/*`, `SelectSceneManager.cs` | 同上 |
| Result | `Result/*`, `ResultSceneManager.cs` | 同上 |
| Option | `Option/*`, `OptionSceneManager.cs` | 共通変更なしで並列可 |
| Pause/HUD | `Game/Pause/*`, `Game/Hud/*`, `GameSceneManager.cs` | HUD は排他 (GameSceneManager を共有) |

Unity Editor の実行 (テスト/撮影) は直列化する。

## 7. 再受入までの順序

1. 共通土台 (#1, #2) → EditMode 全件 + Select/Title の実機撮影。
2. Result (#3) → Result 撮影。
3. Title/Option/Pause/HUD (#4–#7) → 各画面撮影。
4. 空状態・未撮影画面 (#8, #9) → History/Ranking/Loading/モーダル撮影。
5. 1920×1080 / 2560×1440 / Mac 2560×1664 で撮影し、**未検証解像度を明記**。
6. 新候補 SHA でビルド → `device-windows` で受入 (`7af9242d` は旧候補として保持)。
   v0.1.22 Draft Release は公開しない。

## 8. この文書の位置づけ

本紙は監査記録であり、完了条件ではない。各項目は「修正コード + テスト + 実機画像」が揃った
時点で完了とする。現時点で完了しているのは **再現 (画像・座標・コード行)** までで、
修正は本ブランチで継続する。

## 9. 修正の記録 (随時追記)

各項目は「正本ノード / 直した内容 / 検証」の順。実機画像が未取得のものは未完了と明記する。

| 正本 | 直した内容 | コード | 検証 |
|---|---|---|---|
| `AqFtx/zCQBY` (戻る 56×56) | 共通部品 `FillBackButton` (arrow-left 24 @(16,4) + Key fs16 @(15,30)) を Title 以外の 5 画面へ伝播。Select は配置漏れ (`At` 未呼び出し) も修正 | `Common/V6Controls.cs`, `Select/View/V6SelectView.cs`, `History/V6HistoryView.cs`, `Ranking/V6RankingView.cs`, `Result/V6ResultView.cs`, `Option/V6OptionView.cs` | EditMode `V6CommonLayoutTests.BackButton_MatchesDesignInternals` / `V6TitleLayoutTests.HeaderNodes_MatchDesignCoordinates` |
| `QJtTv/CtA3V` (Exit 162×56) | log-out 32 @(12,12) + KeyCap (68,12) 32 高 + ラベル (109,7)。アイコンは arrow-left ではなく log-out | `Title/V6TitleView.cs`, `Title/V6TitleLogic.cs` | 同上 |
| `QJtTv/D7gbLV` (更新して再起動 355×56) | 刷新: refresh-cw 32 + ラベル + Cap F3 (h32 / pad 8)。`ButtonKeyCapHeight=32` を共通化 | `Common/V6Controls.cs`, `Title/V6TitleView.cs` | `V6CommonLayoutTests.ButtonKeyCap_UsesDesignMetrics` |
| `QJtTv/L6w6G` (バージョン 280×32) | 「v現行 → v新版」を design 座標で表示。曲情報/カタログ状態/自己ベスト/更新バナーは正本に無いので削除 | `Title/V6TitleView.cs`, `Title/V6TitleLogic.cs` | `V6TitleLayoutTests.HeaderRight_DoesNotDrawNonDesignZones` |
| `QJtTv` 表示状態 | タブ・凡例 ON / フッター右・ヘッダータイトル OFF (正本の enabled) | `V6UiMode.cs` | `V6TitleLayoutTests.TitleVisibility_MatchesDesign` |
| `Uc9v6/T3U7u6` (Menu 180×48) | 中央寄せ・gap 10・KeyCap 自動幅 (padding [0,10])。**Key fs が 0 で「Esc」が描画されていなかった**のを修正 | `Game/Hud/V6GameHudView.cs`, `Game/Hud/V6HudMetrics.cs` | EditMode `V6HudResolutionProfileTests.MenuButton_MatchesDesignInternals` |
| `HcP8N/W1ppF` (Start Prompt) | 「Space to Start」の一文 → KeyCap「Space」(36 高 / fs22) + 「to start」(fs22) の行 | `Game/Hud/V6GameHudView.cs`, `Game/Hud/V6HudLogic.cs` | `V6HudResolutionProfileTests.ReadyPrompt_MatchesDesignStartPrompt` |
| `Uc9v6/Gx3kq` (Combo Bonus) | **未実装のまま**。正本では `BIKrJ` と同じ (1512,460) に重なる。加点は JudgeScoreboard に存在せず、値を出すと捏造になる (加点の追加は今回のスコープ外)。右コラムは `BIKrJ` を使う | `Game/Hud/V6GameHudView.cs` (コメント) | 判断の記録のみ |
| `OENRv/m1Jvyn` ほか | ジャケット解決を `V6JacketResources` に一本化 (拡張子除去・22 灰Φ倶楽部の欠落修正) | `Common/V6JacketResources.cs` | 既存 EditMode + 実機で要確認 |
| 空状態 | Select / History は `AqFtx` の Empty Note (448×140 @(56,300) / icon32 / fs24+fs20)。Ranking は `NO DATA` fs24 | `Select/View`, `History/V6HistoryView.cs`, `Ranking/V6RankingView.cs` | 実機で要確認 |

未完了: 実機撮影 (FHD/2K/Mac の 7 画面 + 空状態 + モーダル)、Result 5 列の実機確認、
Option レーン図、Pause、Issue の起票/コメント。

### 9.1 第 2 ラウンド (2026-11-11、`6dcde9a5` → `d7c73a32`)

r1 実機画像 (mac 2560×1664) と design 描画を突き合わせて見つかった**共通原因**を先に直し、
各画面へ伝播させた。数値は Pencil `execute` の resolved bounds (`ctx.bounds` / `x,y,w,h`) で取得。

| 正本 | 直した内容 | コード | 検証 |
|---|---|---|---|
| `OENRv` / `q9JZcH` / `S1TZD` / `zcSHh` / `B6If6Z` フッター凡例 | **共通契約の誤り**: 凡例 KeyCap を h36 / pad10 / gap 混在で各画面が個別に組んでいた。design は h32 / cap pad(0,8) / key-label gap 8 / item gap 24 / fs22 / y30 で共通 (Title だけ h36 / pad10 / inner10 / gap16)。`V6Legend` に一本化し、幅は TMP 実測 (`GetPreferredValues`) で決める (0.62em 概算は Latin で過大: "Register" 実測 110.5px vs 概算 149px) | `Common/V6Legend.cs` (新規), `Option/History/Ranking/Result/Select/Title` 各 View, `V6OptionLogic`, `V6SelectViewLogic`, `V6ResultLogic`, `V6TitleLogic` | EditMode 1446 件 (r1 の Select 旧 assert 1 件のみ更新) |
| `uDNC0` (Pause ヘッダー) | **戻るボタンは描かない** (第 2 ラウンドの途中で一度配置したが実装ミスとして撤回)。design の `Zone · 戻る` (24,20 56×56) は子・fill を持たない空プレースホルダで、正本描画 `uDNC0-pause.png` にも戻るボタンは出ない。Esc は凡例と「再開」ボタンの KeyCap が示す「再開」なので、ここに `FillBackButton` を置くと design 違反。座標は `BackZone*` として照合用に残す | `Game/Pause/V6PauseView.cs`, `V6PauseViewLogic.cs` (`adcda769`) | `V6PauseLoadingFrameOriginTests.HeaderBackZone_IsAnEmptyPlaceholder_SoNoButtonIsDrawn` |
| `uDNC0/k1VRDM` (Pause フッター) | 3 ボタン (選曲へ 280 / リトライ 220 / 再開 266) を design のグループ構成 (選曲・リトライ 906,896 520×56 = 280+gap16+220 / 再開 1438,896 266×56) へ。凡例は共通 `V6Legend` へ | 同上 | 同上 (ボタン非重なりも assert) |
| `OENRv/m0fT4` (曲リスト) | **r1 で上半分が空・5 行だけ下端に寄る**症状。原因は `CenterScroll` の負値 (-4) をそのまま描画し、空スロットが下側へずれること。表示用 `ListScroll` (0 以上へクランプ) に一本化し、design 定数 (パネル 24,96 560×780 / 行 x16 y48+76i 504×72 / 矢印 140×44 / ドット x536) を単一ソース化 | `Select/View/V6SelectView.cs`, `V6SelectViewLogic.cs` | `V6SelectLayoutTests` / `V6SelectCenterScrollTests` (28 index で空スロットなし) |
| `B6If6Z` Option Desc | `overflowMode=Ellipsis` + 矩形高 40 < 行高 46 で **1 行も描画されない**状態だった。h46 にし Ellipsis を廃止、説明文は折り返し (design 464 幅 / 2 行 / lineSpacing 40) | `Option/V6OptionView.cs`, `Common/V6Controls.cs` (wrapping 引数) | EditMode 全件 |
| `B6If6Z` Option レーン図 | レーンラベルの起点 26 / ラベル枠 40×26 @y246 へ design 合わせ | `Option/V6OptionView.cs` | EditMode 全件 |
| `QJtTv/Logo · 列1-6` (1680×332) | ロゴ画像 `13.png` (5007×990) が旧 import で 2048×512 に縮小され、描画が 1328×332 の左寄せになっていた (design は 1680×332 いっぱい)。maxTextureSize 4096 / NoNpotScale に修正 | `Assets/Game/Resources/V5/Common/13.png.meta` | **実機で再確認が必要** (r2) |
| Key Layout (Option/Pause) | 表示・編集・確定・保存・再起動復元は実装済み。**キャンセル (元に戻す) だけ入口が無かった** → Option の F1 と、Pause でも `KeyConfigUI` 前面時の F1 を `V6OptionView.CancelKeyConfigEdit` (保存値へ戻して閉じる) に配線 | `Option/V6OptionView.cs`, `V6GameRuntime.cs` | 新規 `KeyLayoutEditSessionTests` (4 件) |

このラウンドの証跡: `docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r1/` (修正前) と、
修正後の `mac-2560x1664-r2/` (新候補 SHA で再取得)。
FHD 1920×1080 / 2K 2560×1440 は **この Mac では未検証** (内蔵 2560×1664 のみ実全画面)。


