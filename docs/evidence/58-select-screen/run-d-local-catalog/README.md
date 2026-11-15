# run-d — カタログ到達時の選曲画面 (2026-11-15)

- ビルド: `Builds/macOS/FlashBeat.app` (main `9e650f36` 相当、`CloudCatalogConfig.json` = `http://localhost:5173` の検証ビルド)
- API: ローカル Void dev server (`vp dev`) + ローカル D1 seed (`who` / chartVersion 1 / 139 notes)
- 方法: プロキシ無しで `-autotest v6screens -autotest-scenes SelectScene` を実行
- Player.log: `[GManager] cloud catalog applied: local=29 merged=29 remote=1` (8 回のポーリングすべて成功)
- 結果: `RESULT pass=27 fail=0`。`v6screen-SelectScene-1.png` にリモート曲 `Who` の行が出ている (SYMBOL ログに `SongRow Who`)
- 注意: 「到達」は**ローカル API** に対するもの。本番 `flashbeat.rebuildup.dev` への到達はコードパスが同一 (origin のみ差分) だが、実測はローカルのみ
