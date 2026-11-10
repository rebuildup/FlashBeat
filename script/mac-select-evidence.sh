#!/usr/bin/env bash
# 選曲画面のカタログ受入条件 (Issue #58) の実機証跡を採るためのラッパ。
#
#   script/mac-select-evidence.sh --out <dir> [--song <曲名>] [--app <app path>]
#                                 [--width <w>] [--height <h>] [--timeout <sec>]
#
# 既存の script/mac-songend-harness.sh と同じハーネス (builtin の
# AutoTestV6SongEnd) を **特定の曲** で走らせる。builtin ハーネスは
# 「すぐ終わる曲」を既定で選ぶので、カタログ末尾の曲 (スクロールして
# 最後に到達する曲) を実機で選ぶには -autotest-song を素通しする必要がある。
# builtin 側を編集せずに済ませるため、本スクリプトがラッパとして
# その引数を渡す。
#
# 実ディスプレイ全画面 (実機のネイティブ解像度) で起動する。窓リサイズによる
# 擬似全画面は不可。
#
# 起動は実行ファイル直叩き (pid を保持し、自分の子だけを kill する)。
# ユーザーが手元で起動した FlashBeat には触らない。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/Builds/macOS/FlashBeat.app"
outdir=""
song=""
width=2560
height=1664
timeout=1500
offline=0
manual=0

while [ $# -gt 0 ]; do
  case "$1" in
    --app) app="$2"; shift 2 ;;
    --out) outdir="$2"; shift 2 ;;
    --song) song="$2"; shift 2 ;;
    --width) width="$2"; shift 2 ;;
    --height) height="$2"; shift 2 ;;
    --timeout) timeout="$2"; shift 2 ;;
    --offline) offline=1; shift ;;
    --manual) manual=1; shift ;;
    -h|--help)
      sed -n '2,18p' "$0"
      exit 0
      ;;
    *) echo "[select-evidence] unknown argument: $1" >&2; exit 2 ;;
  esac
done

[ -n "$outdir" ] || { echo "[select-evidence] --out is required" >&2; exit 2; }
mkdir -p "$outdir"
outdir="$(cd "$outdir" && pwd)"

if [ ! -d "$app" ]; then
  echo "[select-evidence] app not found: $app" >&2
  exit 1
fi
binary="$app/Contents/MacOS/FlashBeat"
if [ ! -x "$binary" ]; then
  echo "[select-evidence] player binary not found or not executable: $binary" >&2
  exit 1
fi

result="$outdir/autotest-v6songend-result.txt"
log="$outdir/Player.log"

if [ "$offline" = 1 ]; then
  # カタログ未到達 (クラウドに到達できない) 状態。
  # UnityWebRequest は libcurl ベースなので http_proxy / https_proxy を読む。
  # ループバックの未開ポートへ向ければ外部 API へ出られない。
  result="$outdir/autotest-v6screens-result.txt"
  echo "[select-evidence] offline mode: the cloud catalog must NOT be reachable"
  export http_proxy="http://127.0.0.1:1"
  export https_proxy="http://127.0.0.1:1"
  export HTTP_PROXY="http://127.0.0.1:1"
  export HTTPS_PROXY="http://127.0.0.1:1"
  export ALL_PROXY="http://127.0.0.1:1"
fi

app_pid=""
cleanup() {
  if [ -n "$app_pid" ] && kill -0 "$app_pid" 2>/dev/null; then
    echo "[select-evidence] stopping the player (pid $app_pid)"
    kill "$app_pid" 2>/dev/null || true
    wait "$app_pid" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

echo "[select-evidence] app=$app out=$outdir song='${song:-<builtin default>}' res=${width}x${height}"
if [ "$offline" = 1 ]; then
  # 選曲画面だけを撮って終わる (fullscreen 実機)。
  args=(
    -screen-width "$width" -screen-height "$height" -screen-fullscreen 1
    -autotest v6screens -autotest-scenes SelectScene -autotest-out "$outdir" -logFile "$log"
  )
elif [ "$manual" = 1 ]; then
  # 手動操作モード: builtin の自動テストは使わず、**実キーの入力**だけで
  # 選曲 → ゲーム → Result → Retry までを進める (画面は -autoshot が自前で撮る)。
  # 入力は osascript (System Events) で行うが、**最前面が FlashBeat のときだけ**
  # 送る。(ユーザーの他のアプリにキーを飛ばさないため)
  args=(
    -screen-width "$width" -screen-height "$height" -screen-fullscreen 1
    -autoshot "$outdir" -autoshot-interval 4 -autoshot-count 90 -logFile "$log"
  )
else
  args=(
    -screen-width "$width" -screen-height "$height" -screen-fullscreen 1
    -autotest v6songend -autotest-out "$outdir" -logFile "$log"
  )
  if [ -n "$song" ]; then
    args+=(-autotest-song "$song")
  fi
fi

"$binary" "${args[@]}" &
app_pid=$!
echo "[select-evidence] player started (pid $app_pid)"

# --- 手動操作モードのヘルパ -----------------------------------------------

# 最前面のプロセスが FlashBeat か (違えばキーを送らない)。
front_is_player() {
  local name
  name="$(osascript -e 'tell application "System Events" to return name of first process whose frontmost is true' 2>/dev/null)"
  [ "$name" = "FlashBeat" ]
}

# 最前面を FlashBeat にする (既に最前面なら何もしない)。
focus_player() {
  # activate を使う (System Events の frontmost 指定では全画面 Space へ移らないことがある)。
  osascript -e 'tell application "FlashBeat" to activate' >/dev/null 2>&1 || true
  sleep 2
}

# キーを 1 つ送る (key code)。最前面が FlashBeat でなければ送らない。
send_key() {
  local code="$1" label="$2"
  if ! front_is_player; then
    echo "[select-evidence] SKIP the key '$label': FlashBeat is not frontmost" >&2
    return 1
  fi
  osascript -e "tell application \"System Events\" to key code $code" >/dev/null 2>&1
  echo "[select-evidence] sent the key '$label' (key code $code)"
  sleep 1
}

# ログにパターンが現れるまで待つ (既定 1 回以上)。
# 第 3 引数で「最低出現回数」を指定できる (2 回目の遷移を待つのに使う)。
wait_for_log() {
  local pattern="$1" limit="$2" min="${3:-1}" waited=0 hits=0
  while [ "$waited" -lt "$limit" ]; do
    if [ -f "$log" ]; then
      hits="$(grep -c "$pattern" "$log" 2>/dev/null | head -1)"
      [ -z "$hits" ] && hits=0
      if [ "$hits" -ge "$min" ]; then
        echo "[select-evidence] the log shows '$pattern' (x$hits)"
        return 0
      fi
    fi
    if ! kill -0 "$app_pid" 2>/dev/null; then
      echo "[select-evidence] the player exited while waiting for '$pattern'" >&2
      return 1
    fi
    sleep 2
    waited=$((waited + 2))
  done
  echo "[select-evidence] TIMEOUT waiting for '$pattern' (x$hits, need $min)" >&2
  return 1
}

if [ "$manual" = 1 ]; then
  echo "[select-evidence] manual mode: driving the real keys"
  focus_player

  # 1. タイトル → 選曲 (Space = Start)。
  wait_for_log "scene load end TitleScene" 90 || true
  sleep 2
  send_key 49 "Space (title: start)" || true

  # 2. 選曲: 4 曲分下へ ( Neanderthal / まにまに / Who / ツイッターランド)、
  #     然后 Space = Ready。
  wait_for_log "scene load end SelectScene" 90 || true
  sleep 3
  focus_player
  send_key 125 "Down (1/4)" || true
  send_key 125 "Down (2/4)" || true
  send_key 125 "Down (3/4)" || true
  send_key 125 "Down (4/4)" || true
  sleep 1
  send_key 49 "Space (select: ready)" || true

  # 3. ゲーム: メディアの準備を待って Space で再生。
  wait_for_log "scene load end GameScene" 90 || true
  wait_for_log "VideoPlayer prepared" 300 || true
  sleep 3
  focus_player
  send_key 49 "Space (game: play)" || true
  if ! wait_for_log "mediaTime=" 30; then
    focus_player
    send_key 49 "Space (game: play, retry)" || true
    wait_for_log "mediaTime=" 30 || true
  fi

  # 4. 曲終了 → Result。
  wait_for_log "scene load end ResultScene" 300 || true
  sleep 4

  # 5. Retry (R) → ゲーム画面へ戻る (譜面 / セッションのリセット)。
  game_loads="$(grep -c "scene load end GameScene" "$log" 2>/dev/null | head -1)"
  [ -z "$game_loads" ] && game_loads=0
  focus_player
  send_key 15 "R (result: retry)" || true
  wait_for_log "scene load end GameScene" 90 $((game_loads + 1)) || true
  sleep 6

  echo "[select-evidence] manual mode finished"
  cleanup
  app_pid=""
fi

waited=0
while [ "$manual" != 1 ] && [ "$waited" -lt "$timeout" ]; do
  if [ -f "$result" ]; then break; fi
  if ! kill -0 "$app_pid" 2>/dev/null; then
    echo "[select-evidence] the player exited before writing the result file" >&2
    break
  fi
  sleep 5
  waited=$((waited + 5))
done

if kill -0 "$app_pid" 2>/dev/null; then
  sleep 2
fi
cleanup
app_pid=""

echo "[select-evidence] screenshots:"
find "$outdir" -name '*.png' | sort

status=0
if [ "$manual" = 1 ]; then
  # 手動モードは結果ファイルを作らない (Player.log の遷移履歴で判定する)。
  echo "[select-evidence] manual mode summary:"
  grep -E "scene load (start|end) |RESULT|playback summary|Retry|retry" "$log" 2>/dev/null | tail -20
  for marker in "scene load end SelectScene" "scene load end GameScene" "mediaTime=" "scene load end ResultScene"; do
    if ! grep -q "$marker" "$log" 2>/dev/null; then
      echo "[select-evidence] FAIL: the manual run never reached '$marker'" >&2
      status=1
    fi
  done
  # Retry でゲーム画面に戻ったこと (GameScene の遷移が 2 回以上)。
  game_loads="$(grep -c "scene load end GameScene" "$log" 2>/dev/null | head -1)"
  [ -z "$game_loads" ] && game_loads=0
  echo "[select-evidence] the GameScene was loaded $game_loads time(s) (2 = the retry returned to the game)"
  if [ "$game_loads" -lt 2 ]; then
    echo "[select-evidence] FAIL: the retry did not return to the game scene" >&2
    status=1
  fi
elif [ ! -f "$result" ]; then
  echo "[select-evidence] FAIL: no result file ($result)" >&2
  status=1
else
  echo "[select-evidence] result:"
  cat "$result"
  if [ "$offline" = 1 ]; then
    # v6screens の結果ファイルは fail= の形式ではないので、ログ側の FAIL 行だけで判定する。
    :
  elif ! grep -q 'fail=0' "$result"; then
    echo "[select-evidence] FAIL: the result file does not report fail=0" >&2
    status=1
  fi
fi

if [ -f "$log" ]; then
  if grep -q 'FAIL ' "$log"; then
    echo "[select-evidence] FAIL: the harness logged a failing check:" >&2
    grep 'FAIL ' "$log" >&2
    status=1
  fi
else
  echo "[select-evidence] FAIL: no player log ($log)" >&2
  status=1
fi

if [ "$status" = 0 ]; then
  echo "[select-evidence] OK: the harness passed"
else
  echo "[select-evidence] the run FAILED (see the diagnostics above)" >&2
fi
exit "$status"
