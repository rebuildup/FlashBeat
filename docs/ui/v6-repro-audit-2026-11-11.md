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

> この表は**第 1 ラウンド (r1) 時点の記録**で、その後 §9 の各ラウンドで修正した項目
> (Title のタブ/凡例/フッター右、Option のレーン図/キー行、Result の 5 列・
> ジャケット、Select の Esc 位置/設定カラム、Modal の Placeholder など) は
> 現在の状態と一致しない。現状の判定は §9 の各節と実機画素を正とする。

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

### 9.2 第 3 ラウンド (2026-11-11、行部品の一本化と Result/Ranking の実測差)

r1/r2 の実機画像と design 描画を突き合わせ、**行の内部座標が画面ごとに写経され、どれも
design と違う**という共通原因を先に潰した。数値は Pencil `execute` の resolved bounds
(`ctx.bounds` = `x,y,w,h`、親内座標)。

| 正本 | 直した内容 | コード | 検証 |
|---|---|---|---|
| `S1TZD/S1TZD` 曲行 `mcCOI` / `OENRv/Odv71` (504×72) | Select / Ranking / 共通部品が Level x14・Thumb x72・Title x182・Grade x432 と三者三様に写経し、Ranking は Column レイアウトのため曲名が 2 行に折れて行が破綻していた。`V6Lists.SongRow` を design 実測 (Level (12,14) 44 / Thumb (68,9) 96×54 / Texts (176,2) 248×69 = Title 248×38 fs26 + gap2 + Composer 248×29 fs20 / Grade (436,18) 56×36) に直し、1 行固定 + Ellipsis にして Select・Ranking の両方をこの部品へ置換 | `Common/V6Lists.cs`, `Select/View/V6SelectView.cs`, `Ranking/V6RankingView.cs` | `V6RankingDesignLayoutTests.SongRow_MatchesDesignInternals` / `SongList_RowsAreBuiltByCommonSongRow`, `V6CommonLayoutTests` |
| `S1TZD` ランキング行 `Hbxqf` (496×56) / `q9JZcH` `T8C1Hu` (496×52) / `smhrU/tcgRX` (496×56) | `RankRow` / `PlayerRow` を design 実測 (Pos (12,中央) 36 / Name (60,中央) 160×32 / Value (232,中央) 180×32 右 / Grade (424,中央) 56×36 / Plays (424,中央) 56×29 fs20 `$text-dim` 右) へ。選択行 (YOU) は背景 `$k6` + Pos `$text`。旧テストの 44 / 140 は Select の行 (下表) の値だったため design 値へ更新 | `Common/V6Lists.cs`, `V6HistoryRankingLayoutTests`, `V6CommonLayoutTests` | `V6RankingDesignLayoutTests.RankRow_MatchesDesignInternals_At56And52` / `PlayerRow_MatchesDesignInternals` |
| `S1TZD` SCORE RANKING ヘッド `H8yC4` / パネル `qICtu`·`smhrU` | 曲名チップが Local/Online セグメントに重なり `Online` が画面外へ切れていた。ヘッドを絶対座標へ (Title (12,7) 139×26 / Song チップ (159,4) 159×32 = Thumb 40×24 + Text 111×32 NoWrap / Segment (335,0) 156×40 = Local (4,4)・Online (80,4) 72×32)、パネル角丸 8。PLAYERS は design の見出し (Title + 右の「累計スコア」) のみで、実装都合の文言「プレイヤー別の集計には対応していません」と疑似矢印を削除 | `Ranking/V6RankingView.cs`, `V6RankingLogic.cs` | 同上 (`ScoreRankingPanelAndHead_MatchDesign`, `PlayersPanel_HeadOnly`) |
| `S1TZD` ドットレール `vQc4K` | 全ドットが同座標に潰れていた (`Center(dot,24,d)` が y を 0 に上書き)。Select と同じ `DotRectX`/`DotOffsets` 配置 (ピッチ 12、ドット 8×8 / 選択 10×10) へ | `Ranking/V6RankingView.cs` | `V6RankingDesignLayoutTests` (ドット y が単調増加) |
| `S1TZD` 情報ゾーン `w4siL` | Ranking ヘッダーの曲名 Text に **Rect を与えておらず** 既定の 100×100 中央寄せで design より右下へずれていた。Title (0,1) 320×43 fs30 / Artist (0,46) 320×29 fs20 を明示し 1 行固定 + Ellipsis | `Ranking/V6RankingView.cs` | `V6RankingDesignLayoutTests.Header_SongInfo_MatchesDesignCoordinates` |
| `zcSHh` 曲別ランキング `PdoPy` | design の `Song` 見出し ((20,52) fs30) が**存在しなかった**。ヘッド下に曲名を 1 行 (実データ長に合わせ幅 484 / Ellipsis) で追加し、空状態は行領域 (y92..404) の中央へ移動。ヘッド Title は (12,5) h26 | `Result/V6ResultView.cs`, `V6ResultLogic.cs` | `V6ResultDesignLayoutTests.SongRankingPanel_HeadAndSongTitle_MatchDesign` |
| `zcSHh` 曲別ランキング行 `Chlqd` = Select `jnIY9` (496×44) | 両画面が同じ行を 8px 右へずらして写経していた (Pos x4 / Name x52 / Score x200 / Grade x436)。design (Pos (12,4) 36 / Name (60,6) 140×32 / Value (212,6) 180×32 右 / Grade (404,4) 56×36) を持つ `V6Lists.CompactRankRow` に一本化し、Result・Select の両方から使う | `Common/V6Lists.cs`, `Result/V6ResultView.cs`, `Select/View/V6SelectView.cs` | `V6ResultDesignLayoutTests.CompactRankRow_MatchesDesignInternals` |
| `zcSHh` 楽曲情報 `KKIxx` | Stat を design の y20 (Label (0,0) h26 / Value (0,28) h43) へ戻した。旧実装の y56 は Title を全幅 660 に広げた代償で、design より 36px 下。Title/Artist を Stat 列の左端 (x348) までに制限 (204) して横方向で分離し、長い実データは Ellipsis にした。なお design の Value 矩形は 64 高セルを 7px 超える (design に clip は無く、バンド内 91 に収まる) ため、テストはバンド内包で判定する | `Result/V6ResultLogic.cs`, `Result/V6ResultView.cs`, `V6ResultFramePlacementTests` | `V6ResultDesignLayoutTests.SongInfoBand_MatchesDesignCoordinates` |

このラウンドの検証: `script/compile-check.sh` OK、Unity EditMode **1461 件すべて合格**
(新規 `V6RankingDesignLayoutTests` / `V6ResultDesignLayoutTests` を含む。compile-check は
テストアセンブリをコンパイルしないため、実コンパイルのゲートは EditMode 側)。

未完了 (次): History 行スタックの縦中央化 (A) と Sort チップ/Count の位置 (C)、
修正後ビルドの実機撮影 r3、FHD/2K の実表示確認、Issue の起票・コメント。

### 9.3 第 4 ラウンド (2026-11-11、Ellipsis 0 行の共通原因を潰す)

r3 (commit `dfc634a9` の Mac ビルド、2560×1664 実機) で、直前のラウンドで追加した
**Result の曲名・作家名、結果画面の曲別ランキング曲名、Ranking ヘッダーの曲名が
丸ごと描画されない**退行が出た。design 矩形はどれも正しく、原因は TMP 側の描画条件だった。

**実測 (EditMode の一時プローブ、後に削除)** — NotoSansJP-Medium SDF は
`faceInfo.lineHeight 69.504 / pointSize 48 = fs × 1.448`。TMP は Ellipsis のとき
**矩形の実効高 (rect 高 − margin 上下) が行送り未満なら 1 文字も描かない**。
`lineSpacing` を −44.8 に詰めてもこの判定は変わらない (行間専用)。実測:

| fs × 矩形高 | 行送り | Ellipsis の結果 |
|---|---|---|
| 20 × 29 | 28.96 | 描画 (境界一致) |
| 23 × 33 | 33.35 | **0 文字** |
| 26 × 36 | 37.65 | **0 文字** |
| 30 × 43 | 43.44 | **0 文字** |
| 34 × 36 | 49.23 | **0 文字** |

design 矩形はこの境界を 0.3〜13px 下回るため、`V5Ui.FitEllipsisToLineHeight` を追加した
(上下 margin を同じ負値にして実効高だけを広げる。垂直中央揃えなので**矩形サイズも
描画ベースラインも自然描画と一致**することを測定で確認。矩形自体を高くすると 10px 下へ
ずれるので不可)。適用箇所:

| 画面 / 部品 | 直した内容 | コード | 検証 |
|---|---|---|---|
| Result 楽曲情報 Title (fs34 h36) / Artist (fs23 h33) | 0 行で消えていた (r3 実機) | `Result/V6ResultView.cs` | `V6EllipsisFitTests.ResultSongInfo_TitleAndArtist_AreDrawnWithRealData` |
| Result 曲別ランキング Song (fs30 h43) | 同上 | `Result/V6ResultView.cs` | `...ResultSongRanking_SongHeader_IsDrawn` |
| Ranking ヘッダー Song/Title (fs30 h43) / Artist (fs20 h29) / 曲チップ Text (fs22 h32) | 同上 | `Ranking/V6RankingView.cs` | `...RankingHeader_SongTitle_IsDrawn` |
| Select / Ranking 曲行 Title (fs26 h38) / Composer | 余裕は 0.35px しかなく、フォント差で再発しうる | `Common/V6Lists.cs` | `V6EllipsisFitTests` |
| Loading 曲名 (fs22 h26) / 作家 (fs16 h20) | **修正前から 0 行** (ローディング中に曲名が出ていなかった) | `Game/Loading/V6LoadingView.cs` | — |
| Pause 曲名 | 以前 43→46 に逃げていた design 値を **h43 に戻した** | `Game/Pause/V6PauseView.cs` | — |

テスト側の共通原因も直した: 旧 `V6TextLineFitTests` は「lineSpacing を織り込んだ行送り ≤
矩形高」だけを検証し、**メッシュ生成後の characterCount を見ていなかった**ため、
この退行を通していた。`ForceMeshUpdate` 後の描画文字数で判定する形に書き換えた
(`V6TextLineFitTests` 3 件 + 新規 `V6EllipsisFitTests` 7 件)。

このラウンドの検証: `script/compile-check.sh` OK、Unity EditMode **1468 件すべて合格**。

### 9.4 第 4 ラウンドの実機確認と「枠が描画されている」指摘の再測定

r4 = 9.3 の修正を含む `f1d5e545` の Mac ビルドを、2560×1664 の実ディスプレイ全画面
(`-screen-fullscreen 1 -screen-width 2560 -screen-height 1664`) で 8 画面撮影。
`autotest-v6screens-result.txt` は `RESULT pass=17 fail=0 symbols=41 screenshots=8
resolution=2560x1664`。切り出しで Result の曲名「ロストアン…」/作家名「稲葉曇」、
Ranking ヘッダーの「ロストアンブレラ」、Pause の曲名、Result 曲別ランキングの曲名が
**実データで描画されている**ことを確認した (r3 の 0 行退行は解消)。

オーナー指摘「表示されるべきでない枠が描画されている」について、r4 の Select で見える
外周の淡い矩形を design と画素比較した。design 書き出し `OENRv.png` (1728×972 を 2 倍の
3456×1944 RGBA で書き出し) を走査すると、四辺とも外側 4px (= design 2px) が
`(235,235,235,51)` = `#EBEBEB33`、その内側は `alpha 0`。

| 位置 | design 実測 (2x 書き出し) | 実装 |
|---|---|---|
| 上/下/左/右の外周 | `#EBEBEB33` 2px (design px) | `V6SelectView.BuildFrameStroke()` の `#EDEDED33` 2px |
| 矩形の内側 | alpha 0 (透明) | — |

つまり外周 2px の stroke は**design に実在する共有フレーム rw7zx の stroke** で、
実装は design どおり。黒背景に対して 51/255 の白なので淡く見えるが、描画されているのが
正しい。他画面の外周も同じ `#EDEDED33` で、r4 の 8 画面で design のパネル定義
(fill/radius) と食い違う枠はこの走査では見つからなかった。ただし全画面の全境界を
1px 単位で照合したわけではないので、残余は Issue #242 の実機確認項目に残す。

### 9.5 第 5 ラウンド (2026-11-11、History の座標を design q9JZcH に合わせる)

History を design 実測と突き合わせ、3 点のずれを修正した。

| 対象 | 旧 | design (正本) | 修正後 |
|---|---|---|---|
| 履歴行スタック | 常に上寄せ `RowY0=48` | 4 行が y 254/326/398/470 (上余白 254・下余白 246 の中央) | n≤9 のとき `(780 − 72n)/2 + 8`、10 行以上は `RowY0` + スクロール |
| 行内部 | Pos x14 / Title x64 h64 / Score x296 h64 | Pos (12,14) / Title (60,15) 220×35 fs24 / Score (292,15) 120×35 右 / Grade (424,14) | 同左。Title は 1 行 Ellipsis (h35 ≥ 行送り 34.75 なので 0 行にならない) |
| ヘッダー Sort | 132×44 @(840,26)・中央揃え | 104×29 @(864,33.5)・テキスト (12,0) 80×29 左 | 同左。design 外のラベル「カタログ順」(5 文字) は枠を変えず最小 16px へ自動縮小 |
| ヘッダー Count | 右端 親内 1680 (= ページ 1704) | 右端 親内 1656 (= ページ 1680、情報ゾーン右 padding 24 の内側) | `At(count, 1376, 24, 280, 48, pivot(0,0.5))` |

`V6HistoryLogic` に design 定数と純関数 `HistoryFirstRowY(int count)` を追加し、
`V6HistoryDesignLayoutTests` (7 件) で n=4→254 / n=2→326 / n≥10→48 と
View の実配置 (Pos/Title/Score/Grade/Sort/Count) を pin した。
検証: `script/compile-check.sh` OK、Unity EditMode **1475 件すべて合格**。

続けて r5 の実機画像を design と突き合わせ、**中央パネルの曲名が 13px 下がって
Artist Row に食い込んでいる**のを見つけた。design 正本 `q9JZcH` 中央の
`f54HAH/DcLR3` は Meta 内 **(0,9) 512×50 fs48** だが、実装は `sizeDelta` だけを
変えており既定アンカー (中央) のまま = y22 に置かれていた。Select の同じ構造
(`dRztp`) も同一コードなので両方を `At(title, 0, 9, 512, 50)` に修正した
(`c6506361`、`HistoryCenterMeta_Title_MatchesDesign` / `SelectCenterMeta_Title_MatchesDesign`)。

r5 の Option 画面は、オーナー指摘 3 点がすべて design どおりになっていることを
drawing ではなく座標で確認した:

| 指摘 | design 実測 | r5 実機 (page 座標) |
|---|---|---|
| レーン図が 3D コーン | — | 平面 8 レーン (細線 + ノート) に置換済み |
| キーキャップが行ごとに減る左詰め | 4 行 13/12/12/11、行ごとに +17px の階段、x 896..1362 / 913..1343 / 930..1360 / 947..1341 | 13/12/12/11、x 1313..1777 / 1330..1758 / 1347..1775 / 1364..1756 (= design + frame origin (416,346) と一致) |
| 右パネル下部の見出し欠落 | `Desc Title` = 選択行ラベル (design 例は「キー設定」fs32) | 選択行ラベル (NoteSpeed) を表示。design に無い固定見出しは足していない |

なお History の design 再現は omp ワーカー (opencode-go) に委譲したが、19 分間
出力・編集とも 0 のまま停止したため、ルータの規定どおり supervisor が引き取って
実装・検証まで完了させた。




### 9.6 r6 の画素による最終確認 (Esc 位置 / Result ジャケット)

r6 は **`c6506361`** (中央パネル曲名の修正) のビルド。ビルド成果物の時刻
(`Builds/macOS/FlashBeat.app` = 14:28:28) と `c6506361` の commit 時刻 (14:27:55) が一致し、
行送り硬化 `9edaffd7` (14:31:23) は**含まない**。`9edaffd7` の検証は EditMode 1477 件のみで、
実機証跡は r7 で取り直す (下記)。

2560×1664 実ディスプレイ全画面 (`-screen-fullscreen 1 -screen-width 2560 -screen-height 1664`)
で撮影し、`autotest-v6screens-result.txt` は
`RESULT pass=17 fail=0 symbols=45 screenshots=8`。画像は design 1px = 画面 1px、
frame origin (416,346)。

硬化 `9edaffd7` を含む `7cfcfe51` で**同じ 8 画面を r7 として再取得**し、同じ
`RESULT pass=17 fail=0 symbols=45 screenshots=8 resolution=2560x1664` を確認した
(`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r7`)。以下の画素測定は r6 の画像で行った。

| 指摘 | design | r6 実測 (画素) |
|---|---|---|
| Esc / 戻るボタンの位置 | Select フレーム (24,20) 56×56 → 画面 x 440..496 / y 366..422 | `#3A3A3A` の塗りが y=394 で x 441..494、x=490 で y 367..420 (いずれも AA 1px を除く内側)。中心 (468,394) も `#3A3A3A`。ヘッダー中央 (≈x 1256) には無い |
| Result の曲サムネイル | `Song ThumbX/Y = 16,28` 112×63、Main (164,96) → 画面 x 596..708 / y 1106..1169 | 同領域に `01-rosen.jpg` の濃淡 (上端 181 前後、中段 15〜181 の絵柄) が乗っている。空枠 (`#2D2D2D` 単色) ではない |

Result のジャケットは「拡張子付きパスを `Resources.Load` へ渡していた」共通原因 (C4) の
修正後に、実データで描画されていることを画素で確認したもの。design の矩形にジャケットが
載っているかは、この一領域の色分布で判定できる (空枠なら全画素同色)。

### 9.7 02b Select · 該当なし (design AqFtx) の再現

空状態は design に独立フレーム (`AqFtx`) があるのに、実装は音楽アイコン + 独自文言で
作られていた。design の resolved bounds と Pencil 描画で確認し、次のように直した。

| 対象 | 旧 | design (正本) | 修正後 |
|---|---|---|---|
| Empty Note パネル | (56,300) 448×140 `$k0` r8 | 同左 | 同左 |
| アイコン | `music` 32、下寄せ (y≈16) | 虫めがね + x (`UIN4a`) 32×32、上端 y14 | design から `search-x.png` を書き出して同位置へ |
| Title | (0,52) 448×32 | (0,54) 448×35 fs24 中央 | 同左 |
| Hint | (0,88) 448×28 | (0,97) 448×29 fs20 中央 | 同左 |
| SCORE RANKING 空状態 | 左寄せの長文 (24,92) | 中央 "NO DATA" (8,155) 496×35 fs24 `$text-dim` | 同左。理由はヘッダーの StatusMessage が示す |
| PLAYERS 空状態 | 左寄せ「データがありません」(24,60) | 中央 "NO DATA" (8,149) 496×35 | 同左 |

`V6SelectEmptyStateTests` 4 件で座標・アライン・スプライト名 (`search-x`) を固定。
`AutoTestV6Screens` に検索ヒット 0 件の撮影 (`SelectScene-Empty`) を追加し、実機で
この画面自体を証跡に残せるようにした。

r9 (`b70dd31a`、空状態の撮影ハーネス込み) で実機撮影し、`SelectScene-Empty` が
`pass=112 fail=0`、全体は `RESULT pass=19 fail=0 symbols=43 screenshots=9
resolution=2560x1664`。画像の該当領域 (画面 (496,742) 448×140) には design どおり
虫めがね+x のインク・曲名の白文字・淡色のヒントが乗り、上の行スロットは空
(背景のみ) であることを画素で確認した。

なお r9 の OptionScene は `pass=181` と r7 の `pass=359` から減っているが、
スクリーンショットは PSNR ∞ (画素完全一致) で、containment の pass 件数は
撮影時の可視グラフィック列挙に依存するため比較対象にしない (fail=0 が判定)。

### 9.8 05b Result · 登録モーダル (design lqHaN / YwPw9) の再現

Result 画面の登録モーダルは v5 の `ResultRankingOverlay` を置き換える新 UI で、
design の正本は 05b (`lqHaN`)、ダイアログ本体は `YwPw9` (444,231) 840×510。
`AutoTestV6Screens` の ResultScene 分岐に、監査用 API でモーダルを開いて撮る
`ResultScene-RegisterModal` を追加した (入力は注入しない)。

**design の内部座標 (ダイアログ内) と修正後の実装**

| 要素 | design 矩形 | 中身 |
|---|---|---|
| Head | (28,28) 180×43 | "スコアを登録" fs30 w700 |
| Name Prompt | (404,38) 380×29 | "プレイヤー名を入力↓" fs20 右寄せ |
| Name Field | (28,112) 784×84 `$k0` r8 | Value (24,20) 736×58 fs40、未入力は Placeholder "YOU" `$text-dim` |
| Input Hints | (28,208) 784×32 | Cap (0,0) 104×32 `$k4` / Hint (128,4.5) fs16 / Legend 移動 (509,3) / Legend 削除 (634,3) |
| Summary | (28,264) 784×140 `$k2` r8 | Thumb (16,20) 88×50 / Title (120,18) / Artist (120,52) / Grade (704,38) 64×40 / Divider (16,94) 752×1 / Record (16,104) / Score Group (400,26) 288×52 |
| Buttons | (28,432) 784×50 | Skip (318,0) 198×50 `$k4` / Register (532,0) 252×50 primary |

**修正前 (r10, build `3ec4af9e`) に実機で出ていた崩れ**

- "スコアを登録" が 2 行に折り返す。design の hug 幅 180 に対し、ゲームの
  NotoSansJP は同じ fs30 で 187 px 描くため TMP の既定 wrap (Normal) が折り返す。
- "SCORE" → "SCOR"/"E"、"Skip" → "Ski"/"p"、"Register" → "Registe"/"r"、
  "Backspace" → "Backspac"/"e" (いずれも hug 幅がフォント実寸より数 px 狭い)。
- Score Group / Input Hints / Buttons を `V5GameUi.Row`
  (HorizontalLayoutGroup, childControlWidth=false) で組んでいたため、
  幅未指定の TMP 子が 0 幅になり縦に積まれていた。
- Name Field が空欄 (design は "YOU")。

containment 監査 (`pass=119`) はこの折返しを検出できない。design 画像と
画素で突き合わせて初めて分かる種類の崩れだった。

**修正**

- `3ec4af9e`: モーダル全体を design の明示座標 (Row を使わない `At`) へ。
- `23d52037`: モーダル内の全テキストを `OneLine` (NoWrap) にし、hug 幅は
  design の矩形のまま 1 行で描く。Score Value の左詰めは design の
  インク実測で確定 (下記)。Select 設定行も同コミットで修正 (§9.9)。
- `5fc33046`: Score Value を design どおり左詰めに (生ノードは alignment
  未指定 = 既定左、インクも左端 +1.5 px から始まる)。
- `410eaac7`: Name Field の Placeholder "YOU" (`$text-dim`)、入力時は `$text`。

**design 画像 (Pencil `Export` lqHaN.png 2x) と r11 実機の画素比較 (ダイアログ内座標)**

画面は design 座標 1:1 (frame origin (416,346) = (2560−1728)/2, (1664−972)/2、
ダイアログ左上は画面 (860,577))。

| 要素 | design インク | r11 インク | 判定 |
|---|---|---|---|
| Head | 30.5..207, 37.5..65 | 30..217, 37..64 | 左端一致。幅 +10.5 は `$font-ui` と NotoSansJP の字幅差 |
| Name Prompt | 587.5..778, 44.5..62 | 587..777, 45..62 | 右寄せ一致 (±1) |
| Score Label | 520..568, 311.5..323 | 520..569, 311..322 | 一致 |
| Score Value | 585.5..713, 305..337 | 585..601, 305..330 | 左端 +0.5 = 左詰め一致 (右端は実データ "0" の分だけ短い) |
| Record Label | 45..168 | 45..160 | 左端一致。design は "前回最高 921,880"、実機は "前回最高 74,626" |
| Record Value | 576.5..646.5 ("+66,633") | 空 | design は更新時の差分。r11 はスコア 0 で未更新のため空 (実データ) |
| Button Skip の塗り | 346..543.5, 432..481.5 | 347..542, 433..480 | 一致 (±1) |
| Button Register の塗り | 560..811.5, 432..481.5 | 561..810, 433..480 | 一致 (±1) |

design と文言が異なる箇所は実データ・方式依存で、覆い隠さずそのまま出す:

- 入力上限: design の見本は "最大8文字"。実機はオンライン登録 (RankingClient,
  16 文字) で開くため "最大16文字"。ローカル曲では head "この端末の名前" /
  "Save" / "最大8文字 (A-Z 0-9)" になる (v5 と同じ語彙、テストで固定済み)。
- Grade は未ランクの "–"、SCORE は 0 (監査実行時は未プレイ)。
- Score Value の文字列は design 見本 "988,513" に対し実データ。

**Placeholder の検証 (r12, build `410eaac7`)**

design の Name Field の "YOU" のインクはダイアログ内 (52,148)..(131,178)
(fs40 `$text-dim`)。r12 実機は (52,149)..(135,177) で、左端・上端が 1 px 以内で
一致し、色も `$text-dim` (#8C8C8C、110..200 の範囲にのみインクがあり白インクは
無い) だった。幅 +4 px は NotoSansJP と `$font-ui` の字幅差。画像で
"YOU" が design と同じ位置に淡色で出ていることを確認した。

r11 は `RESULT pass=23 fail=0 symbols=61 screenshots=11` で
`ResultScene-RegisterModal pass=119 fail=0`、r12 (`410eaac7`、Placeholder 修正後) も
同じ `RESULT pass=23 fail=0 symbols=61 screenshots=11 resolution=2560x1664`、
`ResultScene-RegisterModal pass=119 fail=0`
(`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r11` / `...-r12`)。

### 9.9 02c Select · 設定 (design wSjAm) 右カラムの再現

「このプレイの設定」は Ranking カラムの位置を差し替えるパネル。design の
行は Pencil の flex 配置で座標が生ノードに無いため、`wSjAm.png` の書き出しと
実機画像の画素で位置を確定した。design は列 (1120,96)、行は列内
(24, 64+68k) 512×56 (背景 `$k2` #2D2D2D、r8)。

| 行内要素 | design (行内) | 旧実装 | 修正後 |
|---|---|---|---|
| Icon | (12,16) 24×24 | 中央寄せで左端 x0 | `PlaceTopLeft(12,16,24,24)` |
| Label | x48、行の縦中央 fs24 | pivot 補正漏れで 28 px 上 | `At(48, 0, 240, 56, pivot(0,0.5))` + `At` の補正修正 |
| Value | 右端 448 に右寄せ、幅 = TMP 実測 (design hug 101/53/27) | 固定幅 96 の中央寄せ | `MeasureWidth` + `At(448−w)` + Right + NoWrap |
| chevron-left | 448−値幅−52、y8、40×40 | x300 固定 | 同上 |
| chevron-right | (460,8) 40×40 | x300 固定 | `At(460,8)` |

`At(Component, x, y, w, h, pivot)` の `anchoredPosition` 補正
(`x + pivot.x*w`, `−(y + (1−pivot.y)*h)`) が Select 側だけ欠けており、
pivot (0,0.5) の Label / Value が design より 28 px 上に描かれていた。
Result 側と同じ契約に揃えた。

**r11 実機の画素 (画面座標。列左 1560 / 行左 1584 = design 1120+24+416, 96+64+346)**

| 対象 | design (行内) | r11 実測 (行内) |
|---|---|---|
| 行の背景 | 0..512 | 1586..2093 (≈2..509、角 r8 の AA を除く) |
| chevron-left (Key Guide) | 294..334 | 296..330 (走査窓の左端でクリップ) |
| chevron-left (Lyrics) | 343..383 | 342..379 |
| chevron-left (NoteSpeed) | 369..409 | 369..406 |
| chevron-right (3 行共通) | 460..499.5 | 461..498 |
| Value の右端 | 447 / 445.5 / 446 | 446 / 445 / 445 |

値の左端は design の `$font-ui` と NotoSansJP の字幅差で数 px ずれるが、
右端 (design の hug 右端 448) は 1.5 px 以内で一致する。値を "Compact" /
"Auto" / "10" と design と同じ実データで表示していることも画像で確認した。

撮影は `AutoTestV6Screens` の Select 分岐に `OpenQuickSettings` /
`CloseQuickSettings` を足して行った。r11 で
`SelectScene-Settings pass=192 fail=0`
(`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r11/v6screen-SelectScene-Settings-4.png`)。
`V6SelectSettingsLayoutTests` 2 件で Head・3 行の矩形、Icon/Label/Value/
chevron の座標とアライン、開閉を固定した。

### 9.10 r12 → r13 Title / Option の再測定と Exit の修正

§1 の r1 記録に残っている Option と Title の不一致は、`6dcde9a5` / `57cc5b5c` /
`f906da9e` 以降のビルドで解消済みだった。r12 (mac 2560×1664 実全画面) の画素で
確認し、その過程で **Title の Exit だけが design と別物**であることが分かったので
直した。

#### Option · Play (design `B6If6Z`)

設計のキーグリッドは 4 行の段違い (行頭 x の差が 17 ピッチ = `{8,25,42,59}`、
キャップ 34×40 を 36 ピッチ、行ピッチ 44)。r12 実機は 1 行目の左端を基準に
各行が +17 / +34 / +51 で並び、キャップ間隔 36・行間 44 で一致した。
キャップ内の数字はレーン番号 (Key 1/Q/A/Z → すべて "1") で、設計どおり
割り当てレーンを出す実装になっている (§1 の「3D コーン」「左詰め」は r1 の記録)。

#### Title (design `QJtTv`)

| 対象 | design インク (フレーム座標) | r12 実機 |
|---|---|---|
| タブ行 | y520..572 | y520..572 |
| 左下凡例 | (35.5,915.5)-(397,938.5) | (36,916)-(398,937) |
| ヘッダー右 (版表示) | (1114.5,36.5)-(1365.5,63.5) | (1115,36)-(1365,62) |

#### Exit (`CtA3V`) — r12 で発見した不一致、`8d3970f9` で修正

design の `Exit` フレームは **fill なし**の 162×56 で、塗りは子の
`Icon Button` (`Z6C0w`: (0,0) 56×56 `$k4` r8、log-out 32 @(12,12)) だけ。
KeyCap (`D7mVkL`: (68,12) 29×32 #3A3A3A r4) と Label (`qttPo`: (109,6.5) 53×43
fs30) は透明な背景に直接載る。

旧実装は Exit 全体を 162×56 の `$k4` r8 で塗っていたため、r12 の実機では
3 要素が 1 本の横長ピルに乗っていた (y30 の塗りが design 24..79.5 に対し
25..184)。`V6TitleExit` に組み立てを集約し、透明な入れ物 (クリックは従来どおり
162×56 全体) + 56×56 Icon Button に変更。ラベルは design 実測の y6.5 へ。

r13 の画素 (`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r13/`):

| 対象 | design | r13 実機 |
|---|---|---|
| Icon Button の塗り (y30) | x24..79.5 | x25..78 |
| KeyCap の塗り (縦, x96) | y32..63.5 | y33..62 |
| KeyCap の "E" インク | (102.5,42)-(111,57.5) | (103,42)-(111,56) |
| "Exit" ラベルインク | (135.5,37.5)-(185,61.5) | (137,36)-(185,58) |

162 px 幅の塗り (旧ピル) は r13 で消えた。`V6TitleExitTests` 4 件で
透明コンテナ / 56 px 超の塗りが無いこと / KeyCap・Label の座標と fs /
クリックで `onExit` が呼ばれることを固定した。r13 は
`RESULT pass=23 fail=0 symbols=61 screenshots=11 resolution=2560x1664`。

### 9.11 Result · 判定行 (design `EpX3e`) の 11px ずれ

r13 の Result 画像で、判定行 (Perfect/Great/Bad/Miss/Combo) だけが design より
約 11.5px 下に描かれているのを見つけた (他の要素 — SCORE 列・ゲージ・曲情報 —
は 1〜2px 以内で一致)。design `EpX3e` は (0,448) 840×140、セル 160×140 を
168 間隔で、各セルの中身は Label (0,0) 66×29 fs20 / Value (0,43) 83×70 fs48 を
**左上揃え**で置く。実装は `VerticalLayoutGroup` に任せ、TMP 既定の
`TextAlignmentOptions.Left` (縦中央) と子の既定サイズが混ざっていた。

Label/Value を design 矩形 + `TopLeft` で明示配置 (`12ffaa1e`)。r14 の画素:

| 対象 | design | r13 (修正前) | r14 (修正後) |
|---|---|---|---|
| Perfect ラベルのインク | y552..566.5 | y564..576 | y553..566 |
| 判定値のインク | y607..643.5 | y618..653 | y606..642 |
| ラベル列 x (5 列) | 166/333/502/670/837 | 同左 | 166/334/502/670/838 |

`V6ResultDesignLayoutTests.JudgementsRow_MatchesDesignCoordinates` で 5 セルの
矩形・fs・アラインを固定した。r14 は
`RESULT pass=23 fail=0 symbols=57 screenshots=11 resolution=2560x1664`。

なお r13 の OptionScene は同じ `V6 Option View` が監査上 2 重に存在し
(`pass=359 symbols=8`)、r14 は 1 つ (`pass=181 symbols=4`) だった。スクリーン
ショットに差は無く、判定行の修正とは無関係の重複生成なので、別途追う。

### 9.12 曲ローディング (design `g4Q3F`) の design 一致と、古いバイナリで撮っていた件

ローディングは design `g4Q3F` を正本に、ヘッダー (戻る / ジャケット / 曲情報) と
フッター凡例の 4 点を実測で合わせた (`1b992790`, `b2a782c2`, `be7c68ca`)。

#### 直したこと (共通部品優先)

| 症状 | 原因 | 修正 |
|---|---|---|
| 凡例の Esc Cap が design x56 でなく x81 | `V6Legend.Item` は**親ローカル**座標。フレーム座標 56 をそのまま渡すとフッター分 24 ずれた | `FooterLegendX - FooterX` を渡す (`V6LoadingViewLogic.FooterLegendX = 56`) |
| ジャケットが 56×27 の帯 | 16:9 素材 320×180 に `preserveAspect` をかけたため letterbox | design `N3HPJ` は `mode:"cover"`。`V6LoadingViewLogic.CoverCropRect` で中央 180×180 を切り出す |
| 曲名/アーティストが数 px 下がる | `VerticalLayoutGroup` の縦中央寄せ + TMP 既定 | design 矩形を明示 (`Song Title` (0,0) 111×32 TopLeft / `Song Artist` (0,34) 113×23 TopLeft) |
| 戻るボタンのアイコンが chevron (8px 幅) | 手組みで `chevron-left` を入れていた | 共通部品 `V6Controls.FillBackButton` (arrow-left 24 @ (16,4) + Key fs16 @ (15,30) 26×23) |

#### 検証中の落とし穴: r17 は古いバイナリで撮っていた

`./script/mac-verify build <SHA>` の後に撮った r17 が r16 と**画素完全一致**
(diff bbox `None`) で、戻るアイコンも chevron のままだった。原因は撮影が
ビルド完了前だったこと:

- r17 スクリーンショット mtime `15:54:14`
- `FlashBeat.app/Contents/MacOS/FlashBeat` mtime `15:55:12` (58 秒後)

つまり撮影は旧 app (r16 と同じ `b2a782c2` 以前) を起動していた。ビルド完了後に
r18 を撮り直すと、r17 → r18 の差分 bbox は **frame (44,28)–(59,43)** —
戻るアイコンの領域そのもの。教訓: `script/mac-verify build` の後に撮る前に、
app バイナリの mtime がビルドログより新しいことを確認する。

#### r18 (2560×1664 実全画面, `be7c68ca`) の実測

design 書き出し `/private/tmp/design-render/g4Q3F.png` (1728×972) と同座標で比較。
値はフレーム座標のインク bbox (design は alpha>0 の実画素)。

| 対象 | design | r18 | 差 |
|---|---|---|---|
| 戻るアイコン (arrow-left) | (44,28)–(59,42) 16×15 | (44,28)–(59,43) 16×16 | ≤1px |
| 戻るキー文字 (Esc) | (40,57)–(62,68) | (41,57)–(63,68) | 1px |
| 戻るボタン Cap | (24,20)–(79,75) 56×56 | 同左 | 0 |
| ジャケット | (1104,20)–(1159,75) 56×56 | 同左 | 0 |
| 曲名インク | (1172,27)–(1280,47) | (1173,28)–(1280,46) | ≤1px |
| アーティストインク | (1172,59)–(1281,73) | (1172,58)–(1280,72) | ≤1px |
| フッター Cap | (56,906)–(110,941) 55×36 | (57,907)–(110,940) | 1px (AA) |
| フッター「戻る」 | (121,916)–(161,935) | (123,916)–(162,934) | 2px |

`RESULT pass=25 fail=0 symbols=63 screenshots=12 resolution=2560x1664`。

#### design との意図的な差 (issue 化済みの範囲)

- design `g4Q3F` の Main は空。実装は「準備中…」+ 進捗バーを中央に置く
  (Issue #224 の要求)。design に無い情報なので一致判定の対象外。
- 背景: design フレームに背景塗りは無い。実装は同梱の
  `Resources/Videos/loading.mp4` を敷いている。
- フッター凡例の `決定` / `曲送り` は design 側で 0×0 (不可視プレースホルダ) の
  ため、実装は `戻る` だけを出さない — design と同じく Cap 位置だけを踏襲。

#### テスト

`V6LoadingViewLogicTests` に追加: `CoverCropRect` 4 件 (横長/縦長/正方形/不正)、
`FooterLegend_MatchesDesignCoordinates` (x56 / y30 / h36 / gap10/16 / fs22)、
`FooterLegend_StaysInsideDesignFooter`。EditMode は 1498 pass / 0 fail。

#### 未解決として残す観測

- r15〜r18 の OptionScene は `pass=359 symbols=8` (V6 Option View が二重) で、
  r14 の 181 と非決定的。ローディングの修正とは無関係。追跡項目。
  → §9.13 で原因を特定・修正した。

### 9.13 「Option の要素が二重に監査される」の正体 — 毎フレーム再構築 × フレーム末 Destroy

§9.12 で保留にした OptionScene の `pass=359 symbols=8` / `181 symbols=4` の揺れ。
識別ログ (`NewCanvas` / `OptionSceneManager.Start` / `AUDIT-CANVAS`) を足して
実機で確認したところ、**Canvas も OptionSceneManager も 1 つ**だった:

```
[OptionSceneManager] Start id=... frame=1031
[V5GameUi] NewCanvas V6 Option Canvas id=... scene=OptionScene children=0
[AutoTestV6Screens] AUDIT-CANVAS OptionScene name=V6 Option Canvas id=...
  scene=OptionScene active=True childCount=1 graphics=179
```

原因は Canvas の重複ではなく、**同じ Canvas の中に同じ名前の子が 2 世代同居**
していたこと:

1. `OptionSceneManager.Update` が毎フレーム `optionView.Refresh()` → `Rebuild()` を呼ぶ。
2. `Rebuild()` は Header / Main / Footer を `ClearChildren` するが、破棄は
   `V5GameUi.DestroyNow` = 再生中の `Object.Destroy` (フレーム末)。
3. 同一フレーム内では「古い Footer + 新しい Footer」が両方生きている。
   監査 (コルーチン → Update 後) がこの瞬間に走ると両世代を数える。
   → Graphic 数 179 / 357、symbol 4 / 8。スクリーンショットは描画前に
   破棄が済むため差が出ない (`r18` と `r19` の OptionScene 画像は画素一致)。

修正 (共通原因を先に):

- `Assets/Game/Scripts/UI/V6/Common/V6Hierarchy.cs` を新設し、
  `ClearChildren` の 6 箇所の重複実装 (Option / Title / Select / Result / History /
  Ranking) を集約。破棄を予約する**前に** `SetParent(null)` で階層から外す。
- `OptionSceneManager.Update` の毎フレーム `Refresh()` を削除。表示は入力
  ハンドラと `KeyBindings.Changed` が Rebuild を起こすので不要 (毎フレーム
  全 UI 再構築は GC とレイアウトの無駄でもあった)。
- `V6HierarchyTests` 3 件: 直後に子が 0 / null 安全 / Option の Footer 凡例が
  `KeyCap ↑↓` 1 個だけ (Rebuild 3 回後)。

実機 (2560×1664) の結果 — 2 回連続で同一:

| 実行 | OptionScene | symbols | Canvas 数 |
|---|---|---|---|
| r18 (修正前) | pass=359 fail=0 | 8 | 1 (二重世代を計上) |
| r19 (修正後) | pass=181 fail=0 | 4 | 1 (graphics=179) |
| r20 (修正後) | pass=181 fail=0 | 4 | 1 |

`RESULT pass=25 fail=0 symbols=59 screenshots=12` (r19 / r20 同値)。r18 との
画像差は OptionScene が 0 画素、他は背景アニメーションのフレーム差と
Title のビルド版数表示 (`v0.1.0+be7c68ca`) のみ。EditMode は 1501 pass / 0 fail。

#### 未解決として残す観測 (続き)

- 単一シーン実行 (`-autotest-scenes OptionScene` のみ) では
  `no legacy graphics are visible on OptionScene` が FAIL する。正体は
  DontDestroyOnLoad の `KeyConfigCanvas` (Key / KeyCap F2) で、通常の
  5 シーン連続実行では出ない。シーン順に依存する起動手順の問題で、
  画面の再現とは別件。追跡項目。


### 9.14 第 r21 ラウンド — design 正本画像との全 12 画面突き合わせ

`ffe1147f` のビルド (r21) を 2560×1664 **実フルスクリーン** (Mac の実解像度そのもの) で
撮影し、12 画面すべてを design ノードの実測 bounds と突き合わせた。証跡:
`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r21/` (12 PNG + Player.log)、
`RESULT pass=25 fail=0 symbols=59 screenshots=12 resolution=2560x1664`。

| 画面 | r21 の判定 | 根拠 |
|---|---|---|
| Title (`QJtTv`) | 一致 | ロゴは design の `13.png` (白ベタ 1.99M px + #3FA9F5 影 356K px) と同形。タブは 280 ピッチで隙間なし (design 0/280/560/840)、キーキャップ右端差は 12 対 10 の 2px のみ |
| Select (`OENRv`) | 一致 | 中央パネル / 検索欄 (虫めがね 20 @(12,14)) / 右カラム 2 パネル。ヘッダー見出し "SCORE RANKING"・"PLAYERS"・「累計スコア」は 1 行 (レイアウトグループが preferred width を確保) |
| Select 空 (`AqFtx`) | 一致 | Empty Note 448×140 @(56,300)。文言は実データ由来 |
| Select 設定 (`wSjAm`) | 一致 | 右カラムの設定 4 行と説明 |
| History (`q9JZcH`) | 一致 | 戻るボタン (0,20) 56×56 / 見出し x72 / Sort (864,33.5) 104×29 / Count 右端 1656 / 行スタック縦中央 |
| Ranking (`S1TZD`) | **不一致 → 修正** (§下記) | パネル見出しが 2 行に折り返し |
| Option (`B6If6Z`) | 一致 | 設定パネル (304,68) 512×688 $k2 r16、行 496×56 r8、選択行 #525252 を画素で確認 (行上端 y ではなく +10px 内部で採取 — 上端は AA で暗く見える) |
| Result (`zcSHh`) | 一致 | 5 列 (Perfect/Great/Bad/Miss/Combo)、Grade 280×280、右 2 パネル、判定 5 列。グレードは実スコア 0 のため "–" |
| Result 登録モーダル (`lqHaN`) | 一致 | Dialog 840×510 / Head / Name / Summary / Buttons |
| Game HUD (`Uc9v6`) | 一致 | 判定・ゲージ・SCORE/COMBO・下部キー・Menu (Esc キャップ)。3D プレイフィールドは意図的例外のまま |
| Pause (`uDNC0`) | 一致 | 左設定 4 行 (選択行 #525252)、レーン図、右キー割当 13/12/12/11 |
| Loading (`g4Q3F`) | 一致 | ジャケット + 進捗 (進捗表現は design main が空のため実装が決めた追加要素 — ヘッダーコメントに明記済み) |

**修正した不一致 (Ranking 見出しの折り返し)**:

- 実機: "SCORE RANKING" が "SCORE" / "RANKING"、"PLAYERS" が "PLAYER" / "S" の 2 行、
  「累計スコア」も折り返し。design (`qICtu/Title` 139×26 fs18、`smhrU/Title` 74×26 fs18、
  `smhrU/Metric` 90×26 fs18) はいずれも 1 行。
- 原因: `V5GameUi.Text` の既定が折り返し有効。この書体では "SCORE RANKING" が fs18 で
  ≈145px となり 139px 枠に収まらない。`V6Controls.Text` は NoWrap 既定だが Ranking は
  V5 ヘルパを使っていた (Result の「累計スコア」は既に NoWrap 済みで、同じ症状の先例)。
- 修正: `V6RankingView.BuildScoreRanking` / `BuildPlayers` の 3 ラベルへ `TextWrappingModes.NoWrap`
  を明示 (design の rect は変更しない)。NoWrap 時は 139px を数 px 超えて描かれるが、
  右隣の Song チップは x159 で衝突しない (実測 145px → 右端 157)。
- 回帰テスト: `V6RankingDesignLayoutTests` に
  ① 各見出しの `textWrappingMode == NoWrap`、② `ForceMeshUpdate` 後の `textInfo.lineCount == 1`
  を追加。EditMode 1505 pass / 0 fail (前回 1504 + 1 件)。

**判定を変えなかった点** (design と文字が違うが意図的):

- History の件数ラベル: design `q9JZcH/Count` は "4 plays" だが、実装は「記録 N曲」。
  これは Issue #239 で「プレイ回数ではなくベスト記録のある曲数」を出すと決めた結果
  (`docs/evidence/239-v6-front-door.md:26`)。値を偽らない方針を維持する。
- Ranking が見出し下に Song チップと Local/Online セグメントを持つのは design どおり。
  Online 非表示はローカル曲の仕様 (spec 167)。

**この 1 回で確認できていないこと** (PASS を設計受入の代用にしない):

- 1920×1080 / 2560×1440 の実フルスクリーン。この Mac の実ディスプレイは 2560×1664 のみで、
  窓リサイズによる擬似フルスクリーンは検証として認めない方針のため未実施。
- Windows (exhibit 機) の実機。無断で触れない制約により未実施。
- 実スコアが入った状態の Result / 本番ランキングの描画 (現状はローカル記録のみで
  "NO DATA" / スコア 0)。

### 9.15 r22 / r23 — Ranking 見出しの実機確認と Option 4 タブの design 突き合わせ

**r22 (`21442a14`)** — §9.14 で直した Ranking 見出しの実機確認。2560×1664 実フルスクリーンで
再撮影し、"SCORE RANKING" / "PLAYERS" / 「累計スコア」が 1 行で描かれることを画素で確認した
(`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r22/`、`RESULT pass=25 fail=0`)。

**r23 (`398fe6bc`)** — 自動撮影に Option の 4 タブ個別撮影を追加して初めて、
Display / Sound / Data の右パネルを design と突き合わせた。ここで **3 タブ分の不一致**が出た。

| design | 不一致 (r23) | 原因 |
|---|---|---|
| `EzLfb` (Display) | ① 判定色の凡例 (Judge PERFECT / GREAT / BAD / MISS) が無い。② 説明ブロックが design の Divider y456 ではなく y544 (Play の値) | 右パネルの追加要素を Play タブしか実装していなかった。説明ブロックは 1 つの定数を全タブで共有 |
| `z7joSy` (Sound) | ① 音量の値 (fs40 「100%」) とトラックが無い。② 説明ブロックが y472 ではなく y544 | 同上 |
| `DQHHp` (Data) | 説明ブロックが design の y380 ではなく y544。3 行の説明がパネル下端 (688) を越える | 同上 |
| `B6If6Z` (Play) | 一致 (キー一覧・説明ともに design 座標) | — |

修正 (`Assets/Game/Scripts/UI/V6/Option/V6OptionLogic.cs` / `V6OptionView.cs`):

- 説明ブロックの Divider Y をタブ別に: Play 544 / Display 456 / Sound 472 / Data 380
  (design 実測)。Title は +20 / Desc は +70 の相対配置。
- Display: 判定チップ (24,360) 106×64 r8 を 118 ピッチで 4 個。Marker (37,16) 32×4 r2 の色は
  design の `$text` / `$ok` / `$warn` / `$danger`、Label fs18 `$text-dim` 中央。
- Sound: Value (24,360) 106×58 fs40 + Track (24,420) 464×20 r4 `$k1`。Fill 幅は実音量の比率
  (design の 100% = 464 全幅)。
- 回帰テスト `V6OptionTabPreviewTests` 6 件 (タブ別 Y ×4 / 判定チップ座標と色 / 音量 Fill の
  比例と clamp)。

**design と行数が違う点 (意図的)**: design `EzLfb` の設定列は 9 行 (Live Rank / Hit Timing /
Judge Text / Progress Bar を含む) だが、この 4 つはゲームに設定も挙動も存在しない。
動かない行を描くと「操作できるのに何も起きない」嘘の UI になるため、実データのある 5 行
(Flash / FlashTiming / FlashSpeed / Key Guide / Lyrics) だけを出す。V5 `OptionView` と
Pause Display の `PauseDisplay_HasNoDeadRows` (Issue #167) と同じ判断で、
`V6HierarchyTests.OptionDisplay_HasNoRowsForFeaturesThatDoNotExist` に固定した。
**この 4 行を design どおり出すには機能そのものの実装 (別 Issue) が必要** — 見た目だけの
追加はしない。オーナー判断が必要ならここが論点。

**未検証**: 2560×1440 / 1920×1080 の実フルスクリーン (この Mac の実ディスプレイは 2560×1664
のみ)、Windows 実機 (exhibit 機は無断で触らない)。r24 で上記修正の実機確認を行う。

### 9.16 r24 — Option 4 タブ修正の実機確認と Desc 行送りの不一致

**r24 (`6c2f6d03`、2560×1664 実フルスクリーン)** — §9.15 で直した 3 タブを実機で確認した
(`docs/evidence/v6-repro-2026-11-11/mac-2560x1664-r24/`、`RESULT pass=31 fail=0 screenshots=15`)。

| design | r24 の実測 (design 座標) | 判定 |
|---|---|---|
| `EzLfb` (Display) | 判定チップ 4 個 (PERFECT / GREAT / BAD / MISS) が (24+118i,360) 106×64 で描画。Marker 32×4 の色 = `$text` / `$ok` / `$warn` / `$danger` | 一致 |
| `z7joSy` (Sound) | Value fs40 (24,360) / Track 464×20 (24,420) / Fill = 音量比 | 一致 |
| `B6If6Z` / `EzLfb` / `z7joSy` / `DQHHp` | 説明ブロックの Divider がタブ別 Y (544 / 456 / 472 / 380)、Title +20 / Desc +70 | 一致 |
| `DQHHp` (Data) | **説明文の 2 行目が design より 10px 下 (impl 502 vs design 492) でパネル下端に接近** | 不一致 |
| `B6If6Z` (Play) | 説明文の 2 行目が design 659 に対し impl 666 (7px 下) | 不一致 |

**原因**: `V6Controls.Text` / `V5Ui.Text` は 1 行描画用に `lineSpacing` を
`(1 - lineHeight/pointSize) × 100 = -44.8` (行送りを fs 分 = 24px に詰める) にしている。
説明文はその上へ `lineSpacing = 40` を足していたため、行送りは
`34.75 + 24×0.40 = 44.35px` になっていた。design の実測行送りは
Data タブ 457→492 = 35px / Pause `uDNC0` 621→659 = 38px で、declared は lineHeight 1.4
(fs24 → 33.6px)。40 は「fs 比で 1.4 倍」のつもりが、実際には TMP の**加算**率として
効いて 1.85 倍になっていたのが誤り。

**修正 (`419e81a4`)**: `lineSpacing = 0` (フォント本来の 34.75px) に戻した。
- Option `V6OptionView.BuildDesc`
- Pause `V6PauseView.BuildPreviewPanel` の Desc (同じ 40 が入っていた)
- 回帰テスト `V6OptionTabPreviewTests.DescLines_UseTheDesignLinePitch`
  (TMP の `lineInfo[].baseline` 差を実測し 34.75 ± 1.5px を固定)

**Pause の実測 (r24)**: 1 行目 622 / 2 行目 670 (design 621 / 659)。同じ原因で 2 行目のみ
11px 下がっていた。r25 で両画面の実機確認を行う。
