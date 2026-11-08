#!/usr/bin/env bash
# v6 UI の実機スクリーンショットを 3 解像度で取る (Issue #223 / #233 検証)。
#
#   script/shot-v6.sh [app path]
#
# 各解像度で 2 回起動する:
#   1) -autotest v6          → 選曲 → Ready → ゲーム(準備) → ゲーム(実プレイ)
#   2) -autoshot-scenes ...  → タイトル / 選曲 (各シーンを単独で)
#
# 出力: /tmp/v6-shots/<width>x<height>/*.png と result.txt
set -euo pipefail

app="${1:-$HOME/work/FlashBeat.v6int/Builds/macOS/FlashBeat.app}"
outroot="/tmp/v6-shots"
mkdir -p "$outroot"

if [ ! -d "$app" ]; then
  echo "app not found: $app" >&2
  exit 1
fi

stop_app() {
  osascript -e 'tell application "FlashBeat" to quit' >/dev/null 2>&1 || true
  pkill -f "FlashBeat.app/Contents/MacOS" >/dev/null 2>&1 || true
  sleep 2
}

for res in "1920 1080" "2560 1440" "2560 1664"; do
  w=$(echo "$res" | cut -d' ' -f1)
  h=$(echo "$res" | cut -d' ' -f2)
  dir="$outroot/${w}x${h}"
  mkdir -p "$dir"
  find "$dir" -type f -delete

  echo "=== ${w}x${h} : autotest v6 ==="
  open -n "$app" --args \
    -screen-width "$w" -screen-height "$h" -screen-fullscreen 1 \
    -autotest v6 -autotest-out "$dir" -logFile "$dir/Player.log"
  sleep 75
  cat "$dir/autotest-v6-result.txt" 2>/dev/null || echo "no result file"
  stop_app

  echo "=== ${w}x${h} : title / select ==="
  open -n "$app" --args \
    -screen-width "$w" -screen-height "$h" -screen-fullscreen 1 \
    -autoshot "$dir" -autoshot-scenes TitleScene,SelectScene \
    -autoshot-interval 4 -logFile "$dir/Player-scenes.log"
  sleep 30
  stop_app
done

echo "screenshots: $outroot"
find "$outroot" -name '*.png' | sort
