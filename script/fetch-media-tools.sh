#!/bin/bash
# Fetch the media-acquisition tools that ship inside the release ZIPs (issue #795).
#
# The Unity player resolves yt-dlp / deno through YouTubeStreamResolver. A user
# who only has the release ZIP has no yt-dlp, no deno and no ffmpeg on PATH, so
# every song failed with "the YouTube stream could not be resolved" (v0.2.2).
# These binaries are therefore bundled under <build>/StreamingAssets/tools/
# by tools/release/package-*. The downloaded files live in tools/media/
# (gitignored) and are pinned by version + SHA-256 in tools/media-tools.lock.
#
# Layout produced (consumed by package-macos.sh / package-win64.ps1):
#   tools/media/macos/yt-dlp            universal Mach-O (x86_64 + arm64)
#   tools/media/macos-arm64/deno        Apple Silicon deno
#   tools/media/macos-x86_64/deno       Intel Mac deno
#   tools/media/windows-x64/yt-dlp.exe
#   tools/media/windows-x64/deno.exe
#
# ffmpeg is intentionally NOT bundled: SelectPlan prefers the combined
# progressive mp4 (itag 18) which needs no merge, and a system ffmpeg is still
# picked up when present for the HD merged path.
#
# Usage: script/fetch-media-tools.sh [--check]
#   --check  verify existing files against the lock without downloading.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MEDIA_DIR="$ROOT/tools/media"
LOCK_FILE="$ROOT/tools/media-tools.lock"
CHECK_ONLY=0
[ "${1:-}" = "--check" ] && CHECK_ONLY=1

YTDLP_VERSION="2026.10.26"
DENO_VERSION="v2.9.8"

# name|url|dest|sha256 (sha256 of the downloaded payload; zip entries are
# verified against the publisher's .sha256sum before extraction)
fetch() {
    local name="$1" url="$2" dest="$3" expected="$4"
    mkdir -p "$(dirname "$dest")"
    if [ -f "$dest" ]; then
        local actual
        actual="$(shasum -a 256 "$dest" | awk '{print $1}')"
        if [ "$actual" = "$expected" ]; then
            echo "[ok] $name already present"
            return 0
        fi
        echo "[fetch] $name: hash mismatch, re-downloading"
    fi
    [ "$CHECK_ONLY" = "1" ] && { echo "[missing] $name"; return 1; }
    echo "[fetch] $name <- $url"
    curl -fL --retry 3 -o "$dest.part" "$url"
    local actual
    actual="$(shasum -a 256 "$dest.part" | awk '{print $1}')"
    if [ "$actual" != "$expected" ]; then
        rm -f "$dest.part"
        echo "error: sha256 mismatch for $name: got $actual want $expected" >&2
        exit 1
    fi
    mv "$dest.part" "$dest"
    chmod +x "$dest" 2>/dev/null || true
}

# unzip a GitHub release asset after verifying the publisher's .sha256sum.
fetch_zip_member() {
    local name="$1" zipurl="$2" sumurl="$3" member="$4" dest="$5" expected_dest_sha="$6"
    local tmp
    tmp="$(mktemp -d "${TMPDIR:-/tmp}/fb-tools.XXXXXX")"
    trap 'rm -rf "$tmp"' RETURN
    if [ -f "$dest" ] && [ "$(stat -f%z "$dest" 2>/dev/null || stat -c%s "$dest")" -gt 1048576 ]; then
        echo "[ok] $name already present"
        return 0
    fi
    if [ "$CHECK_ONLY" = "1" ]; then
        [ -f "$dest" ] || { echo "[missing] $name"; return 1; }
        return 0
    fi
    echo "[fetch] $name <- $zipurl"
    curl -fL --retry 3 -o "$tmp/a.zip" "$zipurl"
    curl -fL --retry 3 -o "$tmp/a.zip.sha256" "$sumurl"
    # .sha256sum は "<hash>  <filename>" 形式と PowerShell Get-FileHash 形式
    # ("Hash      : <HASH>") の両方があるので 64hex をそのまま拾う。
    local want
    want="$(grep -oiE '[0-9a-f]{64}' "$tmp/a.zip.sha256" | head -1 | tr 'A-F' 'a-f')"
    local got
    got="$(shasum -a 256 "$tmp/a.zip" | awk '{print $1}')"
    if [ "$got" != "$want" ]; then
        echo "error: publisher sha256 mismatch for $name" >&2
        exit 1
    fi
    unzip -o -q -j "$tmp/a.zip" "$member" -d "$tmp/out"
    mkdir -p "$(dirname "$dest")"
    mv "$tmp/out/$member" "$dest"
    chmod +x "$dest"
}

M="https://github.com/yt-dlp/yt-dlp/releases/download/$YTDLP_VERSION"
D="https://github.com/denoland/deno/releases/download/$DENO_VERSION"

# yt-dlp binaries (sha256 recorded in tools/media-tools.lock)
fetch "yt-dlp macos"        "$M/yt-dlp_macos"                    "$MEDIA_DIR/macos/yt-dlp"        "0f922c15ccc0b83eefa1c8bc9104e434318c4bf16e408e1ee93b7745a6efe0aa"
fetch "yt-dlp windows x64"  "$M/yt-dlp.exe"                      "$MEDIA_DIR/windows-x64/yt-dlp.exe" "f5389d3698ecde4ef06f4e6b1ce0294fe9ac1396228c8990b8d3416d6d177a7d"

# deno (publisher .sha256sum verified inside fetch_zip_member)
fetch_zip_member "deno macos arm64"  "$D/deno-aarch64-apple-darwin.zip"        "$D/deno-aarch64-apple-darwin.zip.sha256sum"        "deno"     "$MEDIA_DIR/macos-arm64/deno"    ""
fetch_zip_member "deno macos x86_64" "$D/deno-x86_64-apple-darwin.zip"         "$D/deno-x86_64-apple-darwin.zip.sha256sum"         "deno"     "$MEDIA_DIR/macos-x86_64/deno"   ""
fetch_zip_member "deno windows x64"  "$D/deno-x86_64-pc-windows-msvc.zip"     "$D/deno-x86_64-pc-windows-msvc.zip.sha256sum"     "deno.exe" "$MEDIA_DIR/windows-x64/deno.exe" ""

echo "[done] media tools under $MEDIA_DIR"
"$MEDIA_DIR/macos/yt-dlp" --version
"$MEDIA_DIR/macos-arm64/deno" --version
