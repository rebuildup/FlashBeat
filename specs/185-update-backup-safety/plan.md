---
description: "Issue #185 実装計画 — 退避の所有権で削除範囲を限定する"
---

# Plan: #185 更新が別インストールの退避を削除する (所有権スコープ)

**Issue**: #185 | **Branch**: `w-185` | **Base**: `main` | **Date**: 2026-11-08
**Spec**: [`specs/185-update-backup-safety/spec.md`](spec.md)

## Summary

`tools/update/Update.ps1` の退避 (バックアップ) 処理を「インストール単位のスコープ」に
作り直す。退避フォルダ名にインストール安定 ID を含め (`FlashBeat-backup-<installKey>-<runId>`)、
退避フォルダ内に所有 manifest (`flashbeat-backup-owner.json`) を書き、
クリーンアップは「名前が自名前空間内」かつ「所有 manifest が自 identity と一致」の退避だけを
対象にする。判定は `Update.ps1` 内の純粋関数として実装し、外部ファイルを dot-source しない
(配布物は `Update.ps1` 1 ファイルのまま維持する。配布経路 `Assets/Editor/BuildScript.cs` は触らない)。
検証は Pester を使わない依存ゼロのアサーションスクリプト (.scope / .flow) を
`tools/update/tests/` に置く。

## Owned Paths

```text
tools/update/Update.ps1
tools/update/tests/Run-UpdateScopeTests.ps1
tools/update/tests/Run-UpdateFlowTests.ps1
tools/update/tests/Invoke-UpdateForTest.ps1
tools/update/tests/TestHelpers.ps1
docs/operations/update-patch.md
specs/185-update-backup-safety/**
```

**Structure Decision**: 配布物は `BuildScript.cs` が `tools/update/Update.ps1` だけを複製する
ため、判定ロジックを別ファイルに分けると配布物で関数が見つからず更新が壊れる。
よって純粋関数は `Update.ps1` に同居させ、テストは `Update.ps1` を dot-source して取り込む
(本体は dot-source 時に走らないよう `Invoke-FlashBeatUpdateMain` 関数に閉じ込め、
`$MyInvocation.InvocationName -ne '.'` のときだけ実行する)。

## Dependencies & Gate

- **Depends on**: なし
- **Blocks**: 展示機 (Windows) の更新運用。実機 E2E は Supervisor が実施
- **Gate**: V1 (scope 28/28) / V2 (flow 16/16) / V3 (parse OK) が通ること

## Exclusions

- Unity 側 (`Assets/**`) の変更。`UpdateChecker.cs` が起動するのは
  **インストール先にある配布物内の `tools/update/Update.ps1`** なので、
  旧版から新版への最初の 1 回だけは旧仕様で動く (Known Issues)。
- Release の公開・削除、GitHub 上の配布物変更。
- 実機 Windows での実行検証。

## Constitution Check

- Authority: `constitution/CONSTITUTION.md` と `organization/profiles/release-driven-solo.md` は変更しない。
- Mutable Ownership Safety: 触るのは `tools/update/**` と `docs/operations/update-patch.md` のみ。
  他の Issue (展示更新・Web) の資産 (Unity コード・web・実メディア) には触れない。
- Evidence Integrity: 検証ログは本ブランチの SHA に紐づけて PR に添付する。
- Violations: なし。

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | スコープ検証 | `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` | PASS 28 / FAIL 0 |
| V2 | 更新フロー結合 | `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1` | PASS 16 / FAIL 0 |
| V3 | 構文チェック | `pwsh -NoProfile -c "[System.Management.Automation.Language.Parser]::ParseFile(...)"` | PARSE OK |
| V4 | 実機 (Windows) | Supervisor が展示機で Update → 起動 | 本 PR では未実行 |

## Rollback

`git revert` で本 Issue のマージコミットを戻す。変更範囲は `tools/update/**` と
`docs/operations/update-patch.md` のみ。他ブランチ・他 Issue に影響しない。
旧形式 (`FlashBeat-backup-<stamp>`) の退避は手動で削除できる状態のまま残る。
