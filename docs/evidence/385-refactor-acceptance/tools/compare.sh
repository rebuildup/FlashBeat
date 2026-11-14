#!/usr/bin/env bash
# usage: compare.sh <dirA> <dirB>  — pair PNGs by filename, report diffs
# pngdiff.swift exit codes: 0 = identical, 1 = diff found (normal result),
# 2 = load failure, 3 = size mismatch. Anything other than 0/1 is a
# tool error and fails the whole run.
set -u
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PNGDIFF="$DIR/pngdiff.swift"
A="$1"; B="$2"
status=0
for pa in "$A"/*.png "$A"/*/*.png; do
  [ -f "$pa" ] || continue
  rel="${pa#$A/}"
  pb="$B/$rel"
  if [ ! -f "$pb" ]; then echo "MISSING_IN_B $rel"; status=1; continue; fi
  out=$(swift "$PNGDIFF" "$pa" "$pb" 0 2>&1)
  rc=$?
  if [ $rc -eq 0 ] || [ $rc -eq 1 ]; then
    echo "$rel :: $out"
  else
    echo "$rel :: TOOL_ERROR exit=$rc :: $out"
    status=1
  fi
done
exit $status
