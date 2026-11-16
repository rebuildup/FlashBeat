# 開発収束・品質ゲート運用 (2026-11-16)
 
正本: [#250](https://github.com/rebuildup/FlashBeat/issues/250)。本書は「何をいつ検証するか」の運用契約であり、製品の個別仕様は各 Issue / `specs/*` / ADR を正本とする。旧展示向け文書の「毎PR/毎Issueで実機」「オーナー目視まで実装完了不可」と矛盾する場合は、本書が**開発工程の判定**に優先する。**製品機能・品質基準を削除するものではない**。

## 1. 分離する3つの状態

1. **Code-complete**: 受入条件をコード・再現可能な自動テストで満たし、差分に関係する軽量検証と独立レビューを完了。根拠 SHA / 実行コマンド / 結果 / 未確認事項を PR に記録した状態。実機操作・目視画像・Windows ホストは必要条件にしない。Unity に関係するコード差分が未コンパイルなら code-complete と主張せず `compile-unverified` と明記。
2. **Integrated**: code-complete の差分を開発統合先へ反映。利用可能なソース品質ゲートが green。ブロックするのは再現可能な欠陥、契約違反、Unity 関連コード差分の未検証コンパイル、データ破壊など**具体的な失敗**であり、機器の不在自体ではない。
3. **Release-candidate**: 一区切りの統合後にだけ build / 全 EditMode / Web Playwright E2E / 成果物のバージョン・SHA・権利検査をまとめて行う。結果は該当 SHA に紐付ける。実機手動操作・オーナー目視は任意の追加観測であり、開発・Issue の完了要件に戻さない。物理デバイス特有の不具合を見つけたら別途再現条件と回帰テストを作成する。

公開・配布チャネルや本番データ書き込みの承認境界は既存 `docs/operations/update-release.md` / `production-api-acceptance.md` の権限ルールに従う。「実機で遊んだこと」と「コード品質」を混同しない。未測定項目を measured/PASS と書かない。

## 2. 品質と所要コストのレーン

| レーン | いつ | 必須の検証 | 禁止・省略する作業 |
|---|---|---|---|
| L0 scope/implementation | 各 bounded Issue / PR | Spec/受入条件の整合、影響範囲の差分レビュー、関連するピュアロジックのテスト・失敗再現 | 画面起動、毎回 full Unity build、毎回全画面×多解像度を撮影しない |
| L1 cheap CI | PR / 通常 main 反映 | `verify` の Unity asset/meta/json/整合性、Web 変更のみ `web/check` (tsc/unit/build)、PR review | Unity ビルド、Playwright、本番デプロイ、実端末スモークを発火させない |
| L2 Unity batch compile | 同一領域で関連改修がまとまった時／候補凍結前 | 単一 Editor 所有者でコンパイルと関連 EditMode、候補では**全 EditMode** (失敗 0 / 収集 0 の偽成功禁止) | 独立 worktree ごとに別 Unity Editor を起動しない。実機 GUI 操作不要 |
| L3 candidate | 機能の一区切り・release 候補のみ (明示起動) | 候補 SHA の全 Unity EditMode を L2 で実行し、Win/Mac build を必要な対象に対して実施、Web `workflow_dispatch` E2E、成果物 ID/SHA、media policy、代表的な contract/scenario 証拠 | PR ごとの重い CI/デプロイを行わない。実機を「次へ進めない」理由にしない |

GitHub Actions: `verify.yml` は PR で走る。`web.yml` の `check` は Web の PR/push で走り `browser-e2e` は `workflow_dispatch` 限定。`build-win64.yml` / `build-macos.yml` は `workflow_dispatch` 限定。GitHub-hosted Unity のライセンス未設定を**コードの failure** と誤読しない。Unity 関連コードの品質根拠はコンパイル成功・自動テスト・レビューの組。ドキュメント/Web のみの変更に Unity compile は要求しないであり、CI 緑の「範囲」を PR で明記する。

## 3. 実機依存要求を完了条件にしない方法

- 画面: レイアウトの不変条件（主要操作は画面内、入力到達性、スクロール可能領域、文字・領域の交差）を`V6ScreenAudit` 等で機械検査する。report-only の検出を PASS と誤読しない。画像は必要に応じて自動キャプチャし、スナップショット比較の基準をソース管理する。目視は探索であって完了の唯一根拠ではない。
- ゲーム: 判定/スコア/HOLD/メディア時刻と一時停止の純粋ロジックは決定的 fixture で固定。操作フローは状態遷移テストと偽 API に対する失敗→再試行→成功シナリオで固定。ドライバ依存の実 FPS 数値は未測定と正直に残す。
- 資源と性能: cache entry / bytes/TTL / scene unload の上限・所有・再入場を決定的にテスト。Profiler の前後差は性能改善を「実測した」と宣言したい場合に限る。実機未測定を理由に correctness の Issue を開け続けない。
- Web: DOM / Playwright で入力・表示・overflow/console error・API 結線を自動判定。差分画像の人間の好みは別のデザインレビューで記録し、コード品質 gate と混同しない。外部 API/本番書き込みはモックだけで本番 PASS としない。
- いずれも `tests skipped`、未到達、fixture だけの成功、単なるスクリーンショット撮影成功を「機能 PASS」に変換しない。

## 4. Issue を収束させる規則

- 各 Open Issue を `implementation` / `code-verified` / `release-observation` / `contract-deferred` / `obsolete-with-proof` に分類し、**次のコード作業・不合格を再現できるテスト・根拠 PR**のいずれかを記録する。単に「実機待ち」「オーナー判断待ち」は最終状態にしない。
- `release-observation` だけなら製品の未実装ではない。観測用のチェックリストを release 管理 Issue に移し、実装 Issue をテスト根拠付きで Close できる。新たなコード欠陥が見つかれば具体的な子 Issue を開く。**未達の機能を観測へ偽装しない。**
- 後方互換性や仕様判断未確定（例 #30 D1/D5）は既存形式の維持を暫定契約とし、**破壊的移行は実施しない**。未決事項は feature-deferred として記録するが、無関係な実装は継続する。
- 優先度は (1) 実害のある再現可能な P0、(2) 自動テストで閉じられる機能欠陥、(3) 開発を止める仕組みの排除、(4) 純粋な release 観測。子 Issue の独立完了を親の全実機ゲートで無効化しない。
- 1 Issue 1 PR/Owned paths を保ちつつ独立レビューは差分の危険度に応じる。テスト増殖より**回帰の実害**を先に定義し、既存テストとの重複を避ける。実装・レビュー・統合は worker が実行し、ユーザーへの逐次承認依頼を行わない。権限が必要な外部操作や公開だけ境界を守る。
- 1サイクルは「まとめて実装 → L0/L1 → L2 (必要時) → 統合 → 在庫を再分類」。全件コードを終えてから L3 を一回の候補として実施する。release→main の承認がプロジェクトの保護ルールで必要な場合だけ従う。

## 5. 直近の収束順

1. **#96 / #523**: `1024×768/1280×720` の操作喪失は**実バグ**であり、スクリーンショット不足ではない。スケール 1 のままならレスポンシブ再配置などで全主要操作・文字を見える状態にする。新契約と境界レイアウト/入力到達性テストを #96 の spec/Issue に記録する。解像度ごとの別実機確認を行わない。
2. **#75 / #59 / #242**: 既存の検証コードと実測証拠を整理し、未テストの製品動作はテストで補う。画面目視・実Win64 完走の「観測のみ」を実装タスクから切り離す。
3. **#219**: 現行 3D を壊さず、残る v6 Canvas の期待動作を仕様/自動テストで判定。表示方式の切替は確定契約に従う。実機画像比較待ちだけでは止めない。
4. **#381/#397/#416**: 未計測 FPS/Native 性能を correctness の必須 gate としない。リスクの高い cache ownership、再入場・失敗 callback を deterministic tests で担保し、定量性能 claim は未検証と区別する。
5. **#30**: HOLD 確定契約を維持。D1〜D6 は既存互換を default とし、未決定を理由に HOLD/歌詞表示/キーガイドの既存機能確認を止めない。

最終判定は「ソースを確認した / 未実施 / 実測した」を厳格に区別する。Issue 数だけを減らすことは目的にしない。
