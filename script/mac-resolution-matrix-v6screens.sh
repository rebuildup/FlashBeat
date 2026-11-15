#!/usr/bin/env bash
# Issue #96 の補完: 既存の解像度行列 (mac-resolution-matrix.sh) が対象外にしている
# Result / Game (HUD・Pause・Loading) を、同じ解像度セットで macOS 実機撮影する。
#
#   script/mac-resolution-matrix-v6screens.sh [<FlashBeat.app>] [<出力先>]
#
# 環境変数:
#   RESOLUTIONS  スペース区切りの解像度リスト。
#                既定: "1024x768 1280x720 1920x1080 2560x1440 2560x1664"
#   V6SCENES     -autotest-scenes に渡すカンマ区切りシーン一覧。
#                既定: "ResultScene,GameScene" (Pause/Loading は GameScene 内で撮る)
#
# 各解像度で 1 回起動し、AutoTestV6Screens (-autotest v6screens) が
# v6screen-*.png と autotest-v6screens-result.txt を書いて終了する。
# 実プレイ状態が無い ResultScene は PrepareForAudit / OpenRegisterModalForAudit で
# 最終状態と登録モーダルを再現し、GameScene は HUD / Pause / Loading を監査 API で開く。
#
# 判定 (exit code): 全解像度で
#   1. autotest-v6screens-result.txt があり "resolution=<W>x<H>" が要求解像度と一致
#   2. 撮れた v6screen-*.png の実寸 (sips) が要求解像度と一致
#   3. RESULT 行の fail が 0
# を満たすなら 0、それ以外 1。画の良し悪し (崩れの有無) は判定しない — 人の目視項目。
#
# 起動は実行ファイル直叩き (pid を保持して自分の子だけを kill する)。
# open -n も pkill も使わない。ユーザーが手元で起動した FlashBeat には触らない。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app=""
outdir=""

while [ $# -gt 0 ]; do
  case "$1" in
    -h|--help)
      sed -n '2,32p' "$0"
      exit 0
      ;;
    *)
      if [ -z "$app" ]; then
        app="$1"
      elif [ -z "$outdir" ]; then
        outdir="$1"
      else
        echo "[res-v6] too many arguments" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/resolution-matrix-v6screens-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[res-v6] app not found: $app" >&2
  exit 1
fi
binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[res-v6] player binary not found or not executable: $binary" >&2
  exit 1
fi

resolutions=${RESOLUTIONS:-"1024x768 1280x720 1920x1080 2560x1440 2560x1664"}
scenes=${V6SCENES:-"ResultScene,GameScene"}

app_pid=""
cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    kill "$app_pid" 2>/dev/null
    for _ in $(seq 1 10); do
      kill -0 "$app_pid" 2>/dev/null || break
      sleep 1
    done
    if kill -0 "$app_pid" 2>/dev/null; then
      kill -9 "$app_pid" 2>/dev/null
    fi
  fi
  app_pid=""
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

png_size() {
  local png=$1 w h
  w=$(sips -g pixelWidth "$png" 2>/dev/null | awk '/pixelWidth/ {print $2}')
  h=$(sips -g pixelHeight "$png" 2>/dev/null | awk '/pixelHeight/ {print $2}')
  if [ -n "$w" ] && [ -n "$h" ]; then
    echo "${w}x${h}"
  else
    echo "unknown"
  fi
}

overall=0
: > "$outdir/summary.txt"

for res in $resolutions; do
  if ! printf '%s' "$res" | grep -Eq '^[0-9]+x[0-9]+$'; then
    echo "[res-v6] invalid resolution (expect WxH): $res" >&2
    exit 2
  fi
  w=${res%x*}
  h=${res#*x}

  run_dir="$outdir/$res"
  mkdir -p "$run_dir"
  log="$run_dir/Player.log"
  result="$run_dir/autotest-v6screens-result.txt"

  rm -f "$run_dir"/v6screen-*.png "$log" "$result"

  echo "[res-v6] === $res === app=$app scenes=$scenes"
  "$binary" \
    -screen-width "$w" -screen-height "$h" -screen-fullscreen 1 \
    -autotest v6screens -autotest-scenes "$scenes" \
    -autotest-out "$run_dir" -logFile "$log" &
  app_pid=$!
  echo "[res-v6] player started (pid $app_pid)"

  waited=0
  per_timeout=180
  while [ "$waited" -lt "$per_timeout" ]; do
    kill -0 "$app_pid" 2>/dev/null || break
    [ -f "$result" ] && break
    sleep 2
    waited=$((waited + 2))
  done
  if kill -0 "$app_pid" 2>/dev/null; then
    sleep 3
  fi
  cleanup

  run_status=0

  # --- 判定 1: 結果ファイルと解像度表記 ---
  if [ -f "$result" ] && grep -q "resolution=${w}x${h}" "$result"; then
    echo "[res-v6] result resolution ok: ${w}x${h}"
  else
    echo "[res-v6] result resolution MISMATCH (want ${w}x${h}): $result" >&2
    run_status=1
  fi

  # --- 判定 2: RESULT fail=0 ---
  fails=$(grep -oE 'RESULT pass=[0-9]+ fail=[0-9]+' "$result" 2>/dev/null | tail -1 | grep -oE 'fail=[0-9]+' | cut -d= -f2)
  if [ "${fails:-1}" = "0" ]; then
    echo "[res-v6] audit fail=0"
  else
    echo "[res-v6] audit fail=${fails:-missing}" >&2
    run_status=1
  fi

  # --- 判定 3: PNG の実寸 ---
  png_count=$(find "$run_dir" -name 'v6screen-*.png' 2>/dev/null | wc -l | tr -d ' ')
  dim_ok=0
  dim_bad=0
  for png in $(find "$run_dir" -name 'v6screen-*.png' 2>/dev/null | sort); do
    size=$(png_size "$png")
    if [ "$size" = "$res" ]; then
      dim_ok=$((dim_ok + 1))
    else
      dim_bad=$((dim_bad + 1))
      echo "[res-v6] png size MISMATCH: $(basename "$png") is $size (want $res)" >&2
    fi
  done
  if [ "$png_count" -gt 0 ] && [ "$dim_bad" -eq 0 ]; then
    echo "[res-v6] png size ok: $dim_ok/$png_count"
  else
    echo "[res-v6] png MISMATCH: count=$png_count bad=$dim_bad" >&2
    run_status=1
  fi

  if [ "$run_status" -eq 0 ]; then
    echo "[res-v6] $res PASS" | tee -a "$outdir/summary.txt"
  else
    echo "[res-v6] $res FAIL" | tee -a "$outdir/summary.txt"
    overall=1
  fi
done

echo "[res-v6] done -> $outdir (overall=$([ $overall -eq 0 ] && echo PASS || echo FAIL))"
exit $overall
