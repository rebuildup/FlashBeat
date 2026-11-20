#!/usr/bin/env bash
# Issue #744: Unity を起動しない範囲の RC preflight。
#
# docs/qa/release-candidate-gate.md の「scriptable」項目を機械実行する。
# 各チェックは PASS / FAIL / SKIP (前提不足) を出力し、FAIL があれば
# 非 0 で終了する。SKIP を PASS と読み替えないこと — Unity EditMode /
# PlayMode / build / E2E はこのスクリプトの対象外で、候補ゲート文書側の
# 手順で別途実施する。
#
# Usage: script/qa-preflight.sh [--with-web]
#   --with-web  web/ の依存が入っている時だけ tsc + vitest を実行する
#               (未インストールなら SKIP)。
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

WITH_WEB=0
for arg in "$@"; do
  case "$arg" in
    --with-web) WITH_WEB=1 ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

fails=0
pass() { printf 'PASS  %s\n' "$1"; }
fail() { printf 'FAIL  %s\n' "$1"; fails=$((fails+1)); }
skip() { printf 'SKIP  %s (%s)\n' "$1" "$2"; }

# --- 1. C# compile (Unity Editor 不要の型検査) ---
# tests-editor は tests / flashbeat の成果物に依存するため順に実行する。
for asm in flashbeat editor tests tests-editor; do
  if bash script/compile-check.sh --assembly "$asm" >/dev/null 2>&1; then
    pass "compile-check: $asm"
  else
    fail "compile-check: $asm (詳細: Temp/compile-check-$asm.log)"
  fi
done

# --- 2. 新規/変更 .cs に .meta があるか (serialized 参照の前提) ---
# Unity は .meta 無しの .cs を開いた時に GUID を採番するが、commit 漏れは
# 別 checkout で GUID 不整合を起こす (Issue #744 で実例あり)。
missing_meta=0
while IFS= read -r f; do
  [ -f "$f.meta" ] || { echo "       missing .meta: $f"; missing_meta=$((missing_meta+1)); }
done < <(git ls-files '*.cs' | while read -r f; do [ -f "$f" ] && echo "$f"; done)
if [ "$missing_meta" -eq 0 ]; then
  pass "all tracked .cs files have .meta"
else
  fail "$missing_meta tracked .cs files lack .meta"
fi

# --- 3. VERSION ファイルの形式 (X.Y.Z) ---
if [ -f VERSION ] && grep -qE '^[0-9]+\.[0-9]+\.[0-9]+$' VERSION; then
  pass "VERSION format ($(cat VERSION))"
else
  fail "VERSION missing or not X.Y.Z"
fi

# --- 4. chart 契約 schema の byte 一致 (正本 vs web 同梱コピー) ---
if [ -f contracts/schemas/chart.v1.schema.json ] && \
   [ -f web/src/lib/chart/chart.v1.schema.json ] && \
   cmp -s contracts/schemas/chart.v1.schema.json web/src/lib/chart/chart.v1.schema.json; then
  pass "chart.v1.schema.json copies identical"
else
  fail "chart.v1.schema.json drift between contracts/ and web/"
fi

# --- 5. parity fixtures 存在 (score parity / timing parity の入力) ---
parity_ok=1
for f in contracts/parity/score-vectors.json contracts/parity/chart-timing.json; do
  [ -f "$f" ] || { echo "       missing: $f"; parity_ok=0; }
done
if [ "$parity_ok" -eq 1 ]; then
  pass "parity fixtures present"
else
  fail "parity fixtures missing"
fi

# --- 6. Web unit/type checks (依存がある時だけ) ---
if [ "$WITH_WEB" -eq 1 ]; then
  if [ -d web/node_modules ]; then
    if (cd web && npx tsc --noEmit) >/dev/null 2>&1; then
      pass "web tsc --noEmit"
    else
      fail "web tsc --noEmit"
    fi
    if (cd web && npx vitest run) >/dev/null 2>&1; then
      pass "web vitest"
    else
      fail "web vitest"
    fi
  else
    skip "web checks" "web/node_modules なし (vp install が必要)"
  fi
else
  skip "web checks" "--with-web 未指定"
fi

echo "---"
if [ "$fails" -eq 0 ]; then
  echo "qa-preflight: FAIL なし (SKIP 項目は RC ゲート文書で個別実施)"
  exit 0
else
  echo "qa-preflight: FAIL $fails 件"
  exit 1
fi
