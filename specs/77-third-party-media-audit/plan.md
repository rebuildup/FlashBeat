---
description: "Issue #77 実装計画 — 公開 Release のメディア同梱監査と配布方針"
---

# Plan: #77 旧 Release の第三者メディア同梱の監査と配布方針

**Issue**: #77 | **Branch**: `w-185` | **Base**: `main` | **Date**: 2026-11-08
**Spec**: [`specs/77-third-party-media-audit/spec.md`](spec.md)

## Summary

公開済み Release を 1 件ずつ実測する調査ツール `tools/release/audit-release-media.py` を作り、
その実出力 (Evidence) と推奨を `docs/operations/release-media-audit.md` にまとめる。
判定は ZIP 全体ではなく **HTTP Range で終端ディレクトリ (central directory) だけ** を取り、
ZIP 内のエントリ一覧 (名前 + 非圧縮サイズ) を取得してから
「第三者楽曲メディアの焼き込み有無」を判定する。これにより 1.36GB の
`v0.1.0-win64-743c88d` も数 KB の通信で `resources.resource` = 1,399,081,540 bytes を
実測できる (既存監査記録の値と一致)。GitHub 上の Release 自体 (公開・削除・タグ) は触らない。

## Owned Paths

```text
tools/release/audit-release-media.py
docs/operations/release-media-audit.md
docs/operations/update-release.md
specs/77-third-party-media-audit/**
```

**Structure Decision**: 判定ロジックは既存の PowerShell 群 (`tools/release/package-win64.ps1`) と
同じ `tools/release/` に置く。ZIP の中央ディレクトリ解析は Python 標準ライブラリだけで
書けるため依存 0 で、macOS / Linux / Windows のどれでも同じコマンドで再現できる。

## Dependencies & Gate

- **Depends on**: なし (GitHub への読み取りアクセスだけ)
- **Blocks**: なし。判断 (取り下げ / 再パッケージ) はオーナー / Supervisor が実施
- **Gate**: 17 件の判定結果と推奨が `docs/operations/release-media-audit.md` に残っていること

## Exclusions

- Release の公開・削除・タグ書き換え・アセットの再アップロード。
- `Assets/Game/Resources/**` の実メディアの移動・削除。
- Unity ビルド (`Assets/**`) と `web/**` の変更。

## Constitution Check

- Authority: `constitution/CONSTITUTION.md` と `organization/profiles/release-driven-solo.md` は変更しない。
- Mutable Ownership Safety: 触るのは `tools/release/**` と `docs/operations/**` のみ。
  他の Issue (更新安全性・展示運用) の資産には触れない。
- Evidence Integrity: 判定は実行日 (2026-11-08) の実出力で、Release タグと結び付けて記録する。
- Violations: なし (GitHub の変更を伴う操作は行わない)。

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | 全 Release 監査 | `GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py` | 17 件の verdict 表 |
| V2 | 疑わしい 1 件の個別確認 | `python3 tools/release/audit-release-media.py --tag v0.1.0-win64-743c88d` | resources.resource = 1,399,081,540 bytes |
| V3 | 現行版 (最新) の確認 | `python3 tools/release/audit-release-media.py --tag v0.1.14-win64-617c2b6` | resources.resource = 196,446,346 bytes / song-free |

## Rollback

`git revert` で本ブランチのマージコミットを戻す。GitHub 上の公開物は変更していないため、
戻しても公開状態に影響しない。
