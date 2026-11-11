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
#      a deletion made outside the Editor);
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
    -h|--help) sed -n '2,33p' "$0"; exit 0 ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
done

exec python3 -I - "$ROOT" <<'PY'
import json
import os
import re
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
    dirnames[:] = sorted(d for d in dirnames if d != "Library" and not d.startswith("."))

    for name in dirnames:
        path = os.path.join(dirpath, name)
        if not os.path.exists(path + ".meta"):
            missing_meta.append(path)

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


def report(title, paths):
    if not paths:
        return 0
    print("FAIL %s (%d)" % (title, len(paths)))
    for path in sorted(paths):
        # Callers pass either absolute asset paths or pre-formatted "rel:line: ..." strings.
        print("  " + (os.path.relpath(path, root) if os.path.isabs(path) else path))
    return 1


failures = 0
failures += report("asset without .meta", missing_meta)
# Orphan metas are a **warning**, not a failure: `Assets/Packages` and
# `Assets/StreamingAssets` are tracked only by their `.meta` (their contents
# are gitignored), so a fresh CI checkout has the `.meta` without the folder
# and looks exactly like a leftover of a deletion.
if orphan_meta:
    print("WARN orphan .meta (asset is gone, or an empty ignored folder):")
    for path in sorted(orphan_meta):
        print("  " + os.path.relpath(path, root))
failures += report("unparsable .asmdef / Resources .json", bad_json)
failures += report("CRLF line ending in .cs", crlf)
failures += report("tab indentation in .cs", tabs)
failures += report("Player.log path does not use ProjectSettings companyName", log_path_scan)

if failures:
    print("")
    print("Repository integrity check failed. See docs/operations/ci-pr-gate.md.")
    sys.exit(1)

print(
    "Repository integrity check passed (Assets/: meta pairing, JSON, LF, no tabs;"
    " Player.log path uses companyName=%s)." % (company_name or "unknown (ProjectSettings not found)")
)
PY
