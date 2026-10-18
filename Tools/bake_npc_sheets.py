"""Bakes walk / sit / idle sheets for every town NPC look into Assets/Resources/TownLife.

The town NPCs (Art/Town/NPC/npc_<char>_<v>_f0..5.png) are palette recolors of the LimeZu
idle_anim "down" frames. We learn each look's colour map from those 6 frames and apply it to
the pack's run / sit / idle sheets, so every look can walk and sit. A few extra townsfolk
looks are made by hue-shifting clothes, so roaming people don't double the quest givers.

Output per look (16x32 frames unless noted):
  walk_<look>.png  384x32  right(0-5) up(6-11) left(12-17) down(18-23)
  idle_<look>.png  384x32  same order
  sit_<look>.png   384x32  12 frames of 32x32: facing right (0-5), left (6-11)
Run from the sunod-game folder:  python Tools/bake_npc_sheets.py
"""
import colorsys, glob, os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACK = os.path.join(ROOT, "Assets/Art/Packs/Modern tiles_Free/Characters_free")
NPC = os.path.join(ROOT, "Assets/Art/Town/NPC")
OUT = os.path.join(ROOT, "Assets/Resources/TownLife")
CHARS = {"adam": "Adam", "alex": "Alex", "amelia": "Amelia", "bob": "Bob"}


def load(p):
    return np.array(Image.open(p).convert("RGBA"))


def key(px):
    return (int(px[0]), int(px[1]), int(px[2]))


def learn_map(char, variant):
    src = load(os.path.join(PACK, f"{CHARS[char]}_idle_anim_16x16.png"))
    cmap = {}
    for f in range(6):
        s = src[:, (18 + f) * 16:(19 + f) * 16]
        d = load(os.path.join(NPC, f"npc_{char}_{variant}_f{f}.png"))
        if s.shape != d.shape:
            continue
        m = (s[:, :, 3] > 0) & (d[:, :, 3] > 0)
        for y, x in zip(*np.nonzero(m)):
            cmap.setdefault(key(s[y, x]), key(d[y, x]))
    return cmap


def apply_map(img, cmap):
    out = img.copy()
    known = list(cmap.keys())
    for y, x in zip(*np.nonzero(img[:, :, 3] > 0)):
        k = key(img[y, x])
        if k not in cmap:
            # a colour only the run/sit frames use: borrow the nearest learned one
            best = min(known, key=lambda c: sum((a - b) ** 2 for a, b in zip(c, k)))
            cmap[k] = cmap[best]
        out[y, x, :3] = cmap[k]
    return out


def skinlike(rgb):
    h, s, v = colorsys.rgb_to_hsv(*(c / 255 for c in rgb))
    return 0.02 < h < 0.11 and 0.2 < s < 0.65 and v > 0.6


def hue_map(img, shift, sat=1.0):
    cmap = {}
    for y, x in zip(*np.nonzero(img[:, :, 3] > 0)):
        k = key(img[y, x])
        if k in cmap:
            continue
        h, s, v = colorsys.rgb_to_hsv(*(c / 255 for c in k))
        if skinlike(k) or s < 0.12:
            cmap[k] = k
        else:
            r, g, b = colorsys.hsv_to_rgb((h + shift) % 1.0, min(1, s * sat), v)
            cmap[k] = (int(r * 255), int(g * 255), int(b * 255))
    return cmap


def save(arr, name):
    Image.fromarray(arr).save(os.path.join(OUT, name))


os.makedirs(OUT, exist_ok=True)
looks = sorted({os.path.basename(p)[4:-7] for p in glob.glob(os.path.join(NPC, "npc_*_f0.png"))})
for look in looks:
    char, variant = look.split("_")
    cmap = learn_map(char, variant)
    for kind, sheet in (("walk", "run"), ("idle", "idle_anim"), ("sit", "sit")):
        save(apply_map(load(os.path.join(PACK, f"{CHARS[char]}_{sheet}_16x16.png")), dict(cmap)), f"{kind}_{look}.png")

# extra townsfolk (roamers): hue-shifted clothes on the base characters
extras = [("adam", 0.33, 1.0), ("alex", 0.55, 1.1), ("amelia", 0.80, 1.0), ("bob", 0.15, 1.2),
          ("adam", 0.62, 0.9), ("alex", 0.95, 1.0), ("amelia", 0.45, 1.1), ("bob", 0.72, 0.9)]
for i, (char, shift, sat) in enumerate(extras):
    run = load(os.path.join(PACK, f"{CHARS[char]}_run_16x16.png"))
    cmap = hue_map(run, shift, sat)
    for kind, sheet in (("walk", "run"), ("idle", "idle_anim"), ("sit", "sit")):
        img = load(os.path.join(PACK, f"{CHARS[char]}_{sheet}_16x16.png"))
        cm = dict(cmap)
        cm.update({k: v for k, v in hue_map(img, shift, sat).items() if k not in cm})
        save(apply_map(img, cm), f"{kind}_town{i}.png")
print("baked", len(looks), "looks +", len(extras), "townsfolk")
