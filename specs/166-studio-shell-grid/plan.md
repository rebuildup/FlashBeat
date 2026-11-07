# Plan: Studio全画面のグリッド・共通要素・操作フロー再構成

**Issue**: #166 | **Branch**: `166` | **Base**: `release-0-1-11` (2f2b6ba)
**Spec**: [spec.md](spec.md)

## Summary

`app-shell.tsx` から汎用 `StudioShell` (Sidebar レール + 主領域 + スクロール方針) を抽出し、
Catalog 固有のフィルタ/読み込みロジックは `AppShell` に残す。`chart.tsx` / `publish.tsx` /
`new-song.tsx` を `StudioShell` に組み込み、hex 直書きの画面別シェルをセマンティックトークンに
置き換える。ページヘッダー帯 (戻りリンク / 題名 / 主CTA) の共通スタイルを `panel.tsx` に置き、
Catalog→Editor→Publish→Catalog再読込 の導線を Playwright 仕様として固定する。

## Owned Paths

`web/src/app/app-shell.tsx`, `web/src/app/sidebar.tsx`, `web/src/app/panel.tsx`,
`web/src/app/drawer.tsx`, `web/pages/{index,chart,new-song,publish}.tsx`,
新規 Panda/Park トークン・レシピファイル, `specs/166-studio-shell-grid/**`。

## Dependencies & Gate

topbar (Worker C) は編集しない — 呼ぶだけ。chart-editor / song-publisher / new-song components
は各 Worker の領域 — ページファイルから呼ぶだけ。Gate: `npx tsc --noEmit` / `npx vp build` /
`npm test` 緑 + 実ブラウザ目視 (FHD/2K/Mac) + Playwright 仕様の同梱。

## Constitution Check

排他ファイル所有を維持。Unity Editor / Library / Cloudflare / 本番DB には触れない。

## Verification Plan

V1–V5 (spec.md の表) を候補 SHA で実行し、結果を最終報告に添付。

## Rollback

PR の revert。Owned Paths 境界により他 Issue に影響しない。
