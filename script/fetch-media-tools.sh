#!/bin/bash
# Fetch the media-acquisition tools that ship inside the release ZIPs (issue #795).
#
# The Unity player resolves yt-dlp / deno / ffmpeg / ffprobe through
# YouTubeStreamResolver. A user who only has the release ZIP has no yt-dlp,
# no deno and no ffmpeg on PATH, so every song failed with "the YouTube
# stream could not be resolved" (v0.2.2) and MediaNormalizer (issue #796)
# could not repair videos with a broken time_base. These binaries are
# therefore bundled under <build>/StreamingAssets/tools/ by
# tools/release/package-*. The downloaded files live in tools/media/
# (gitignored) and are pinned by version + SHA-256 in tools/media-tools.lock.
#
# Layout produced (consumed by package-macos.sh / package-win64.ps1):
#   tools/media/macos/yt-dlp            universal Mach-O (x86_64 + arm64)
#   tools/media/macos-arm64/deno        Apple Silicon deno
#   tools/media/macos-x86_64/deno       Intel Mac deno
#   tools/media/macos-arm64/ffmpeg      Apple Silicon ffmpeg 9.0.2
#   tools/media/macos-arm64/ffprobe     Apple Silicon ffprobe 9.0.2
#   tools/media/macos-x86_64/ffmpeg     Intel Mac ffmpeg 9.0.2
#   tools/media/macos-x86_64/ffprobe    Intel Mac ffprobe 9.0.2
#   tools/media/windows-x64/yt-dlp.exe
#   tools/media/windows-x64/deno.exe
#   tools/media/windows-x64/ffmpeg.exe  ffmpeg 9.1 essentials (gyan.dev)
#   tools/media/windows-x64/ffprobe.exe ffprobe 9.1 essentials (gyan.dev)
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

# unzip a zip after verifying the publisher's checksum file AND the pinned
# archive SHA-256. specs is a whitespace-separated list of
# "member;dest;sha256" entries (member paths may contain directories; an
# empty sha256 falls back to the legacy "dest exists and is > 1 MiB" check).
# One archive may carry several tools (gyan essentials holds ffmpeg.exe,
# ffprobe.exe and ffplay.exe), so this fetches the zip once and extracts
# every listed member.
fetch_zip_members() {
    local name="$1" zipurl="$2" sumurl="$3" expected_zip_sha="$4" specs="$5"
    local tmp
    tmp="$(mktemp -d "${TMPDIR:-/tmp}/fb-tools.XXXXXX")"

    local all_ok=1 spec member dest sha
    for spec in $specs; do
        IFS=';' read -r member dest sha <<<"$spec"
        if [ ! -f "$dest" ]; then all_ok=0; break; fi
        if [ -n "$sha" ]; then
            [ "$(shasum -a 256 "$dest" | awk '{print $1}')" = "$sha" ] || { all_ok=0; break; }
        elif [ "$(stat -f%z "$dest" 2>/dev/null || stat -c%s "$dest")" -le 1048576 ]; then
            all_ok=0; break
        fi
    done
    if [ "$all_ok" = "1" ]; then
        echo "[ok] $name already present"
        rm -rf "$tmp"
        return 0
    fi
    if [ "$CHECK_ONLY" = "1" ]; then
        echo "[missing] $name"
        rm -rf "$tmp"
        return 1
    fi

    echo "[fetch] $name <- $zipurl"
    curl -fL --retry 3 -o "$tmp/a.zip" "$zipurl"
    local got
    got="$(shasum -a 256 "$tmp/a.zip" | awk '{print $1}')"
    if [ -n "$expected_zip_sha" ] && [ "$got" != "$expected_zip_sha" ]; then
        echo "error: sha256 mismatch for $name archive: got $got want $expected_zip_sha" >&2
        exit 1
    fi
    if [ -n "$sumurl" ]; then
        curl -fL --retry 3 -o "$tmp/a.zip.sha256" "$sumurl"
        # .sha256sum は "<hash>  <filename>" 形式と PowerShell Get-FileHash 形式
        # ("Hash      : <HASH>") の両方があるので 64hex をそのまま拾う。
        local want
        want="$(grep -oiE '[0-9a-f]{64}' "$tmp/a.zip.sha256" | head -1 | tr 'A-F' 'a-f')"
        if [ "$got" != "$want" ]; then
            echo "error: publisher sha256 mismatch for $name" >&2
            exit 1
        fi
    fi
    for spec in $specs; do
        IFS=';' read -r member dest sha <<<"$spec"
        unzip -o -q -j "$tmp/a.zip" "$member" -d "$tmp/out"
        mkdir -p "$(dirname "$dest")"
        mv "$tmp/out/$(basename "$member")" "$dest"
        chmod +x "$dest"
        if [ -n "$sha" ]; then
            [ "$(shasum -a 256 "$dest" | awk '{print $1}')" = "$sha" ] || {
                echo "error: extracted sha256 mismatch for $dest" >&2
                exit 1
            }
        fi
        rm -rf "$tmp/out"
    done
    rm -rf "$tmp"
}

# unzip a GitHub release asset after verifying the publisher's .sha256sum.
fetch_zip_member() {
    fetch_zip_members "$1" "$2" "$3" "" "$4;$5;${6:-}"
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

# ffmpeg / ffprobe (issue #796): MediaNormalizer は time_base が壊れた
# 動画を ffmpeg で再エンコードして直す。クリーンな環境 (ffmpeg 無し) では
# この修復が発動せず Who 等が ~1/6 速で再生されるため、リリース ZIP に
# 同梱する。macOS は martin-riedl.de の署名済み release 9.0.2 zip
# (arm64 / x86_64 別)、Windows は gyan.dev (GyanD/codexffmpeg) の
# ffmpeg 9.1 essentials_build。いずれもアーカイブ SHA-256 を lock で
# pin し、publisher の checksum ファイルとも照合する。
# Windows の zip は GitHub release asset から取り、.sha256 は gyan.dev
# の公開値と照合する (同一バイト列であることを sha256 で確認済み:
# ffmpeg-9.1-essentials_build.zip == ffmpeg-release-essentials.zip)。
# ffplay.exe は同梱しない。
MR="https://ffmpeg.martin-riedl.de/download/macos"
fetch_zip_members "ffmpeg macos arm64" \
    "$MR/arm64/1789931890_9.0.2/ffmpeg.zip" \
    "$MR/arm64/1789931890_9.0.2/ffmpeg.zip.sha256" \
    "c8ed4c4e6978a03c485edbfe4e0a5dc2380f8a30bba5150531b31b094492d924" \
    "ffmpeg;$MEDIA_DIR/macos-arm64/ffmpeg;2e11c6f90993cdb79fff84d3f90044d28316b310e75b3e030cfc9a54f2c9d384"
fetch_zip_members "ffprobe macos arm64" \
    "$MR/arm64/1789931890_9.0.2/ffprobe.zip" \
    "$MR/arm64/1789931890_9.0.2/ffprobe.zip.sha256" \
    "fcbe839537485eaee7a7a8bc5cbc0f90d53617e80943e8a5b2e31cb851197ea6" \
    "ffprobe;$MEDIA_DIR/macos-arm64/ffprobe;2738aa46a7f9acbc8ab09a6715554c90f7de603171ac403726765685df6a0059"
fetch_zip_members "ffmpeg macos x86_64" \
    "$MR/amd64/1789931006_9.0.2/ffmpeg.zip" \
    "$MR/amd64/1789931006_9.0.2/ffmpeg.zip.sha256" \
    "7c6b4125b191cbf773832dc51f424cf2b6bb7da43007d1e066f95909e47cacd4" \
    "ffmpeg;$MEDIA_DIR/macos-x86_64/ffmpeg;b25689cf2211c0582d317e849912769a1f1c93904fe102c485b5364a425633d1"
fetch_zip_members "ffprobe macos x86_64" \
    "$MR/amd64/1789931006_9.0.2/ffprobe.zip" \
    "$MR/amd64/1789931006_9.0.2/ffprobe.zip.sha256" \
    "2322438ed2f6319a691291b247d09c69dcaa3a982460d1f269a7e1af335cfdfd" \
    "ffprobe;$MEDIA_DIR/macos-x86_64/ffprobe;b5df9964b0c2048318fafedcd4e3bbafbc561033cee77de8da1840685d8a9d0d"
GY="https://github.com/GyanD/codexffmpeg/releases/download/9.1/ffmpeg-9.1-essentials_build.zip"
fetch_zip_members "ffmpeg windows x64" \
    "$GY" \
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256" \
    "b9bbbb3acece000cbeec994f856649354e52c773807841e78908374b24d658fd" \
    "ffmpeg-9.1-essentials_build/bin/ffmpeg.exe;$MEDIA_DIR/windows-x64/ffmpeg.exe;ed8f66cb877087a96f5bbb40a2944f0e764f2ad8870d5cb647360d912e62bc07 ffmpeg-9.1-essentials_build/bin/ffprobe.exe;$MEDIA_DIR/windows-x64/ffprobe.exe;df0199f606faa88e9ad192732a7c3987acfb647663534c297772e17730611a08"

echo "[done] media tools under $MEDIA_DIR"
"$MEDIA_DIR/macos/yt-dlp" --version
"$MEDIA_DIR/macos-arm64/deno" --version
# ffmpeg / ffprobe は実行中マシンの arch のものだけ起動確認する
# (他 arch のバイナリは Rosetta 無し環境で失敗し得るため)。
if [ "$(uname -s)" = "Darwin" ]; then
    case "$(uname -m)" in
        arm64)  FFARCH=macos-arm64 ;;
        *)      FFARCH=macos-x86_64 ;;
    esac
    "$MEDIA_DIR/$FFARCH/ffmpeg" -version | head -1
    "$MEDIA_DIR/$FFARCH/ffprobe" -version | head -1
fi
