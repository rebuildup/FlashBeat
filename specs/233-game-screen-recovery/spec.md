# Spec: 233 — ゲーム画面復旧 (旧 UI 排他制御 / コード側)

Issue: #233 ([P0 BLOCKER/Owner real PC] ゲーム画面崩壊・プレイフィールド不可視・HUD重複) — branch `w/e233`。

## 目的

GameScene で「いま表示してよい UI はどれか」を **単一の正本** で決め、
旧 (v5) UI と v6 UI が同時に表示されない状態を保証する。
実 Win64 での画面確認は Owner 作業のため、本 spec はコード側の排他制御と
自動テストを対象にする。

## 背景 (Issue #233 で指摘された原因)

- `GameHudRuntime.HideLegacyHud` が **Start 時の 1 回限り** の名前ベース
  `SetActive(false)` で、後から生成 / 再有効化された旧 UI を取りこぼす。
- v5 の `GameHudView` (HUD) が Ready 中も常時可視で、`GameReadyView` と同時表示
  になる (HUD の Combo 0 が巨大表示され、曲情報と衝突する)。
- v6 側 (`V6UiMode.HideLegacyV5Canvases`) は毎フレーム再適用方式で再実行可能だが、
  v5 側に同じ構造がない。

## 要件

1. **状態の単一正本**: `GameUiPhase` (Loading / Ready / Playing / Paused / Failed) を
   1 箇所 (`GameUiExclusivity`) で定義する。フェーズの入力は既存の観測値のみ
   (HudPlayState.Evaluate の結果、GManager.played、PauseMenuView.IsOpen)。
   新しい通知経路は導入しない。
2. **決定的な再適用**: 遷移ごとに v5 の Canvas / View の表示 / 非表示を
   状態から再適用する (名前ベースの 1 回処理を廃止)。純粋関数
   (`ResolvePhase` / `Resolve`) と冪等な適用 (`Apply`) に分け、EditMode テストから
   直接呼べるようにする。
3. **両モードで正しい排他**:
   - v6 無効 (`V6UiMode.GameHud == false`): 従来どおり v5 が表示を担う。
     Ready 中は Ready 専用表示 (HUD は隠す — Issue #233 の二重表示是正)、
     再生中は HUD、ポーズ中は HUD の上にポーズ (別 Canvas なので両方可視が正)、
     Loading / Failed はそれぞれ単独のオーバーレイ。
   - v6 有効 (`V6UiMode.GameHud == true`): 表示の所有は v6。v5 の描画要素
     (Ready / HUD / Loading / Error) は全フェーズで非表示。例外は v6 ポーズ無効時の
     v5 ポーズのみ (未移植分の役割維持)。
4. **旧 UI 非表示の再適用**: レガシー HUD (SpacetoStart / ScoreText / など) の
   非表示を Start の 1 回から定期的な再適用 (0.5 秒間隔、冪等) に変える。
5. **自動テストの前提更新**: `AutoTestV6` の「旧 UI が残っていないか」検査から
   V5ReadyCanvas の免除を外す (v6 モードでは全フェーズで非表示が正)。

## 非対象

- 実 Win64 での目視確認・画面撮影 (Owner 作業。`docs/evidence/233-legacy-ui-exclusivity.md` に残存リスクとして記録)。
- プレイフィールド / 動画描画 / カメラ / 判定の修復 (Issue #233 の別タスク)。
- 新しい UI 要素の追加 (Pencil v6 契約にないものは作らない)。

## 完了条件

- 上記 1–5 の実装と EditMode テスト (`GameUiExclusivityTests`) が揃い、
  `script/compile-check.sh --assembly flashbeat` と tests アセンブリの
  compile-check が通ること。
- `docs/evidence/233-legacy-ui-exclusivity.md` に実装・検証・残存リスクを記録すること。
