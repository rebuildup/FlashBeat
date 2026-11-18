#!/usr/bin/env bash
# Integrity check for the `linguist-vendored` paths declared in .gitattributes.
#
# Why this exists:
#   `.gitattributes` marks third-party code as `linguist-vendored` so GitHub's
#   language statistics exclude it (Issue #636). A `linguist-vendored` mark is
#   only honest while the marked tree stays byte-identical to its pinned
#   upstream. If someone edits a file -- or adds a new file -- inside a marked
#   directory, the change would be silently excluded from the language stats
#   and could hide project code inside "vendored" space.
#
# What it checks:
#   Every `linguist-vendored` pattern in .gitattributes must be a `dir/**`
#   directory pattern. For each directory, all tracked files except `*.meta`
#   (Unity-generated, covered by check-repo-integrity.sh) are hashed and
#   compared against config/linguist-vendored.sha256. Any added, removed or
#   modified file fails the check.
#
#   `.agents/skills/` and `.claude/skills/` are linguist-vendored too, but
#   they are excluded here: skills-lock.json + `mise run skills-check` already
#   pin their content to upstream commits.
#
# Usage:
#   script/linguist-vendored.sh            check the working tree (default)
#   script/linguist-vendored.sh --update   regenerate the manifest
#   script/linguist-vendored.sh --root <dir>
#
# Regenerate the manifest whenever a vendored import is added, updated or
# removed -- and re-verify the new content against its upstream first.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODE="check"
while [ $# -gt 0 ]; do
  case "$1" in
    --update) MODE="update"; shift ;;
    --root) ROOT="$2"; shift 2 ;;
    -h|--help) sed -n '2,33p' "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done
ROOT="$(cd "$ROOT" && pwd)"

MANIFEST="$ROOT/config/linguist-vendored.sha256"
ATTRS="$ROOT/.gitattributes"
TMPD="$(mktemp -d -t linguist-vendored.XXXXXX)"
trap 'rm -rf "$TMPD"' EXIT

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

# Collect `dir/** linguist-vendored` directory roots from .gitattributes.
: > "$TMPD/roots"
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in ''|\#*) continue ;; esac
  pattern="${line%%[[:space:]]*}"
  attrs="${line#*"$pattern"}"
  case "$attrs" in
    *linguist-vendored*) ;;
    *) continue ;;
  esac
  pattern="${pattern%\"}"; pattern="${pattern#\"}"
  case "$pattern" in
    */\*\*) printf '%s\n' "${pattern%/\*\*}" >> "$TMPD/roots" ;;
    *)
      echo "FAIL .gitattributes: linguist-vendored pattern must be 'dir/**' form: $pattern" >&2
      exit 1
      ;;
  esac
done < "$ATTRS"
LC_ALL=C sort -u -o "$TMPD/roots" "$TMPD/roots"

# Roots whose integrity is already guaranteed by skills-lock.json.
: > "$TMPD/roots-filtered"
while IFS= read -r r; do
  [ -z "$r" ] && continue
  case "$r" in
    .agents/skills|.claude/skills) continue ;;
  esac
  printf '%s\n' "$r" >> "$TMPD/roots-filtered"
done < "$TMPD/roots"

current="$TMPD/current"
{
  echo "# linguist-vendored manifest (Issue #636)"
  echo "# Regenerate: script/linguist-vendored.sh --update"
  echo "# Format: <sha256>  <path>; *.meta excluded (Unity-generated)."
  while IFS= read -r r; do
    [ -z "$r" ] && continue
    [ -d "$ROOT/$r" ] || { echo "FAIL vendored root missing: $r" >&2; exit 1; }
    while IFS= read -r -d '' f; do
      rel="${f#"$ROOT"/}"
      case "$rel" in *.meta) continue ;; esac
      printf '%s  %s\n' "$(sha256_of "$f")" "$rel"
    done < <(git -C "$ROOT" ls-files -z -- "$r")
  done < "$TMPD/roots-filtered" | LC_ALL=C sort -k2
} > "$current"

if [ "$MODE" = "update" ]; then
  mkdir -p "$(dirname "$MANIFEST")"
  cp "$current" "$MANIFEST"
  echo "manifest written: $MANIFEST ($(grep -vc '^#' "$MANIFEST") entries)"
  exit 0
fi

if ! diff -u "$MANIFEST" "$current" > "$TMPD/diff" 2>&1; then
  {
    echo "FAIL linguist-vendored manifest mismatch (added/removed/modified files under .gitattributes linguist-vendored paths)."
    echo "  If the change is an intentional vendored update, re-verify it against upstream"
    echo "  and run: script/linguist-vendored.sh --update"
    echo "--- diff (manifest -> working tree) ---"
    head -60 "$TMPD/diff"
  } >&2
  exit 1
fi
echo "linguist-vendored manifest OK ($(grep -vc '^#' "$MANIFEST") entries)"
