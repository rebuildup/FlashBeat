#!/usr/bin/env bash
# License-free repository integrity check for FlashBeat.
#
# What this is for:
#   Unity cannot run on a pull request (it needs a license, and fork PRs
#   cannot read repository secrets). So a PR shows **no Unity check at all** --
#   which is easy to misread as "the PR passed Unity CI". This script is the
#   part of the Unity side that *can* be verified without a license: the rules
#   that a human or a worker breaks most often and that Unity only reports
#   later, inside the Editor.
#
# It checks, over Assets/:
#   1. every asset (file and folder) has its sibling `.meta`      (Unity would
#      generate one, and a second run would then differ from the commit);
#   2. no orphan `.meta` whose asset is gone                      (a leftover of
#      a deletion made outside the Editor; the gitignored
#      `Assets/Packages` / `Assets/StreamingAssets` folders are exempt --
#      they are tracked only by their `.meta`);
#   3. every `.asmdef` and every Resources chart `.json` parses   (a broken
#      assembly definition takes the whole project down);
#   4. `.cs` files use LF and no tabs in our own code              (`.editorconfig`
#      / `.gitattributes`; vendored code under Assets/NoteEditor/Plugins and
#      Assets/ThirdParty is exempt because it is kept verbatim upstream).
#   5. operational docs / acceptance tools spell the Windows Player.log path with
#      the real `companyName` from ProjectSettings (`...\LocalLow\<companyName>\
#      FlashBeat\Player.log`). A wrong segment sends the owner's evidence run to
#      "Player.log not found" and silently produces NO-DATA. Historical evidence
#      records (`docs/operations/exhibit-evidence-*/`) are exempt.
#   6. `.github/workflows/*.{yml,yaml}` parses as YAML and has no duplicate
#      mapping keys (Issue #545: a corrupted build-win64.yml survived because
#      nothing validated the syntax; every push then recorded a phantom
#      failure and dispatch could not run).
#   7. files under `.gitattributes` `linguist-vendored` paths still match
#      config/linguist-vendored.sha256 (Issue #636: vendored marking is only
#      honest while the marked tree stays identical to upstream);
#   8. no tracked file reappears under `Resources/Videos/UnityYoutube/` or
#      `Resources/Musics/` (Issues #823/#794: third-party song videos and
#      wavs were removed from HEAD; playback resolves stream URLs at
#      runtime, so the local media must not return).
#
# This is NOT a substitute for the Unity EditMode run. A PR that passes this
# can still fail to compile; the compile gate is the maintainer's local Unity
# EditMode run (docs/operations/ci-pr-gate.md).
#
# Usage:
#   script/check-repo-integrity.sh [--root <dir>]   (default: repository root)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
while [ $# -gt 0 ]; do
  case "$1" in
    --root) ROOT="$2"; shift 2 ;;
    -h|--help) sed -n "2,$(( $(grep -n '^set -euo pipefail' "$0" | head -1 | cut -d: -f1) - 1 ))p" "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

# Workflow YAML check (Issue #545 / #552). Psych's AST parser
# (YAML.parse_file) is used instead of load_file/safe_load: it is a pure
# syntax parse with no object instantiation, behaves identically on
# Psych 3/4/5 (macOS system Ruby vs CI Ruby), and never rejects legal
# workflow scalars (aliases, dates). Duplicate mapping keys are reported
# separately because Psych's loader silently keeps the last value while
# GitHub rejects the workflow. When ruby is absent the check degrades to
# a warning; the WARN line makes the skipped gate visible.
workflow_yaml_fail=0
workflow_yaml_checked=0
if command -v ruby >/dev/null 2>&1; then
  yaml_report="$(ruby -ryaml - "$ROOT" <<'RUBY'
root = ARGV[0]
Dir.glob(File.join(root, ".github", "workflows", "*.{yml,yaml}")).sort.each do |path|
  rel = path.sub(root + "/", "")
  begin
    doc = YAML.parse_file(path)
    dups = []
    walk = lambda do |node|
      case node
      when Psych::Nodes::Mapping
        seen = {}
        node.children.each_slice(2) do |key_node, value_node|
          if key_node.is_a?(Psych::Nodes::Scalar)
            dups << key_node.value if seen[key_node.value]
            seen[key_node.value] = true
          end
          walk.call(value_node)
        end
      when Psych::Nodes::Sequence
        node.children.each { |child| walk.call(child) }
      end
    end
    doc.children.each { |child| walk.call(child) }
    if dups.empty?
      puts "OK\t#{rel}"
    else
      puts "DUP\t#{rel}\t#{dups.uniq.join(',')}"
    end
  rescue Psych::Exception => e
    detail = e.message.to_s.lines.first.to_s.strip
    puts "FAIL\t#{rel}\t#{detail}"
  end
end
RUBY
)"
  while IFS=$'\t' read -r status rel detail; do
    [ -n "$status" ] || continue
    case "$status" in
      OK) workflow_yaml_checked=$((workflow_yaml_checked + 1)) ;;
      DUP)
        echo "FAIL duplicate mapping key(s) in workflow YAML: $rel ($detail)"
        workflow_yaml_fail=1
        ;;
      FAIL)
        echo "FAIL unparsable workflow YAML: $rel ($detail)"
        workflow_yaml_fail=1
        ;;
    esac
  done <<< "$yaml_report"
  echo "workflow YAML: $workflow_yaml_checked file(s) parsed OK"
else
  echo "WARN ruby not found; workflow YAML parse check SKIPPED (gate degraded)"
fi

# python3 の結果と YAML チェックの結果を集約するため exec は使わない
# (片方の FAIL がもう片方の検査結果を隠さないようにする。#552)。
python_status=0
if ! python3 -I - "$ROOT" <<'PY'
import json
import os
import re
import subprocess
import sys

root = sys.argv[1]
assets = os.path.join(root, "Assets")

# Vendored trees: kept verbatim from upstream, so their formatting is not ours.
VENDORED = (
    os.path.join("Assets", "NoteEditor", "Plugins"),
    os.path.join("Assets", "ThirdParty"),
)

missing_meta = []
orphan_meta = []
bad_json = []
crlf = []
tabs = []


def is_vendored(path):
    rel = os.path.relpath(path, root)
    return any(rel == v or rel.startswith(v + os.sep) for v in VENDORED)


for dirpath, dirnames, filenames in os.walk(assets):
    all_dirs = sorted(d for d in dirnames if d != "Library" and not d.startswith("."))

    for name in all_dirs:
        path = os.path.join(dirpath, name)
        if not os.path.exists(path + ".meta"):
            missing_meta.append(path)

    # .bundle は Unity が単一アセットとして扱うプラグイン束で、内部ファイルに
    # .meta は存在しない (FlashBeatNative.bundle 等)。降りずにスキップする。
    dirnames[:] = [d for d in all_dirs if not d.endswith(".bundle")]

    for name in filenames:
        if name.startswith("."):
            continue
        path = os.path.join(dirpath, name)
        if name.endswith(".meta"):
            if not os.path.exists(path[:-5]):
                orphan_meta.append(path)
            continue
        if not os.path.exists(path + ".meta"):
            missing_meta.append(path)

        if name.endswith(".asmdef"):
            try:
                with open(path, encoding="utf-8-sig") as source:
                    json.load(source)
            except Exception as error:  # noqa: BLE001 - report, do not raise
                bad_json.append("%s: %s" % (path, error))
        elif name.endswith(".json") and os.sep + "Resources" + os.sep in path:
            try:
                with open(path, encoding="utf-8-sig") as source:
                    json.load(source)
            except Exception as error:  # noqa: BLE001
                bad_json.append("%s: %s" % (path, error))

        if name.endswith(".cs"):
            with open(path, "rb") as source:
                body = source.read()
            if b"\r\n" in body:
                crlf.append(path)
            if b"\t" in body and not is_vendored(path):
                tabs.append(path)


# --- Player.log path: must use ProjectSettings companyName ------------------
# Windows stores the player log at
#   %USERPROFILE%\AppData\LocalLow\<companyName>\<productName>\Player.log
# so the folder name is build metadata, not a guess. If a runbook tells the owner
# the wrong segment, the evidence run reports "Player.log not found" and every
# fullscreen verdict degrades to NO-DATA without failing loudly.
settings_path = os.path.join(root, "ProjectSettings", "ProjectSettings.asset")
company_name = None
if os.path.exists(settings_path):
    with open(settings_path, encoding="utf-8", errors="replace") as source:
        for line in source:
            matched = re.match(r"^\s*companyName:\s*(.+?)\s*$", line)
            if matched:
                company_name = matched.group(1).strip().strip('"')
                break

log_path_scan = []
if company_name:
    log_path = re.compile(r"LocalLow[\\/]+([A-Za-z0-9_.\-]+)[\\/]+FlashBeat", re.IGNORECASE)
    scan_roots = [
        os.path.join(root, "docs", "operations"),
        os.path.join(root, "tools", "acceptance"),
    ]
    for scan_root in scan_roots:
        if not os.path.isdir(scan_root):
            continue
        for dirpath, dirnames, filenames in os.walk(scan_root):
            # Historical evidence records quote the wrong path on purpose.
            dirnames[:] = sorted(
                d for d in dirnames if not d.startswith(".") and not d.startswith("exhibit-evidence-")
            )
            for name in filenames:
                if not name.endswith((".md", ".ps1")):
                    continue
                path = os.path.join(dirpath, name)
                with open(path, encoding="utf-8", errors="replace") as source:
                    for number, line in enumerate(source, 1):
                        for matched in log_path.finditer(line):
                            if matched.group(1).lower() != company_name.lower():
                                log_path_scan.append(
                                    "%s:%d: LocalLow\\%s (companyName is %s)"
                                    % (os.path.relpath(path, root), number, matched.group(1), company_name)
                                )



# --- Fast dev gate: keep heavy release jobs out of PR/main push -------------
ci_gate_errors = []
workflow_root = os.path.join(root, ".github", "workflows")
if os.path.isdir(workflow_root):
    for name in ("build-win64.yml", "build-macos.yml"):
        path = os.path.join(workflow_root, name)
        if not os.path.isfile(path):
            ci_gate_errors.append("%s: required workflow missing" % name)
            continue
        with open(path, encoding="utf-8") as source:
            workflow = source.read()
        if re.search(r"(?m)^  push:\s*$", workflow):
            ci_gate_errors.append("%s: heavyweight build must not run on push" % name)
        if not re.search(r"(?m)^  workflow_dispatch:\s*$", workflow):
            ci_gate_errors.append("%s: candidate trigger is missing" % name)

    mac_path = os.path.join(workflow_root, "build-macos.yml")
    if os.path.isfile(mac_path):
        with open(mac_path, encoding="utf-8") as source:
            mac_workflow = source.read()
        test_step = mac_workflow.find("- name: EditMode テスト（release candidate）")
        build_step = mac_workflow.find("- name: macOS ビルド")
        if test_step < 0 or build_step < 0 or test_step >= build_step:
            ci_gate_errors.append("build-macos.yml: full EditMode must precede candidate build")
        if "TestResults.xml" not in mac_workflow or "xmllint --nonet --xpath" not in mac_workflow:
            ci_gate_errors.append("build-macos.yml: missing machine-readable EditMode verdict")
    win_path = os.path.join(workflow_root, "build-win64.yml")
    if os.path.isfile(win_path):
        with open(win_path, encoding="utf-8") as source:
            win_workflow = source.read()
        if "EditMode テストが完全に合格していません" not in win_workflow:
            ci_gate_errors.append("build-win64.yml: missing nonempty/full-pass EditMode verdict")

    web_path = os.path.join(workflow_root, "web.yml")
    if os.path.isfile(web_path):
        with open(web_path, encoding="utf-8") as source:
            workflow = source.read()
        if not re.search(r"(?m)^  workflow_dispatch:\s*$", workflow):
            ci_gate_errors.append("web.yml: candidate E2E dispatch missing")
        if not re.search(
            r"(?m)^  browser-e2e:\s*\n    if: github\.event_name == 'workflow_dispatch'\s*$",
            workflow,
        ):
            ci_gate_errors.append("web.yml: browser E2E must be dispatch-only")
    else:
        ci_gate_errors.append("web.yml: required workflow missing")

def report(title, paths):
    if not paths:
        return 0
    print("FAIL %s (%d)" % (title, len(paths)))
    for path in sorted(paths):
        # Callers pass either absolute asset paths or pre-formatted "rel:line: ..." strings.
        print("  " + (os.path.relpath(path, root) if os.path.isabs(path) else path))
    return 1


# --- Tracked secret-looking files (.env* / .dev.vars, #552) -----------------
# .gitignore alone does not stop `git add -f` or an already-tracked file, so
# the gate itself rejects secret-named paths in the index. `.example` files
# are the documented schema and stay allowed.
secret_looking = []
try:
    tracked = subprocess.run(
        ["git", "-C", root, "ls-files"], capture_output=True, text=True, check=False
    ).stdout.splitlines()
except OSError:
    tracked = []
for rel in tracked:
    base = rel.rsplit("/", 1)[-1]
    if base == ".dev.vars" or (base.startswith(".env") and not base.endswith(".example")):
        secret_looking.append(rel)

# --- Third-party media reintroduction (Issue #823 / #794) ------------------
# Assets/Game/Resources/Videos/UnityYoutube/ held 27 YouTube song videos.
# Playback always resolves the stream URL at runtime (VideoPlayer.source = Url),
# so the local clips were dead weight; they were removed from HEAD so the repo
# no longer carries third-party media. Re-adding anything under that path is a
# license regression, so the gate rejects it outright.
banned_media = [
    rel for rel in tracked
    if rel.startswith("Assets/Game/Resources/Videos/UnityYoutube/")
    or rel.startswith("Assets/Game/Resources/Musics/")
]

failures = 0
failures += report("asset without .meta", missing_meta)
# Orphan metas FAIL, except under `Assets/Packages` and
# `Assets/StreamingAssets`: those folders are gitignored, so a fresh CI
# checkout has the `.meta` without the folder and looks exactly like a
# leftover of a deletion. Anything else is a real orphan and fails the gate.
ORPHAN_META_IGNORE = ("Assets/Packages", "Assets/StreamingAssets")


def orphan_meta_ignored(path):
    rel = os.path.relpath(path, root)
    asset = rel[:-5] if rel.endswith(".meta") else rel
    return any(
        asset == ignored or asset.startswith(ignored + os.sep)
        for ignored in ORPHAN_META_IGNORE
    )


real_orphan_meta = [p for p in orphan_meta if not orphan_meta_ignored(p)]
ignored_orphan_meta = [p for p in orphan_meta if orphan_meta_ignored(p)]
if ignored_orphan_meta:
    print("INFO orphan .meta under gitignored folders (expected on a fresh checkout):")
    for path in sorted(ignored_orphan_meta):
        print("  " + os.path.relpath(path, root))
failures += report("orphan .meta (asset is gone)", real_orphan_meta)
failures += report("unparsable .asmdef / Resources .json", bad_json)
failures += report("CRLF line ending in .cs", crlf)
failures += report("tab indentation in .cs", tabs)
failures += report("Player.log path does not use ProjectSettings companyName", log_path_scan)
failures += report("heavy CI gate accidentally enabled", ci_gate_errors)
failures += report("tracked secret-looking file (.env*/.dev.vars)", secret_looking)
failures += report("third-party media reintroduced under Resources/Videos/UnityYoutube or Resources/Musics", banned_media)

if failures:
    print("")
    print("Repository integrity check failed. See docs/operations/ci-pr-gate.md.")
    sys.exit(1)

print(
    "Repository integrity check passed (Assets/: meta pairing, JSON, LF, no tabs;"
    " Player.log path uses companyName=%s)." % (company_name or "unknown (ProjectSettings not found)")
)
PY
then
  python_status=1
fi

# linguist-vendored manifest check (Issue #636). Runs against the repository
# root, not only Assets/, so it is a separate step rather than part of the
# Assets-scoped Python block above.
if ! bash "$ROOT/script/linguist-vendored.sh" --root "$ROOT"; then
  vendored_status=1
else
  vendored_status=0
fi

if [ "$workflow_yaml_fail" -ne 0 ] || [ "$python_status" -ne 0 ] || [ "$vendored_status" -ne 0 ]; then
  exit 1
fi
