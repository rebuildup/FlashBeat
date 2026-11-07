#!/bin/bash
# FlashBeat macOS updater (self-update / manual apply).
#
# The app's UpdateChecker launches this script with -Apply -WaitPid -Relaunch after the
# user explicitly confirms an update. It can also be run manually by an operator.
#
# What it does:
#   1. waits for the running FlashBeat process (the app that launched it) to exit
#   2. obtains the release ZIP (local path or GitHub Release tag) and verifies SHA-256
#   3. extracts to a temporary directory on the same volume as the install
#   4. replaces FlashBeat.app, keeping one backup generation
#   5. relaunches the new app (only with -Relaunch)
#
# Exit codes match tools/update/Update.ps1:
#   0 success / 1 parameters or precondition / 2 network / 3 SHA-256 /
#   4 ZIP / 5 content / 6 already running / 7 switch
#
# The result is always written to last-result.json for the next app start
# (issue #125), even when the update fails.

set -u

EXIT_OK=0
EXIT_PARAM=1
EXIT_NETWORK=2
EXIT_SHA=3
EXIT_ZIP=4
EXIT_CONTENT=5
EXIT_RUNNING=6
EXIT_SWITCH=7

INSTALL_DIR=""
ZIP_PATH=""
EXPECTED_SHA=""
TAG=""
REPO="rebuildup/FlashBeat"
ASSET_NAME=""
RESULT_DIR=""
LOG_PATH=""
WAIT_PID=""
APPLY=0
RELAUNCH=0

usage() {
    cat <<'EOF'
Usage: update-macos.sh -InstallDir <FlashBeat.app> [options]

  -InstallDir <path>       FlashBeat.app to update (required)
  -ZipPath <path>          Local update ZIP
  -Tag <tag>               GitHub Release tag (e.g. v0.1.14-macos-abc1234)
  -AssetName <name>        ZIP asset name for -Tag (default: FlashBeat-<tag>.zip)
  -ExpectedSha256 <hex>    Expected ZIP SHA-256 (default: read the .sha256 sidecar)
  -Repo <owner/repo>       GitHub repository for -Tag (default: rebuildup/FlashBeat)
  -ResultDir <path>        Where to write last-result.json
                           (default: ~/Library/Application Support/FlashBeat/update)
  -LogPath <path>          Log file (default: <parent of InstallDir>/FlashBeat-update.log)
  -WaitPid <pid>           Wait for this PID to exit before switching (max 300 s)
  -Apply                   Assert this is a user-confirmed apply (app passes this)
  -Relaunch                Start the new FlashBeat.app after a successful switch
  -h, --help               Show this help
EOF
}

log() {
    local line
    line="[$(date -u +"%Y-%m-%dT%H:%M:%SZ")] $*"
    printf '%s\n' "$line"
    if [ -n "${LOG_PATH:-}" ]; then
        printf '%s\n' "$line" >>"$LOG_PATH" 2>/dev/null || true
    fi
}

json_escape() {
    printf '%s' "$1" | tr '\n\r\t' '   ' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

write_result() {
    local result="$1"
    local exit_code="$2"
    local message="$3"
    local to_version="${4:-}"
    [ -n "${RESULT_DIR:-}" ] || return 0
    mkdir -p "$RESULT_DIR" 2>/dev/null || return 0

    local result_file="$RESULT_DIR/last-result.json"
    local tmp_file="$result_file.tmp.$$"
    {
        printf '{\n'
        printf '  "result": "%s",\n' "$(json_escape "$result")"
        printf '  "exitCode": %s,\n' "$exit_code"
        printf '  "fromVersion": "%s",\n' "$(json_escape "${FROM_VERSION:-}")"
        printf '  "toVersion": "%s",\n' "$(json_escape "$to_version")"
        printf '  "tag": "%s",\n' "$(json_escape "${TAG:-}")"
        printf '  "message": "%s",\n' "$(json_escape "$message")"
        printf '  "timeUtc": "%s"\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
        printf '}\n'
    } >"$tmp_file" 2>/dev/null && mv "$tmp_file" "$result_file" 2>/dev/null
}

cleanup() {
    if [ -n "${WORK_DIR:-}" ] && [ -d "$WORK_DIR" ]; then
        rm -rf "$WORK_DIR"
    fi
}

fail() {
    local exit_code="$1"
    local message="$2"
    local to_version="${3:-}"
    log "ERROR: $message"
    write_result "failed" "$exit_code" "$message" "$to_version"
    cleanup
    exit "$exit_code"
}

abort() {
    local exit_code="$1"
    local message="$2"
    log "ABORT: $message"
    write_result "aborted" "$exit_code" "$message"
    cleanup
    exit "$exit_code"
}

# --- arguments -------------------------------------------------------------

while [ $# -gt 0 ]; do
    case "$1" in
        -InstallDir) INSTALL_DIR="${2:-}"; shift 2 ;;
        -ZipPath) ZIP_PATH="${2:-}"; shift 2 ;;
        -ExpectedSha256) EXPECTED_SHA="${2:-}"; shift 2 ;;
        -Tag) TAG="${2:-}"; shift 2 ;;
        -AssetName) ASSET_NAME="${2:-}"; shift 2 ;;
        -Repo) REPO="${2:-}"; shift 2 ;;
        -ResultDir) RESULT_DIR="${2:-}"; shift 2 ;;
        -LogPath) LOG_PATH="${2:-}"; shift 2 ;;
        -WaitPid) WAIT_PID="${2:-}"; shift 2 ;;
        -Apply) APPLY=1; shift ;;
        -Relaunch) RELAUNCH=1; shift ;;
        -h|--help) usage; exit "$EXIT_OK" ;;
        *) usage; exit "$EXIT_PARAM" ;;
    esac
done

if [ -z "$INSTALL_DIR" ]; then
    usage
    echo "error: -InstallDir is required" >&2
    exit "$EXIT_PARAM"
fi

# Accept either the .app itself or its parent folder.
if [ -d "$INSTALL_DIR/FlashBeat.app" ]; then
    INSTALL_DIR="$INSTALL_DIR/FlashBeat.app"
fi
INSTALL_DIR="${INSTALL_DIR%/}"
APP_PARENT="$(dirname "$INSTALL_DIR")"
if [ -z "$RESULT_DIR" ]; then
    RESULT_DIR="$HOME/Library/Application Support/FlashBeat/update"
fi
if [ -z "$LOG_PATH" ]; then
    LOG_PATH="$APP_PARENT/FlashBeat-update.log"
fi
mkdir -p "$(dirname "$LOG_PATH")" 2>/dev/null || true

if [ -d "$INSTALL_DIR" ] && [ -f "$INSTALL_DIR/Contents/Info.plist" ]; then
    FROM_VERSION="$(defaults read "$INSTALL_DIR/Contents/Info.plist" CFBundleShortVersionString 2>/dev/null || true)"
    FROM_VERSION="${FROM_VERSION%%+*}"
else
    FROM_VERSION=""
fi

log "start: install=$INSTALL_DIR zip=${ZIP_PATH:-${TAG:-<none>}} apply=$APPLY relaunch=$RELAUNCH"

if [ "$APPLY" -ne 1 ] && [ -z "${FLASHBEAT_UPDATE_ALLOW_MANUAL:-}" ] && [ -f "$INSTALL_DIR/Contents/MacOS/FlashBeat" ]; then
    # The in-app flow always passes -Apply. A manual operator can opt in explicitly.
    abort "$EXIT_PARAM" "アプリからの更新は -Apply 付きで実行してください (手動実行は FLASHBEAT_UPDATE_ALLOW_MANUAL=1 を設定)"
fi

if [ -z "$ZIP_PATH" ] && [ -z "$TAG" ]; then
    abort "$EXIT_PARAM" "-ZipPath または -Tag を指定してください"
fi

if [ ! -d "$APP_PARENT" ]; then
    abort "$EXIT_PARAM" "インストール先の親フォルダがありません: $APP_PARENT"
fi
if [ ! -w "$APP_PARENT" ]; then
    abort "$EXIT_PARAM" "インストール先の親フォルダに書き込み権限がありません: $APP_PARENT"
fi

# --- wait for the launching app / reject double starts ----------------------

if [ -n "$WAIT_PID" ]; then
    waited=0
    while kill -0 "$WAIT_PID" 2>/dev/null; do
        if [ "$waited" -ge 300 ]; then
            abort "$EXIT_RUNNING" "FlashBeat (PID $WAIT_PID) の終了を 300 秒待ちましたが終了しませんでした"
        fi
        sleep 1
        waited=$((waited + 1))
    done
    log "waited for PID $WAIT_PID ($waited s)"
fi

others="$(ps -axo pid=,command= | grep -F "$INSTALL_DIR/Contents/MacOS/FlashBeat" | awk '{print $1}' | grep -v -x -e "$$" -e "${WAIT_PID:-}" || true)"
if [ -n "$others" ]; then
    abort "$EXIT_RUNNING" "他の FlashBeat が実行中です (PID: $(echo "$others" | tr '\n' ' '))"
fi

# --- obtain the ZIP --------------------------------------------------------

WORK_DIR="$APP_PARENT/.flashbeat-update.$$"
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR" || abort "$EXIT_PARAM" "作業フォルダを作成できません: $WORK_DIR"
trap cleanup EXIT

if [ -n "$TAG" ] && [ -z "$ZIP_PATH" ]; then
    if [ -z "$ASSET_NAME" ]; then
        ASSET_NAME="FlashBeat-${TAG#v}.zip"
    fi
    ZIP_PATH="$WORK_DIR/$ASSET_NAME"
    url="https://github.com/$REPO/releases/download/$TAG/$ASSET_NAME"
    log "download: $url"
    if ! curl -fL --retry 3 --connect-timeout 15 -o "$ZIP_PATH" "$url"; then
        abort "$EXIT_NETWORK" "更新 ZIP をダウンロードできませんでした: $url"
    fi
    if [ -z "$EXPECTED_SHA" ]; then
        sidecar="$WORK_DIR/$ASSET_NAME.sha256"
        if curl -fL --retry 3 --connect-timeout 15 -o "$sidecar" "$url.sha256"; then
            EXPECTED_SHA="$(awk 'NR==1 {print $1}' "$sidecar")"
        fi
    fi
fi

if [ ! -f "$ZIP_PATH" ]; then
    abort "$EXIT_PARAM" "更新 ZIP が見つかりません: $ZIP_PATH"
fi

if [ -z "$EXPECTED_SHA" ] && [ -f "$ZIP_PATH.sha256" ]; then
    EXPECTED_SHA="$(awk 'NR==1 {print $1}' "$ZIP_PATH.sha256")"
fi
if [ -z "$EXPECTED_SHA" ]; then
    abort "$EXIT_PARAM" "期待する SHA-256 がありません (-ExpectedSha256 か .sha256 sidecar が必要)"
fi

# --- verify SHA-256 --------------------------------------------------------

actual_sha="$(shasum -a 256 "$ZIP_PATH" | awk '{print $1}')"
expected_lower="$(printf '%s' "$EXPECTED_SHA" | tr '[:upper:]' '[:lower:]')"
actual_lower="$(printf '%s' "$actual_sha" | tr '[:upper:]' '[:lower:]')"
if [ "$actual_lower" != "$expected_lower" ]; then
    fail "$EXIT_SHA" "SHA-256 が一致しません。ZIP が破損、または想定と違うファイルです。"
fi
log "sha256 ok: $actual_lower"

# --- extract and validate --------------------------------------------------

EXTRACT_DIR="$WORK_DIR/extract"
mkdir -p "$EXTRACT_DIR" || fail "$EXIT_ZIP" "展開先を作成できません"
if ! ditto -x -k "$ZIP_PATH" "$EXTRACT_DIR"; then
    fail "$EXIT_ZIP" "ZIP を展開できませんでした (破損の可能性)"
fi

NEW_APP="$EXTRACT_DIR/FlashBeat.app"
if [ ! -d "$NEW_APP" ]; then
    NEW_APP="$(find "$EXTRACT_DIR" -maxdepth 3 -type d -name 'FlashBeat.app' -print -quit 2>/dev/null || true)"
fi
if [ -z "$NEW_APP" ] || [ ! -d "$NEW_APP" ]; then
    fail "$EXIT_CONTENT" "ZIP 内に FlashBeat.app がありません"
fi
if [ ! -f "$NEW_APP/Contents/MacOS/FlashBeat" ]; then
    fail "$EXIT_CONTENT" "ZIP 内の FlashBeat.app に実行ファイルがありません"
fi
if [ ! -d "$NEW_APP/Contents/Resources/Data" ]; then
    fail "$EXIT_CONTENT" "ZIP 内の FlashBeat.app に Contents/Resources/Data がありません"
fi

to_version=""
for candidate in "$EXTRACT_DIR/version.json" "$NEW_APP/Contents/Resources/version.json"; do
    if [ -f "$candidate" ]; then
        to_version="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$candidate" | head -1)"
        if [ -z "$TAG" ]; then
            TAG="$(sed -n 's/.*"tag"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$candidate" | head -1)"
        fi
        [ -n "$to_version" ] && break
    fi
done
if [ -z "$to_version" ] && [ -n "$TAG" ]; then
    to_version="${TAG#v}"
    to_version="${to_version%-macos-*}"
fi

# Remove the download quarantine flag if present (the app was already approved by the user).
xattr -dr com.apple.quarantine "$NEW_APP" 2>/dev/null || true

# --- switch ----------------------------------------------------------------

backup="$APP_PARENT/FlashBeat-backup-$(date +%Y%m%d%H%M%S).app"
if [ -d "$INSTALL_DIR" ]; then
    if ! mv "$INSTALL_DIR" "$backup"; then
        fail "$EXIT_SWITCH" "現在の FlashBeat.app を退避できませんでした"
    fi
    log "backup: $backup"
fi

if ! mv "$NEW_APP" "$INSTALL_DIR"; then
    if [ -d "$backup" ] && [ ! -d "$INSTALL_DIR" ]; then
        mv "$backup" "$INSTALL_DIR" 2>/dev/null || true
    fi
    fail "$EXIT_SWITCH" "新しい FlashBeat.app を配置できませんでした (退避から復元しました)"
fi

# Keep only the newest backup generation.
if [ -d "$backup" ]; then
    for old in "$APP_PARENT"/FlashBeat-backup-*.app; do
        [ -e "$old" ] || continue
        [ "$old" = "$backup" ] && continue
        rm -rf "$old" 2>/dev/null || true
    done
fi

if [ ! -f "$INSTALL_DIR/Contents/MacOS/FlashBeat" ] || [ ! -d "$INSTALL_DIR/Contents/Resources/Data" ]; then
    rm -rf "$INSTALL_DIR" 2>/dev/null || true
    if [ -d "$backup" ]; then
        mv "$backup" "$INSTALL_DIR" 2>/dev/null || true
    fi
    fail "$EXIT_SWITCH" "切替後の配置検査に失敗しました (退避から復元しました: $backup)" "$to_version"
fi

log "switch ok: $INSTALL_DIR (to $to_version)"
write_result "success" "$EXIT_OK" "更新しました" "$to_version"

if [ "$RELAUNCH" -eq 1 ]; then
    if open -n "$INSTALL_DIR"; then
        log "relaunched: $INSTALL_DIR"
    else
        log "WARNING: 再起動に失敗しました。手動で FlashBeat.app を起動してください。"
    fi
fi

cleanup
trap - EXIT
exit "$EXIT_OK"
