#!/usr/bin/env bash
# HOLD プローブハーネス (Issue #278 / Issue #59) を macOS 実機で走らせる。
#
#   script/mac-holdprobe-harness.sh [<app path>] [<out dir>]
#
# 1. 引数のプレイヤー (既定: Builds/macOS/FlashBeat.app) を **実ディスプレイ全画面**
#    (2560 x 1664) で起動し、"-autotest v6hold" でハーネスを走らせる。
# 2. 別に **キー押下ドライバ** を走らせる。プレイヤーが書いた
#    hold-schedule.txt (各 HOLD の開始/終端の絶対時刻) を読むと、その時刻に
#    該当するレーンのキーを **押したまま** にする。
#    なぜ必要か: HOLD の保持フェーズは判定 (Judge.IsLaneKeyDown) が
#    UnityEngine.Input.GetKey = **実キーの押下状態** を読むので、キー注入
#    (KeyBindings.InjectKeyDown) だけでは保持できない。
#
#    注意 (実測 2026-11-10 macOS run 2): macOS の Unity legacy Input は
#    CGEvent の合成キーイベントを Input.GetKey として **見ない**。
#    このドライバのキー押下は判定に反映されないので、HOLD の保持は
#    プレイヤー側の注入シーム (KeyBindings.InjectKeyHold) が担う。
#    ドライバは「実キーを押す経路が動くなら動く」補助として残す。
# 3. 結果ファイル (autotest-v6hold-result.txt) が現れるまで待つ。
# 4. **結果を検証する**: 結果ファイルが無い / fail=0 でない / ログに FAIL 行が
#    ある のいずれかなら非 0 で終わる。
#
# 注意:
# - 窓リサイズによる擬似全画面は不可 (実ディスプレイの全画面只能用)。
# - OS レベルのスクリーンショット (screencapture) は macOS の Space 分離で
#   デスクトップを撮るため **証拠には使わない** (プレイヤーの
#   ScreenCapture だけが証拠)。
# - 起動は実行ファイル直叩き (pid を保持して自分の子だけを殺す)。ユーザーが
#   手元で起動した FlashBeat は kill しない。
# - キー押下は CGEvent (CoreGraphics) 経由。macOS のアクセシビリティ権限が
#   無い環境ではキーイベントが届かないため、HOLD は「途中で切った (Broken)」
#   判定になり、その場合はハーネスが fail=1 で終了する (黙って通さない)。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app=""
outdir=""

while [ $# -gt 0 ]; do
  case "$1" in
    -h|--help)
      sed -n '2,30p' "$0"
      exit 0
      ;;
    *)
      if [ -z "$app" ]; then
        app="$1"
      elif [ -z "$outdir" ]; then
        outdir="$1"
      else
        echo "[holdprobe] too many arguments" >&2
        exit 2
      fi
      shift
      ;;
  esac
done

[ -n "$app" ] || app="$root/Builds/macOS/FlashBeat.app"
[ -n "$outdir" ] || outdir="$root/Builds/holdprobe-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[holdprobe] app not found: $app" >&2
  exit 1
fi

binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[holdprobe] player binary not found or not executable: $binary" >&2
  exit 1
fi

app_pid=""
driver_pid=""

cleanup() {
  if [ -n "$driver_pid" ] && kill -0 "$driver_pid" 2>/dev/null; then
    kill "$driver_pid" 2>/dev/null || true
    wait "$driver_pid" 2>/dev/null || true
  fi
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    echo "[holdprobe] stopping the player (pid $app_pid)"
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

result="$outdir/autotest-v6hold-result.txt"
log="$outdir/Player.log"
schedule="$outdir/hold-schedule.txt"
helper="$outdir/keyhold"

# --- キー押下ヘルパー (CGEvent) ---------------------------------------------
# 用途: HOLD の保持フェーズ用に「キーを押したまま」にする。
cat > "$outdir/keyhold.swift" <<'SWIFT'
import Foundation
import CoreGraphics

// usage: keyhold down|up|tap <virtual key code> [pid]
let args = CommandLine.arguments
guard args.count >= 3, let code = UInt16(args[2]) else { print("bad args"); exit(2) }
let mode = args[1]
var targetPid: pid_t = 0
if args.count >= 4, let p = pid_t(args[3]) { targetPid = p }

func post(_ down: Bool) {
    guard let ev = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: down) else { return }
    if targetPid != 0 {
        ev.postToPid(targetPid)
    } else {
        ev.post(tap: .cghidEventTap)
    }
}

switch mode {
case "down": post(true)
case "up": post(false)
case "tap": post(true); usleep(60000); post(false)
default: print("bad mode"); exit(2)
}
SWIFT

if command -v swiftc >/dev/null 2>&1 && swiftc -O "$outdir/keyhold.swift" -o "$helper" 2>"$outdir/keyhold-build.log"; then
  echo "[holdprobe] key helper built: $helper"
else
  echo "[holdprobe] WARNING: could not build the key helper (swiftc missing?)" >&2
  echo "[holdprobe] the HOLD hold phase will be reported as broken" >&2
  helper=""
fi

# epoch 秒 (小数) を返す。
now_epoch() {
  perl -MTime::HiRes=time -e 'printf "%.3f", time'
}

# 指定した epoch 秒まで待つ (.Sleep は浮動小数可)。
wait_until() {
  local target="$1"
  while :; do
    local now diff sleep_for
    now="$(now_epoch)"
    diff="$(perl -e "printf '%.3f', $target - $now")"
    if perl -e "exit($diff <= 0 ? 0 : 1)"; then
      break
    fi
    sleep_for="$(perl -e "my \$d = $diff; \$d = 0.2 if \$d > 0.2; printf '%.3f', \$d")"
    sleep "$sleep_for"
  done
}

# --- キー押下ドライバ -------------------------------------------------------
# プレイヤーが hold-schedule.txt を書く → 各 HOLD の先頭 0.5 秒前にキーを
# 押して、終端 0.6 秒後に離す。
# 「先頭より前」に押さないと、注入された押下が先頭判定を済ませてしまい、
# 保持開始時点でキーが上がっている = Broken 判定になる。
drive_keys() {
  local deadline=$(( $(date +%s) + 900 ))
  while [ ! -f "$schedule" ]; do
    if ! kill -0 "$app_pid" 2>/dev/null; then return 0; fi
    if [ "$(date +%s)" -gt "$deadline" ]; then
      echo "[holdprobe] the schedule file never appeared" >&2
      return 0
    fi
    sleep 0.1
  done

  # ファイルが書かれる瞬間に読むと中途半端な行を読みうるので、stable になるまで待つ。
  sleep 0.3

  while IFS= read -r line; do
    [ -n "$line" ] || continue
    lane="$(printf '%s' "$line" | sed -n 's/.*lane=\([0-9]*\).*/\1/p')"
    keycode="$(printf '%s' "$line" | sed -n 's/.*keycode=\(-\{0,1\}[0-9]*\).*/\1/p')"
    head_wall="$(printf '%s' "$line" | sed -n 's/.*headWall=\([0-9.]*\).*/\1/p')"
    end_wall="$(printf '%s' "$line" | sed -n 's/.*endWall=\([0-9.]*\).*/\1/p')"

    if [ -z "$keycode" ] || [ "$keycode" = "-1" ]; then
      echo "[holdprobe] lane $lane has no mappable key - skipped (injection only)"
      continue
    fi
    if [ -z "$head_wall" ] || [ -z "$end_wall" ]; then
      echo "[holdprobe] cannot parse the schedule line: $line" >&2
      continue
    fi
    if [ -z "$helper" ]; then
      echo "[holdprobe] no key helper - lane $lane skipped"
      continue
    fi

    # 先頭は判定幅 (PERFECT 0.10 秒) の内側で **少し前** に押す。
    # 0.5 秒前に押すと判定幅の外なので先頭が当たらない。
    # 終端は判定幅 (GREAT 0.15 秒) の内側で離す。外すと中断 (Broken) になる。
    down_at="$(perl -e "printf '%.3f', $head_wall - 0.05")"
    up_at="$(perl -e "printf '%.3f', $end_wall + 0.05")"
    echo "[holdprobe] lane $lane: key $keycode down at $down_at, up at $up_at"
    wait_until "$down_at"
    "$helper" down "$keycode" "$app_pid" || true
    wait_until "$up_at"
    "$helper" up "$keycode" "$app_pid" || true
  done < "$schedule"
}

echo "[holdprobe] app=$app out=$outdir"
"$binary" \
  -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 \
  -autotest v6hold -autotest-out "$outdir" -logFile "$log" &
app_pid=$!
echo "[holdprobe] player started (pid $app_pid)"

drive_keys &
driver_pid=$!

for _ in $(seq 1 240); do
  if [ -f "$result" ]; then break; fi
  if ! kill -0 "$app_pid" 2>/dev/null; then
    echo "[holdprobe] the player exited before writing the result file" >&2
    break
  fi
  sleep 5
done

# 結果ファイルを書いたらプレイヤーは自分で終了する (Application.Quit)。
if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
fi
cleanup
app_pid=""
driver_pid=""

echo "[holdprobe] screenshots:"
find "$outdir" -name '*.png' | sort

# --- 結果の検証 (ここが exit code の唯一の根拠) ---------------------------

status=0

if [ ! -f "$result" ]; then
  echo "[holdprobe] FAIL: no result file ($result)" >&2
  echo "[holdprobe] see the player log: $log" >&2
  status=1
else
  echo "[holdprobe] result:"
  cat "$result"

  if ! grep -q 'fail=0' "$result"; then
    echo "[holdprobe] FAIL: the result file does not report fail=0" >&2
    status=1
  fi
fi

if [ -f "$log" ]; then
  if grep -q 'FAIL ' "$log"; then
    echo "[holdprobe] FAIL: the harness logged a failing check:" >&2
    grep 'FAIL ' "$log" >&2
    status=1
  fi
else
  echo "[holdprobe] FAIL: no player log ($log)" >&2
  status=1
fi

if [ "$status" = 0 ]; then
  echo "[holdprobe] OK: the harness passed"
else
  echo "[holdprobe] the run FAILED (see the diagnostics above)" >&2
fi

exit "$status"
