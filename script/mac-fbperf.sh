#!/usr/bin/env bash
# GameScene のフレームタイムを macOS 実機で測る (Issue #381 / #393–#396)。
#
#   script/mac-fbperf.sh [<app path>] [<out dir>] [<song>] [<seconds>]
#
# 何をするか:
#   1. プレイヤーを **実ディスプレイ全画面** で起動し、"-autotest v6songend" で
#      実カタログの曲を選ばせ、GameScene へ入る。
#   2. "-fbperf" を付けるので FrameTimeProbe が 5 秒ごとに
#      `[PerfProbe] event=window scene=GameScene ... avgFps / p50 / p95 / p99 /
#      worst / gc0 / gcKBPerFrame` を Player.log に出す。
#   3. 実行後、GameScene の窓だけを抜き出して集計する。
#
# 何を測らないか:
#   - メディア準備の待ち時間 (それは #395 の担当)。ここは **演奏中の定常状態**。
#   - GPU 時間。`-fbperf` はフレーム時間 (CPU+GPU の融合した壁時計) しか出さない。
#     GPU 内訳が要るなら Unity Profiler を繋ぐ (このスクリプトの範囲外)。
#
# 注意:
#   - 窓リサイズによる擬似全画面は不可。
#   - 出力は「比較できる数値」であって合否ではない。改善量は before/after で出す。
set -uo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
app="${1:-$root/Builds/macOS/FlashBeat.app}"
outdir="${2:-$root/docs/evidence/381-fbperf/$(git -C "$root" rev-parse --short HEAD)}"
song="${3:-ツイッターランド}"
seconds="${4:-90}"

mkdir -p "$outdir"
log="$outdir/Player.log"
result="$outdir/autotest-v6songend-result.txt"
rm -f "$log" "$result"

# Unity のプレイヤーログは実行ファイルの隣ではなく、macOS では
# ~/Library/Logs/<company>/<product>/Player.log に書かれる。起動前の位置を覚えて
# おき、起動後に伸びた分だけをこの実行のログとして切り出す (前回の実行と混ぜない)。
player_log="$HOME/Library/Logs/KimuraYusuke/FlashBeat/Player.log"
log_offset=0
if [ -f "$player_log" ]; then
  log_offset=$(wc -c < "$player_log" | tr -d ' ')
fi

echo "[fbperf] app=$app"
echo "[fbperf] out=$outdir"
echo "[fbperf] song=$song seconds=$seconds"

# 実行ファイルを直叩きして pid を保持する (手元で起動した FlashBeat は殺さない)。
# 曲の終端まで見たいので、曲長 + 余裕を上限にする (seconds が小さいときは
# 演奏中の定常状態だけを見る)。
"$app/Contents/MacOS/FlashBeat" \
  -autotest v6songend \
  -autotest-out "$outdir" \
  -autotest-song "$song" \
  -fbperf \
  > "$outdir/stdout.log" 2>&1 &
pid=$!

# 指定秒数だけ走らせてから止める (曲終了より先に切り上げてよい:
# 見たいのは演奏中の定常フレームタイム)。
elapsed=0
while [ "$elapsed" -lt "$seconds" ]; do
  if ! kill -0 "$pid" 2>/dev/null; then
    break
  fi
  sleep 1
  elapsed=$((elapsed + 1))
done

if kill -0 "$pid" 2>/dev/null; then
  kill -TERM "$pid" 2>/dev/null || true
  # 終了処理で最後の窓が出るので少し待つ。
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
  kill -KILL "$pid" 2>/dev/null || true
fi
wait "$pid" 2>/dev/null || true

if [ ! -f "$player_log" ]; then
  echo "[fbperf] Player.log not found at $player_log (the player may not have started)" >&2
  exit 3
fi

# Unity は起動時にログを切り詰めて書き直すことがあるため、バイト位置での
# 差分切り出しは当てにならない (実測: 起動後も前回の内容が残っていた)。
# このプレイヤーは 1 実行 1 ログなので、そのままコピーしてから
# 「この実行の開始時刻以降」の行だけを使う。
cp "$player_log" "$log"

python3 - "$log" "$outdir/summary.txt" <<'PY'
import re, sys, statistics

log_path, out_path = sys.argv[1], sys.argv[2]
lines = open(log_path, encoding='utf-8', errors='replace').read().splitlines()

window = re.compile(
    r'\[PerfProbe\] event=window scene=(?P<scene>\S+) frames=(?P<frames>\d+)'
    r' avgFps=(?P<avg>[\d.]+) p50=(?P<p50>[\d.]+) p95=(?P<p95>[\d.]+)'
    r' p99=(?P<p99>[\d.]+) worst=(?P<worst>[\d.]+) gc0=(?P<gc0>\d+)'
    r' gcKBPerFrame=(?P<kb>[\d.]+)')
spike = re.compile(r'\[PerfProbe\] event=spike scene=(?P<scene>\S+) frameMs=(?P<ms>[\d.]+)')

rows = [m.groupdict() for m in map(window.match, lines) if m]
spikes = [g for g in (m.groupdict() for m in map(spike.match, lines) if m)]

game = [r for r in rows if r['scene'] == 'GameScene']

def summarize(label, group):
    if not group:
        return [f'{label}: no window (measurement did not reach this scene)']
    avgs = [float(r['avg']) for r in group]
    p95s = [float(r['p95']) for r in group]
    p99s = [float(r['p99']) for r in group]
    worst = [float(r['worst']) for r in group]
    gc0 = sum(int(r['gc0']) for r in group)
    kb = [float(r['kb']) for r in group]
    return [
        f'{label}: windows={len(group)}',
        f'  avgFps  min={min(avgs):.1f}  median={statistics.median(avgs):.1f}  max={max(avgs):.1f}',
        f'  p95 ms  median={statistics.median(p95s):.2f}  worst={max(p95s):.2f}',
        f'  p99 ms  median={statistics.median(p99s):.2f}  worst={max(p99s):.2f}',
        f'  worst frame ms  max={max(worst):.1f}',
        f'  gc0 total={gc0}  gcKBPerFrame median={statistics.median(kb):.1f}',
    ]

out = []
out.append('== all scenes ==')
for scene in sorted({r['scene'] for r in rows}):
    out += summarize(scene, [r for r in rows if r['scene'] == scene])
out.append('')
out.append('== GameScene (steady state) ==')
out += summarize('GameScene', game)
out.append('')
out.append('== spikes (frame > 250ms) ==')
if not spikes:
    out.append('none')
else:
    for s in spikes:
        out.append(f"  scene={s['scene']} frameMs={float(s['ms']):.1f}")

text = '\n'.join(out) + '\n'
open(out_path, 'w', encoding='utf-8').write(text)
print(text)
PY

echo "[fbperf] summary: $outdir/summary.txt"
# GameScene の窓が 1 つも無ければ失敗扱い (測れていない実行を成功にしない)。
if ! grep -q "GameScene: windows=" "$outdir/summary.txt"; then
  echo "[fbperf] no GameScene window was measured" >&2
  exit 4
fi
