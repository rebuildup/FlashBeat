# run-c — カタログ未到達時の選曲画面 (2026-11-15)

- ビルド: `Builds/macOS/FlashBeat.app` (main `9e650f36` 相当、ローカル API 向け検証ビルド)
- 方法: `script/mac-select-evidence.sh --offline` — `http_proxy` を `127.0.0.1:1` に向けて外部 API へ出られない状態で `-autotest v6screens -autotest-scenes SelectScene` を実行
- Player.log: `[CloudCatalog] GET http://localhost:5173/api/v1/catalog failed: Cannot connect to destination host (status=0)` → `[SelectScene] catalog unavailable (status=0) - using the built-in song list`
- 結果: `RESULT pass=27 fail=0`。`v6screen-SelectScene-Error-7.png` が未到達時の表示、`SelectScene-1.png` が内蔵リストでの通常表示
