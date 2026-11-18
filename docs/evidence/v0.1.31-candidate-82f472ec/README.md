# v0.1.31 リリース候補 包括QA証跡 (2026-11-19)

`release-0-1-31` の包括QA記録。対象 SHA は 2 段階:

- **`f62954e3`**: ハーネス群の一括実行 (play/keyguide/v6screens/v6songend/v6interact/v6hold/resultinput)
- **`82f472ec`**: 最終候補。`f62954e3` との差分は `AutoTestPlay.cs` へのクリック音実再生チェック追加のみ (PR #703) で、プロダクトコードは同一。そのため一括結果は `f62954e3` のものを有効とし、`play` のみ `82f472ec` で再実行して新規チェックを含めて確認した (`play-82f472ec/`)

- ビルド: Unity 6000.5.6f1、`FLASHBEAT_VERSION_BASE=0.1.31` → `Application.version = 0.1.31+82f472ec`
- パッケージ (ローカル `dist/`、未公開): `FlashBeat-0.1.31-{macos,win64}-82f472ec.zip` + `.sha256`
- 実行バイナリ: macbuild `FlashBeat.app` (上記 ZIP と同一ソース)

## ハーネス結果 (すべて fail=0)

| ハーネス | SHA | 結果 | 内容 | 出力 |
|---|---|---|---|---|
| `-autotest play` | f62954e3 | pass=20 fail=0 | Title→Select→Game→Playback 全経路 | `play/` |
| `-autotest play` | 82f472ec | **pass=22 fail=0** | 上記 + レーンキー押下で judge AudioSource が実際にワンショット再生 (クリック音 #672/#682 の実挙動) | `play-82f472ec/` |
| `-autotest keyguide` | f62954e3 | pass=5 fail=0 | v6 タイトルのフッター凡例: 項目数・日本語フォント・日本語テキスト | `keyguide/` |
| `-autotest v6screens` | f62954e3 | 全画面 fail=0 (RESULT pass=90 fail=0, symbols=95) | Title / Select (7状態) / Option (4タブ) / Result (+RegisterModal) / Game (HUD・Pause・Loading)。clip summary: 画面外 selectable/text = 0 | `v6screens/` (スクリーンショット21枚) |
| `-autotest v6songend` | f62954e3 | pass=35 fail=0 | 「ツイッターランド」完走。judged=130 == chartNotes=130、コンボ記録、songEndState=Completed、forcedByDeadline=False、Result の v6 canvas・曲名・スコア | `songend/` |
| `-autotest v6interact` | f62954e3 | pass=97 fail=0 | 画面遷移・モーダル・ボタン操作の実挙動 | `v6interact/` |
| `-autotest v6hold` | f62954e3 | pass=41 fail=0 | ホールドノーツを含むプレイ→結果 | `v6hold/` |
| `-autotest resultinput` | f62954e3 | modalOpened=True | 登録モーダルは開く。実キー入力前提のためヘッドレスでは name="" のまま (下記「未観測」) | `resultinput/` |

## 更新処理の実ファイル副作用 (Issue #670)

`update-macos.sh` を擬似インストールへ実実行し、正常適用・SHA 不一致失敗・.app 拒否の
3 経路で「全書き込みが `<install>/.flashbeat-update/` 内・境界外ゼロ・失敗時無変更・
拒否時無書き込み」を観測。詳細は `updater-side-effects.md`。
Windows 側は `Run-UpdateFlowTests.ps1` (34/34) が実 FS で同等検証済み。

## 描画出力のピクセル検査 (#675 補助証跡)

スクリーンショットの実ピクセルを集計 (内部状態 assert ではなく描画結果):

- `v6hold-04-hold-body.png`: ホールド中のプレイフィールド領域 (x=0–640, y=768–1024) に
  純白 (>200,>200,>200) 画素が約 2,000 個 (2x2 サンプリング) 集中 — ノーツ/ホールドバーが
  白く描画されていることの描画証跡
- `v6songend-06-03-gameplay.png`: 画面左上の HUD 領域に判定/スコアの白文字。
  プレイフィールド中央は暗い背景上にノーツが少数 (この瞬間の譜面位置依存)

## この証跡がカバーする範囲

- 画面到達・レイアウト: 全 v6 画面で画面外はみ出し 0
- 実プレイ経路: 実曲 DL→準備→再生→判定→結果登録まで完走 (2 曲分: play の songID=1、songend のツイッターランド、v6hold)
- 判定・コンボ・曲終了状態機械
- クリック音の実 AudioSource 再生 (状態レベル。音色・音量の聴取は未実施)
- 更新の実ファイル副作用 (macOS 実スクリプト適用 + Windows フローテスト)

## 未観測・未検証 (機械検証 PASS の代用にしない項目)

- **実機 (Win64 / 展示機) での観測**: 本証跡は開発 Mac の macOS ビルド。Win64 パッケージは生成済みだが実機実行は未実施
- **視覚的確認**: スクリーンショットは生成・ピクセル集計したが、ノーツの白さ・判定落下アニメーション・歌詞パネル寸法・プレビュー再現性の「見た目の正しさ」は人間の目視が必要
- **音声**: クリック音が「鳴る」ことは AudioSource 状態で確認済み。旧音源との同一性 (Click 2.wav)・音量・同時押しの聴感は未確認
- **リザルト入力の実タイピング**: resultinput はモーダル起動まで。文字・カーソル・IME・選択の余白 (#676) は実キーボード入力での確認が必要
- **実更新の通し**: ローカル ZIP 経路は検証済み。実 Release からの DL・Relaunch・異常終了復旧は未実施
- **ポーズ中の音量・再開**: ApplyMainVolume の両 AudioSource 反映は EditMode 済み。実聴は未実施

## 観測された非致命的ログ

- `AVFoundationVideoMedia error while reading ... resources.resource` — 動画メディア読み取り警告。全シーンのスクリーンショットと songend 完走は成功
- `Color primaries is unknown or unsupported by AVFoundationVideoMedia` — 色空間フォールバック警告 (色味の確認は目視項目)

## 関連する機械検証 (同一コード面)

- Unity EditMode: 2034/2034 PASS (`b2733484` 時点。以降の差分は docs・検証補助コードのみ)
- `script/compile-check.sh`: flashbeat / tests / tests-editor すべて PASS
- updater シェルテスト: Update.ps1 scope 46/46・integration 34/34、update-macos.sh scope 36/36
- web: npm test (Node 156 + Vitest 453 + E2E 22) PASS、format/lint 0 warnings
