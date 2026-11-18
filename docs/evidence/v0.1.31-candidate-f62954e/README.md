# v0.1.31 リリース候補 包括QA証跡 (2026-11-18)

`release-0-1-31` HEAD `f62954e3095aec7a8149c2234b7572405534daf9` を対象に、
macOS (Apple Silicon / macOS 27.2.1) の実ビルドで `-autotest` ハーネスを実行した記録。
実機レビュー指摘 (#669 配下) と再監査 finding の修正が入った候補であり、
過去の PASS は流用していない。

- ビルド: Unity 6000.5.6f1、`FLASHBEAT_VERSION_BASE=0.1.31` → `Application.version = 0.1.31+f62954e3`
- パッケージ (ローカル `dist/`、未公開):
  - `FlashBeat-0.1.31-macos-f62954e.zip` SHA-256 `ba831f0f…4eb22b`
  - `FlashBeat-0.1.31-win64-f62954e.zip` SHA-256 (package-win64.ps1 出力、MediaPolicy=bundled)
- 実行バイナリ: `FlashBeat.macbuild/Builds/macOS/FlashBeat.app` (上記 ZIP と同一ソース)

## ハーネス結果 (すべて fail=0)

| ハーネス | 結果 | 内容 | 出力 |
|---|---|---|---|
| `-autotest play` | **pass=20 fail=0** | Title→Select→Game→Playback の全経路。v6 タイトル/選曲ビュー、フッター凡例、メディア準備 (V6LoadingView 経路)、再生開始・メディア時計進行・映像再生 | `play/` |
| `-autotest keyguide` | **pass=5 fail=0** | v6 タイトルのフッター凡例: 項目数・日本語フォント・日本語テキスト | `keyguide/` |
| `-autotest v6screens` | **全画面 fail=0** (RESULT pass=90 fail=0, symbols=95) | Title / Select (通常・Refetch・Skeleton・LoadingKeepsRows・Error・Empty・Settings・History・Ranking) / Option (4タブ) / Result (+RegisterModal) / Game (HUD・Pause・Loading)。clip summary: 画面外 selectable/text = 0 | `v6screens/` (スクリーンショット21枚) |
| `-autotest v6songend` | **pass=35 fail=0** | 「ツイッターランド」完走。judged=130 == chartNotes=130 (ランキング受理条件)、コンボ上昇、songEndState=Completed、forcedByDeadline=False、Result 画面の v6 canvas・曲名・スコア表示 | `songend/` |
| `-autotest v6interact` | **pass=97 fail=0** | 画面遷移・モーダル・ボタン操作の実挙動 | `v6interact/` |
| `-autotest v6hold` | **pass=41 fail=0** | ホールドノーツを含むプレイ→結果 | `v6hold/` |
| `-autotest resultinput` | modalOpened=True | Result 登録モーダルは開く。テキスト入力は「実キー入力」を前提とするハーネスのため、ヘッドレス実行では name="" のまま (下記「未観測」参照) | `resultinput/` |

## この証跡がカバーする範囲

- **画面到達・レイアウト**: 全 v6 画面で selectable/text の画面外はみ出し 0 (v6screens clip summary)
- **実プレイ経路**: 実曲のダウンロード→準備→再生→判定→結果登録まで完走 (v6songend / v6hold)
- **判定・コンボ**: judged 数と譜面ノーツ数の一致、コンボ・最大コンボの記録
- **曲終了**: end state machine 完走、強制完了でないこと、Result 遷移
- **入力 UI**: 結果登録モーダルの起動 (resultinput)

## 未観測・未検証 (機械検証 PASS の代用にしない項目)

- **実機 (Win64 / 展示機) での観測**: 本証跡は開発 Mac の macOS ビルド。Win64 パッケージは生成済みだが実機実行は未実施
- **視覚的確認**: スクリーンショットは生成したが、本モデルでは画像内容を目視検証できない。ノーツの白さ (#675)、判定落下アニメーション (#671)、歌詞パネル寸法 (#674)、プレビュー再現性 (#677) の「見た目の正しさ」は人間の目視または実機確認が必要
- **音声**: クリック音 (#672/#682)・判定音の実際の音色/音量/同時押しは聴取未実施
- **リザルト入力の実タイピング**: resultinput はモーダル起動まで。文字・カーソル・IME・選択の余白 (#676) は実キーボード入力での確認が必要
- **アップデータの実ファイル副作用**: 境界は EditMode/シェルテストで検証済みだが、実際の更新 ZIP 適用・異常終了リカバリの通し確認は未実施
- **ポーズ中の音量・再開**: ApplyMainVolume の両 AudioSource 反映は EditMode テスト済み。実聴での確認は未実施

## 観測された非致命的ログ

- `AVFoundationVideoMedia error while reading ... resources.resource` (1件) — 動画メディア読み取りの警告。全シーンのスクリーンショット取得と songend 完走は成功しており、致命的ではないが記録として残す
- `Color primaries is unknown or unsupported by AVFoundationVideoMedia` — 色空間フォールバック警告 (色味の確認は目視項目)

## 関連する機械検証 (同一コード面)

- Unity EditMode: 2034/2034 PASS (`b2733484` 時点。以降の差分は docs (#698) と検証補助コード (#700) のみで、EditMode 対象のプロダクト/テストコードに変更なし)
- `script/compile-check.sh`: flashbeat / tests / tests-editor すべて PASS
- updater シェルテスト: Update.ps1 scope 46/46・integration 34/34、update-macos.sh scope 36/36
- web: npm test (Node 156 + Vitest 453 + E2E 22) PASS、format/lint 0 warnings
