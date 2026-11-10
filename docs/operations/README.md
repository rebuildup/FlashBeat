# docs/operations — 運用ドキュメント一覧

FlashBeat の展示・リリース・クラウド運用に関する文書の一覧です。
**どの文書が誰向けか**を起点に並べています。

上位の約束: [`constitution/CONSTITUTION.md`](../../constitution/CONSTITUTION.md) /
運用プロファイル: [`organization/profiles/release-driven-solo.md`](../../organization/profiles/release-driven-solo.md)。

---

## 一覧

| 文書 | 誰向けか | 何が書いてあるか | 読むべき場面 |
|---|---|---|---|
| [`exhibit-runbook.md`](exhibit-runbook.md) | **展示運営者**（展示 PC でゲームを起動する人） | 起動手順、展示フォルダの場所、起動時の自動更新、オフライン時の挙動、展示中のパッチ適用手順、復旧手順、やってはいけないこと | 展示の前・展示中・展示後。**まず読むのはこれ** |
| [`festival-runbook.md`](festival-runbook.md) | 展示運営者 + リリース承認者 | 旧世代の展示フォルダ記録、キーボード操作早見表、1280×720 スモークチェックリスト、証跡の置き場所、未成立事項 | 具体的な操作確認と、展示成果物の来歴の確認 |
| [`update-patch.md`](update-patch.md) | 展示運営者 + 開発者 | 更新の仕組み（起動時確認 → staging → 終了時適用）、更新チャンネル（stable / preview）、`Update.ps1` のパラメータと終了コード、手動適用手順 | 更新が効かないとき、`Update.ps1` を実行するとき |
| [`update-release.md`](update-release.md) | リリース作成者（Packaging / Release 発行） | 配布物の仕様（タグ / アセット命名 / `.sha256` 形式 / `version.json`）、**版数ルール**、候補の判定規則、パッチ版の発行手順、既存バイナリ互換 | 新しい Win64 ビルドを Release として出すとき |
| [`release-approval-checklist.md`](release-approval-checklist.md) | リリース承認者 | 成果物の fingerprint、プレイ証拠、1280×720 UX スモーク、秘密情報チェック、承認記録の書式 | 「このビルドを展示に使ってよいか」を判断するとき |
| [`festival-development-path.md`](festival-development-path.md) | 開発者 + リリース担当 | 役割分担（Mac / Windows 実機 / CI）、展示中に反映できるものと再ビルドが要るもの、コンテンツ更新手順、CI → Release → 運営者適用の手順、展示 PC を中断させない運用ルール、現時点の制約 | 展示を止めずに開発とパッチ配布を続けたいとき |
| [`unity-ci-license.md`](unity-ci-license.md) | オーナー + 開発者 | Unity CI が緑になる条件 (secret の名前と挙動のみ。値は書かない)、ライセンス失敗とソース失敗の切り分け | Unity ビルドを CI で緑にしたいとき |
| [`cloudflare-bootstrap.md`](cloudflare-bootstrap.md) | 開発者 + 運用者 | Cloudflare アカウント / 認証情報の扱い、Infisical の束縛、ツール（`cf` / `void`）の固定版、権限と禁止境界、再検証手順 | クラウドを触るとき、権限が疑わしいとき |
| [`d1-migrations.md`](d1-migrations.md) | 開発者 | D1 のスキーマ（3 テーブル）、migration ファイル、ローカル / リモートの適用手順、制約、テスト | スキーマを変えるとき、公開カタログが壊れたとき |
| [`ci-pr-gate.md`](ci-pr-gate.md) | 開発者 + レビュー担当 + オーナー | PR で走る検証と走らない検証 (Unity は PR では走らない)、ライセンス不要の `verify` / `web`、Unity ビルドの責任範囲、緑の PR の読み方 | PR を出す・レビューするとき、CI が赤い/空のとき |
| [`production-api-acceptance.md`](production-api-acceptance.md) | 開発者 + オーナー（承認者） | 本番環境での公開 API 受け入れ（#73 / #135）の実施条件。変更するデータ、復旧方法、必要権限、手順、やらないこと | 本番で書き込みを伴う受け入れを行うか判断するとき |
| [`spec-decisions-2026-11-10.md`](spec-decisions-2026-11-10.md) | オーナー + 開発者 | 受入フェーズの仕様判断の整理: #30 (HOLD は #164 で凍結済み / D1 は歌詞形式 / D1・D5 は決定まで実装しない)、#222 (45 曲はスケーラビリティ要件)、#219 (展示は 3D)、#129 (Mac アプリの棚卸しと同一 SHA 併置) | 「この仕様は決まっているのか」を確認するとき、実装を始めてよいか判断するとき |
| [`issue-241-vs-343-display-settings.md`](issue-241-vs-343-display-settings.md) | オーナー + 開発者 | Display 設定 (Key Guide / Lyrics) の寿命について #241 の記述と #343 の実装が矛盾している件。影響範囲と 2 案の差分 (未決定・コード変更なし) | 表示設定の永続化をどうするか決めるとき |
| [`windows-machine-availability-2026-11-10.md`](windows-machine-availability-2026-11-10.md) | オーナー + 開発者 | Herdr 経由で到達できる Windows 機の読み取り専用調査結果。**`device-windows` は展示 PC と同一の可能性が高い** (引き継ぎ文書 + 実機ログの GPU 一致) ため、同一性の確認が取れるまで接続・操作しないこと、画面キャプチャで証明できる項目とできない項目 | Windows 実機の受入をどこで行うか決めるとき |

---

## 読む順序の目安

| 目的 | 読む順 |
|---|---|
| 展示を運営したい | [`exhibit-runbook.md`](exhibit-runbook.md) → （操作の細部は [`festival-runbook.md`](festival-runbook.md)） |
| 展示中に譜面やカタログを更新したい | [`festival-development-path.md`](festival-development-path.md) §3–§4 → [`d1-migrations.md`](d1-migrations.md) |
| 展示中にコード修正を配りたい | [`festival-development-path.md`](festival-development-path.md) §5 → [`update-release.md`](update-release.md) → [`exhibit-runbook.md`](exhibit-runbook.md) §6 |
| リリース承認の判断をする | [`release-approval-checklist.md`](release-approval-checklist.md) → [`update-release.md`](update-release.md) |

---

## このディレクトリの外にある関連資料

| 場所 | 内容 |
|---|---|
| [`../evidence/index.md`](../evidence/index.md) | 成果物 fingerprint と証跡の索引 |
| [`../architecture/festival-mvp.md`](../architecture/festival-mvp.md) | 全体のアーキテクチャ決定と契約 |
| [`../../web/AGENTS.md`](../../web/AGENTS.md) | Web Studio（Void / Vite+ / D1）の構成とコマンド |
| [`../../release/check/README.md`](../../release/check/README.md) | リリースゲートの補助スクリプト（`verify-stage.ps1` / `drive-gameplay.ps1` / `redact-log.ps1`） |
| [`../../tools/release/package-win64.ps1`](../../tools/release/package-win64.ps1) / [`../../tools/update/Update.ps1`](../../tools/update/Update.ps1) | パッケージングと更新適用の実装 |

## このディレクトリの運用ルール

- 各文書は **1 つの事実の正本**を持ちます（同じ事実を複数の文書で別の書き方にしません）。
- 手順を直すときは、**実装か Release の実物と突き合わせてから**書きます。推測で書き換えません。
- 秘密値（トークン / 署名付き URL / 資格情報）は、どの文書にも書きません。
- 新しい運用手順は、まず **この一覧に 1 行足す** とともに追加します。
