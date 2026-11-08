#!/usr/bin/env bash
# integration/v6-followup の HEAD が変わるたびに Mac 実機レビュー用 app を
# 再ビルドして ~/Desktop/FlashBeat-Mac/FlashBeat.app を更新する常駐ループ。
#
#   script/auto-deploy-loop.sh [branch] [interval_sec]
#
# - 展示用 Windows PC には一切触らない (Mac だけで完結する)。
# - ビルドは script/mac-verify build 経由 (専用 worktree / Library 共有)。
# - 同時実行防止: ロックファイル + pid チェック。
set -uo pipefail

root="$(git rev-parse --show-toplevel)"
branch="${1:-integration/v6-followup}"
interval="${2:-60}"
desktop="${FLASHBEAT_DESKTOP_DIR:-$HOME/Desktop/FlashBeat-Mac}"
log="${TMPDIR:-/tmp}/fb-auto-deploy.log"
lock="${TMPDIR:-/tmp}/fb-auto-deploy.lock"

cd "$root"
mkdir -p "$desktop"

if [ -f "$lock" ] && kill -0 "$(cat "$lock" 2>/dev/null)" 2>/dev/null; then
  echo "[auto-deploy] already running (pid $(cat "$lock"))" >&2
  exit 1
fi
echo $$ > "$lock"
trap 'rm -f "$lock"' EXIT

# 起動時点で既に入っている commit はビルド済み前提 (無駄な再ビルドを避ける)
last="$(git rev-parse "$branch" 2>/dev/null || echo none)"
while true; do
  head="$(git rev-parse "$branch" 2>/dev/null || echo none)"
  if [ "$head" != "$last" ]; then
    echo "[auto-deploy] $(date +%H:%M:%S) building $branch @ ${head:0:7}" >> "$log"
    if script/deploy-mac-desktop.sh "$branch" >> "$log" 2>&1; then
      echo "[auto-deploy] $(date +%H:%M:%S) deployed ${head:0:7}" >> "$log"
      last="$head"
    else
      echo "[auto-deploy] $(date +%H:%M:%S) BUILD/DEPLOY FAILED @ ${head:0:7}" >> "$log"
      # 失敗しても同じ commit を再試行し続ける (次の HEAD で再度試す)
    fi
  fi
  sleep "$interval"
done
