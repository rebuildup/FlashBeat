#!/bin/bash
# update-macos.sh の install scope / 所有 manifest (issue #185 parity, issue #574) の検証。
#
# update-macos.sh を UPDATE_MACOS_FUNCTIONS_ONLY=1 で source して純粋関数を取り込み、
# Windows 版 Update.ps1 と同じ契約を確認する:
#   1. install key / run id / 退避名の名前空間
#   2. 所有 manifest の書き込みと一致判定 (キー / パス)
#   3. 旧世代削除の選択 (自名前空間 + manifest 一致のみ削除、他は保持)
#
# Usage: bash tools/update/tests/test-update-macos.sh
# 終了コード: 0 = 全テスト成功 / 1 = 失敗あり

set -u

repo_root="$(cd "$(dirname "$0")/../../.." && pwd)"
script="$repo_root/tools/update/update-macos.sh"
[ -f "$script" ] || { echo "[FATAL] update-macos.sh not found: $script" >&2; exit 2; }

export UPDATE_MACOS_FUNCTIONS_ONLY=1
# shellcheck disable=SC1090
. "$script"

passed=0
failed=0

assert_true() { # name condition
    if [ "$2" -eq 0 ]; then
        passed=$((passed + 1))
        printf '[PASS] %s\n' "$1"
    else
        failed=$((failed + 1))
        printf '[FAIL] %s\n' "$1"
    fi
}

assert_eq() { # name expected actual
    if [ "$2" = "$3" ]; then
        passed=$((passed + 1))
        printf '[PASS] %s\n' "$1"
    else
        failed=$((failed + 1))
        printf '[FAIL] %s (expected=[%s] actual=[%s])\n' "$1" "$2" "$3"
    fi
}

tmp="$(mktemp -d "${TMPDIR:-/tmp}/flashbeat-macscope-test.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT

echo "=== update-macos.sh install scope tests ==="

# ── T1-T6: install key / run id / 退避名 ─────────────────────────────
assert_eq "T1 canonical app key" "flashbeat" "$(flashbeat_install_key 'FlashBeat.app')"
assert_eq "T2 secondary app key" "flashbeat-0.1.14" "$(flashbeat_install_key 'FlashBeat-0.1.14.app')"
assert_eq "T3 key strips path" "flashbeat" "$(flashbeat_install_key "$tmp/FlashBeat.app")"
run1="$(new_run_id)"
run2="$(new_run_id)"
assert_true "T4 run ids differ" "$([ "$run1" != "$run2" ] && echo 0 || echo 1)"
assert_true "T5 run id format" "$(printf '%s' "$run1" | grep -Eq '^[0-9]{8}-[0-9]{6}-[0-9]+-[0-9a-f]{4,8}$' && echo 0 || echo 1)"

backup_name="$(scoped_backup_name flashbeat "$run1")"
assert_eq "T6 backup name namespaced" "FlashBeat-backup-flashbeat-$run1" "$backup_name"
assert_true "T7 own backup name in scope" "$(test_scoped_backup_name "$backup_name" flashbeat && echo 0 || echo 1)"
assert_true "T8 other install backup name out of scope" "$(test_scoped_backup_name "$(scoped_backup_name flashbeat-0.1.14 "$run1")" flashbeat && echo 1 || echo 0)"
assert_true "T9 legacy backup name out of scope" "$(test_scoped_backup_name 'FlashBeat-backup-20261107-013942' flashbeat && echo 1 || echo 0)"

# ── T10-T16: owner manifest ──────────────────────────────────────────
install_a="$tmp/parent/FlashBeat.app"
install_b="$tmp/parent/FlashBeat-0.1.14.app"
mkdir -p "$install_a/Contents" "$install_b/Contents"
key_a="$(flashbeat_install_key "$install_a")"
key_b="$(flashbeat_install_key "$install_b")"
norm_a="$(norm_cmp "$install_a")"

bdir="$tmp/backups/FlashBeat-backup-$key_a-$run1"
mkdir -p "$bdir"
write_owner_manifest "$bdir" "$key_a" "$norm_a" "FlashBeat.app" "true" "$run1" "0.1.14"

assert_true "T10 manifest written" "$([ -f "$bdir/flashbeat-backup-owner.json" ] && echo 0 || echo 1)"
assert_true "T11 owner matches self" "$(backup_owner_matches "$bdir" "$key_a" "$norm_a" && echo 0 || echo 1)"
assert_true "T12 owner does not match other key" "$(backup_owner_matches "$bdir" "$key_b" "$norm_a" && echo 1 || echo 0)"
assert_true "T13 owner does not match other path" "$(backup_owner_matches "$bdir" "$key_a" "$(norm_cmp "$install_b")" && echo 1 || echo 0)"

# manifest 無し → 所有不明
mkdir -p "$tmp/backups/FlashBeat-backup-$key_a-20261108-000000-1-aaaaaa"
assert_true "T14 no manifest => not owned" "$(backup_owner_matches "$tmp/backups/FlashBeat-backup-$key_a-20261108-000000-1-aaaaaa" "$key_a" "$norm_a" && echo 1 || echo 0)"

# ── T15-T22: 旧世代削除の選択 ────────────────────────────────────────
backup_root="$tmp/backups"
current="$backup_root/FlashBeat-backup-$key_a-$run1"
old_owned="$backup_root/FlashBeat-backup-$key_a-20261108-010101-1111-aaaaaa"
old_other_key="$backup_root/FlashBeat-backup-$key_b-20261108-020202-2222-bbbbbb"
old_no_manifest="$backup_root/FlashBeat-backup-$key_a-20261108-030303-3333-cccccc"
old_legacy_name="$backup_root/FlashBeat-backup-20261107-013942"
for d in "$old_owned" "$old_other_key" "$old_no_manifest" "$old_legacy_name"; do mkdir -p "$d"; done
write_owner_manifest "$old_owned" "$key_a" "$norm_a" "FlashBeat.app" "true" "20261108-010101-1111-aaaaaa" "0.1.13"
write_owner_manifest "$old_other_key" "$key_b" "$(norm_cmp "$install_b")" "FlashBeat-0.1.14.app" "false" "20261108-020202-2222-bbbbbb" "0.1.14"

assert_true "T15 current is kept" "$(backup_is_deletable "$current" "$current" "$key_a" "$norm_a" && echo 1 || echo 0)"
assert_true "T16 owned old generation is deletable" "$(backup_is_deletable "$old_owned" "$current" "$key_a" "$norm_a" && echo 0 || echo 1)"
assert_true "T17 other install backup is kept" "$(backup_is_deletable "$old_other_key" "$current" "$key_a" "$norm_a" && echo 1 || echo 0)"
assert_true "T18 no-manifest backup is kept" "$(backup_is_deletable "$old_no_manifest" "$current" "$key_a" "$norm_a" && echo 1 || echo 0)"
assert_true "T19 legacy-format backup is kept" "$(backup_is_deletable "$old_legacy_name" "$current" "$key_a" "$norm_a" && echo 1 || echo 0)"

# ── T20-T22: ログ世代管理 ────────────────────────────────────────────
log_dir="$tmp/logs"
mkdir -p "$log_dir"
for i in $(seq 1 25); do
    printf 'x\n' > "$log_dir/FlashBeat-update-20260101-0000$(printf '%02d' "$i")-1-aaaaaa.log"
done
LOG_DIR="$log_dir"
prune_update_logs
remaining="$(ls -1 "$log_dir"/FlashBeat-update-*.log 2>/dev/null | wc -l | tr -d ' ')"
assert_eq "T20 logs pruned to 20" "20" "$remaining"
assert_true "T21 newest logs kept" "$([ -f "$log_dir/FlashBeat-update-20260101-000025-1-aaaaaa.log" ] && echo 0 || echo 1)"
assert_true "T22 oldest logs removed" "$([ ! -f "$log_dir/FlashBeat-update-20260101-000001-1-aaaaaa.log" ] && echo 0 || echo 1)"

# ── T23-T36: issue #670 の境界検証 (実ファイルシステムの副作用つき) ──
install_dir="$tmp/install/FlashBeat"
mkdir -p "$install_dir"

# T23-T25: path_within の包含判定
if path_within "$install_dir/.flashbeat-update/work" "$install_dir"; then r23=0; else r23=1; fi
assert_true "T23 install-internal path is within" "$r23"
if path_within "$tmp/outside/x" "$install_dir"; then r24=0; else r24=1; fi
assert_true "T24 outside path is not within" "$([ "$r24" -eq 1 ] && echo 0 || echo 1)"
if path_within "$install_dir-evil/x" "$install_dir"; then r25=0; else r25=1; fi
assert_true "T25 sibling prefix path is not within" "$([ "$r25" -eq 1 ] && echo 0 || echo 1)"

# T26: symlink 経由の脱出は「内部」とみなさない (物理パス比較)
mkdir -p "$tmp/real-target"
ln -s "$tmp/real-target" "$install_dir/escape-link" 2>/dev/null
if path_within "$install_dir/escape-link/sub" "$install_dir"; then r26=0; else r26=1; fi
assert_true "T26 symlink escape is not within" "$([ "$r26" -eq 1 ] && echo 0 || echo 1)"
rm -f "$install_dir/escape-link"

# T27: require_inside_install は境界外を拒否する
INSTALL_DIR="$install_dir"
if require_inside_install "$tmp/outside" "test" 2>/dev/null; then r27=0; else r27=1; fi
assert_true "T27 require_inside_install rejects outside" "$([ "$r27" -eq 1 ] && echo 0 || echo 1)"

# T28-T29: write_result は境界外の RESULT_DIR には書かない
RESULT_DIR="$tmp/outside-results"
INSTALL_DIR="$install_dir"
LOG_PATH=""
write_result "failed" 1 "test" ""
assert_true "T28 result outside install is not written" "$([ ! -f "$tmp/outside-results/last-result.json" ] && echo 0 || echo 1)"
RESULT_DIR="$install_dir/.flashbeat-update/results"
write_result "success" 0 "test" ""
assert_true "T29 result inside install is written" "$([ -f "$RESULT_DIR/last-result.json" ] && echo 0 || echo 1)"
RESULT_DIR=""

# T30: move_dir_contents は .flashbeat-update を必ず除外する (backup の自己内包防止)
src_dir="$install_dir/swap-src"
dst_dir="$install_dir/.flashbeat-update/backup/gen1"
mkdir -p "$src_dir/.flashbeat-update/work" "$src_dir/FlashBeat_Data" "$dst_dir"
printf 'x' > "$src_dir/FlashBeat_Data/d.dll"
printf 'y' > "$src_dir/.flashbeat-update/work/w.tmp"
move_dir_contents "$src_dir" "$dst_dir"
assert_true "T30 .flashbeat-update stays in place during swap" "$([ -d "$src_dir/.flashbeat-update" ] && echo 0 || echo 1)"
assert_true "T31 .flashbeat-update is not moved into backup" "$([ ! -d "$dst_dir/.flashbeat-update" ] && echo 0 || echo 1)"
assert_true "T32 other contents are moved" "$([ -f "$dst_dir/FlashBeat_Data/d.dll" ] && echo 0 || echo 1)"

# T33: warn_legacy_layout は境界外の旧ファイルを検出するだけで変更しない
legacy_parent="$tmp/legacy-parent"
mkdir -p "$legacy_parent/.flashbeat-update" "$legacy_parent/FlashBeat-backup-20261107-013942.app"
printf 'old' > "$legacy_parent/FlashBeat-update.log"
printf 'x' > "$legacy_parent/.flashbeat-update/stale.bin"
APP_PARENT="$legacy_parent"
warn_legacy_layout >/dev/null 2>&1
assert_true "T33 legacy log left untouched" "$([ -f "$legacy_parent/FlashBeat-update.log" ] && echo 0 || echo 1)"
assert_true "T34 legacy update root left untouched" "$([ -f "$legacy_parent/.flashbeat-update/stale.bin" ] && echo 0 || echo 1)"
assert_true "T35 legacy backup app left untouched" "$([ -d "$legacy_parent/FlashBeat-backup-20261107-013942.app" ] && echo 0 || echo 1)"
legacy_new="$(find "$legacy_parent" -mindepth 1 -maxdepth 1 ! -name '.flashbeat-update' ! -name 'FlashBeat-backup-20261107-013942.app' ! -name 'FlashBeat-update.log' | wc -l | tr -d ' ')"
assert_eq "T36 no new dirs created in legacy parent" "0" "$legacy_new"
APP_PARENT=""

echo ""
echo "=== update-macos.sh scope tests: PASS $passed / FAIL $failed ==="
[ "$failed" -eq 0 ]
