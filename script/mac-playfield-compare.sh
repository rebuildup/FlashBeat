#!/usr/bin/env bash
# Issue #219: v6 プレイフィールド (遠近法) の ON/OFF を実機で比較する。
#
#   script/mac-playfield-compare.sh [<FlashBeat.app>] [<出力先>]
#
# 同じ autotest ハーネス (-autotest v6playfield) を 2 回走らせる:
#   legacy/ … -playfield 0 (既定の 3D レーン)
#   v6/     … -playfield 1 (v6 遠近法プレイフィールド)
#
# 判定は各 run の autotest-v6-result.txt (fail=0 か) と Player.log で行う。
# 画像の目視比較は docs/evidence/219-playfield-compare.md を参照。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app=""
outdir=""

# 引数は位置指定 (0〜2 個)。`shift 2` は 1 個のとき無限ループになるため使わない。
app="${1:-}"
outdir="${2:-}"

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/playfield-compare-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[playfield-compare] player binary not found: $binary" >&2
  exit 1
fi

run_one() {
  local name="$1" playfield="$2"
  local dir="$outdir/$name"
  mkdir -p "$dir"
  # 出力先を再利用したときに前回の結果を今回の結果と取り違えないよう先に消す
  # (待ちループと最終判定は「今回の run が書いたファイル」だけを見る)。
  rm -f "$dir/autotest-v6-result.txt"
  echo "[playfield-compare] run=$name playfield=$playfield"
  # exec で置き換える: 背景で走らせた関数の子ではなく、$! がそのまま
  # プレイヤーの PID になる (cleanup が確実に本人を止める)。
  exec "$binary" \
    -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
    -autotest v6playfield -playfield "$playfield" \
    -autotest-out "$dir" -logFile "$dir/Player.log"
}

app_pid=""
cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    kill "$app_pid" 2>/dev/null
  fi
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

# (a) 既定 (3D レーン) の証跡。
run_one legacy 0 &
app_pid=$!
for _ in $(seq 1 60); do
  [ -f "$outdir/legacy/autotest-v6-result.txt" ] && break
  kill -0 "$app_pid" 2>/dev/null || break
  sleep 5
done
if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
  kill "$app_pid" 2>/dev/null
fi
wait "$app_pid" 2>/dev/null
app_pid=""

# (b) v6 遠近法プレイフィールドの証跡。
run_one v6 1 &
app_pid=$!
for _ in $(seq 1 60); do
  [ -f "$outdir/v6/autotest-v6-result.txt" ] && break
  kill -0 "$app_pid" 2>/dev/null || break
  sleep 5
done
if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
  kill "$app_pid" 2>/dev/null
fi
wait "$app_pid" 2>/dev/null
app_pid=""

echo "[playfield-compare] screenshots:"
find "$outdir" -name '*.png' | sort

status=0
for name in legacy v6; do
  result="$outdir/$name/autotest-v6-result.txt"
  if [ ! -f "$result" ]; then
    echo "[playfield-compare] MISSING result: $name/autotest-v6-result.txt"
    status=1
  elif ! grep -q 'fail=0' "$result"; then
    echo "[playfield-compare] FAIL in $name:"
    grep 'FAIL' "$result" || true
    status=1
  fi
done

if [ "$status" = 0 ]; then
  echo "[playfield-compare] OK"
fi
exit "$status"
