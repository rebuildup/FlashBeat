---
description: "Issue #77 タスク — 公開 Release のメディア同梱監査と配布方針の文書化"
---

# Tasks: #77 旧 Release の第三者メディア同梱の監査と配布方針

**Issue**: #77 | **Branch**: `w-185` | **Base**: `main`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [x] **T1** [P] 判定ツール `tools/release/audit-release-media.py` を作る
  (Releases API 一覧 → ZIP の終端ディレクトリを Range 取得 → エントリ一覧・非圧縮サイズを解析 →
  ルーズなメディア件数 / `resources.resource` サイズ / manifest 同梱の有無から verdict を出力)
  — [done when: `python3 tools/release/audit-release-media.py` が全 17 件の verdict を出力し、
  ZIP 全体 (1.36GB) をダウンロードせずに判定できる]
- [x] **T2** [P] 実行して実出力を確定する (全 Release / 個別タグ / JSON)
  — [done when: `v0.1.0-win64-743c88d` だけ `third-party-media`
  (`resources.resource` = 1,399,081,540 bytes)、他 16 件は `song-free`
  (196,446,346 bytes) であることが確認できる]
- [x] **T3** 監査結果と推奨を `docs/operations/release-media-audit.md` に書く
  (Release ごとの一覧表、判定手順、推奨と理由、Known Issues)
  — [done when: 一覧表・手順・推奨・実行コマンドと実出力 (Evidence) が揃う]
- [x] **T4** 発行手順へルールを追記する (`docs/operations/update-release.md`:
  「media policy 導入前の Release は配布対象から外す」+ 監査 doc への参照)
  — [done when: ルールと参照が追記される]
- [x] **T5** 最終報告に監査結果 (17 件の verdict 表・推奨・未実行事項) を記す
  — [done when: 報告書に Evidence と「Release の公開・削除は未実施 (Supervisor 承認待ち)」が明記される]

## Dependencies & Execution Order

1. [T1] → [T2] → [T3]
2. [T4] は [T1] と並行可能 (ドキュメントのみ)
3. [T5] は [T1]〜[T4] の後

**Blocking dependency**: なし。GitHub への読み取りアクセスだけを使う。

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | ツール実行 | `GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py` | 17 件の verdict 出力、exit 0 |
| T2 | 個別確認 | `python3 tools/release/audit-release-media.py --tag v0.1.0-win64-743c88d` | resources.resource = 1,399,081,540 bytes / third-party-media |
| T3 | 文書 | `docs/operations/release-media-audit.md` を目視 | 一覧・手順・推奨・Evidence が揃う |
| T4 | 文書 | `docs/operations/update-release.md` を目視 | ルールと参照がある |
| T5 | 報告 | 最終報告の VALIDATION / KNOWN_ISSUES 欄 | 実出力つき・未実施事項の明記 |

## Rollback

`git revert` で本ブランチのマージコミットを戻す。GitHub 上の Release は変更していないため、
公開状態に影響しない。

## Notes

- Scope discipline: 実装は上記タスクのみ。Release の公開・削除は行わない。
- 判定の数値は実行日 (2026-11-08) 時点の公開物に対する実測値であり、
  Release が差し替われば再実行が必要 (手順を doc に残す)。
