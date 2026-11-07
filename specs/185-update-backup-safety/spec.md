---
description: "Issue #185 — 更新が別インストールの退避 (バックアップ) を削除しない (所有権で削除範囲を限定)"
---

# Spec: #185 更新が別インストールの退避を削除する (所有権スコープ)

**Issue**: #185 | **Branch**: `w-185` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: 「同じ親フォルダにある別インストールのバックアップまで削除する。削除対象を正規インストール先に限定し、他インストールを残す。」

## Objective

同じ親フォルダ (例: `C:\Users\rebui\Desktop`) に複数の FlashBeat インストール
(`FlashBeat` / `FlashBeat-Exhibit` / `FlashBeat-0.1.14` など) がある状態で
片方を更新しても、**更新対象でないインストールの退避 (復旧データ) を消さない**。
退避の削除は「更新対象のインストール自身が所有すると確認できた世代」に限定し、
所有が確認できない退避 (旧形式) は自動削除しない。

## Owned Paths

- `tools/update/Update.ps1` — 退避の命名・所有 manifest・クリーンアップ判定
- `tools/update/tests/Run-UpdateScopeTests.ps1` — 純粋関数と回帰の検証
- `tools/update/tests/Run-UpdateFlowTests.ps1` — 更新フロー結合テスト
- `tools/update/tests/Invoke-UpdateForTest.ps1` — テスト用ラッパー
- `tools/update/tests/TestHelpers.ps1` — 共通アサーション
- `docs/operations/update-patch.md` — 退避の所有権と保持契約の追記
- `specs/185-update-backup-safety/**` — 本 spec

## Exclusions (explicitly out of scope)

- `Assets/**` (Unity の `UpdateChecker.cs` / `BuildScript.cs`) — 触らない。
  したがって **配布物顿了同じインストールの `tools/update/Update.ps1` が実行される**という
  制約 (初回更新だけは旧仕様で動く) は本 Issue では解消できない。→ Known Issues に記録。
- Release の公開・削除・タグ書き換え、GitHub 上の配布物そのものの変更。
- Windows 実機での更新 E2E (展示機) — Supervisor が直列で行う。
- 実メディア (`Assets/Game/Resources/**`) の移動・削除。

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 隣のインストールの退避を消さない (Priority: P1)

**Given** 同じ親フォルダに `FlashBeat` (正規インストール) と `FlashBeat-0.1.14` があり、
`FlashBeat-0.1.14` の更新で作られた退避 (復旧データ) が残っている
**When** `FlashBeat` を更新する
**Then** `FlashBeat-0.1.14` の退避フォルダは中身ごと残る (ログに「他名前空間」として保持理由が出る)

**Independent Test**: `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1`
(T20 / T24 / T25 / T28) と `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1`
(T3 / T4 / T12 / T13)

### Scenario 2 — 自分の退避は 1 世代だけ残す (Priority: P1)

**Given** 同じインストールを 2 回連続で更新した
**When** 2 回目の更新が終わる
**Then** そのインストールの退避は最新の 1 世代だけ残り、古い世代は削除される

**Independent Test**: `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` (T23 / T10)
と `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1` (T6 / T10 / T11)

### Scenario 3 — 所有不明の退避は自動削除しない (Priority: P2)

**Given** 旧形式 (`FlashBeat-backup-<yyyyMMdd-HHmmss>`、所有 manifest なし) の退避が親フォルダにある
**When** どのインストールも更新する
**Then** その退避は自動削除されない (理由 `other-namespace` / `no-manifest` としてログに残る)

**Independent Test**: `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` (T13 / T21 / T25)

### Edge Cases

- 同時刻に複数の更新が走っても、退避フォルダと作業フォルダを共有しない (実行 ID に PID と乱数を含める)。
- 所有 manifest の書き込みに失敗したら退避を開始せず、終了コード 7 で中止する (黙って成功にしない)。
- 他インストールの退避フォルダ名が自インストールの名前空間の前方一致 (“flashbeat” と “flashbeat-0.1.14” など) になっても、削除対象にならない
  (名前 + 所有 manifest の二重判定)。
- インストール先のフォルダ名を変えた場合、古い退避は所有不一致として残る (自動削除しない)。

## Requirements

- **R1**: 退避フォルダ名はインストール単位の名前空間を持つ
  (`FlashBeat-backup-<installKey>-<runId>`)。`installKey` はフォルダ名から正規化した安定 ID、
  `runId` は `yyyyMMdd-HHmmss-<pid>-<乱数>`。
- **R2**: 退避フォルダには所有 manifest (`flashbeat-backup-owner.json`) を書き、
  所有インストールのパス・安定 ID・正規インストール先かどうか・退避した版を記録する。
- **R3**: 退避の削除対象は「名前が自インストールの名前空間内」**かつ**
  「所有 manifest が自インストールの identity と一致 (キー + 正規化パス)」の退避だけ。
- **R4**: 所有 manifest が無い退避・壊れた退避・他インストールの退避・旧形式の退避は
  自動削除せず、保持理由 (`other-namespace` / `no-manifest` / `invalid-manifest` /
  `owner-key-mismatch` / `owner-path-mismatch`) をログに残す。
- **R5**: 同じインストールについては「旧版 1 世代だけ残す」契約を維持する
  (今回の退避 1 世代 + 自インストールの旧世代は削除)。
- **R6**: 作業フォルダもインストール単位の名前空間 + 実行 ID とし、同時刻の複数更新で共有しない。
- **R7**: 判定ロジックはテスト可能な純粋関数として `tools/update/Update.ps1` に実装し、
  外部ファイルを dot-source しない (配布物は `Update.ps1` 1 ファイルのまま。配布経路は
  `Assets/Editor/BuildScript.cs` の管轄で、触らない)。
- **R8**: 失敗は黙って成功扱いしない。所有 manifest の書き込み失敗は終了コード 7、
  既存配置を触らず中止する。
- **R9**: 検証スクリプト (Pester 不使用・依存ゼロ) を `tools/update/tests/` に置き、
  macOS / Windows のどちらの pwsh / Windows PowerShell 5.1 でも実行できる。

## Dependencies & Gate

- **Depends on**: なし (更新ツール単体で完結する)
- **Blocks**: 展示機での更新運用 (Issue #22 の実機ゲート) の前提条件之一
- **Gate**: 下記 V1〜V3 がすべて通ること。Windows 実機での更新 → 起動確認は Supervisor が実施。

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | スコープ (純粋関数 + 回帰) | `pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1` | PASS 28 / FAIL 0、終了コード 0 |
| V2 | 更新フロー結合 | `pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1` | PASS 16 / FAIL 0、終了コード 0 |
| V3 | 構文 | `pwsh -NoProfile -c "[System.Management.Automation.Language.Parser]::ParseFile('tools/update/Update.ps1', ...)"` | PARSE OK |
| V4 | 実機 (Windows) 更新 → 起動 | Supervisor が展示機で実施 | 未実施 (本 PR では未実行) |

## Rollback

`git revert` で本ブランチのマージコミットを戻す。触るのは `tools/update/**` と
`docs/operations/update-patch.md` だけで、他 Issue の資産 (Unity コード・web・実メディア) に
影響しない。退避フォルダの命名が変わらないため、旧形式のまま手動削除は可能。

## Success Criteria

- **SC1**: 同じ親フォルダの 2 インストールを交互に更新しても、互いの退避が残る (V1 / V2)。
- **SC2**: 同じインストールは旧版 1 世代だけ保持する (V1 / V2)。
- **SC3**: 所有不明の退避は自動削除されない (V1 / V2)。
- **SC4**: 更新失敗時は終了コードとログで明示され、既存配置は変更されない (V2 の T14 / T15)。
