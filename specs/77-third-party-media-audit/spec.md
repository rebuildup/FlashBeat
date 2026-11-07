---
description: "Issue #77 — 公開済み Release の第三者楽曲メディア同梱の監査と_HANDLE方針"
---

# Spec: #77 旧 Release の第三者メディア同梱の監査と配布方針

**Issue**: #77 | **Branch**: `w-185` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: 「古い公開 Release に第三者楽曲のメディアが同梱されたまま公開されている。調査して報告し、取り下げ or 再パッケージの方針を docs/operations/ に書く。実 Release の削除・置換はしない。」

## Objective

公開済み Release を 1 件ずつ実測し、**どの ZIP に第三者楽曲メディアが含まれるか**を
判定できる手順と結果を残し、どの Release を配布対象から外すか /
どの Release を再パッケージするかの推奨 (理由つき) を `docs/operations/release-media-audit.md` に書く。
実 Release の公開・削除・タグの書き換えはしない (判断はオーナー / Supervisor)。

## Owned Paths

- `tools/release/audit-release-media.py` — 判定手順を再現可能にする調査ツール
- `docs/operations/release-media-audit.md` — 監査結果と推奨
- `docs/operations/update-release.md` — 発行手順へのルール追記 (差し替え先)
- `specs/77-third-party-media-audit/**` — 本 spec

## Exclusions (explicitly out of scope)

- Release の公開・削除・タグ書き換え・アセット再アップロード (オーナー承認が必要)。
- `Assets/Game/Resources/**` の実メディアの移動・削除。
- `Assets/**` (Unity ビルド) と `web/**` の変更。
- 新しい Release の発行。

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — どの ZIP に第三者曲があるか判定できる (Priority: P1)

**Given** 公開済み Release が 17 件あり、そのうち ZIP が 1.36GB の 1 件が
`resources.resource` に第三者楽曲を焼き込んでいると疑われている
**When** `tools/release/audit-release-media.py` を実行する
**Then** Release ごとに「同梱メディアあり / なし」と、その根拠
(`resources.resource` の非圧縮サイズ、ルーズなメディアエントリ数、
`version.json` / `flashbeat-media-policy.json` の有無) が出力される

**Independent Test**: `python3 tools/release/audit-release-media.py --json` の出力で
17 件すべての verdict が得られ、`v0.1.0-win64-743c88d` だけが `third-party-media` になる

### Scenario 2 — 1.36GB を落とさずに判定できる (Priority: P1)

**Given** 1.36GB の ZIP アセット
**When** 判定する
**Then** ZIP 全体ではなく終端ディレクトリ (central directory) だけを範囲取得し、
`]resources.resource` の非圧縮サイズ (1,399,081,540 bytes) が分かる

**Independent Test**: 判定に要した通信量が ZIP サイズを大きく下回り、
判定値が `docs/evidence/win64-2026-11-06-issue13/media-rights-audit.md` の記録
(1,399,081,540 bytes) と一致する

### Scenario 3 — 配布対象の判定基準が文書に残る (Priority: P2)

**Given** 新しい Release を出すとき
**When** 発行手順を確認する
**Then** 「media policy 導入前 (version.json / manifest なし) の Release は配布対象から外す」
というルールと、対象 Release の一覧が文書にある

**Independent Test**: `docs/operations/release-media-audit.md` と
`docs/operations/update-release.md` にルールと一覧がある

### Edge Cases

- draft の Release は公開されていないため監査対象外 (将来公開就不是再監査する)。
- ZIP64 (64 ビット超) の ZIP でも判定できる (中央ディレクトリオフセットの ZIP64 対応)。
- 範囲取得に対応しないサーバーではエラーとして記録し、判定を「不明」で残す (推測しない)。

## Requirements

- **R1**: 調査ツールは ZIP を丸ごとダウンロードせず、HTTP Range で終端ディレクトリだけを取得する。
- **R2**: 判定根拠は (a) ルーズなメディアエントリの一覧・件数、
  (b) `FlashBeat_Data/resources.resource` の非圧縮サイズ、
  (c) `version.json` / `flashbeat-media-policy.json` / `update-channel.txt` の有無。
- **R3**: 判定は「third-party-media」/「song-free」の 2 値で、根拠を添えて出力する。
- **R4**: 監査結果を `docs/operations/release-media-audit.md` に、
  実行コマンドと実出力 (Evidence) つきで記録する。
- **R5**: 推奨 (取り下げ / prerelease 降格 / 本文注記 / 再パッケージ) を理由つきで書く。
- **R6**: Release の公開・削除・タグ書き換えは本 Issue では行わない。

## Dependencies & Gate

- **Depends on**: なし (読み取り専用の調査のみ)
- **Blocks**: なし (判断はオーナー / Supervisor)
- **Gate**: 17 件の判定結果と推奨が `docs/operations/release-media-audit.md` にあること

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 全 Release 監査 | `GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py` | 17 件分の verdict 出力。third-party-media は 1 件のみ |
| V2 | 対象 Release の個別確認 | `python3 tools/release/audit-release-media.py --tag v0.1.0-win64-743c88d` | resources.resource = 1,399,081,540 bytes / verdict=third-party-media |
| V3 | 手段の再現 | `python3 tools/release/audit-release-media.py --json` | JSON 出力 (Evidence 添付用) |

## Rollback

`git revert` で本ブランチのマージコミットを戻す。変更は調査ツールとドキュメントのみで、
GitHub 上の公開物には影響しない (そもそも変更していない)。

## Success Criteria

- **SC1**: 公開済み 17 件の Release について、同梱メディアの有無と根拠が文書に残った。
- **SC2**: 判定手順がコマンド 1 本で再現できる (1.36GB のダウンロード不要)。
- **SC3**: 第三者曲を含む Release の扱いの推奨が理由つきで書かれた。
