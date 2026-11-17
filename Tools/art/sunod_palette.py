"""SUNOD master palette ("Umaga" = morning).

One palette for the whole game world: 14 hue ramps x 8 steps. Shadows lean to blue-violet,
highlights lean to warm yellow, so everything looks lit by the same morning sun.

    from sunod_palette import PALETTE, remap, C
"""
import colorsys
import numpy as np
from PIL import Image

# name: (hue degrees, saturation)
RAMPS = {
    "red": (4, 0.86), "orange": (24, 0.90), "amber": (44, 0.92), "lime": (84, 0.80),
    "green": (118, 0.70), "teal": (165, 0.66), "cyan": (192, 0.70), "blue": (218, 0.68),
    "violet": (268, 0.50), "pink": (338, 0.60), "brown": (24, 0.56), "skin": (22, 0.42),
    "warm": (40, 0.13), "cool": (245, 0.15),
}
VAL = [0.17, 0.28, 0.40, 0.53, 0.66, 0.79, 0.90, 0.97]
SAT = [0.80, 0.90, 0.98, 1.00, 0.96, 0.82, 0.58, 0.32]
N = len(VAL)
SHADOW_HUE, LIGHT_HUE = 262.0, 52.0


def _toward(h, target, amount):
    d = ((target - h + 180) % 360) - 180
    return (h + max(-amount, min(amount, d))) % 360


def _ramp(h, s):
    out = []
    for i in range(N):
        t = i / (N - 1.0)
        hh = _toward(h, SHADOW_HUE, 20 * (1 - t) ** 1.5) if t < 0.5 else _toward(h, LIGHT_HUE, 16 * (t - 0.4) ** 1.2)
        r, g, b = colorsys.hsv_to_rgb(hh / 360.0, min(1.0, s * SAT[i]), VAL[i])
        out.append((round(r * 255), round(g * 255), round(b * 255)))
    return out


C = {name: _ramp(*hs) for name, hs in RAMPS.items()}   # C["green"][0..7] dark -> light
INK = (28, 23, 38)        # darkest outline / void
PAPER = (255, 248, 234)   # warmest white
PALETTE = [INK, PAPER] + [c for r in C.values() for c in r]


def _lab(rgb):
    rgb = np.asarray(rgb, dtype=np.float64) / 255.0
    lin = np.where(rgb > 0.04045, ((rgb + 0.055) / 1.055) ** 2.4, rgb / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.9505, 1.0, 1.089])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


_PAL = np.array(PALETTE, dtype=np.uint8)
_PAL_LAB = _lab(_PAL)


def remap(im):
    """Snap every pixel's colour to the palette (alpha kept)."""
    im = im.convert("RGBA")
    a = np.array(im)
    rgb = a[..., :3].reshape(-1, 3)
    uniq, inv = np.unique(rgb, axis=0, return_inverse=True)
    lab = _lab(uniq)
    idx = np.empty(len(uniq), dtype=np.int64)
    for s in range(0, len(uniq), 4096):
        d = ((lab[s:s + 4096, None, :] - _PAL_LAB[None]) ** 2).sum(-1)
        idx[s:s + 4096] = d.argmin(1)
    a[..., :3] = _PAL[idx][inv.reshape(-1)].reshape(a[..., :3].shape)
    return Image.fromarray(a, "RGBA")


if __name__ == "__main__":
    sw = Image.new("RGB", (N * 16, (len(C) + 1) * 16))
    for y, r in enumerate(C.values()):
        for x, c in enumerate(r):
            sw.paste(c, (x * 16, y * 16, x * 16 + 16, y * 16 + 16))
    sw.paste(INK, (0, len(C) * 16, N * 8, len(C) * 16 + 16)); sw.paste(PAPER, (N * 8, len(C) * 16, N * 16, len(C) * 16 + 16))
    sw.save("palette_umaga.png")
    print(len(PALETTE), "colours -> palette_umaga.png")
