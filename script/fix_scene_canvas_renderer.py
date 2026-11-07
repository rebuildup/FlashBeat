#!/usr/bin/env python3
"""Repair two structural defects in Unity scene YAML (Issue #153).

Defect A - missing CanvasRenderer (u!222)
    Unity logs an Error ("Creating missing CanvasRenderer component for
    TextMeshProUGUI") when a scene is opened and a TMP text object has no
    CanvasRenderer. This script rewrites the scene YAML so every TMP-owning
    GameObject references one.

Defect B - SceneRoots block in the middle of the file
    Unity writes the `--- !u!1660057539` (SceneRoots) block as the LAST block of
    a scene file. When it sits in the middle, the objects that follow it are no
    longer reachable for the transform hierarchy and Unity logs
    "Problem detected while opening the Scene file: ... Transform child can't be
    loaded". This script moves the block to the end without touching any other
    line.

Usage:
    python3 script/fix_scene_canvas_renderer.py <scene.unity> ...           # defect A
    python3 script/fix_scene_canvas_renderer.py --scene-roots-last <scene.unity> ...
"""
import re
import sys

HEADER = re.compile(r"^--- !u!(\d+) &(\d+)")

SCENE_ROOTS_TYPE = "1660057539"


def split_blocks(lines):
    """Return (header_line, body_lines) blocks in file order."""
    blocks = []
    current = None
    for line in lines:
        match = HEADER.match(line)
        if match:
            current = {"type": match.group(1), "id": int(match.group(2)), "header": line, "body": []}
            blocks.append(current)
        elif current is not None:
            current["body"].append(line)
        else:
            # Content before the first block (rare) is preserved verbatim.
            blocks.append({"type": None, "id": None, "header": None, "body": [line]})
    return blocks


def fix(path):
    with open(path, encoding="utf-8") as handle:
        original = handle.read()
    lines = original.split("\n")
    blocks = split_blocks(lines)

    tmp_ids = {
        b["id"]
        for b in blocks
        if b["type"] == "114" and any(l.strip().startswith("m_text:") for l in b["body"])
    }

    used = {b["id"] for b in blocks if b["id"] is not None}
    next_id = 9000000000000090000

    for block in blocks:
        if block["type"] != "1":
            continue
        body = "\n".join(block["body"])
        components = [int(x) for x in re.findall(r"component: \{fileID: (\d+)\}", body)]
        types = {
            b["type"]
            for b in blocks
            if b["id"] is not None and b["id"] in components
        }
        if "222" in types:
            continue
        if not any(c in tmp_ids for c in components):
            continue

        while next_id in used:
            next_id += 1
        cr_id = next_id
        used.add(cr_id)

        pattern = re.compile(r"([ \t]*m_Component:\n(?:[ \t]*- component: \{fileID: \d+\}\n)+)")
        match = pattern.search(body)
        if not match:
            raise SystemExit(f"{path}: no m_Component list on GameObject {block['id']}")
        indent = re.search(r"\n([ \t]*)- component:", match.group(1)).group(1)
        addition = f"{indent}- component: {{fileID: {cr_id}}}\n"
        block["body"] = (body[: match.end(1)] + addition + body[match.end(1) :]).split("\n")

        block["extra"] = [
            f"--- !u!222 &{cr_id}",
            "CanvasRenderer:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {block['id']}}}",
            "  m_CullTransparentMesh: 1",
        ]

    out = []
    added = 0
    for block in blocks:
        if block["header"] is not None:
            out.append(block["header"])
        out.extend(block["body"])
        extra = block.get("extra")
        if extra:
            out.extend(extra)
            added += 1

    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(out))
    print(f"{path}: added {added} CanvasRenderer component(s)")
    return added


def move_scene_roots_last(path):
    """Move the SceneRoots (u!1660057539) block to the end of the scene file.

    Unity always writes that block last. A hand-assembled scene that keeps it in
    the middle makes the objects after it unreachable for the transform
    hierarchy, which Unity reports as "Transform child can't be loaded".
    Only the block's position changes; no line inside it is modified.
    """
    with open(path, encoding="utf-8") as handle:
        original = handle.read()
    lines = original.split("\n")
    blocks = split_blocks(lines)

    root_indexes = [i for i, b in enumerate(blocks) if b["type"] == SCENE_ROOTS_TYPE]
    if not root_indexes:
        print(f"{path}: no SceneRoots block, nothing to move")
        return 0
    if root_indexes == [len(blocks) - 1]:
        print(f"{path}: SceneRoots block already last, nothing to move")
        return 0
    if len(root_indexes) > 1:
        raise SystemExit(f"{path}: {len(root_indexes)} SceneRoots blocks, expected at most 1")

    index = root_indexes[0]
    block = blocks.pop(index)
    blocks.append(block)

    out = []
    for b in blocks:
        if b["header"] is not None:
            out.append(b["header"])
        out.extend(b["body"])

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(out))
    print(f"{path}: moved SceneRoots block to the end (block {index} -> {len(blocks) - 1})")
    return 1


if __name__ == "__main__":
    arguments = sys.argv[1:]
    scene_roots_only = False
    if arguments and arguments[0] == "--scene-roots-last":
        scene_roots_only = True
        arguments = arguments[1:]

    if scene_roots_only:
        total = 0
        for argument in arguments:
            total += move_scene_roots_last(argument)
        print(f"total {total}")
    else:
        total = 0
        for argument in arguments:
            total += fix(argument)
        print(f"total {total}")
