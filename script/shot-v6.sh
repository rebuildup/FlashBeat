#!/usr/bin/env bash
# v6 UI の実機スクリーンショットを 3 解像度で取る (Issue #223 検証)。
#
#   script/shot-v6.sh [app path]
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

for res in "1920 1080" "2560 1440" "2560 1664"; do
  w=$(echo "$res" | cut -d' ' -f1)
  h=$(echo "$res" | cut -d' ' -f2)
  dir="$outroot/${w}x${h}"
  mkdir -p "$dir"
  rm -f "$dir"/*.png "$dir"/*.txt "$dir"/Player.log
  echo "=== ${w}x${h} ==="
  open -n "$app" --args \
    -screen-width "$w" -screen-height "$h" -screen-fullscreen 1 \
    -autotest v6 -autotest-out "$dir" -logFile "$dir/Player.log"
  sleep 40
  if [ -f "$dir/autotest-v6-result.txt" ]; then
    cat "$dir/autotest-v6-result.txt"
  else
    echo "no result file (timeout?)"
  fi
  # アプリを閉じる (次回分)
  osascript -e 'tell application "FlashBeat" to quit' >/dev/null 2>&1 || true
  pkill -f "FlashBeat.app" >/dev/null 2>&1 || true
  sleep 2
done

echo "screenshots: $outroot"
ls -R "$outroot" | head -40
