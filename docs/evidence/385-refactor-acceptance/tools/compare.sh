#!/usr/bin/env bash
# usage: compare.sh <dirA> <dirB>  — pair PNGs by filename, report diffs
A="$1"; B="$2"
for pa in "$A"/*.png "$A"/*/*.png; do
  [ -f "$pa" ] || continue
  rel="${pa#$A/}"
  pb="$B/$rel"
  if [ ! -f "$pb" ]; then echo "MISSING_IN_B $rel"; continue; fi
  out=$(swift /Users/rebui/work/flashbeat-385-acceptance/pngdiff.swift "$pa" "$pb" 0 2>/dev/null)
  echo "$rel :: $out"
done
