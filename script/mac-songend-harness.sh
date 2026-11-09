#!/usr/bin/env bash
# 曲終了ハーネス (Issue #233 / #226) を macOS 実機で走らせる。
#
#   script/mac-songend-harness.sh [<app path>] [<out dir>]
#
# 1. 引数のプレイヤー (既定: Builds/macOS/FlashBeat.app) を **実ディスプレイ全画面** で
#    起動し、"-autotest v6songend" でハーネスを走らせる。
# 2. 結果ファイル (autotest-v6songend-result.txt) が現れるまで待って、ログと
#    スクリーンショットを out dir に集める。
#
# 注意: 窓リサイズによる擬似全画面は不可 (実ディスプレイの全画面只能用)。
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="${1:-$root/Builds/macOS/FlashBeat.app}"
outdir="${2:-$root/Builds/songend-$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$outdir"

if [ ! -d "$app" ]; then
  echo "app not found: $app" >&2
  exit 1
fi

stop_app() {
  osascript -e 'tell application "FlashBeat" to quit' >/dev/null 2>&1 || true
  pkill -f "FlashBeat.app/Contents/MacOS" >/dev/null 2>&1 || true
  sleep 2
}

echo "[songend] app=$app out=$outdir"
stop_app
open -n "$app" --args \
  -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
  -autotest v6songend -autotest-out "$outdir" -logFile "$outdir/Player.log"

result="$outdir/autotest-v6songend-result.txt"
for _ in $(seq 1 180); do
  if [ -f "$result" ]; then break; fi
  sleep 5
done

# OS レベルのスクリーンショット (実ディスプレイの全画面) も残す。
screencapture -x "$outdir/os-screen.png" 2>/dev/null || echo "[songend] (OS screencapture unavailable)"

stop_app

if [ -f "$result" ]; then
  echo "[songend] result:"
  cat "$result"
else
  echo "[songend] no result file (see $outdir/Player.log)" >&2
fi
echo "[songend] screenshots:"
find "$outdir" -name '*.png' | sort
