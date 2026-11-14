#!/usr/bin/env bash
# #416 Native 資源の寿命・再入場メモリを macOS 実機で測る。
#
#   script/mac-v6mem.sh [<app path>] [<out dir>] [<ui rounds>] [<games>] [<songs>]
#
# 何をするか:
#   1. プレイヤーを実ディスプレイ全画面で起動し、"-autotest v6mem" で
#      AutoTestV6Mem を動かす。UI 巡回 (Title/Select/Option) を rounds 回、
#      プレイ再入場 (選曲→Game→Result) を games 回まわし、各チェック点で
#      資源センサス (Profiler API + オブジェクト列挙 + 監査カウンタ) を
#      Player.log と autotest-v6mem-result.txt に出す。
#   2. 末尾で遠隔ジャケットキャッシュの LRU 退避 (RemoteCacheCapacity 超過)
#      と DestroyAll の解放を検証する。
#   3. ハーネスは自分で Application.Quit するので、プロセス終了を待つ
#      (上限 timeout 秒。メディアの初回ダウンロードを含むため長め)。
#
# 数値の読み方は docs/evidence/416-*/README.md を参照。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="${1:-$root/Builds/macOS/FlashBeat.app}"
outdir="${2:-$root/docs/evidence/416-mem/$(git -C "$root" rev-parse --short HEAD)}"
rounds="${3:-3}"
games="${4:-2}"
songs="${5:-ツイッターランド,まにまに}"
ui="${V6MEM_UI:-TitleScene,SelectScene,OptionScene,SelectScene}"
timeout="${V6MEM_TIMEOUT:-2400}"

mkdir -p "$outdir"
result="$outdir/autotest-v6mem-result.txt"
rm -f "$result"

player_log="$HOME/Library/Logs/KimuraYusuke/FlashBeat/Player.log"
log="$outdir/Player.log"

echo "[v6mem] app=$app"
echo "[v6mem] out=$outdir"
echo "[v6mem] rounds=$rounds games=$games songs=$songs ui=$ui timeout=${timeout}s"

open -n "$app" --args \
  -autotest v6mem \
  -autotest-out "$outdir" \
  -autotest-rounds "$rounds" \
  -autotest-games "$games" \
  -autotest-songs "$songs" \
  -autotest-ui "$ui"

pid=$(pgrep -f "$app/Contents/MacOS/FlashBeat" | head -1)
if [ -z "$pid" ]; then
  echo "[v6mem] player did not start" >&2
  exit 3
fi

elapsed=0
while [ "$elapsed" -lt "$timeout" ]; do
  if ! kill -0 "$pid" 2>/dev/null; then
    break
  fi
  sleep 2
  elapsed=$((elapsed + 2))
done

if kill -0 "$pid" 2>/dev/null; then
  echo "[v6mem] timeout after ${timeout}s; terminating" >&2
  kill -TERM "$pid" 2>/dev/null || true
  sleep 5
  kill -KILL "$pid" 2>/dev/null || true
fi
wait "$pid" 2>/dev/null || true

# Unity は起動時にログを書き直すことがあるため丸ごとコピーする
# (mac-fbperf.sh と同じ方針: 1 実行 1 ログとして扱う)。
if [ -f "$player_log" ]; then
  cp "$player_log" "$log"
fi

if [ ! -f "$result" ]; then
  echo "[v6mem] result file missing: $result" >&2
  exit 4
fi

echo "[v6mem] done: $result"
grep -c "census" "$result" || true
