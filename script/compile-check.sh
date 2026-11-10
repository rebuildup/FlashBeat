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
#   script/compile-check.sh [--project <dir>] [--assembly flashbeat|editor|tests|tests-editor]
#
#   tests        = Assets/Game/Tests (FlashBeat.V5CanvasTests など)
#   tests-editor = Assets/Tests/Editor (FlashBeat.Tests / 95 ファイル)
#
# Exit codes:
#   0 = コンパイル成功
#   1 = 型エラー・コンパイル失敗 (詳細は Temp/compile-check-<assembly>.log)
#   2 = 前提不足で実行できない (不明な assembly / Editor 用 DLL が未生成)
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
# PackageCache は読み取り専用で参照できる (Unity が書き込まない)。
if [ ! -e "$PROJECT_DIR/Library/PackageCache" ]; then
  ln -s "$CANON_LIB/PackageCache" "$PROJECT_DIR/Library/PackageCache"
fi
# ScriptAssemblies は Unity が書き込む場所なので、symlink にして共有すると
# 別プロジェクトの Unity が中身を消してしまう (実際に起きた)。自分のプロジェクトが
# 既に持っているならそれを使う。まだ無いときだけ、canonical 側に中身がある
# 場合に限って symlink する (Unity を起動しない作業用)。
if [ ! -e "$PROJECT_DIR/Library/ScriptAssemblies" ]; then
  if [ -n "$(ls -A "$CANON_LIB/ScriptAssemblies" 2>/dev/null | head -1)" ]; then
    ln -s "$CANON_LIB/ScriptAssemblies" "$PROJECT_DIR/Library/ScriptAssemblies"
  else
    echo "warning: Library/ScriptAssemblies が無く、Unity を一度も開いていません" >&2
    echo "         (Unity を起動するか、canonical 側でビルドしてください)" >&2
  fi
fi

case "$ASSEMBLY" in
  flashbeat)    SRC_ROOT="Assets/Game/Scripts" ;;
  editor)       SRC_ROOT="Assets/Editor" ;;
  tests)        SRC_ROOT="Assets/Game/Tests" ;;
  tests-editor) SRC_ROOT="Assets/Tests/Editor" ;;
  *) echo "unknown assembly: $ASSEMBLY" >&2; exit 2 ;;
esac

# The csproj must live at the project root: the compile items are relative to
# it. *.csproj is gitignored and Temp/ holds the intermediate output.
OUT="$PROJECT_DIR/FlashBeat.compilecheck.csproj"
mkdir -p "$PROJECT_DIR/Temp"

python3 - "$TEMPLATE_CSPROJ" "$OUT" "$PROJECT_DIR" "$SRC_ROOT" "$ASSEMBLY" <<'PY'
import os, re, sys

template, out, project_dir, src_root, assembly = sys.argv[1:6]
text = open(template, encoding="utf-8-sig").read()

# Drop every Compile Include from the template and re-add the real file set,
# so newly added files are picked up without opening the Editor.
text = re.sub(r'[ \t]*<Compile Include="[^"]+"[^>]*/>\r?\n', "", text)

# ProjectReference を外す (関連プロジェクトを触らないため)。代わりに Unity が
# ビルド済みの DLL を直接参照する。
text = re.sub(r'[ \t]*<ProjectReference Include="[^"]+"[^>]*/>\r?\n', "", text)

dll_refs = []
sa = os.path.join(project_dir, "Library", "ScriptAssemblies")
# tests アセンブリは FlashBeat 本体の型 (GManager / V5Tokens / V6 の UI) を使うので、
# FlashBeat.dll を参照に足す。Unity がビルドしたものは新規 .cs を含まないので、
# この worktree で直前にビルドした flashbeat の DLL を優先する (flashbeat ビルド
# 自身には足さない: 自分自身の古い DLL で型が解決されるのを防ぐ)。
dll_ref_names = ["SimpleFadeSystem", "UniRx", "NoteEditor"]
if assembly in ("tests", "tests-editor"):
    # Editor 専用テストは UnityEngine.TestRunner (UnityTest) と
    # NoteEditor.Editor (MigrateLegacyTextJson / SongSwitchPrompt) も使う。
    dll_ref_names += ["FlashBeat", "UnityEngine.TestRunner", "NoteEditor.Editor"]
for name in dll_ref_names:
    path = os.path.join(sa, name + ".dll")
    if assembly in ("tests", "tests-editor") and name == "FlashBeat":
        fresh = os.path.join(project_dir, "Temp", "bin", "Debug", "FlashBeat.dll")
        if os.path.exists(fresh):
            path = fresh
    if os.path.exists(path):
        hint = os.path.relpath(path, project_dir).replace(os.sep, "/")
        dll_refs.append(
            f'    <Reference Include="{name}">\n'
            f'      <HintPath>{hint}</HintPath>\n'
            f'      <Private>false</Private>\n'
            f'    </Reference>'
        )

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
extra = ""
if dll_refs:
    extra = "  <ItemGroup>\n%s\n  </ItemGroup>\n" % "\n".join(dll_refs)
text = text.replace("</Project>", "  <ItemGroup>\n%s\n  </ItemGroup>\n%s</Project>" % (items, extra))
open(out, "w", encoding="utf-8").write(text)
print(f"compile-check: {len(sources)} sources -> {out}")
PY

# test 系は FlashBeat.dll を参照する。この worktree で flashbeat をビルドしていない
# 場合は Library/ScriptAssemblies のもの (= Unity が最後に import した時点) を使うので、
# 新しく足した型が「無い」と誤って言われることがある。先に --assembly flashbeat を
# 実行するのが正しい順序だと分かるように明示する。
if { [ "$ASSEMBLY" = "tests" ] || [ "$ASSEMBLY" = "tests-editor" ]; } &&
   [ ! -f "$PROJECT_DIR/Temp/bin/Debug/FlashBeat.dll" ]; then
  echo "compile-check: 注意 — Temp/bin/Debug/FlashBeat.dll がありません。" >&2
  echo "  Library/ScriptAssemblies/FlashBeat.dll (Unity の最終 import 時点) で型を解決します。" >&2
  echo "  最新ソースで検証するには先に --assembly flashbeat を実行してください。" >&2
fi

cd "$PROJECT_DIR"
# BuildProjectReferences=false: Unity が生成した関連 csproj (NoteEditor / UniRx) を
# 再ビルドしない。参照 DLL は既に Library/ScriptAssemblies にある。
# ここでは対象アセンブリの型エラーだけを見たい。
# tests ビルドは AssemblyName が FlashBeat のままなので、そのまま出すと
# flashbeat ビルドの Temp/bin/Debug/FlashBeat.dll を上書きしてしまう
# (次回の tests ビルドの「新しめの FlashBeat.dll」参照が壊れる)。
# そこで tests だけ別の OutputPath に出す。
OUT_PATH="Temp/bin/Debug"
if [ "$ASSEMBLY" = "tests" ]; then
  OUT_PATH="Temp/bin/tests"
fi
if [ "$ASSEMBLY" = "tests-editor" ]; then
  OUT_PATH="Temp/bin/tests-editor"
  # Editor 専用アセンブリ (NoteEditor / NoteEditor.Editor) は Unity が一度でも
  # プロジェクトを開いた時にだけ生成される。無いままビルドすると
  # 「型が見つからない」が 40 件出て原因が分からないので、先に明示して止める。
  if [ ! -f "$PROJECT_DIR/Library/ScriptAssemblies/NoteEditor.Editor.dll" ]; then
    echo "compile-check: 実行できません (tests-editor) — Library/ScriptAssemblies/NoteEditor.Editor.dll がありません。" >&2
    echo "  Editor 用アセンブリは Unity でプロジェクトを開いた時に生成されます。" >&2
    echo "  この worktree で一度 Unity (batchmode 可) を実行してから再試行してください。" >&2
    exit 2
  fi
fi

# 終了コードを意味のあるものにする。以前は `| grep ... || true` で終わっていたため、
# 型エラーがあっても exit 0 になり「通った」と誤読できた (2026-11-10 修正)。
BUILD_LOG="Temp/compile-check-$ASSEMBLY.log"
mkdir -p Temp
set +e
dotnet build "$OUT" -v q --nologo -p:BuildProjectReferences=false -p:OutputPath="$OUT_PATH/" >"$BUILD_LOG" 2>&1
BUILD_STATUS=$?
set -e

grep -E "error|Build succeeded|エラー" "$BUILD_LOG" | head -60 || true

if [ "$BUILD_STATUS" -ne 0 ] || grep -qE "^\s*.*error [A-Z]+[0-9]+" "$BUILD_LOG"; then
  echo "compile-check: FAILED ($ASSEMBLY) — 詳細: $PROJECT_DIR/$BUILD_LOG" >&2
  exit 1
fi

echo "compile-check: OK ($ASSEMBLY)"
