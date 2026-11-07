#!/usr/bin/env bash
# Compile-check FlashBeat C# sources without launching the Unity Editor.
#
# Why: several workers edit Unity C# in parallel, and only one Unity Editor
# may own the Library at a time. This script builds the same assembly with
# `dotnet build` against the Unity managed DLLs, which catches syntax and
# typing errors while the integrator still holds the real Editor.
#
# The project file is derived from the Unity-generated csproj (so the
# reference set matches Unity exactly); only the compile item list is
# regenerated from disk, so files added since the last Editor open are
# picked up.
#
# Usage:
#   script/compile-check.sh [--project <dir>] [--assembly flashbeat|editor|tests]
#
# Defaults: --project $PWD --assembly flashbeat
set -euo pipefail

PROJECT_DIR="$PWD"
ASSEMBLY="flashbeat"
TEMPLATE_CSPROJ="${TEMPLATE_CSPROJ:-/Users/rebui/work/FlashBeat/FlashBeat.csproj}"
CANON_LIB="${CANON_LIB:-/Users/rebui/work/FlashBeat/Library}"

while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT_DIR="$2"; shift 2 ;;
    --assembly) ASSEMBLY="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

if [ ! -f "$TEMPLATE_CSPROJ" ]; then
  echo "template csproj missing: $TEMPLATE_CSPROJ" >&2
  echo "open the project once with the Unity Editor to regenerate it" >&2
  exit 1
fi

# The csproj resolves package assemblies through Library/PackageCache and
# Library/ScriptAssemblies. A fresh worktree has no Library, so borrow the
# canonical one read-only; nothing here writes into it.
mkdir -p "$PROJECT_DIR/Library"
for sub in PackageCache ScriptAssemblies; do
  if [ ! -e "$PROJECT_DIR/Library/$sub" ]; then
    ln -s "$CANON_LIB/$sub" "$PROJECT_DIR/Library/$sub"
  fi
done

case "$ASSEMBLY" in
  flashbeat) SRC_ROOT="Assets/Game/Scripts" ;;
  editor)    SRC_ROOT="Assets/Editor" ;;
  tests)     SRC_ROOT="Assets/Game/Tests" ;;
  *) echo "unknown assembly: $ASSEMBLY" >&2; exit 2 ;;
esac

# The csproj must live at the project root: the compile items are relative to
# it. *.csproj is gitignored and Temp/ holds the intermediate output.
OUT="$PROJECT_DIR/FlashBeat.compilecheck.csproj"
mkdir -p "$PROJECT_DIR/Temp"

python3 - "$TEMPLATE_CSPROJ" "$OUT" "$PROJECT_DIR" "$SRC_ROOT" <<'PY'
import os, re, sys

template, out, project_dir, src_root = sys.argv[1:5]
text = open(template, encoding="utf-8-sig").read()

# Drop every Compile Include from the template and re-add the real file set,
# so newly added files are picked up without opening the Editor.
text = re.sub(r'[ \t]*<Compile Include="[^"]+"[^>]*/>\r?\n', "", text)

sources = []
for dirpath, dirnames, filenames in os.walk(os.path.join(project_dir, src_root)):
    dirnames[:] = [d for d in dirnames if d not in ("obj", "bin")]
    for name in filenames:
        if name.endswith(".cs"):
            full = os.path.join(dirpath, name)
            rel = os.path.relpath(full, project_dir)
            sources.append(rel.replace(os.sep, "/"))
sources.sort()

items = "\n".join(f'    <Compile Include="{s}" />' for s in sources)
text = text.replace("</Project>", "  <ItemGroup>\n%s\n  </ItemGroup>\n</Project>" % items)
open(out, "w", encoding="utf-8").write(text)
print(f"compile-check: {len(sources)} sources -> {out}")
PY

cd "$PROJECT_DIR"
dotnet build "$OUT" -v q --nologo 2>&1 | grep -E "error|Build succeeded|エラー|警告" | head -60 || true
