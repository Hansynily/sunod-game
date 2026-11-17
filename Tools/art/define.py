"""The "definition" pass: gives flat sprites the look of the town art (the trees, the houses).

    define(im) -> im     (im must already be on the Umaga palette, hard alpha)

What it adds, with light coming from the top-left:
  - a coloured outline along the silhouette (darker step of the pixel's own ramp, darkest on
    the bottom and right),
  - a highlight just inside the top and left edges, a shade just inside the bottom and right,
  - a contact shadow line under anything that overhangs another colour,
  - grain on big flat areas (wood gets plank dashes, everything else a sparse speckle).
"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sunod_palette import C, INK, PAPER

_RAMP = {}
for _name, _r in C.items():
    for _i, _c in enumerate(_r):
        _RAMP.setdefault(tuple(_c), (_name, _i))
_WOODY = ("brown", "orange", "amber", "skin")


def step(c, n):
    """The colour n steps lighter (n > 0) or darker (n < 0) on its own ramp."""
    c = tuple(int(v) for v in c)
    if c == tuple(INK):
        return C["cool"][min(7, max(0, n))] if n > 0 else c
    if c == tuple(PAPER):
        return C["warm"][max(0, 7 + n)] if n < 0 else c
    if c not in _RAMP:
        return c
    name, i = _RAMP[c]
    j = i + n
    if j < 0:
        return tuple(INK)
    if j > 7:
        return tuple(PAPER)
    return tuple(C[name][j])


def define(im, outline=True, grain=True):
    a = np.array(im.convert("RGBA"))
    h, w = a.shape[:2]
    src = a.copy()
    solid = src[..., 3] >= 128

    def op(y, x):
        return 0 <= y < h and 0 <= x < w and solid[y, x]

    def col(y, x):
        return tuple(int(v) for v in src[y, x, :3])

    big = h >= 9 and w >= 9
    # how many pixels of each colour: only big flat areas get grain
    area = {}
    for y in range(h):
        for x in range(w):
            if solid[y, x]:
                area[col(y, x)] = area.get(col(y, x), 0) + 1

    for y in range(h):
        for x in range(w):
            if not solid[y, x]:
                continue
            c = col(y, x)
            up, dn, lf, rt = op(y - 1, x), op(y + 1, x), op(y, x - 1), op(y, x + 1)
            out = c
            edge = not (up and dn and lf and rt)
            if edge and big and outline:
                thin = (up + dn + lf + rt) <= 1
                if not thin:
                    out = step(c, -3 if (not dn or not rt) else -2)
            elif not edge:
                # one pixel inside the silhouette: light from the top-left
                e_up = not op(y - 2, x)
                e_lf = not op(y, x - 2)
                e_dn = not op(y + 2, x)
                e_rt = not op(y, x + 2)
                if big and (e_up or e_lf) and not (e_dn or e_rt):
                    out = step(c, 1)
                elif big and (e_dn or e_rt) and not (e_up or e_lf):
                    out = step(c, -1)
                else:
                    # contact shadow: a different, lighter part sits right above this pixel
                    cu = col(y - 1, x)
                    if cu != c and cu in _RAMP and c in _RAMP and area.get(c, 0) >= 12 and op(y + 1, x) and col(y + 1, x) == c:
                        if _RAMP[cu][1] >= _RAMP[c][1] and _RAMP[cu][0] != _RAMP[c][0]:
                            out = step(c, -1)
                    elif grain and area.get(c, 0) >= 70 and c in _RAMP:
                        woody = _RAMP[c][0] in _WOODY
                        if woody:
                            if (y % 4 == 2) and ((x + (y // 4) * 5) % 9 in (0, 1, 2)):
                                out = step(c, -1)
                            elif (y % 4 == 0) and ((x + (y // 4) * 3) % 11 == 5):
                                out = step(c, 1)
                        else:
                            k = (x * 7 + y * 13) % 23
                            if k == 0:
                                out = step(c, -1)
                            elif k == 11:
                                out = step(c, 1)
            a[y, x, :3] = out
    return Image.fromarray(a, "RGBA")
