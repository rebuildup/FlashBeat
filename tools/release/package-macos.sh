#!/bin/bash
# Package a macOS Unity build (FlashBeat.app) as a versioned ZIP + SHA-256 sidecar.
#
# Contract (same as tools/release/package-win64.ps1):
#   - Release tag:     v<semver>-macos-<shortsha>
#   - ZIP asset:       FlashBeat-<semver>-macos-<shortsha>.zip
#   - ZIP root:        FlashBeat.app/ + version.json
#   - Sidecar:         <zip>.sha256, "<64 hex>  <filename>\n"
#   - No loose audio/video media (third-party songs are excluded at build time).
#
# Example:
#   tools/release/package-macos.sh \
#     --source Builds/macOS/FlashBeat.app \
#     --out dist \
#     --version 0.1.14-macos-abc1234 \
#     --commit-sha abc1234... \
#     --media-policy song-free \
#     [--commit-date 2026-11-18T09:26:15+09:00]
#
# version.json の createdAt は package-win64.ps1 と同じく「ビルド元 commit の
# 日時」に pin する (既定は git show -s --format=%cI、--commit-date で明示可)。
# 実行時刻を使うと同一 SHA から ZIP が再現しない (issue #651)。

set -euo pipefail

SOURCE_DIR=""
OUT_DIR=""
VERSION=""
COMMIT_SHA=""
MEDIA_POLICY="song-free"
COMMIT_DATE=""
MAX_RESOURCES_BYTES=$((300 * 1024 * 1024))

usage() {
    cat <<'EOF'
Usage: package-macos.sh --source <FlashBeat.app> --out <dir> --version <semver-macos-sha> --commit-sha <40 hex> [--media-policy bundled|free|song-free]

  --source        Builds/macOS/FlashBeat.app (or its parent folder)
  --out           Output directory for the ZIP and .sha256
  --version       <semver>-macos-<shortsha> (GitHub Release tag = v<version>)
  --commit-sha    Full 40-character commit SHA of the build source
  --media-policy  bundled (default), free, or song-free
  --commit-date   ISO 8601 commit date for version.json createdAt
                  (default: resolved from --commit-sha via git)
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --source) SOURCE_DIR="${2:-}"; shift 2 ;;
        --out) OUT_DIR="${2:-}"; shift 2 ;;
        --version) VERSION="${2:-}"; shift 2 ;;
        --commit-sha) COMMIT_SHA="${2:-}"; shift 2 ;;
        --media-policy) MEDIA_POLICY="${2:-}"; shift 2 ;;
        --commit-date) COMMIT_DATE="${2:-}"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage; echo "error: unknown argument: $1" >&2; exit 1 ;;
    esac
done

fail() {
    echo "error: $*" >&2
    exit 1
}

[ -n "$SOURCE_DIR" ] || { usage; fail "--source is required"; }
[ -n "$OUT_DIR" ] || { usage; fail "--out is required"; }
[ -n "$VERSION" ] || { usage; fail "--version is required"; }
[ -n "$COMMIT_SHA" ] || { usage; fail "--commit-sha is required"; }

case "$MEDIA_POLICY" in
    bundled|free|song-free) ;;
    *) fail "--media-policy must be bundled, free, or song-free" ;;
esac

if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+-macos-[0-9a-f]{7,40}$ ]]; then
    fail "--version must match <semver>-macos-<shortsha>: $VERSION"
fi
if [[ ! "$COMMIT_SHA" =~ ^[0-9a-f]{40}$ ]]; then
    fail "--commit-sha must be a full 40-character lowercase hex SHA: $COMMIT_SHA"
fi

short_sha="${VERSION#*-macos-}"
case "$COMMIT_SHA" in
    "$short_sha"*) ;;
    *) fail "--version SHA ($short_sha) is not a prefix of --commit-sha ($COMMIT_SHA)" ;;
esac

# version.json の createdAt は「パッケージ実行時刻」ではなく「ビルド元 commit の
# 日時」にする (package-win64.ps1 -CommitDate と同じ再現性契約、issue #651)。
# 既定は git show で commit の committer date (%cI, ISO 8601) を引き、
# --commit-date で明示もできる。UTC の "...Z" 表記へ揃える。
if [ -z "$COMMIT_DATE" ]; then
    repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
    COMMIT_DATE="$(git -C "$repo_root" show -s --format=%cI "$COMMIT_SHA" 2>/dev/null || true)"
fi
[ -n "$COMMIT_DATE" ] || fail "commit date for $COMMIT_SHA could not be resolved; pass --commit-date (ISO 8601)"
# %cI は "2026-11-18T09:26:15+09:00" 形式。BSD date の %z はコロン無し offset
# を要求するので、offset のコロンを除いてから UTC へ変換する。
commit_date_norm="$(printf '%s' "$COMMIT_DATE" | sed -E 's/Z$/+0000/; s/([+-][0-9]{2}):([0-9]{2})$/\1\2/')"
CREATED_AT="$(date -u -j -f "%Y-%m-%dT%H:%M:%S%z" "$commit_date_norm" +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || true)"
[ -n "$CREATED_AT" ] || fail "--commit-date is not parseable ISO 8601: $COMMIT_DATE"

if [ -d "$SOURCE_DIR/FlashBeat.app" ]; then
    APP_PATH="$SOURCE_DIR/FlashBeat.app"
elif [ -d "$SOURCE_DIR" ]; then
    APP_PATH="$SOURCE_DIR"
else
    fail "source not found: $SOURCE_DIR"
fi
APP_PATH="${APP_PATH%/}"

[ -f "$APP_PATH/Contents/MacOS/FlashBeat" ] || fail "FlashBeat executable is missing: $APP_PATH/Contents/MacOS/FlashBeat"
[ -d "$APP_PATH/Contents/Resources/Data" ] || fail "Contents/Resources/Data is missing: $APP_PATH"
[ -f "$APP_PATH/Contents/Info.plist" ] || fail "Contents/Info.plist is missing: $APP_PATH"

if [ ! -x "$APP_PATH/Contents/MacOS/FlashBeat" ]; then
    fail "FlashBeat executable is not executable: $APP_PATH/Contents/MacOS/FlashBeat"
fi

RESOURCES_DIR="$APP_PATH/Contents/Resources"
DATA_DIR="$RESOURCES_DIR/Data"

# ── media policy ────────────────────────────────────────────────────────────
loose_media="$(find "$APP_PATH" -type f \( \
    -iname '*.wav' -o -iname '*.mp3' -o -iname '*.mp4' -o -iname '*.mov' \
    -o -iname '*.ogg' -o -iname '*.m4a' -o -iname '*.webm' -o -iname '*.avi' \
    \) -print -quit 2>/dev/null || true)"
if [ -n "$loose_media" ]; then
    fail "third-party media must not be shipped as loose files: ${loose_media#"$APP_PATH"/}"
fi

if [ "$MEDIA_POLICY" = "song-free" ]; then
    manifest=""
    for candidate in "$RESOURCES_DIR/flashbeat-media-policy.json" "$APP_PATH/flashbeat-media-policy.json"; do
        if [ -f "$candidate" ]; then manifest="$candidate"; break; fi
    done
    [ -n "$manifest" ] || fail "song-free requires flashbeat-media-policy.json (build it with BuildScript)"
    for required in "Assets/Game/Resources/Musics" "Assets/Game/Resources/Videos/UnityYoutube"; do
        grep -q -F "\"$required\"" "$manifest" || fail "manifest does not exclude $required: $manifest"
    done
    echo "[media] third-party song media excluded (manifest: $manifest)"
fi

packed_size=0
if [ -f "$DATA_DIR/resources.resource" ]; then
    packed_size="$(stat -f%z "$DATA_DIR/resources.resource")"
else
    # macOS Unity builds split Resources into resources.assets + resources.assets.resS.
    packed_size="$(find "$DATA_DIR" -maxdepth 1 -type f -name 'resources.*' -exec stat -f%z {} + 2>/dev/null | awk '{s += $1} END {print s + 0}')"
fi
if [ "$packed_size" -gt 0 ] && [ "$MEDIA_POLICY" != "bundled" ]; then
    if [ "$packed_size" -gt "$MAX_RESOURCES_BYTES" ]; then
        fail "packed Resources data is $packed_size bytes (limit $MAX_RESOURCES_BYTES): third-party media may still be packed"
    fi
fi

# ── bundled media tools (issue #795 / #796) ───────────────────────────────
# yt-dlp + deno + ffmpeg + ffprobe を StreamingAssets/tools/ へ同梱する。
# 配布 ZIP だけを入手した利用者の環境には yt-dlp / deno / ffmpeg が存在せず、
# YouTube からの楽曲取得が全曲失敗していた (v0.2.2 実機受入不合格)。さらに
# ffmpeg / ffprobe が無いと MediaNormalizer (issue #796) が time_base の
# 壊れた動画を修復できず、Who 等が ~1/6 速で再生される。バイナリは
# script/fetch-media-tools.sh が tools/media/ (gitignore) へ
# version + SHA-256 pin で取得する。
# macOS ビルドは universal なので arm64 / x86_64 両方の deno / ffmpeg /
# ffprobe を入れ、実行時に OSArchitecture で選ぶ。
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_SRC="$REPO_ROOT/tools/media"
for required in \
    "macos/yt-dlp" \
    "macos-arm64/deno" "macos-arm64/ffmpeg" "macos-arm64/ffprobe" \
    "macos-x86_64/deno" "macos-x86_64/ffmpeg" "macos-x86_64/ffprobe"; do
    [ -f "$TOOLS_SRC/$required" ] || fail "media tool missing: $TOOLS_SRC/$required (run script/fetch-media-tools.sh)"
done
TOOLS_DST="$DATA_DIR/StreamingAssets/tools"
mkdir -p "$TOOLS_DST/macos" "$TOOLS_DST/macos-arm64" "$TOOLS_DST/macos-x86_64"
cp "$TOOLS_SRC/macos/yt-dlp" "$TOOLS_DST/macos/yt-dlp"
for arch in macos-arm64 macos-x86_64; do
    cp "$TOOLS_SRC/$arch/deno"    "$TOOLS_DST/$arch/deno"
    cp "$TOOLS_SRC/$arch/ffmpeg"  "$TOOLS_DST/$arch/ffmpeg"
    cp "$TOOLS_SRC/$arch/ffprobe" "$TOOLS_DST/$arch/ffprobe"
done
chmod +x "$TOOLS_DST/macos/yt-dlp" \
    "$TOOLS_DST/macos-arm64/deno" "$TOOLS_DST/macos-arm64/ffmpeg" "$TOOLS_DST/macos-arm64/ffprobe" \
    "$TOOLS_DST/macos-x86_64/deno" "$TOOLS_DST/macos-x86_64/ffmpeg" "$TOOLS_DST/macos-x86_64/ffprobe"
# sanity probe: このビルド機の arch に対応する deno / ffmpeg / ffprobe だけ
# 実際に起動する (他 arch のバイナリは Rosetta 無し環境で --version に
# 失敗し得るため)。
"$TOOLS_DST/macos/yt-dlp" --version >/dev/null || fail "bundled yt-dlp failed --version"
case "$(uname -m)" in
    arm64)  FFARCH=macos-arm64 ;;
    *)      FFARCH=macos-x86_64 ;;
esac
"$TOOLS_DST/$FFARCH/deno" --version >/dev/null || fail "bundled deno ($FFARCH) failed --version"
"$TOOLS_DST/$FFARCH/ffmpeg" -version >/dev/null || fail "bundled ffmpeg ($FFARCH) failed -version"
"$TOOLS_DST/$FFARCH/ffprobe" -version >/dev/null || fail "bundled ffprobe ($FFARCH) failed -version"

# quarantine された .app 内で child process として起動されるため、利用者が
# ZIP をブラウザから落とすと Gatekeeper の評価対象になる。yt-dlp_macos は
# adhoc 署名なので、そのままでは quarantine 下で exec が殺され得る。
# Developer ID Application 署名があればツールへ付与する (deno は既に署名済み
# だが統一のため再署名する)。署名 identity が無い環境では警告に留める
# (署名なし配布は従来どおり利用者の Gatekeeper 回避操作に依存する)。
SIGN_IDENTITY="$(security find-identity -v -p codesigning 2>/dev/null | grep 'Developer ID Application' | head -1 | sed -E 's/.*"(.*)"/\1/')"
if [ -n "$SIGN_IDENTITY" ]; then
    for tool in \
        "$TOOLS_DST/macos/yt-dlp" \
        "$TOOLS_DST/macos-arm64/deno" "$TOOLS_DST/macos-arm64/ffmpeg" "$TOOLS_DST/macos-arm64/ffprobe" \
        "$TOOLS_DST/macos-x86_64/deno" "$TOOLS_DST/macos-x86_64/ffmpeg" "$TOOLS_DST/macos-x86_64/ffprobe"; do
        codesign --force --sign "$SIGN_IDENTITY" --timestamp "$tool" || fail "codesign failed: $tool"
    done
    echo "[media-tools] codesigned bundled tools with '$SIGN_IDENTITY'"
else
    echo "[media-tools] WARNING: no Developer ID Application identity; bundled tools stay unsigned" >&2
fi
echo "[media-tools] bundled yt-dlp + deno + ffmpeg + ffprobe (macos arm64/x86_64) into StreamingAssets/tools"

# ── output ──────────────────────────────────────────────────────────────────
mkdir -p "$OUT_DIR"
OUT_DIR="$(cd "$OUT_DIR" && pwd)"
ZIP_NAME="FlashBeat-$VERSION.zip"
ZIP_PATH="$OUT_DIR/$ZIP_NAME"
if [ -e "$ZIP_PATH" ]; then
    fail "output ZIP already exists: $ZIP_PATH"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/flashbeat-macos-package.XXXXXX")"
trap 'rm -rf "$work_dir"' EXIT

version_json="$work_dir/version.json"
cat >"$version_json" <<EOF
{
  "version": "$(printf '%s' "$VERSION" | sed 's/-macos-.*//')",
  "buildSha": "$COMMIT_SHA",
  "tag": "v$VERSION",
  "platform": "macos",
  "mediaPolicy": "$MEDIA_POLICY",
  "createdAt": "$CREATED_AT"
}
EOF

echo "[1/4] creating ZIP: $ZIP_PATH"
# ditto preserves symlinks, executable bits, and bundle metadata. --keepParent keeps
# the top-level FlashBeat.app entry so the ZIP extracts to FlashBeat.app/.
ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ZIP_PATH"
# Add version.json at the ZIP root (next to FlashBeat.app).
(cd "$work_dir" && zip -q -g "$ZIP_PATH" version.json)

echo "[2/4] inspecting ZIP"
# Read the listing once: piping into grep -q would SIGPIPE unzip and fail under pipefail.
zip_listing="$(unzip -Z1 "$ZIP_PATH")"
entries="$(printf '%s\n' "$zip_listing" | wc -l | tr -d ' ')"
[ "$entries" -gt 0 ] || fail "ZIP is empty"
grep -q '^FlashBeat\.app/Contents/MacOS/FlashBeat$' <<<"$zip_listing" || fail "ZIP does not contain FlashBeat.app/Contents/MacOS/FlashBeat"
grep -q '^FlashBeat\.app/Contents/Resources/Data/' <<<"$zip_listing" || fail "ZIP does not contain FlashBeat.app/Contents/Resources/Data"
grep -q '^version\.json$' <<<"$zip_listing" || fail "ZIP does not contain version.json"

echo "[3/4] writing SHA-256 sidecar"
(
    cd "$OUT_DIR"
    shasum -a 256 "$ZIP_NAME" | awk '{print $1 "  " $2}' >"$ZIP_NAME.sha256"
)
hash="$(awk '{print $1}' "$ZIP_PATH.sha256")"

size="$(stat -f%z "$ZIP_PATH")"
echo "[4/4] done: entries=$entries size=$size bytes"
echo "SHA-256: $hash"
echo "Sidecar: $ZIP_PATH.sha256"
echo "Tag (GitHub Release): v$VERSION"
