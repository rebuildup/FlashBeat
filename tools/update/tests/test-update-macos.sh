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

echo ""
echo "=== update-macos.sh scope tests: PASS $passed / FAIL $failed ==="
[ "$failed" -eq 0 ]
