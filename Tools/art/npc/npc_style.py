"""Shared drawing rules for the storybook townsfolk (the style approved on Aling Rosa v6).

 - 16x32 frames, one warm dark-brown outline around the whole figure
 - flat 3-tone cel shading (shadow, base, light), light from the top-left
 - the far side of big masses (hair, hats, clothes) one tone darker
 - a soft cast shadow under hair / hats / sleeves
 - Stardew-style eyes: brow line, white + pupil
"""
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # Tools/art (palette)
from sunod_palette import C, INK, PAPER  # noqa: E402,F401

W, H = 16, 32
LINE = C["brown"][0]
EYE_WHITE = C["warm"][7]


def render(rows, ramp, far="", cast=""):
    """rows: 32 strings of 16 material letters ('.' = empty). ramp: letter -> [shadow, base, light].
    far: materials darkened on the far (right) side. cast: materials shaded under another part."""
    assert len(rows) == H, len(rows)
    for i, r in enumerate(rows):
        assert len(r) == W, (i, r)
    m = [list(r) for r in rows]
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    get = lambda x, y: m[y][x] if 0 <= x < W and 0 <= y < H else "."
    for y in range(H):
        for x in range(W):
            k = m[y][x]
            if k == ".":
                continue
            r = ramp[k]
            up, lf, dn, rt = get(x, y - 1), get(x - 1, y), get(x, y + 1), get(x + 1, y)
            v = 1
            if x >= W // 2 + 2 and k in far:
                v = 0
            if up not in (".", k) and k in cast:
                v = 0
            if dn == "." or rt == ".":
                v = 0
            if (up == "." or lf == ".") and v == 1 or (up == "." and lf == "."):
                v = 2
            img.putpixel((x, y), r[v] + (255,))
    px = img.load()
    for y in range(H):
        for x in range(W):
            if m[y][x] != ".":
                continue
            if not any(get(x + dx, y + dy) != "." for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            l, r = get(x - 1, y), get(x + 1, y)
            if l != "." and r != "." and l in "LF" and r in "LF":
                continue                                   # keep the gap between the legs open
            px[x, y] = LINE + (255,)
    return img


def put(img, pts, col):
    for x, y in pts:
        img.putpixel((x, y), tuple(col)[:3] + (255,))


def bob(img, by=1, split=25):
    """Breathing / walking dip: everything above `split` moves down, the feet stay planted."""
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(img.crop((0, split, W, H)), (0, split))
    out.alpha_composite(img.crop((0, 0, W, split)), (0, by))
    return out
