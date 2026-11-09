#!/usr/bin/env python3
"""design v6 の各シーンを MacBook (2560x1664) 解像度フレームとして画像化する。

design の実レンダリング PNG (docs/design/v6/screens/*.png, すべて 1728x972 = design 空間)
を、design の Resolution Check が定める MacBook 位置 (416, 346) に **1:1** で
置いて 2560x1664 のフレーム画像にする。
出力: docs/design/v6/screens/macbook/<name>-macbook-2560x1664.png

実機 (2560x1664) のスクリーンショットと 1:1 で重ね比べられることが目的。
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'docs/design/v6/screens')
OUT = os.path.join(ROOT, 'docs/design/v6/screens/macbook')

W, H = 2560, 1664
ORIGIN = (416, 346)     # design: Resolution · MacBook Air M5 2560×1664
BG = (27, 27, 27)       # $bg #1B1B1B (design の bg-anim3 は本리에無いため代用)

# HUD だけは MacBook 固有フレーム (2560x1664) が実 PNG として存在する
FULL = {
    'QrSwA-hud-mac.png': 'hud-macbook-2560x1664.png',
}

# 画面として比較する design レンダリング (1728x972 のものだけ)
SCREENS = {
    'OENRv-select-v6.png',
    'AqFtx-select-v6-empty.png',
    'wSjAm-select-v6-settings.png',
    'uDNC0-pause.png',
    'g4Q3F-song-loading.png',
    'nrfw8-game-playfield.png',
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name in sorted(os.listdir(SRC)):
        if not name.endswith('.png'):
            continue
        src = Image.open(os.path.join(SRC, name)).convert('RGBA')
        if name not in FULL and name not in SCREENS:
            continue
        if name in FULL:
            img = Image.new('RGBA', (W, H), BG + (255,))
            img.alpha_composite(src, (0, 0))
            out = os.path.join(OUT, FULL[name])
        else:
            img = Image.new('RGBA', (W, H), BG + (255,))
            img.alpha_composite(src, ORIGIN)
            slug = name[:-4]
            out = os.path.join(OUT, '%s-macbook-2560x1664.png' % slug)
        img.convert('RGB').save(out)
        print('wrote', os.path.relpath(out, ROOT))


if __name__ == '__main__':
    main()
