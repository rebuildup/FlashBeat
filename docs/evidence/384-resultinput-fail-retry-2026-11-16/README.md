# #384: Result 登録「失敗 → 再入力 → 成功」の一連操作 (macOS 実機)

## これは何の証拠か

Issue #384 の受入条件「ランキングへの書き込みはテスト環境で確認する」のうち、
**登録失敗 → 再入力 → 成功を Result 画面から 1 セッションの連続操作として
実行した記録**。本番 API への書き込みは行っていない。

## 環境

- Player: `Builds/macOS/FlashBeat.app` (main `8402f33a` + PR #519、
  `CloudCatalogConfig.json` のみ一時的に `http://localhost:5199` へ変更してビルド。
  ソースは検証後に本番値へ復元済み)
- API: `web/` の Void dev サーバ `http://localhost:5199` (ローカル D1)
- 起動: `-autotest resultinput -autotest-remote who -autotest-perfect 139
  -autotest-score 1000000 -autotest-combo 139 -autotest-seconds 300`
- 操作: macOS の実キーイベント (System Events keystroke / CGEvent)。
  入力はペーストとキー入力の併用 (検証中に IMK の文字確定が不安定だったため、
  クリップボード経由の貼り付けで文字列を入れた。キー削除・Enter・Cmd+A は実キー)。

## 実行した連続操作 (Player-run8.log)

1. ResultScene を開き、登録モーダルが自動で開く。保存名 `テストABC123` が復元される。
2. 名前欄をクリックしてフォーカスし、`Q` + `FAILNAME12345` を入力
   → `QFAILNAME12345` (14 文字。client 上限 16 以内・server 製品上限 12 超過)。
3. Enter で確定・送信 → **サーバが拒否** (`leaderboard-after-fail-run8b.json` に
   追加なし)。入力文字列は保持されたまま、モーダルは再試行可能な状態で残る。
4. もう一度名前欄をクリックしてフォーカスを戻し、Cmd+A で全選択 →
   `RETRYOK` を入力 → Enter で再送信 → **成功**
   (`leaderboard-after-success-run8.json` に `{"nickname":"RETRYOK","score":1000000}` が追加)。
5. `autotest-resultinput-result.txt`: `modalOpened=True name="RETRYOK"`。

## 判定できること

- 失敗後も入力欄へ再フォーカスでき、文字の置き換え・再送信が機能する。
- 二重送信ガードを挟んで失敗 → 再入力 → 成功が 1 セッションで完走する。
- 書き込み先はローカル D1 のみ (leaderboard JSON は `localhost:5199` の応答)。

## 判定できないこと・注意

- IME (日本語) の入力は本 run では未実施 (ASCII のみ)。日本語 IME の実機確認は
  `docs/evidence/384-resultinput-online/` 系の先行証跡を参照。
- `Player-run2.log`〜`run7.log` はフォーカス/IME が不安定だった試行の記録で、
  成功証拠ではない (run8 が連続操作の完走記録)。
- 検証前のローカル D1 には先行試行の `テストABC123` が残っている
  (ローカル環境のみ。本番には一切書き込んでいない)。
