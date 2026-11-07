# PR #150 / Issue #149 — Publish 状態の実画面証跡

検証: 2026-11-07 / ブランチ `149` (PR #150) をローカル dev サーバ
(`FLASHBEAT_ADMIN_TOKEN=devtoken vp dev`) で起動し、本物の Chromium で撮影。

| ファイル | 内容 |
|---|---|
| `before-publish-error-1440x900.png` | 変更前 (expo-web / PR #150 の base)。素のテキスト `Song no longer available in Catalog. Retry Catalog` |
| `after-publish-error-1440x900.png` | 変更後。ブランドヘッダー + 見出し + 再試行 / Catalog に戻る |
| `after-publish-error-1280x720.png` | 変更後 (1280x720) |
| `after-publish-error-390x844.png` | 変更後 (390x844、CDP 実機エミュレーション)。横方向のはみ出しなし (`documentElement.scrollWidth == innerWidth`) |
| `after-catalog-empty-1440x900.png` | 参照: Catalog の空状態 |

再現コマンド:

```sh
cd web && FLASHBEAT_ADMIN_TOKEN=devtoken vp dev
# 1440x900
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --disable-gpu \
  --hide-scrollbars --virtual-time-budget=8000 --window-size=1440,900 \
  --screenshot=after.png "http://localhost:5179/publish?songId=1&version=1"
```
