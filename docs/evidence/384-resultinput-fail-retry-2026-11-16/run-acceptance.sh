#!/bin/bash
# Issue #384: Result 登録の「失敗 -> 再入力 -> 成功」を macOS 実機で一連実行する。
# 前提: web/ の Void dev サーバが http://localhost:5199 で起動済み、
#       Builds/macOS/FlashBeat.app が baseUrl=http://localhost:5199 でビルド済み。
set -u
ROOT=/Users/rebui/work/FlashBeat
OUT="$ROOT/docs/evidence/384-resultinput-fail-retry-2026-11-16"
APP="$ROOT/Builds/macOS/FlashBeat.app"
PLAYER_LOG="$HOME/Library/Logs/KimuraYusuke/FlashBeat/Player.log"
API="http://localhost:5199"
mkdir -p "$OUT"

log_mark() { [ -f "$PLAYER_LOG" ] && wc -l < "$PLAYER_LOG" | tr -d ' ' || echo 0; }
new_logs() {
  # Unity は起動時に Player.log を切り詰める。行数が mark を下回ったら
  # 新しいファイルとして全体を読み、そうでなければ mark 以降だけを読む。
  local lines=0
  [ -f "$PLAYER_LOG" ] && lines=$(wc -l < "$PLAYER_LOG" | tr -d ' ')
  if [ "$lines" -lt "$LOG_MARK" ]; then
    cat "$PLAYER_LOG" 2>/dev/null
  else
    tail -n +$((LOG_MARK + 1)) "$PLAYER_LOG" 2>/dev/null
  fi
}
wait_log() { # pattern timeout
  local pat="$1" timeout="$2" i
  for i in $(seq 1 "$timeout"); do
    if new_logs | grep -q "$pat"; then return 0; fi
    sleep 1
  done
  return 1
}
screenshot() { screencapture -x "$OUT/$1" 2>/dev/null || true; }
leaderboard() { curl -sS -m 5 "$API/api/v1/leaderboards/who?version=1"; }

echo "== precondition ==" | tee "$OUT/precondition.txt"
{
  echo "date=$(date '+%Y-%m-%d %H:%M:%S %Z')"
  echo "app=$APP"
  echo "api=$API"
  echo "catalog=$(curl -sS -m 5 "$API/api/v1/catalog")"
  echo "leaderboard_before=$(leaderboard)"
  strings "$APP/Contents/Resources/Data/resources.assets" | grep -A3 -B1 'CloudCatalogConfig' | head -10 || true
} | tee -a "$OUT/precondition.txt"

LOG_MARK=$(log_mark)
"$APP/Contents/MacOS/FlashBeat" \
  -autotest resultinput \
  -autotest-out "$OUT" \
  -autotest-remote who \
  -autotest-perfect 139 \
  -autotest-score 1000000 \
  -autotest-combo 139 \
  -autotest-seconds 90 \
  > "$OUT/run.log" 2>&1 &
PLAYER_PID=$!
echo "player pid=$PLAYER_PID" | tee "$OUT/run-info.txt"

if ! wait_log 'modal open' 90; then
  echo "modal did not open" | tee -a "$OUT/run-info.txt"
  kill "$PLAYER_PID" 2>/dev/null || true
  exit 1
fi
osascript -e "tell application \"$APP\" to activate" >/dev/null 2>&1
sleep 1
screenshot "01-modal-open.png"

# 失敗系: client 上限 16 以内・server の製品上限 12 超過の名前で送信する。
osascript -e 'tell application "System Events" to keystroke "FAILNAME12345"'
sleep 0.5
new_logs | grep 'AutoTestResultInput' | tail -5 | tee "$OUT/name-after-fail-typing.txt"
screencapture -x "$OUT/02-fail-name.png" 2>/dev/null || true
osascript -e 'tell application "System Events" to key code 36'
sleep 4
screenshot "03-after-fail-submit.png"
leaderboard > "$OUT/leaderboard-after-fail.json"
echo "leaderboard_after_fail=$(cat "$OUT/leaderboard-after-fail.json")" | tee -a "$OUT/run-info.txt"
new_logs | grep 'AutoTestResultInput' | tail -8 | tee "$OUT/name-after-fail-submit.txt"

# 失敗後は TMP が入力欄のフォーカスを外すため、実ユーザーと同じく欄をクリックして戻す。
# 画面は 3420x2148 rendering / 3420x2224 display。v6 frame は等倍中央配置で、
# modal dialog (444,231 840x510) 内の Name Field (28,112 784x84) の中央を押す。
python3 - <<'PY' > "$OUT/click-point.txt"
sw, sh = 3420, 2148
fw, fh = 1728, 972
ox, oy = (sw-fw)//2, (sh-fh)//2
dx, dy, dw, dh = 444, 231, 840, 510
fx, fy, fw2, fh2 = 28, 112, 784, 84
x = ox + dx + fx + fw2//2
y = oy + dy + fy + fh2//2
print(x, y)
PY
read CLICK_X CLICK_Y < "$OUT/click-point.txt"
echo "click=$CLICK_X,$CLICK_Y" | tee -a "$OUT/run-info.txt"
osascript - "$CLICK_X" "$CLICK_Y" <<'OSA'
on run argv
  set x to (item 1 of argv) as integer
  set y to (item 2 of argv) as integer
  tell application "System Events"
    click at {x, y}
  end tell
end run
OSA
sleep 0.7
# クリック位置が入力欄でなければ、選択済みのまま文字が入るかをログで確認する。
osascript -e 'tell application "System Events" to keystroke "RETRYOK"'
sleep 0.5
new_logs | grep 'AutoTestResultInput' | tail -8 | tee "$OUT/name-after-retry-typing.txt"
screencapture -x "$OUT/04-retry-name.png" 2>/dev/null || true
osascript -e 'tell application "System Events" to key code 36'

# 成功 chip (約 2.5 秒) を撮ってから leaderboard を読む。
sleep 2
screenshot "05-after-success-submit.png"
for i in $(seq 1 15); do
  LB=$(leaderboard)
  if printf '%s' "$LB" | grep -q 'RETRYOK'; then break; fi
  sleep 1
done
leaderboard > "$OUT/leaderboard-after-success.json"
echo "leaderboard_after_success=$(cat "$OUT/leaderboard-after-success.json")" | tee -a "$OUT/run-info.txt"
new_logs | grep 'AutoTestResultInput' | tail -12 | tee "$OUT/name-final.txt"

wait "$PLAYER_PID"
echo "player exit=$?" | tee -a "$OUT/run-info.txt"
cat "$OUT/autotest-resultinput-result.txt" 2>/dev/null | tee -a "$OUT/run-info.txt"
