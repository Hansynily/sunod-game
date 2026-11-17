"""Light touch-up of the ORIGINAL player frames (boy + girl). Shapes and poses are not changed.

Reads each frame as committed in git and writes the enhanced copy over the working file:
  - the pure black outline becomes the world's ink colour (a soft violet-black),
  - the shirt's grey shade becomes a cool morning-shadow tint,
  - a short shine is added on the hair, a few rows under the crown.
Run from the sunod-game folder:  python Tools/art/enhance_player.py
"""
import glob, io, os, subprocess
import numpy as np
from PIL import Image

DIR = "Assets/Art/Characters/Player/Frames"
INK = (28, 23, 38)
SHIRT_SHADE_OLD, SHIRT_SHADE_NEW = (220, 220, 220), (204, 210, 234)
HAIR = {(38, 43, 68): (78, 90, 138), (56, 54, 57): (104, 99, 108)}     # main hair colour -> shine


def enhance(im):
    a = np.array(im.convert("RGBA"))
    rgb, al = a[..., :3], a[..., 3]
    solid = al > 0
    is_col = lambda c: solid & (rgb[..., 0] == c[0]) & (rgb[..., 1] == c[1]) & (rgb[..., 2] == c[2])
    rgb[is_col((0, 0, 0))] = INK
    rgb[is_col(SHIRT_SHADE_OLD)] = SHIRT_SHADE_NEW
    for hair, shine in HAIR.items():
        m = is_col(hair)
        if m.sum() < 30:
            continue
        ys, xs = np.nonzero(m)
        y = ys.min() + 2
        row = np.nonzero(m[y])[0]
        if len(row) < 6:
            continue
        x0, x1 = row.min() + 2, row.max() - 2
        for x in range(x0, x1 + 1):
            # two dashes with a gap, leaning to the lit (left) side
            k = x - x0
            if m[y, x] and m[y - 1, x] and (k < 4 or 6 <= k < 8):
                rgb[y, x] = shine
    return Image.fromarray(a, "RGBA")


n = 0
for png in glob.glob(os.path.join(DIR, "**", "*.png"), recursive=True):
    rel = png.replace("\\", "/")
    if rel.endswith("player_shadow.png"):
        continue
    raw = subprocess.run(["git", "show", "HEAD:" + rel], capture_output=True).stdout
    if not raw:
        continue
    enhance(Image.open(io.BytesIO(raw))).save(png)
    n += 1
print("player frames enhanced:", n)
