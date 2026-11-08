#!/usr/bin/env bash
# Win64 ビルドを Mac 上で行う (Issue #249 / CI の Unity ライセンス問題の実質的な回避)。
#
# 背景 (2026-11-08 実測, GitHub Actions run 42470147986):
#   build-win64 ジョブは **ソースのビルド失敗ではなく Unity ライセンス認証で失敗** する。
#     [Licensing::Client] Error: Code 400 while processing request (status: Machine bindings don't match)
#     No valid Unity Editor license found. Please activate your license.  → exit code 198
#   secret UNITY_LICENSE の .ulf は登録元マシンに紐づくため、GitHub-hosted runner では
#   機械結合が一致しない。CI を緑にするには .ulf ではなく Unity アカウント認証
#   (UNITY_EMAIL / UNITY_PASSWORD) の secret 登録、または .ulf の再発行 (ランナー固定の
#   実行環境が必要なため hosted runner では非現実的) が必要。
#
# 一方、手元の Mac には Unity 6000.5.6f1 + WindowsStandaloneSupport が入っており、
# 同じ BuildScript.BuildStandaloneWindows64 を **Windows 機と同じ出力** で実行できる。
#   - ソースは macOS 固有ではない (Windows ターゲットとしてクロスコンパイルされる)
#   - ただし **実行検証 (実 Win64 でのプレイ) は Mac ではできない** 那是 Owner 作業。
#     このスクリプトは「ビルド成果物が得られる」ことまでを保証する。
#
# 使い方:
#   script/win64-build-mac.sh [ref] [outdir]
#     ref     : ビルド対象 ref (既定 integration/v6-followup)
#     outdir  : ZIP 出力先 (既定 ../dist/win64)
#
# 出力: <outdir>/<version>-win64-<sha>.zip と .sha256
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}"
WORKTREE="${FB_WIN64_WORKTREE:-$REPO_ROOT/../FlashBeat.win64build}"
REF="${1:-integration/v6-followup}"
OUT_DIR="${2:-$REPO_ROOT/../dist/win64}"

[ -x "$UNITY" ] || { echo "Unity not found: $UNITY" >&2; exit 2; }
[ -d "/Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/WindowsStandaloneSupport" ] || {
  echo "WindowsStandaloneSupport module is missing (Unity Hub から Windows Build Support を追加)" >&2; exit 2; }

if [ ! -d "$WORKTREE" ]; then
  git -C "$REPO_ROOT" worktree add -q --detach "$WORKTREE" "$REF"
else
  git -C "$REPO_ROOT" fetch -q origin
  git -C "$WORKTREE" checkout -q --detach "$REF"
fi
( cd "$WORKTREE" && git lfs pull >/dev/null )

# 既存出力の上にビルドすると level1 が壊れるので退避する。
if [ -d "$WORKTREE/Builds/Windows" ]; then
  mv "$WORKTREE/Builds/Windows" "$WORKTREE/Builds/Windows.old.$(date +%s)"
fi

"$UNITY" -batchmode -quit -projectPath "$WORKTREE" \
  -executeMethod BuildScript.BuildStandaloneWindows64 \
  -logFile "$WORKTREE/win64-build.log"
[ -f "$WORKTREE/Builds/Windows/FlashBeat.exe" ] || {
  echo "build failed, see $WORKTREE/win64-build.log" >&2; exit 1; }

mkdir -p "$OUT_DIR"
SHA="$(git -C "$WORKTREE" rev-parse --short HEAD)"
FULL_SHA="$(git -C "$WORKTREE" rev-parse HEAD)"
BASE_VERSION="$(grep -m1 'bundleVersion:' "$WORKTREE/ProjectSettings/ProjectSettings.asset" | awk '{print $2}')"
VERSION="${BASE_VERSION}-win64-${SHA}"
ZIP="$OUT_DIR/${VERSION}.zip"

# PowerShell 版 (tools/release/package-win64.ps1) と同じ構成で固める。
# macOS の zip には -X (extra attributes を保存しない) を付けて再現可能にする。
( cd "$WORKTREE/Builds/Windows" && zip -X -q -r "$ZIP" . )

( cd "$OUT_DIR" && shasum -a 256 "$(basename "$ZIP")" > "$(basename "$ZIP").sha256" )

cat > "$OUT_DIR/${VERSION}.build-info.txt" <<INFO
version=$VERSION
commit=$FULL_SHA
ref=$REF
built_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
builder=script/win64-build-mac.sh
unity=6000.5.6f1 (macOS host, WindowsStandaloneSupport)
note=Windows 実行検証 (実機の 1 曲完走) は未実施。macOS 上でビルドした Windows ターゲットである。
INFO

echo "dist: $OUT_DIR"
ls -la "$OUT_DIR"
