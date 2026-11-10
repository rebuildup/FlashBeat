#!/usr/bin/env bash
# Win64 ビルドを Mac 上で行い、**正規のパッケージスクリプト** に委譲する
# (Issue #35 / #249 / PR #253)。
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
#   - ただし **実行検証 (実 Win64 でのプレイ) は Mac ではできない** それは Owner 作業。
#     このスクリプトは「ビルド成果物が得られる」ことまでを保証する。
#
# **パッケージングは tools/release/package-win64.ps1 だけが行う。**
# このスクリプトはビルドの駆動役で、ZIP は作らない。macOS 側に別の経路を
# 残すと release gate (docs/operations/release-gate.md) に反する成果物ができてしまう:
#   - version.json を同梱しない
#   - FlashBeat_BurstDebugInformation_DoNotShip を除外しない
#   - media policy を検査しない
#   - 3 つ目のアセット (.build-info.txt) を出力する
# したがって **macOS のパッケージング経路は 1 本** (pwsh 側) にする。
#
# 使い方:
#   script/win64-build-mac.sh [ref] [outdir]
#     ref     : ビルド対象 ref (既定 main)
#     outdir  : パッケージ出力先 (既定 ../dist/win64)
#
# 環境変数:
#   UNITY_BIN               Unity 本体 (既定 Unity Hub の 6000.5.6f1)
#   FB_WIN64_WORKTREE       ビルド用 worktree のパス (既定 ../FlashBeat.win64build)
#   FLASHBEAT_VERSION_BASE  バージョン基準 (既定 0.1.0 / BuildScript と同じ既定)
#   FLASHBEAT_UPDATE_CHANNEL 更新チャンネル (既定 stable / BuildScript と同じ既定)
#   PWSH_BIN                PowerShell 7 (既定 pwsh)
#
# 出力: <outdir>/FlashBeat-<version>-win64-<sha>.zip と .zip.sha256 (2 点だけ)
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}"
WORKTREE="${FB_WIN64_WORKTREE:-$REPO_ROOT/../FlashBeat.win64build}"
REF="${1:-main}"
OUT_DIR="${2:-$REPO_ROOT/../dist/win64}"
# パッケージングは **このチェックアウトの** 正規実装を使う。
# (ビルドする worktree の ref は古い可能性があり、macOS 対応版が無い)
PACKAGER="$REPO_ROOT/tools/release/package-win64.ps1"
PWSH="${PWSH_BIN:-pwsh}"
VERSION_BASE="${FLASHBEAT_VERSION_BASE:-0.1.0}"
# 既定は stable。展示・配布候補は stable チャンネルで作る
# (docs/operations/update-release.md)。prerelease を取り込みたい検証ビルドだけが
# FLASHBEAT_UPDATE_CHANNEL=preview を明示する。BuildScript 側の既定も stable であり、
# この wrapper が preview を既定にすると、Release 候補が知らずに preview 扱いになる。
UPDATE_CHANNEL="${FLASHBEAT_UPDATE_CHANNEL:-stable}"

[ -x "$UNITY" ] || { echo "Unity not found: $UNITY" >&2; exit 2; }
[ -d "/Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/WindowsStandaloneSupport" ] || {
  echo "WindowsStandaloneSupport module is missing (Unity Hub から Windows Build Support を追加)" >&2; exit 2; }
command -v "$PWSH" >/dev/null 2>&1 || {
  echo "PowerShell 7 (pwsh) が見つかりません: $PWSH (PWSH_BIN で指定できます)" >&2; exit 2; }
[ -f "$PACKAGER" ] || {
  echo "パッケージスクリプトが見つかりません: $PACKAGER" >&2; exit 2; }

# リモートの追跡 ref を最新にしておく (build する対象が古いと危ない)。
# ネットワークが使えない環境でもローカルの参照で続行できるようにする。
git -C "$REPO_ROOT" fetch -q origin || \
  echo "警告: git fetch に失敗しました (ローカルの参照で続行します)" >&2

# 参照を commit に解決する。
#   1) ローカルのブランチ / タグ / SHA
#   2) リモート追跡ブランチ (origin/<ref>)
# 解決した SHA を detached で checkout することで、
# そのブランチが別の worktree で checkout 済みのときでも失敗しない
# (`git checkout --detach <branch>` は `-b` 相当の DWIM と衝突して失敗する)。
resolve_ref() {
  local ref="$1" sha=""
  if sha="$(git -C "$REPO_ROOT" rev-parse --verify "${ref}^{commit}" 2>/dev/null)"; then
    printf '%s\n' "$sha"; return 0
  fi
  if sha="$(git -C "$REPO_ROOT" rev-parse --verify "origin/${ref}^{commit}" 2>/dev/null)"; then
    printf '%s\n' "$sha"; return 0
  fi
  return 1
}
if ! RESOLVED_REF="$(resolve_ref "$REF")"; then
  echo "ビルド対象の参照を解決できません: $REF (ローカルにも origin にも無い)" >&2
  exit 2
fi
if [ ! -d "$WORKTREE" ]; then
  git -C "$REPO_ROOT" worktree add -q --detach "$WORKTREE" "$RESOLVED_REF"
else
  git -C "$WORKTREE" checkout -q --detach "$RESOLVED_REF"
fi
( cd "$WORKTREE" && git lfs pull >/dev/null )

# 既存出力の上にビルドすると level1 が壊れるので退避する。
if [ -d "$WORKTREE/Builds/Windows" ]; then
  mv "$WORKTREE/Builds/Windows" "$WORKTREE/Builds/Windows.old.$(date +%s)"
fi

FLASHBEAT_VERSION_BASE="$VERSION_BASE" FLASHBEAT_UPDATE_CHANNEL="$UPDATE_CHANNEL" \
  "$UNITY" -batchmode -quit -projectPath "$WORKTREE" \
  -executeMethod BuildScript.BuildStandaloneWindows64 \
  -logFile "$WORKTREE/win64-build.log"
[ -f "$WORKTREE/Builds/Windows/FlashBeat.exe" ] || {
  echo "build failed, see $WORKTREE/win64-build.log" >&2; exit 1; }

mkdir -p "$OUT_DIR"
SHA="$(git -C "$WORKTREE" rev-parse --short HEAD)"
FULL_SHA="$(git -C "$WORKTREE" rev-parse HEAD)"
VERSION="${VERSION_BASE}-win64-${SHA}"
ZIP="$OUT_DIR/FlashBeat-${VERSION}.zip"

# media policy: ビルドが除外マニフェストを残した (= 第三者楽曲を除外した) ときだけ song-free。
if [ -f "$WORKTREE/Builds/Windows/flashbeat-media-policy.json" ]; then
  MEDIA_POLICY="song-free"
else
  MEDIA_POLICY="bundled"
  echo "警告: flashbeat-media-policy.json がありません (mediaPolicy=bundled で処理します)" >&2
fi

# 正規スクリプトは既存の ZIP を上書きしない (release 手順の誤 publish を避けるため)。
if [ -e "$ZIP" ]; then
  echo "既存の成果物を置き換えます: $ZIP"
  rm -f "$ZIP" "$ZIP.sha256"
fi

"$PWSH" -NoProfile -ExecutionPolicy Bypass -File "$PACKAGER" \
  -SourceDir "$WORKTREE/Builds/Windows" \
  -OutDir "$OUT_DIR" \
  -Version "$VERSION" \
  -CommitSha "$FULL_SHA" \
  -MediaPolicy "$MEDIA_POLICY"

echo "dist: $OUT_DIR"
ls -la "$OUT_DIR"
