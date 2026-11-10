# CI / PR 検証の責任範囲

**緑の PR は「Unity が検証済み」を意味しません。** どの検証がどこで走るのかを
1 か所にまとめ、PR のチェック欄を誤読しないための文書です。

最終更新: 2026-11-10 (main `7ccd636` 時点の実測)

---

## 1. よくある誤読 (実際に起きたもの)

> 「PR #343 はチェックが 0 件だった。Unity ライセンスの secret が未登録で
> ビルドが失敗したからだ」

**これは誤り。原因は別。**

| 事実 | 根拠 |
|---|---|
| Unity のワークフロー (`build-win64.yml` / `build-macos.yml`) には **`pull_request` トリガーが無い**。あるのは `workflow_dispatch` と **main への push** だけ | 各ファイルの `on:` |
| したがって PR では Unity のジョブが 1 つも起動せず、**チェック欄は空になる** | `gh pr checks <n>` が `no checks reported` |
| ライセンス未登録は**別の問題**で、**main へ push したとき**に `license-precheck` ジョブが落ちる原因 | `gh run view <id> --log-failed` → `UNITY_EMAIL と UNITY_PASSWORD を Actions Secrets に登録してください` |

つまり「PR に Unity のチェックが出ない」のは**設定どおりの挙動**であり、
「PR が Unity CI を通過した」ことを表しません。

---

## 2. 何がどこで走るか

| Tier | トリガー | 走るもの | ライセンス | 緑が意味すること |
|---|---|---|---|---|
| **1. PR / main push (Web)** | `web/**` を触る PR と main push | `web`: `tsc --noEmit` → `npm test` (73 + 436) → `npm run build` / **`browser-e2e`**: Chromium 導入 → ローカル D1 seed → `npm run test:e2e` (Playwright 12) | 不要 | Web Studio が型・単体・実ブラウザ E2E で通った |
| **1. PR / main push (C# 整合性)** | `Assets/**` / `script/**` を触る PR と main push | `verify`: `script/check-repo-integrity.sh` | 不要 | `.meta` の対応 / `.asmdef`・譜面 JSON の妥当性 / LF・タブ が揃っている (**コンパイルは見ていない**) |
| **2. main push (Unity ビルド)** | `Assets/**` などの main push、または手動 | `build-win64` / `build-macos`: `license-precheck` → Unity 認証 → EditMode (任意) → ビルド | **必須** | 現状は secret 未登録のため **precheck で fail**。**コードの失敗ではない** |
| **3. 手動 / オーナー** | — | Unity EditMode (メンテナの手元、実測)、Windows 実機の受入、本番 API の受入 | — | それぞれの記録 (`TestResults.xml` / `docs/evidence/` / `docs/operations/`) が唯一の証拠 |

### なぜ Unity を PR で走らせないのか

1. Unity は **ライセンスが無いと起動しない** (batchmode でも同じ)。
2. **fork からの PR は repository secret を読めない** (GitHub の仕様)。
3. GitHub-hosted runner では `.ulf` (Personal) の **マシン紐付けが一致しない**。
4. 以上から、PR に Unity を載せると **常に赤** になり、ゲートとして機能しない。

詳細は [`unity-ci-license.md`](unity-ci-license.md)。

---

## 3. 責任範囲

| 対象 | 責任を持つ人 | 何を残すか |
|---|---|---|
| Web の品質 | CI (`web` ワークフロー) | ジョブ結果。ローカル再現は `cd web && npm run test:e2e` |
| C# の**コンパイル**と**テスト** | **メンテナ** (手元 Unity EditMode) | `TestResults.xml` の総数と失敗数、`Scripts have compiler errors` が無いこと |
| C# の**整合性** (meta / JSON / 改行) | CI (`verify` ワークフロー) | ジョブ結果 |
| Windows 実機の受入 | **オーナー** | `docs/operations/win64-candidate-acceptance-*.md` の記録 |
| 本番 API の受入 | **オーナー承認後**にメンテナが実行 | `docs/operations/production-api-acceptance.md` の記録様式 |
| Unity ビルドを CI で緑にする | **オーナー** (`UNITY_EMAIL` / `UNITY_PASSWORD` の登録) | secret 名のみ記録する。値はどこにも書かない |

---

## 4. PR を出す人・レビューする人が守ること

- PR 本文には **「どの Tier の検証を実測したか」** を書く。Unity を触ったなら
  EditMode の実測 (総数 / 失敗数) を書く。**CI が緑でも Unity 未検証と書く。**
- `verify` / `web` が緑でも、**コンパイルが通るとは限らない**。コンパイルの証拠は
  EditMode 実行ログだけ。
- Windows 実機の受入が無い状態では、**Release を公開しない**
  ([`update-release.md`](update-release.md) / [`release-approval-checklist.md`](release-approval-checklist.md))。
- ライセンス secret の値 (パスワード / シリアル / `.ulf` の中身) は、ログ・Issue・PR・
  この文書のどこにも書かない。
