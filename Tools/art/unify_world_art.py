"""Puts all world art on one pixel grid (16 px per unit) and one palette (sunod_palette.py).

  - Sprites imported at 22 or 32 px/unit are resampled to 16 px/unit (same size in the world)
    and their .meta is set to 16.
  - Every pixel's colour is snapped to the Umaga palette.
  - Soft sprites (glows, mist, shadows: lots of half-transparent pixels) are left alone.
  - UI, cutscenes and NPC sprites are not touched.

Safe to run again: sprites already at 16 are only re-snapped (no change the second time).
Run from the sunod-game folder:  python Tools/art/unify_world_art.py [--dry]
"""
import glob, os, re, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sunod_palette import remap

GRID = 16
RESAMPLE = ["Assets/Art/Generated/Quests"]            # single sprites: resample to the 16 grid
RECOLOUR = [                                          # colour snap only
    "Assets/Art/Town/Sprites", "Assets/Art/Tutorial", "Assets/Art/Background",
    "Assets/Art/Packs/Interiors_Tileset/16x16", "Assets/Art/Packs/Modern tiles_Free/Interiors_free/16x16",
    "Assets/Gif/Super_Retro_Collection/Resources/Environments",
    "Assets/Art/Characters/NPCs/Cat", "Assets/Art/Environment/Farm/Objects",
    "Assets/Resources/TownLife", "Assets/Resources/Town", "Assets/Resources/Morning",
    "Assets/Resources/Ambience", "Assets/Resources/TownEvents",
]
DRY = "--dry" in sys.argv


def soft(a):
    al = a[..., 3]
    vis = (al > 0).sum()
    return vis > 0 and ((al > 8) & (al < 247)).sum() / vis > 0.12


def meta_ppu(path):
    m = re.search(r"spritePixelsToUnits: ([\d.]+)", open(path + ".meta", encoding="utf-8").read())
    return float(m.group(1)) if m else None


def set_meta_ppu(path, ppu):
    p = path + ".meta"
    s = open(p, encoding="utf-8").read()
    s = re.sub(r"spritePixelsToUnits: [\d.]+", f"spritePixelsToUnits: {ppu}", s)
    open(p, "w", encoding="utf-8", newline="\n").write(s)


def downscale(im, f):
    w, h = max(1, round(im.width * f)), max(1, round(im.height * f))
    a = np.array(im).astype(np.float64)
    keep_soft = soft(a)
    a[..., :3] *= a[..., 3:4] / 255.0                      # premultiply so edges don't go dark
    r = np.stack([np.array(Image.fromarray(a[..., i]).resize((w, h), Image.BOX)) for i in range(4)], -1)
    al = r[..., 3:4]
    r[..., :3] = np.where(al > 0, r[..., :3] / np.maximum(al, 1e-6) * 255.0, 0)
    if not keep_soft:
        r[..., 3] = np.where(r[..., 3] >= 110, 255, 0)
    return Image.fromarray(np.clip(r + 0.5, 0, 255).astype(np.uint8), "RGBA")


def files(folder):
    return sorted(glob.glob(os.path.join(folder, "**", "*.png"), recursive=True))


n_res = n_col = n_skip = 0
for folder in RESAMPLE + RECOLOUR:
    for f in files(folder):
        if not os.path.exists(f + ".meta"):
            continue
        im = Image.open(f).convert("RGBA")
        ppu = meta_ppu(f)
        resample = folder in RESAMPLE and ppu in (22.0, 32.0)
        if resample:
            im = downscale(im, GRID / ppu)
        a = np.array(im)
        if soft(a):
            n_skip += 1
            if resample and not DRY:
                im.save(f); set_meta_ppu(f, GRID); n_res += 1
            continue
        out = remap(im)
        if not DRY:
            out.save(f)
            if resample:
                set_meta_ppu(f, GRID)
        n_res += resample; n_col += 1
print(f"resampled {n_res}, recoloured {n_col}, soft sprites left alone {n_skip}" + (" (dry run)" if DRY else ""))
