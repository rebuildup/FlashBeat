#!/usr/bin/env bash
# Issue #233 のプレイフィールド証跡を macOS 実機で撮り直す。
#
#   script/mac-playfield-evidence.sh [<app path>] [<out dir>]
#
# 1. プレイヤーを **実ディスプレイ全画面** (2560 x 1664) で起動し、
#    "-autotest v6" のハーネスを走らせる (スクリーンショットは
#    Unity の ScreenCapture だけが使う — OS の screencapture は
#    macOS の Space 分離でデスクトップを撮るため証拠にしない)。
# 2. autotest-v6-result.txt が出るまで待ち、出た結果で **終了コードを返す**。
# 3. 起動は実行ファイル直叩き (pid を保持して自分の子だけを kill する)。
#    ユーザーが手元で起動した FlashBeat には触らない。
#
# 判定 (exit code):
#   0  結果ファイルがあり fail=0
#   1  結果ファイルがない / fail=0 でない / ログに FAIL 行がある
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app=""
outdir=""

while [ $# -gt 0 ]; do
  case "$1" in
    -h|--help)
      sed -n '2,25p' "$0"
      exit 0
      ;;
    *)
      if [ -z "$app" ]; then
        app="$1"
      elif [ -z "$outdir" ]; then
        outdir="$1"
      else
        echo "[playfield] too many arguments" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/playfield-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[playfield] app not found: $app" >&2
  exit 1
fi

binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[playfield] player binary not found or not executable: $binary" >&2
  exit 1
fi

result="$outdir/autotest-v6-result.txt"
log="$outdir/Player.log"
app_pid=""

cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    echo "[playfield] stopping the player (pid $app_pid)"
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

echo "[playfield] app=$app out=$outdir"
"$binary" \
  -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
  -autotest v6 -autotest-out "$outdir" -logFile "$log" &
app_pid=$!
echo "[playfield] player started (pid $app_pid)"

for _ in $(seq 1 60); do
  if [ -f "$result" ]; then break; fi
  if ! kill -0 "$app_pid" 2>/dev/null; then
    echo "[playfield] the player exited before writing the result file" >&2
    break
  fi
  sleep 5
done

if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
fi
cleanup
app_pid=""

echo "[playfield] screenshots:"
find "$outdir" -name '*.png' | sort

status=0
if [ ! -f "$result" ]; then
  echo "[playfield] FAIL: no result file ($result)" >&2
  status=1
else
  echo "[playfield] result:"
  cat "$result"
  if ! grep -q 'fail=0' "$result"; then
    echo "[playfield] FAIL: the result file does not report fail=0" >&2
    status=1
  fi
fi

if [ -f "$log" ]; then
  if grep -q 'FAIL ' "$log"; then
    echo "[playfield] FAIL: the harness logged a failing check:" >&2
    grep 'FAIL ' "$log" >&2
    status=1
  fi
else
  echo "[playfield] FAIL: no player log ($log)" >&2
  status=1
fi

if [ "$status" = 0 ]; then
  echo "[playfield] OK: the harness passed"
else
  echo "[playfield] the run FAILED (see the diagnostics above)" >&2
fi

exit "$status"
