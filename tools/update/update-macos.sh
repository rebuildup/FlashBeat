#!/bin/bash
# FlashBeat macOS updater (self-update / manual apply).
#
# The app's UpdateChecker launches this script with -Apply -WaitPid -Relaunch after the
# user explicitly confirms an update. It can also be run manually by an operator.
#
# What it does:
#   1. waits for the running FlashBeat process (the app that launched it) to exit
#   2. obtains the release ZIP (local path or GitHub Release tag) and verifies SHA-256
#   3. extracts to a work directory on the same volume as the install
#   4. replaces FlashBeat.app, keeping one backup generation
#   5. relaunches the new app (only with -Relaunch)
#
# Exit codes match tools/update/Update.ps1:
#   0 success / 1 parameters or precondition / 2 network / 3 SHA-256 /
#   4 ZIP / 5 content / 6 already running / 7 switch
#
# The result is always written to last-result.json for the next app start
# (issue #125), even when the update fails.
#
# Layout (issue #670 — アプリが制御する全ファイル操作はインストール先フォルダ内のみ):
#   <install>/.flashbeat-update/
#       work/    … ダウンロード・展開の一時作業フォルダ (実行ごとに削除)
#       backup/  … 旧版の退避 (所有 manifest 付き・1 世代)
#       logs/    … 実行ログ (FlashBeat-update-<runId>.log・最新 20 世代)
#       results/ … last-result.json
#       updater/ … UpdateChecker がコピーした自分自身の一時置き場 (起動後に自分で消す)
#   AppData / Application Support / system temp / インストール先の親フォルダへの
#   書き込みは行わない。persistentDataPath は更新関連アーティファクトの置き場ではない。
#
#   macOS .app バンドルは署名済みで、バンドル内への書き込みは署名を壊し境界も守れないため、
#   .app をインストール先とする更新は安全側で拒否する (外部領域へのフォールバックはしない)。
#   .app 以外のレイアウト (Contents/ を直接持つフォルダ等) は中身の一括切替で扱う。
#
#   境界外 (インストール先の親フォルダ) に残る旧更新ファイルは移動も削除もせず、
#   ログで手動削除を推奨するだけにする (データ保全)。
#   退避の削除は Windows 版 (Update.ps1, issue #185) と同じく
#   「名前が自インストールの名前空間内」+「所有 manifest が一致」の二重チェック。

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
LOG_DIR=""
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
                           (default: <install>/.flashbeat-update/results;
                            must be inside the install folder — issue #670)
  -LogDir <path>           Directory for per-run logs
                           (default: <install>/.flashbeat-update/logs;
                            must be inside the install folder;
                            keeps the newest 20 FlashBeat-update-*.log files)
  -LogPath <path>          Log file (default: <LogDir>/FlashBeat-update-<runId>.log)
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
    # issue #670: 結果ファイルもインストール先フォルダ内に限定する (多層防御)。
    if [ -n "${INSTALL_DIR:-}" ] && ! path_within "$RESULT_DIR" "$INSTALL_DIR"; then
        return 0
    fi
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

# ── install scope helpers (issue #185 parity with Update.ps1, issue #574) ──

flashbeat_install_key() {
    # インストール単位の安定 ID (小文字・英数字と . _ - のみ・連続区切りは畳む)。
    # Update.ps1 の ConvertTo-FlashBeatInstallKey と同じ規則。.app 接尾辞は除く。
    local name="${1##*/}"
    name="${name%.app}"
    name="$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')"
    name="$(printf '%s' "$name" | sed -e 's/[^a-z0-9._-]\+/-/g' -e 's/-\{2,\}/-/g' -e 's/^[-._]*//' -e 's/[-._]*$//')"
    [ -n "$name" ] || name="flashbeat"
    printf '%s' "$name"
}

new_run_id() {
    # Update.ps1 の New-FlashBeatRunId と同じ形式: <yyyyMMdd-HHmmss>-<pid>-<hex4-8>
    local rand6
    rand6="$(od -An -N 3 -tx1 /dev/urandom 2>/dev/null | tr -d ' \n')"
    rand6="${rand6:-$(printf '%x' "$$")}"
    printf '%s-%s-%s' "$(date +%Y%m%d-%H%M%S)" "$$" "${rand6:0:6}"
}

scoped_backup_name() {
    # 自インストール専用の退避ディレクトリ名 (名前空間 = インストール単位)。
    printf 'FlashBeat-backup-%s-%s' "$1" "$2"
}

test_scoped_backup_name() {
    # $1 = 候補名, $2 = InstallKey。自分の名前空間内の退避名なら 0。
    # 旧形式 'FlashBeat-backup-<yyyyMMdd-HHmmss>' は所有不明なので 1 (対象外)。
    local prefix="FlashBeat-backup-$2-"
    case "$1" in
        "$prefix"*) ;;
        *) return 1 ;;
    esac
    local rest="${1#"$prefix"}"
    [[ "$rest" =~ ^[0-9]{8}-[0-9]{6}-[0-9]+-[0-9a-f]{4,8}$ ]]
}

resolve_physical() {
    # 絶対化し、存在する最も深い祖先を物理パス (symlink 解決済み) に置き換える。
    # 存在しない末尾要素はそのまま連結する (これから作るもの用)。
    # issue #670 の境界判定で「字句上は内部だが symlink 経由で外」を検出するために使う。
    local p="$1" rest="" base
    p="${p%/}"
    [ -n "$p" ] || p="/"
    while [ ! -d "$p" ] && [ "$p" != "/" ] && [ "$p" != "." ]; do
        rest="/${p##*/}$rest"
        p="$(dirname "$p")"
    done
    if [ -d "$p" ]; then
        base="$(CDPATH= cd -- "$p" 2>/dev/null && pwd -P || printf '%s' "$p")"
    else
        base="$p"
    fi
    printf '%s%s\n' "$base" "$rest"
}

normalize_path() {
    # 比較用の正規化パス (絶対化・物理パス化・末尾 / 除去)。
    local p="$1"
    case "$p" in
        /*) ;;
        *) p="$PWD/$p" ;;
    esac
    p="${p%/}"
    resolve_physical "$p"
}

norm_cmp() {
    normalize_path "$1" | tr '[:upper:]' '[:lower:]'
}

path_within() {
    # $1 が $2 (インストール先) の内側または一致するときだけ 0。
    # 双方を物理パス化して比較するため、symlink/junction 経由の脱出は一致しない
    # (存在する祖先まで実パス解決される)。issue #670 の境界検証。
    local c r
    c="$(norm_cmp "$1")"
    r="$(norm_cmp "$2")"
    c="${c%/}"
    r="${r%/}"
    [ -n "$c" ] && [ -n "$r" ] || return 1
    [ "$c" = "$r" ] && return 0
    case "$c" in
        "$r"/*) return 0 ;;
    esac
    return 1
}

require_inside_install() {
    # $1 = 検査するパス, $2 = 用途の説明。境界外ならメッセージを出して 1 を返す。
    # 呼び出し側は即座に拒否 (書き込みを行わない) すること。
    if path_within "$1" "$INSTALL_DIR"; then
        return 0
    fi
    echo "error: $2 がインストール先フォルダ外です: $1" >&2
    echo "error: 更新関連のファイルは <install>/.flashbeat-update/ 内にのみ書き込めます (issue #670)。" >&2
    return 1
}

write_owner_manifest() {
    # 退避ディレクトリに所有 manifest を書く (これが無い退避は自動削除しない)。
    # 引数: <backup dir> <installKey> <installPath> <installName> <isCanonical> <runId> <version>
    local dir="$1" key="$2" ipath="$3" iname="$4" canonical="$5" runid="$6" version="${7:-}"
    local parent
    parent="$(dirname "$ipath")"
    cat >"$dir/flashbeat-backup-owner.json" <<EOF
{"schema":"flashbeat-backup-owner/1","installPath":"$(json_escape "$ipath")","installName":"$(json_escape "$iname")","installKey":"$key","isCanonical":$canonical,"parentPath":"$(json_escape "$(norm_cmp "$parent")")","runId":"$runid","version":"$(json_escape "$version")","createdAtUtc":"$(date -u +"%Y-%m-%dT%H:%M:%SZ")"}
EOF
}

manifest_field() {
    # manifest JSON から単純な "field": "value" を取り出す (自分で書いた形式専用)。
    sed -n "s/.*\"$2\"[[:space:]]*:[[:space:]]*\"\([^\"]*\)\".*/\1/p" "$1" | head -1
}

backup_owner_matches() {
    # $1 = 退避ディレクトリ, $2 = InstallKey, $3 = 正規化 installPath。
    # 所有 manifest が「今回のインストール」と一致するときだけ 0。
    local mf="$1/flashbeat-backup-owner.json"
    [ -f "$mf" ] || return 1
    local mkey mpath
    mkey="$(manifest_field "$mf" installKey)"
    mpath="$(manifest_field "$mf" installPath)"
    [ -n "$mkey" ] && [ -n "$mpath" ] || return 1
    [ "$mkey" = "$2" ] || return 1
    [ "$(norm_cmp "$mpath")" = "$(norm_cmp "$3")" ]
}

backup_is_deletable() {
    # 旧世代削除の判定 (Update.ps1 Select-FlashBeatStaleBackups と同じ規則):
    #   $1 = 退避ディレクトリ, $2 = 今回の退避 (current), $3 = InstallKey, $4 = 正規化 installPath
    # 名前が自名前空間内 + 所有 manifest 一致のときだけ 0 (削除してよい)。
    local dir="$1" current="$2" key="$3" ipath="$4"
    [ "$dir" = "$current" ] && return 1
    local name="${dir##*/}"
    test_scoped_backup_name "$name" "$key" || return 1
    backup_owner_matches "$dir" "$key" "$ipath"
}

prune_update_logs() {
    # 実行ログの世代管理: FlashBeat-update-*.log を新しい順に最新 20 件だけ残す。
    # ファイル名に実行時刻が入るため名前の降順 = 時系列の降順。
    [ -d "$LOG_DIR" ] || return 0
    local f count=0
    while IFS= read -r f; do
        [ -n "$f" ] || continue
        count=$((count + 1))
        if [ "$count" -gt 20 ]; then
            rm -f "$f" 2>/dev/null || true
        fi
    done <<EOF
$(ls -1 "$LOG_DIR"/FlashBeat-update-*.log 2>/dev/null | sort -r)
EOF
}

warn_legacy_layout() {
    # issue #670: 境界外 (インストール先の親フォルダ) に残る旧レイアウトの更新ファイルは
    # 移動も削除もせず、「手動削除を推奨する」ログだけを出す (データ保全)。
    #   検出対象: <parent>/FlashBeat-update.log / <parent>/.flashbeat-update.* /
    #             <parent>/FlashBeat-backup-*.app
    local d n=0
    if [ -f "$APP_PARENT/FlashBeat-update.log" ]; then
        log "note: legacy file left untouched (delete manually if unwanted): $APP_PARENT/FlashBeat-update.log"
        n=$((n + 1))
    fi
    for d in "$APP_PARENT"/.flashbeat-update.*; do
        [ -e "$d" ] || continue
        log "note: legacy temp dir left untouched (delete manually if unwanted): $d"
        n=$((n + 1))
    done
    for d in "$APP_PARENT"/FlashBeat-backup-*.app; do
        [ -d "$d" ] || continue
        n=$((n + 1))
    done
    if [ "$n" -gt 0 ]; then
        log "note: $n legacy update artifact(s) outside the install folder were NOT moved or deleted (issue #670). Remove them manually if unwanted."
    fi
}

cleanup() {
    # issue #670: 削除はインストール先フォルダ内のパスに限定する。
    # 境界検証を通らないパスは絶対に消さない。
    if [ -n "${WORK_DIR:-}" ] && [ -d "$WORK_DIR" ]; then
        if path_within "$WORK_DIR" "$INSTALL_DIR"; then
            rm -rf "$WORK_DIR"
        else
            log "WARNING: 作業フォルダが境界外のため削除しません: $WORK_DIR"
        fi
    fi
    # UpdateChecker が <install>/.flashbeat-update/updater/FlashBeat-update-*/ に
    # コピーした自分自身のスクリプトを置いた一時ディレクトリを消す。
    # 手動実行など一致しない場所からの起動では UPDATER_TMP_DIR は空のまま。
    if [ -n "${UPDATER_TMP_DIR:-}" ] && [ -d "$UPDATER_TMP_DIR" ]; then
        if path_within "$UPDATER_TMP_DIR" "$INSTALL_DIR"; then
            rm -rf "$UPDATER_TMP_DIR" 2>/dev/null || true
        else
            log "WARNING: updater 一時ディレクトリが境界外のため削除しません: $UPDATER_TMP_DIR"
        fi
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

move_dir_contents() {
    # $1 の直下の全エントリ (dotfile 含む) を $2 へ移す。
    # .flashbeat-update は常に除外する — 退避先 (backup) は .flashbeat-update の内側に
    # あるため、これを移すと「backup が自身の中に移動」して失敗する (issue #670、
    # Windows 版 Update.ps1 の切替除外と同じ契約)。
    local src="$1" dst="$2" item base
    shopt -s dotglob nullglob
    for item in "$src"/*; do
        base="${item##*/}"
        [ "$base" = ".flashbeat-update" ] && continue
        if ! mv "$item" "$dst/$base" 2>/dev/null; then
            shopt -u dotglob nullglob
            return 1
        fi
    done
    shopt -u dotglob nullglob
    return 0
}

restore_from_backup() {
    # 切替失敗時の復元: インストール先の現在の中身 (新規配置分) を WORK_DIR/failed-new
    # へ退避し、backup の中身をインストール先へ戻す。 .flashbeat-update は触らない。
    # $backup / $WORK_DIR / $INSTALL_DIR は本体側で設定済みのグローバル変数。
    local junk="$WORK_DIR/failed-new"
    mkdir -p "$junk" 2>/dev/null || true
    move_dir_contents "$INSTALL_DIR" "$junk" 2>/dev/null || true
    if [ -d "$backup" ]; then
        move_dir_contents "$backup" "$INSTALL_DIR" 2>/dev/null || true
    fi
}

# テスト用: tools/update/tests/ から関数定義だけを取り込むときに使う
# (UPDATE_MACOS_FUNCTIONS_ONLY=1 で source)。引数解析・本体は走らない。
if [ "${UPDATE_MACOS_FUNCTIONS_ONLY:-0}" = "1" ]; then
    return 0 2>/dev/null || exit 0
fi

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
        -LogDir) LOG_DIR="${2:-}"; shift 2 ;;
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
# 絶対パスに正規化する (所有 manifest の installPath と退避の比較に使う)。
# 物理パス (pwd -P) で正規化し、symlink 経由の別名を境界判定で誤魔化せないようにする。
case "$INSTALL_DIR" in
    /*) ;;
    *) INSTALL_DIR="$PWD/$INSTALL_DIR" ;;
esac
if [ -d "$INSTALL_DIR" ]; then
    INSTALL_DIR="$(CDPATH= cd -- "$INSTALL_DIR" 2>/dev/null && pwd -P || printf '%s' "$INSTALL_DIR")"
fi
INSTALL_DIR="${INSTALL_DIR%/}"
APP_PARENT="$(dirname "$INSTALL_DIR")"
INSTALL_NAME="$(basename "$INSTALL_DIR")"
INSTALL_KEY="$(flashbeat_install_key "$INSTALL_NAME")"
INSTALL_DIR_NORM="$(norm_cmp "$INSTALL_DIR")"
IS_CANONICAL=0
[ "$INSTALL_NAME" = "FlashBeat.app" ] && IS_CANONICAL=1
RUN_ID="$(new_run_id)"

# issue #670: .app バンドルは署名済みで、バンドル内に更新領域を作ると署名が壊れる。
# 「アプリフォルダ内に限定」という境界を .app では守れないため、安全側で拒否する。
# 外部領域 (AppData / Application Support / temp / 親フォルダ) へのフォールバックはしない。
# 結果ファイルを書ける境界内の場所が存在しないため、ファイルシステムへの書き込みは
# 一切行わずに非ゼロ終了する。
case "$INSTALL_NAME" in
    *.app)
        echo "error: FlashBeat.app (署名済みバンドル) への更新はサポートされていません (issue #670)。" >&2
        echo "error: バンドル内に .flashbeat-update を作ると署名が壊れ、境界外へのフォールバックも禁止のため中止します。新しい版は .app を手動で差し替えてください。" >&2
        exit "$EXIT_PARAM"
        ;;
esac

# issue #670: 更新領域はインストール先フォルダ内の .flashbeat-update\ に限定する。
# AppData / Application Support / system temp / 親・兄弟フォルダは使わない。
UPDATE_ROOT="$INSTALL_DIR/.flashbeat-update"
WORK_ROOT="$UPDATE_ROOT/work"
BACKUP_ROOT="$UPDATE_ROOT/backup"
RESULTS_ROOT="$UPDATE_ROOT/results"
LOGS_ROOT="$UPDATE_ROOT/logs"

# 境界検証: 更新領域と明示指定の出力先が、物理パス基準でインストール先内にあること。
# symlink/junction 経由で外を指す場合もここで拒否する (書き込み前)。
require_inside_install "$UPDATE_ROOT" "更新領域" || exit "$EXIT_PARAM"
if [ -n "$RESULT_DIR" ]; then
    require_inside_install "$RESULT_DIR" "-ResultDir" || exit "$EXIT_PARAM"
fi
if [ -n "$LOG_DIR" ]; then
    require_inside_install "$LOG_DIR" "-LogDir" || exit "$EXIT_PARAM"
fi
if [ -n "$LOG_PATH" ]; then
    require_inside_install "$(dirname "$LOG_PATH")" "-LogPath" || exit "$EXIT_PARAM"
fi

# UpdateChecker はスクリプトを <install>/.flashbeat-update/updater/FlashBeat-update-*/ に
# コピーして起動する。終了時にその一時ディレクトリを自分で消すため場所を記録する。
UPDATER_TMP_DIR=""
SELF_DIR="$(CDPATH= cd -- "$(dirname "$0")" 2>/dev/null && pwd || true)"
case "$SELF_DIR" in
    */updater/FlashBeat-update-*) UPDATER_TMP_DIR="$SELF_DIR" ;;
esac
# updater 一時コピーも境界内にあることを確認 (境界外なら終了時削除もしない)。
if [ -n "$UPDATER_TMP_DIR" ] && ! path_within "$UPDATER_TMP_DIR" "$INSTALL_DIR"; then
    UPDATER_TMP_DIR=""
fi

if [ -z "$RESULT_DIR" ]; then
    RESULT_DIR="$RESULTS_ROOT"
fi
if [ -z "$LOG_DIR" ]; then
    LOG_DIR="$LOGS_ROOT"
fi
if [ -z "$LOG_PATH" ]; then
    # 1 実行 = 1 ログファイル (実行時刻入りの名前で世代管理する。issue #574)
    LOG_PATH="$LOG_DIR/FlashBeat-update-$RUN_ID.log"
fi
mkdir -p "$(dirname "$LOG_PATH")" 2>/dev/null || true
prune_update_logs

if [ -d "$INSTALL_DIR" ] && [ -f "$INSTALL_DIR/Contents/Info.plist" ]; then
    FROM_VERSION="$(defaults read "$INSTALL_DIR/Contents/Info.plist" CFBundleShortVersionString 2>/dev/null || true)"
    FROM_VERSION="${FROM_VERSION%%+*}"
else
    FROM_VERSION=""
fi

log "start: install=$INSTALL_DIR zip=${ZIP_PATH:-${TAG:-<none>}} apply=$APPLY relaunch=$RELAUNCH"
log "layout: update-root=$UPDATE_ROOT install-key=$INSTALL_KEY run-id=$RUN_ID"

# 境界外 (インストール先の親フォルダ) の旧レイアウト残骸は検出して警告のみ
# (移動・削除はしない。issue #670)。
warn_legacy_layout

if [ "$APPLY" -ne 1 ] && [ -z "${FLASHBEAT_UPDATE_ALLOW_MANUAL:-}" ] && [ -f "$INSTALL_DIR/Contents/MacOS/FlashBeat" ]; then
    # The in-app flow always passes -Apply. A manual operator can opt in explicitly.
    abort "$EXIT_PARAM" "アプリからの更新は -Apply 付きで実行してください (手動実行は FLASHBEAT_UPDATE_ALLOW_MANUAL=1 を設定)"
fi

if [ -z "$ZIP_PATH" ] && [ -z "$TAG" ]; then
    abort "$EXIT_PARAM" "-ZipPath または -Tag を指定してください"
fi

if [ -d "$INSTALL_DIR" ] && [ ! -w "$INSTALL_DIR" ]; then
    abort "$EXIT_PARAM" "インストール先フォルダに書き込み権限がありません: $INSTALL_DIR"
fi
if [ ! -d "$INSTALL_DIR" ] && [ ! -w "$APP_PARENT" ]; then
    abort "$EXIT_PARAM" "インストール先フォルダを作成する権限がありません (親: $APP_PARENT)"
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

# 実行中判定は argv (command) ではなく実行ファイルの実パス (ps の comm) で行う。
# command を grep すると、この判定自身の grep プロセスが検索語に一致して
# 常に「二重起動」と誤判定する (自己一致)。comm はカーネルが持つ実パスなので、
# 相対パス起動でも同じインストール先の .app を確実に検出できる。
others="$(ps -axo pid=,comm= | awk -v exe="$INSTALL_DIR/Contents/MacOS/FlashBeat" -v self="$$" -v wait="${WAIT_PID:-}" '
    {
        pid = $1
        sub(/^[[:space:]]*[0-9]+[[:space:]]+/, "", $0)
        if ($0 == exe && pid != self && pid != wait) { print pid }
    }')"
if [ -n "$others" ]; then
    abort "$EXIT_RUNNING" "他の FlashBeat が実行中です (PID: $(echo "$others" | tr '\n' ' '))"
fi

# --- obtain the ZIP --------------------------------------------------------

WORK_DIR="$WORK_ROOT/$RUN_ID"
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

# issue #670: 展開物にシンボリックリンクが含まれる場合は拒否する
# (リンク先がインストール先の外を指しうるため。ditto はリンクを復元しうる)。
if [ -n "$(find "$EXTRACT_DIR" -type l -print -quit 2>/dev/null)" ]; then
    fail "$EXIT_CONTENT" "展開物にシンボリックリンクが含まれています (境界を守れないため中止)"
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
# issue #670: インストール先は .app ではなく「中身を持つフォルダ」なので、
# ディレクトリごとではなく中身を一括切替する (Windows 版 Update.ps1 と同じ方式)。
# 退避は <install>/.flashbeat-update/backup/FlashBeat-backup-<key>-<runId>/ に
# 旧コンテンツ一式と所有 manifest (flashbeat-backup-owner.json) を入れる。
# Windows 版 (Update.ps1, issue #185) と同じ「名前空間 + 所有 manifest」の契約。

backup="$BACKUP_ROOT/$(scoped_backup_name "$INSTALL_KEY" "$RUN_ID")"
if [ -d "$INSTALL_DIR" ]; then
    if ! mkdir -p "$backup"; then
        fail "$EXIT_SWITCH" "退避フォルダを作成できませんでした: $backup"
    fi
    # 所有 manifest を先に書いてから退避する (manifest が無い退避は自動削除しない)。
    canonical_json="false"
    [ "$IS_CANONICAL" -eq 1 ] && canonical_json="true"
    write_owner_manifest "$backup" "$INSTALL_KEY" "$INSTALL_DIR_NORM" "$INSTALL_NAME" "$canonical_json" "$RUN_ID" "$FROM_VERSION"
    if ! move_dir_contents "$INSTALL_DIR" "$backup"; then
        # 退避済みのものを元に戻す (manifest のみの退避が残る場合は次回成功時に回収)。
        move_dir_contents "$backup" "$INSTALL_DIR" 2>/dev/null || true
        fail "$EXIT_SWITCH" "現在のインストール内容を退避できませんでした"
    fi
    log "backup: $backup"
fi

# 新版 (ZIP 内の FlashBeat.app) の中身をインストール先へ配置する。
if ! mkdir -p "$INSTALL_DIR" || ! move_dir_contents "$NEW_APP" "$INSTALL_DIR"; then
    restore_from_backup
    fail "$EXIT_SWITCH" "新しい版を配置できませんでした (退避から復元しました)"
fi

# Keep only the newest backup generation — but only backups this install owns
# (name namespace + owner manifest, same rule as Update.ps1 / issue #185).
if [ -d "$backup" ]; then
    for old in "$BACKUP_ROOT"/FlashBeat-backup-*; do
        [ -d "$old" ] || continue
        [ "$old" = "$backup" ] && continue
        if backup_is_deletable "$old" "$backup" "$INSTALL_KEY" "$INSTALL_DIR_NORM"; then
            if rm -rf "$old" 2>/dev/null; then
                log "removed old backup (owned): $old"
            else
                log "WARNING: 旧世代の退避の削除に失敗しました (手動で削除してください): $old"
            fi
        else
            log "keeping backup (not owned by this install): $old"
        fi
    done
fi

if [ ! -f "$INSTALL_DIR/Contents/MacOS/FlashBeat" ] || [ ! -d "$INSTALL_DIR/Contents/Resources/Data" ]; then
    restore_from_backup
    fail "$EXIT_SWITCH" "切替後の配置検査に失敗しました (退避から復元しました: $backup)" "$to_version"
fi

log "switch ok: $INSTALL_DIR (to $to_version)"
write_result "success" "$EXIT_OK" "更新しました" "$to_version"

if [ "$RELAUNCH" -eq 1 ]; then
    # .app は拒否済みのため、ここに来るのは .app 以外のレイアウトのみ。
    # フォルダを open してもアプリは起動しないので、再起動は行わず手動起動を促す。
    log "relaunch skipped: non-.app install layout ($INSTALL_DIR) — start the app manually"
fi

cleanup
trap - EXIT
exit "$EXIT_OK"
