#!/usr/bin/env bash
# Issue #96 / #241 の「実機の解像度ごとの画」を macOS で撮る行列ハーネス。
#
#   script/mac-resolution-matrix.sh [<FlashBeat.app>] [<出力先>]
#
# 環境変数:
#   RESOLUTIONS  スペース区切りの解像度リスト。
#                既定: "1024x768 1280x720 1920x1080 2560x1440" (#96 の下限〜上限)
#
# 各解像度で 1 回起動し、AutoShot (-autoshot-scenes) で
# OpeningScene / TitleScene / SelectScene / OptionScene の PNG を撮る。
# ResultScene は実プレイ状態が無いと表示が空虚になるため既定の対象から外した
# (詳細は docs/evidence/96-241-resolution-matrix.md)。
#
# 判定 (exit code): 全解像度で
#   1. 期待枚数 (= scene 数) の PNG が出た
#   2. [AutoShot] ログの (<W>x<H> が要求解像度と一致
#   3. 撮れた PNG の実寸 (sips で確認) が要求解像度と一致
# を満たすなら 0、それ以外 1。
# 画の良し悪し (崩れの有無) は判定しない — それは人の目視項目 (#241)。
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
      sed -n '2,28p' "$0"
      exit 0
      ;;
    *)
      if [ -z "$app" ]; then
        app="$1"
      elif [ -z "$outdir" ]; then
        outdir="$1"
      else
        echo "[res-matrix] too many arguments" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/resolution-matrix-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[res-matrix] app not found: $app" >&2
  exit 1
fi
binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[res-matrix] player binary not found or not executable: $binary" >&2
  exit 1
fi

# 解像度行列 (既定は #96 の下限 1024x768 〜 上限 2560x1440)
resolutions=${RESOLUTIONS:-"1024x768 1280x720 1920x1080 2560x1440"}

# AutoShot に渡すシーン一覧と期待枚数
scenes="OpeningScene,TitleScene,SelectScene,OptionScene"
IFS=',' read -r -a scene_arr <<< "$scenes"
expected=${#scene_arr[@]}

interval=6
count=4
timeout=$((expected * interval + 60))

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

# PNG の実寸を "WxH" で返す (macOS 標準の sips を使用)。
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
    echo "[res-matrix] invalid resolution (expect WxH): $res" >&2
    exit 2
  fi
  w=${res%x*}
  h=${res#*x}

  run_dir="$outdir/$res"
  mkdir -p "$run_dir"
  log="$run_dir/Player.log"

  echo "[res-matrix] === $res === app=$app"
  "$binary" \
    -screen-width "$w" -screen-height "$h" -screen-fullscreen 1 \
    -autoshot "$run_dir" -autoshot-scenes "$scenes" \
    -autoshot-interval "$interval" -autoshot-count "$count" \
    -logFile "$log" &
  app_pid=$!
  echo "[res-matrix] player started (pid $app_pid)"

  # 期待枚数の PNG が出るまで待つ (プロセスが先に死んだら即打ち切り)。
  waited=0
  while [ "$waited" -lt "$timeout" ]; do
    kill -0 "$app_pid" 2>/dev/null || break
    n=$(find "$run_dir" -name 'scene-*.png' 2>/dev/null | wc -l | tr -d ' ')
    if [ "$n" -ge "$expected" ]; then
      break
    fi
    sleep 2
    waited=$((waited + 2))
  done
  if kill -0 "$app_pid" 2>/dev/null; then
    sleep 2
  fi
  cleanup

  # --- 判定 1: 期待枚数 ---
  png_count=$(find "$run_dir" -name 'scene-*.png' 2>/dev/null | wc -l | tr -d ' ')
  run_status=0
  if [ "$png_count" -eq "$expected" ]; then
    echo "[res-matrix] png count ok: $png_count/$expected"
  else
    echo "[res-matrix] png count MISMATCH: $png_count/$expected" >&2
    run_status=1
  fi

  # --- 判定 2: ログの解像度表記 ---
  log_hits=$(grep -cE "\[AutoShot\] .* \(${w}x${h}[),]" "$log" 2>/dev/null || true)
  if [ "$log_hits" -eq "$expected" ]; then
    echo "[res-matrix] log resolution ok: ${w}x${h} x$log_hits"
  else
    echo "[res-matrix] log resolution MISMATCH: ${w}x${h} x$log_hits (want $expected)" >&2
    run_status=1
  fi

  # --- 判定 3: PNG の実寸 ---
  dim_ok=0
  dim_bad=0
  for png in $(find "$run_dir" -name 'scene-*.png' 2>/dev/null | sort); do
    size=$(png_size "$png")
    if [ "$size" = "$res" ]; then
      dim_ok=$((dim_ok + 1))
    else
      dim_bad=$((dim_bad + 1))
      echo "[res-matrix] png size MISMATCH: $(basename "$png") is $size (want $res)" >&2
    fi
  done
  if [ "$dim_bad" -eq 0 ] && [ "$dim_ok" -eq "$expected" ]; then
    echo "[res-matrix] png size ok: $dim_ok/$expected"
  else
    run_status=1
  fi

  # --- 参考: fullscreen (判定には入れない) ---
  # AutoShot の scene 一覧モードのログ行には fullscreen= が付かない
  # (付くのは scene 指定なしモードのみ)。出ていれば True を要求する。
  fs_report="n/a (scene-list モードのログに fullscreen= なし)"
  if grep -q 'fullscreen=' "$log" 2>/dev/null; then
    if grep -q 'fullscreen=True' "$log" 2>/dev/null; then
      fs_report="fullscreen=True"
    else
      fs_report="fullscreen=True なし (要ログ確認)"
      run_status=1
    fi
  fi
  echo "[res-matrix] $res -> $fs_report"

  if [ "$run_status" -eq 0 ]; then
    echo "[res-matrix] $res PASS"
    printf 'PASS %s\n' "$res" >> "$outdir/summary.txt"
  else
    echo "[res-matrix] $res FAIL" >&2
    printf 'FAIL %s\n' "$res" >> "$outdir/summary.txt"
    overall=1
  fi
done

echo "[res-matrix] screenshots:"
find "$outdir" -name 'scene-*.png' | sort
echo "[res-matrix] summary:"
cat "$outdir/summary.txt"

if [ "$overall" -eq 0 ]; then
  echo "[res-matrix] ALL PASS"
else
  echo "[res-matrix] FAILURE (see lines above)" >&2
fi
exit "$overall"
