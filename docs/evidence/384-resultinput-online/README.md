# #384 Result 登録 — オンライン実機受入 (macOS / ローカル API)

実施日: 2026-11-15 / ビルド: `0.1.26+7365b24c` 以降の作業ツリー
(macOS `Builds/macOS/FlashBeat.app`, Unity 6000.5.6f1)

## 構成

- API: `web/` の Void dev サーバ (`vp dev`, http://localhost:5173) + ローカル D1
  (`void db seed`。曲 `who` / chartVersion 1 / noteCount 139)。
- クライアント向け設定: 検証用ビルドのみ
  `Assets/Game/Resources/CloudCatalogConfig.json` の `baseUrl` を
  `http://localhost:5173` に変更してビルド。**検証後に本番 URL へ revert 済み**
  (本番 `https://flashbeat.rebuildup.dev` への書き込みは一切行っていない)。
- ハーネス: `-autotest resultinput` (`AutoTestResultInput.cs`)。
  `-autotest-remote who` でリモート曲のプレイ済みセッションを偽装し、
  登録モーダルをオンライン方式で開く。判定数・スコアは
  `-autotest-perfect/-great/-bad/-miss/-combo/-score` で指定。
- キー入力: `osascript` の `keystroke` / `key code` で実 OS イベントを送出。
  日本語は macOS 標準の Kotoeri ローマ字入力 (IME) 経由。

## 結果

### 文字入力 (実機)

| 経路 | 結果 | 証拠 |
|---|---|---|
| 英数字 (実キー) | `abc123` 入力 → 確定で `ABC123` に正規化・端末内保存 | `../384-resultinput3` (Player.log `name="ABC123"`) |
| 日本語 IME (実キー) | ローマ字 key code → IME が `テスト` に変換・確定文字が入力欄へ | `success/` (Player.log `name="テストABC123"`) |

### オンライン登録 (テスト API)

| ケース | 操作 | 結果 | 証拠 |
|---|---|---|---|
| 成功 | `テストABC123` で Enter 確定 → 自動 POST | `POST /api/v1/scores` → 201、D1 に 1 行格納、ランキング GET に出る | `success/`、D1 `scores` 行、leaderboard `entries:[{"nickname":"テストABC123","score":1000000}]` |
| 二重送信防止 | 成功後に Enter をさらに 3 回 | 追加の POST なし (ワイヤ上も記録なし)、D1 は 1 行のまま | `success/after-enter-mash.png`、D1 `scores` = 1 行 |
| 失敗 (422) | 判定数 0 のセッションで `テストABC123` を確定 | `POST /api/v1/scores` は発行されるがサーバが拒否 (判定合計 ≠ 139)、D1 不変 | `fail422/`、ワーカ observability `POST /api/v1/scores` 05:31、`ClassifySubmitResult(422)→Rejected` は EditMode `RankingSubmissionJudgementSumTests` で固定 |
| オフライン | サーバ停止中に確定送信を試行 | クライアントは送信を試みるが接続不可 (サーバに到達した記録なし)。`UnavailableError → Offline` 分岐は EditMode で固定 | `retry2/` (試行は確認、UI 表示はスクリーンショット `offline-submit.png`) |

### 再試行

`V6RegistrationGate.Fail()` 後は `CanStart == true` に戻り再送できる
(EditMode `ResultRevealTests.RegistrationGateTests.Fail_AllowsRetry`)。
実機での「失敗 → 再入力 → 成功」の一連操作は、フォーカス遷移の都合で
本セッションでは完走できなかった (入力欄は確定時にフォーカスを外す仕様)。
失敗後に再送できること自体は gate の状態機械と EditMode で固定済み。

## 残件

- Win64 実機での同等確認 (Issue #384 の受入条件)。
- 本番 API への実書き込み (オーナー承認が必要。手順は
  `docs/operations/register-online-acceptance.md`)。
- 実機での失敗→再試行の一連操作 (上記のとおり分離して記録)。
