#!/usr/bin/env bash
# 曲終了ハーネス (Issue #233 / #226) を macOS 実機で走らせる。
#
#   script/mac-songend-harness.sh [--with-os-screenshot] [<app path>] [<out dir>]
#
# 1. 引数のプレイヤー (既定: Builds/macOS/FlashBeat.app) を **実ディスプレイ全画面** で
#    起動し、"-autotest v6songend" でハーネスを走らせる。
# 2. 結果ファイル (autotest-v6songend-result.txt) が現れるまで待つ。
# 3. **結果を検証する**: 結果ファイルが無い / fail=0 でない / ログに FAIL 行が
#    ある のいずれかなら非 0 で終わる (失敗した実行が成功として出版されない
#    ようにするため)。
#
# 注意:
# - 窓リサイズによる擬似全画面は不可 (実ディスプレイの全画面只能用)。
# - OS レベルのスクリーンショット (screencapture) は macOS の Space 分離で
#   デスクトップを撮るため **証拠には使わない**。--with-os-screenshot を
#   明示した時だけ撮る (既定は撮らない)。
# - 起動は実行ファイル直叩き (pid を保持して自分の子だけを殺す)。ユーザーが
#   手元で起動した FlashBeat は kill しない。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app=""
outdir=""
with_os_screenshot=0

while [ $# -gt 0 ]; do
  case "$1" in
    --with-os-screenshot) with_os_screenshot=1; shift ;;
    -h|--help)
      sed -n '2,20p' "$0"
      exit 0
      ;;
    *)
      if [ -z "$app" ]; then
        app="$1"
      elif [ -z "$outdir" ]; then
        outdir="$1"
      else
        echo "[songend] too many arguments" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/songend-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[songend] app not found: $app" >&2
  exit 1
fi

binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[songend] player binary not found or not executable: $binary" >&2
  exit 1
fi

app_pid=""

# Ctrl-C でも自分の子だけを殺す (ユーザーが手元で起動した实例には触らない)。
cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    echo "[songend] stopping the player (pid $app_pid)"
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

result="$outdir/autotest-v6songend-result.txt"
log="$outdir/Player.log"

echo "[songend] app=$app out=$outdir"
"$binary" \
  -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
  -autotest v6songend -autotest-out "$outdir" -logFile "$log" &
app_pid=$!
echo "[songend] player started (pid $app_pid)"

for _ in $(seq 1 180); do
  if [ -f "$result" ]; then break; fi
  if ! kill -0 "$app_pid" 2>/dev/null; then
    echo "[songend] the player exited before writing the result file" >&2
    break
  fi
  sleep 5
done

# 結果ファイルを書いたらプレイヤーは自分で終了する (Application.Quit)。
# まだ生きていればここで止める。
if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
fi
cleanup
app_pid=""

if [ "$with_os_screenshot" = 1 ]; then
  screencapture -x "$outdir/os-screen.png" 2>/dev/null \
    || echo "[songend] (OS screencapture unavailable)"
fi

echo "[songend] screenshots:"
find "$outdir" -name '*.png' | sort

# --- 結果の検証 (ここが exit code の唯一の根拠) ---------------------------

status=0

if [ ! -f "$result" ]; then
  echo "[songend] FAIL: no result file ($result)" >&2
  echo "[songend] see the player log: $log" >&2
  status=1
else
  echo "[songend] result:"
  cat "$result"

  if ! grep -q 'fail=0' "$result"; then
    echo "[songend] FAIL: the result file does not report fail=0" >&2
    status=1
  fi
fi

if [ -f "$log" ]; then
  if grep -q 'FAIL ' "$log"; then
    echo "[songend] FAIL: the harness logged a failing check:" >&2
    grep 'FAIL ' "$log" >&2
    status=1
  fi
else
  echo "[songend] FAIL: no player log ($log)" >&2
  status=1
fi

if [ "$status" = 0 ]; then
  echo "[songend] OK: the harness passed"
else
  echo "[songend] the run FAILED (see the diagnostics above)" >&2
fi

exit "$status"
