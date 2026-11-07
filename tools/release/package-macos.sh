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
#     --media-policy song-free

set -euo pipefail

SOURCE_DIR=""
OUT_DIR=""
VERSION=""
COMMIT_SHA=""
MEDIA_POLICY="song-free"
MAX_RESOURCES_BYTES=$((300 * 1024 * 1024))

usage() {
    cat <<'EOF'
Usage: package-macos.sh --source <FlashBeat.app> --out <dir> --version <semver-macos-sha> --commit-sha <40 hex> [--media-policy bundled|free|song-free]

  --source        Builds/macOS/FlashBeat.app (or its parent folder)
  --out           Output directory for the ZIP and .sha256
  --version       <semver>-macos-<shortsha> (GitHub Release tag = v<version>)
  --commit-sha    Full 40-character commit SHA of the build source
  --media-policy  bundled (default), free, or song-free
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --source) SOURCE_DIR="${2:-}"; shift 2 ;;
        --out) OUT_DIR="${2:-}"; shift 2 ;;
        --version) VERSION="${2:-}"; shift 2 ;;
        --commit-sha) COMMIT_SHA="${2:-}"; shift 2 ;;
        --media-policy) MEDIA_POLICY="${2:-}"; shift 2 ;;
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
  "createdAt": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
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
