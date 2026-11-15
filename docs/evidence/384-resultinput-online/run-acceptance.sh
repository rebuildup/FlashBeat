#!/bin/bash
# Issue #384 オンライン登録の実機受入 (macOS, ローカル API http://localhost:5173)
# 使い方: 前提 = web/ で `FLASHBEAT_ADMIN_TOKEN=local-dev-only vp dev` が起動済み、
#         Builds/macOS/FlashBeat.app が baseUrl=http://localhost:5173 でビルド済み。
#   bash run-acceptance.sh <outdir> [seconds] [perfect]
set -u
ROOT=/Users/rebui/work/FlashBeat
OUT="${1:-$ROOT/docs/evidence/384-resultinput-online/success}"
SECONDS_ARG="${2:-240}"
PERFECT="${3:-139}"
mkdir -p "$OUT"

PLAYER_LOG="$HOME/Library/Logs/KimuraYusuke/FlashBeat/Player.log"
APP="$ROOT/Builds/macOS/FlashBeat.app"

# 前回実行のログと混ざらないよう、起動前の行数を記録する
LOG_MARK=0
[ -f "$PLAYER_LOG" ] && LOG_MARK=$(wc -l < "$PLAYER_LOG" | tr -d ' ')

"$APP/Contents/MacOS/FlashBeat" \
    -autotest resultinput \
    -autotest-out "$OUT" \
    -autotest-remote who \
    -autotest-perfect "$PERFECT" \
    -autotest-score 1000000 \
    -autotest-combo "$PERFECT" \
    -autotest-seconds "$SECONDS_ARG" \
    > "$OUT/run.log" 2>&1 &
PLAYER_PID=$!
echo "player pid=$PLAYER_PID out=$OUT"

# 今回起動分のログに「modal open」が出るまで待つ
for i in $(seq 1 90); do
    if tail -n +$((LOG_MARK + 1)) "$PLAYER_LOG" 2>/dev/null | grep -q 'modal open'; then break; fi
    sleep 2
done
tail -n +$((LOG_MARK + 1)) "$PLAYER_LOG" | grep 'AutoTestResultInput' | tail -3

osascript -e "tell application \"$APP\" to activate" >/dev/null 2>&1
sleep 1

# 日本語 IME: てすと (t e s u t o) を key code で打鍵 → IME がかな変換
for kc in 17 14 1 32 17 31; do
    osascript -e "tell application \"System Events\" to key code $kc"
    sleep 0.15
done
sleep 1
# IME の変換を確定 (1 回目の Return は IME が消費)
osascript -e 'tell application "System Events" to key code 36'
sleep 1
# もう一度 Return で入力確定 → 自動登録
osascript -e 'tell application "System Events" to key code 36'
sleep 1

# 登録結果を観測するだけの時間を置く
sleep 8
screencapture -x "$OUT/after-submit.png" 2>/dev/null || true

# 二重送信の確認: さらに Enter を連打しても再送されないこと
for i in 1 2 3; do
    osascript -e 'tell application "System Events" to key code 36'
    sleep 0.5
done
sleep 3
screencapture -x "$OUT/after-enter-mash.png" 2>/dev/null || true

wait $PLAYER_PID
echo "player exited=$?"
tail -n +$((LOG_MARK + 1)) "$PLAYER_LOG" | grep 'AutoTestResultInput' | tail -10
cat "$OUT/autotest-resultinput-result.txt" 2>/dev/null
