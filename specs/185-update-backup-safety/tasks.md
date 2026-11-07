---
description: "Issue #185 タスク — 退避の所有権スコープ実装と検証"
---

# Tasks: #185 更新が別インストールの退避を削除する

**Issue**: #185 | **Branch**: `w-185` | **Base**: `main`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [x] **T1** [P] 退避の所有権スコープを `tools/update/Update.ps1` に実装する
  (identity / 安定 ID / 実行 ID / 退避名・作業フォルダ名 / 所有 manifest 書き込み /
  候補列挙 / 純粋な選択関数 `Select-FlashBeatStaleBackups`、およびクリーンアップ呼び出しの差し替え)
  — [done when: 退避が `FlashBeat-backup-<installKey>-<runId>` 名で生成され、
  所有 manifest が書かれ、削除対象が自インストールの旧世代だけになる]
- [x] **T2** [P] スコープ検証スクリプト `tools/update/tests/Run-UpdateScopeTests.ps1` と
  共通ヘルパ `tools/update/tests/TestHelpers.ps1` を作る
  — [done when: `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` が
  PASS 28 / FAIL 0 で終了コード 0]
- [x] **T3** 更新フロー結合テスト `tools/update/tests/Run-UpdateFlowTests.ps1` と
  テスト用ラッパー `tools/update/tests/Invoke-UpdateForTest.ps1` を作る
  — [done when: `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1` が
  PASS 16 / FAIL 0 で終了コード 0 (2 回更新しても隣のインストールの退避が残る)]
- [x] **T4** `docs/operations/update-patch.md` に退避の所有権・保持契約・
  旧形式退避の扱い・初回更新だけ旧スクリプトで動く制約を追記する
  — [done when: ドキュメントに「削除対象は自インストールの退避のみ」と
  旧形式 (所有不明) を自動削除しない旨が明記される]
- [x] **T5** 構文チェック (V3) と全体の再検証 (V1 / V2) を実行し、結果を報告書に記す
  — [done when: `Parser::ParseFile` が PARSE OK、V1 / V2 が緑で、報告書に実出力を貼る]

## Dependencies & Execution Order

1. [T1] → [T2] / [T3] (判定ロジック実装後にテストが成立する)
2. [T4] は [T1] と並行可能 (ドキュメントのみ)
3. [T5] は [T1]〜[T4] の後

**Blocking dependency**: なし。Windows 実機での更新 → 起動確認は Supervisor が直列で実施する
(本 PR のスコープ外)。

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | 構文 | `pwsh -NoProfile -c "[System.Management.Automation.Language.Parser]::ParseFile('tools/update/Update.ps1',[ref]`$null,[ref]`$e)"` | PARSE OK |
| T2 | スコープ | `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` | PASS 28 / FAIL 0 |
| T3 | 更新フロー | `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1` | PASS 16 / FAIL 0 |
| T4 | ドキュメント | `docs/operations/update-patch.md` を目視 | 保持契約が明記されている |
| T5 | 報告 | 最終報告の VALIDATION 欄 | 実出力付き |

## Rollback

`git revert` で本ブランチのマージコミットを戻す。影響範囲は `tools/update/**` と
`docs/operations/update-patch.md` のみ。他 Issue のブランチには影響しない。

## Notes

- Scope discipline: 実装は上記タスクのみ。Unity 側 (`Assets/**`) は触らない。
- 配布物は `BuildScript.cs` が `Update.ps1` 1 ファイルだけを複製するため、
  ロジックを別ファイルに分けない (分けると配布物で更新が壊れる)。
