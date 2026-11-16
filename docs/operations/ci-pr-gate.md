# CI / PR 検証の責任範囲

現行規約: [開発収束・品質ゲート](development-convergence.md)（2026-11-16）。PR が green でも、実行されていない検証を合格したことにはならない。

| 段階 | 実行タイミング | 作業 | 成功が証明すること |
|---|---|---|---|
| L0 | 変更単位 | 仕様・差分レビュー、関連する deterministic test | 対象のロジック契約を満たす。Unity compile とは区別 |
| L1 / `verify` | 全 PR、関連 main push | `script/check-repo-integrity.sh`、`tools/acceptance/fullscreen-report.test.ps1` | Meta/JSON/改行/受入ツールと CI の低コスト契約。**Unity compile は検証しない** |
| L1 / `web/check` | Web の PR と main push | TypeScript typecheck、Web unit、production build | Web の型・ロジック・バンドル。**Playwright E2E ではない** |
| L2 | 関連 C# 修正をまとめた時 | 単一 Unity Editor/Library 所有者で compile、関連 EditMode tests | Unity バイナリの C# compile と該当テスト。変更 SHA と TestResults.xml / results を記録 |
| L3 / `web/browser-e2e` | 候補 release cut で `workflow_dispatch` | ローカル D1 seed + Chromium Playwright | 対象 Web flow が隔離環境で成立。**本番 API 書き込みの証明ではない** |
| L3 / `build-win64` | 候補 release cut で `workflow_dispatch` | Unity build/package。`run_tests=true` のときだけ EditMode を実行 | ソース SHA の成果物生成。`run_tests=false` の green は EditMode 合格を証明しない | 
| L3 / `build-macos` | 候補 release cut で `workflow_dispatch` | Unity build/package。**EditMode テストは含まない** | 対象 SHA の macOS 成果物生成。EditMode や実機プレイの成功ではない |

## 必須境界

- `verify.yml`: PR なら対象パスに関わらず必ず軽量 gate を出す。`check-repo-integrity.sh` は重い main push/PR CI を戻してしまう事故も検出する。
- `web.yml`: PR/main では `check` のみ、`browser-e2e` は `workflow_dispatch` の場合だけ実行。
- `build-win64.yml` / `build-macos.yml`: main push に反応させない。候補統合の一区切りで明示起動する。毎 Issue や毎 PR の新規 build は不要。
- Unity の hosted runner はライセンスの制約がある。ライセンス起因の失敗を C# コードの失敗とも成功とも扱わない。**Unity を変更した PR のコード品質は、L2 の実際の compile と tests で保証する**。L2 が未実行なら `compile-unverified` と明記して統合の証明を保留する（ただし実機撮影待ちはしない）。
- tests の 0 件収集 / 全 skipped / fixture だけの成功 / スクリーンショット取得完了を機能合格と呼ばない。
- 実 Win64/macOS 端末、目視デザイン最終評価、実 GPU performance 計測は**Issue close / PR merge の必須条件ではない**。必要に応じ release 候補で観測を追加し、観測できていなければ未測定と書く。新たな欠陥はコード側の再現ケースに変換する。
- リリース公開/本番書き込みの権限と出荷物契約: [update-release.md](update-release.md)、[production-api-acceptance.md](production-api-acceptance.md)。公開承認境界と開発マージを混同しない。

過去の状況 (2026-11-10): Unity ビルドは main push ごとに自動実行され、Web の Playwright も PR ごとに実行されていた。これは **PR #522 で停止**した旧運用であり、以下の current gate 表にその条件を戻さない。
