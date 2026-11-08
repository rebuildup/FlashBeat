#!/usr/bin/env bash
# macOS 配布物をビルドして ~/Desktop/FlashBeat-Mac/ に配置する (実機レビュー用)。
#
# Usage: script/deploy-mac-desktop.sh [<ref>]
#   <ref> : ビルド対象 (既定: HEAD of the current branch / origin/main)
#
# - 展示用 Windows PC には一切触らない (Mac だけで完結する)。
# - 既存の FlashBeat.app は 1 世代だけ退避する (README.txt の規則)。
# - ビルドは script/mac-verify build 経由 (専用の worktree で Library を分けて使う)。
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
ref="${1:-HEAD}"
desktop="${FLASHBEAT_DESKTOP_DIR:-$HOME/Desktop/FlashBeat-Mac}"
worktree="${FB_MAC_WORKTREE:-$(dirname "$root")/FlashBeat.macverify}"

cd "$root"
if [ "${FB_SKIP_BUILD:-0}" = "1" ]; then
  echo "[deploy] reusing existing build (FB_SKIP_BUILD=1)"
else
  echo "[deploy] building $ref (worktree=$worktree)"
  script/mac-verify build "$ref"
fi

app="$worktree/Builds/macOS/FlashBeat.app"
[ -d "$app" ] || { echo "[deploy] build output missing: $app" >&2; exit 1; }

version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Contents/Info.plist")"
sha="$(git rev-parse --short "$ref")"

mkdir -p "$desktop"
if [ -d "$desktop/FlashBeat.app" ]; then
  backup="$desktop/FlashBeat-backup-$(date +%Y%m%d%H%M%S).app"
  mv "$desktop/FlashBeat.app" "$backup"
  echo "[deploy] previous app -> $(basename "$backup")"
  # 1 世代だけ残す (古い退避は old/ へ退避する。破壊的削除はしない)
  ls -dt "$desktop"/FlashBeat-backup-*.app 2>/dev/null | tail -n +2 | while read -r old; do
    mkdir -p "$desktop/old"
    echo "[deploy] moving stale backup $(basename "$old") -> old/"
    mv "$old" "$desktop/old/"
  done
fi
cp -R "$app" "$desktop/FlashBeat.app"
echo "[deploy] installed $desktop/FlashBeat.app (version=$version, commit=$sha)"

cat > "$desktop/README.txt" <<EOF
FlashBeat (Mac 版・配布物)
バージョン: $version (commit $sha)
ビルド元: このリポジトリの commit $sha / Unity 6000.5.6f1
更新経路: GitHub Releases からアプリ内で自動更新 (Windows 版と同じ)

■ 起動
FlashBeat.app をダブルクリック。
初回に「開発元を確認できません」と出る場合は 右クリック → 開く。

■ ログ
- Unity: ~/Library/Logs/KimuraYusuke/FlashBeat/Player.log
- 更新スクリプト: $desktop/FlashBeat-update.log
- 更新結果: ~/Library/Application Support/FlashBeat/update/last-result.json

■ 注意
- 曲の映像・音声は yt-dlp で YouTube から取得します (brew install yt-dlp)。
- 展示用 Windows PC はこの手順の対象外です (Mac だけで完結します)。
EOF
echo "[deploy] done"
