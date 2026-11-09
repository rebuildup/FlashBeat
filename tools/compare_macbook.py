#!/usr/bin/env python3
"""実機キャプチャと design v6 の MacBook (2560x1664) フレームを並べる比較画像を作る。

- 実機キャプチャ: /tmp/fb-cap-<x>/scene-NN-<Scene>.png (2560x1664 実機フルスクリーン)
- design フレーム: docs/design/v6/screens/macbook/<name>-macbook-2560x1664.png

v6 の共通フレームは 1728x972 を 1:1 (等倍) で置き、
  origin = ((w-1728)//2, (h-972)//2)
なので、origin=(416,346) の領域だけを切り出して上下に並べる。

usage: python3 tools/compare_macbook.py <capture-dir> <out-dir>
"""
import os
import sys

from PIL import Image

FRAME_W, FRAME_H = 1728, 972
DESIGN_DIR = os.path.join(os.path.dirname(__file__), "..", "docs", "design", "v6", "screens", "macbook")

# シーン名 -> design の macbook フレーム名
PAIRS = {
    "TitleScene": None,
    "SelectScene": "OENRv-select-v6",
    "OptionScene": None,
    "ResultScene": None,
    "GameScene": "nrfw8-game-playfield",
}


def frame_box(size):
    w, h = size
    x = max(0, (w - FRAME_W) // 2)
    y = max(0, (h - FRAME_H) // 2)
    return (x, y, x + FRAME_W, y + FRAME_H)


def main():
    cap_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    for name in sorted(os.listdir(cap_dir)):
        if not name.endswith(".png"):
            continue
        scene = name.split("-")[-1].replace(".png", "")
        design = PAIRS.get(scene)
        real = Image.open(os.path.join(cap_dir, name)).convert("RGB")
        real_frame = real.crop(frame_box(real.size))
        rows = [real_frame]
        labels = ["real"]
        if design:
            dpath = os.path.join(DESIGN_DIR, design + "-macbook-2560x1664.png")
            if os.path.exists(dpath):
                dimg = Image.open(dpath).convert("RGB")
                rows.append(dimg.crop(frame_box(dimg.size)))
                labels.append("design:" + design)
        out = Image.new("RGB", (FRAME_W, FRAME_H * len(rows)), (27, 27, 27))
        for i, row in enumerate(rows):
            out.paste(row, (0, i * FRAME_H))
        out_path = os.path.join(out_dir, "cmp-" + scene + ".png")
        out.save(out_path)
        print(out_path, labels)


if __name__ == "__main__":
    main()
