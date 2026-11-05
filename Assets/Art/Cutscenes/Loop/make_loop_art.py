"""Pixel art for the loop cutscenes (a threat striking at the end of a run, and going back in
time) plus the threat marks left around the wrecked home in each town.

Cutscene layers are 320x180 (the cutscene stage), in the same banded-and-dithered style as
the intro cutscene backgrounds (Cut04 / Cut05). The house is the intro's own house
(Cut02_HouseDay/Cutscene02_House_px.png), re-tinted for each threat's light.
Town marks are 16 px per unit, like the rest of the town art.

Usage: python make_loop_art.py   (writes next to this file and to Assets/Resources/Loop)
"""
import math
import os
import random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CUTS = os.path.join(ASSETS, "Art", "Cutscenes")
RES = os.path.join(ASSETS, "Resources", "Loop")
os.makedirs(RES, exist_ok=True)

W, H = 320, 180
HORIZON = 105
T = (0, 0, 0, 0)
OL = (0x2b, 0x1d, 0x24, 255)


def new(w=W, h=H):
    return Image.new("RGBA", (w, h), T)


def save(im, name, folder=HERE):
    im.save(os.path.join(folder, name + ".png"))


def put(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), c)


def put_wrap(im, x, y, c):
    im.putpixel((x % im.width, y % im.height), c)


def rect(im, x0, y0, x1, y1, c):
    for y in range(max(0, y0), min(im.height, y1)):
        for x in range(max(0, x0), min(im.width, x1)):
            im.putpixel((x, y), c)


def a(c, alpha):
    return (c[0], c[1], c[2], alpha)


# ── backgrounds: banded sky with dithered seams, hills, a far city, the ground ─────────

def banded_sky(im, bands, top=0, bottom=HORIZON):
    n = len(bands)
    step = (bottom - top) / n
    for y in range(top, bottom):
        k = min(n - 1, int((y - top) / step))
        frac = ((y - top) / step) - k
        for x in range(W):
            c = bands[k]
            # two dithered rows where one band meets the next (like the intro art)
            if k + 1 < n and frac > 0.82 and (x + y) % 2 == 0:
                c = bands[k + 1]
            im.putpixel((x, y), a(c, 255))


def hills(im, color, far_color, city_color):
    for (cx, peak, half) in [(40, 84, 80), (130, 82, 75)]:
        for x in range(cx - half, cx + half):
            top = peak + abs(x - cx) * (HORIZON - peak) / half
            for y in range(int(top), HORIZON + 4):
                put(im, x, y, a(far_color, 255))
    rng = random.Random(3)
    x = 250
    for w_, h_ in [(8, 20), (6, 12), (10, 26), (5, 8), (9, 16)]:
        rect(im, x, HORIZON - h_, x + w_, HORIZON + 2, a(city_color, 255))
        x += w_ + rng.randint(1, 3)
    for x in range(W):   # bumpy tree line on the horizon
        bump = 2 + int(2 * math.sin(x * 0.35) + 1.5 * math.sin(x * 0.11 + 1))
        for y in range(HORIZON - bump, HORIZON + 4):
            put(im, x, y, a(color, 255))


def ground(im, base, dark, speck, path=None):
    rng = random.Random(7)
    for y in range(HORIZON + 3, H):
        for x in range(W):
            im.putpixel((x, y), a(base, 255))
    for _ in range(900):
        x, y = rng.randrange(W), rng.randrange(HORIZON + 4, H)
        im.putpixel((x, y), a(rng.choice([dark, speck]), 255))
    if path:
        for y in range(118, H):
            half = 8 + (y - 118) * 0.45
            for x in range(int(160 - half), int(160 + half)):
                c = path if rng.random() > 0.25 else dark
                put(im, x, y, a(c, 255))


THEMES = {
    "typhoon": dict(sky=[(36, 46, 50), (46, 60, 62), (58, 74, 72), (72, 90, 84), (88, 104, 94)],
                    hill=(52, 66, 58), far=(64, 78, 76), city=(44, 54, 56),
                    ground=(70, 84, 58), dark=(56, 68, 48), speck=(88, 102, 70), path=(112, 104, 82),
                    tint=(0.62, 0.70, 0.68)),
    "flood": dict(sky=[(32, 38, 54), (42, 50, 68), (52, 62, 82), (64, 76, 96), (78, 90, 108)],
                  hill=(48, 56, 66), far=(60, 68, 84), city=(40, 46, 60),
                  ground=(62, 72, 58), dark=(50, 58, 48), speck=(78, 88, 70), path=(100, 96, 82),
                  tint=(0.58, 0.64, 0.76)),
    "earthquake": dict(sky=[(60, 50, 50), (78, 64, 58), (98, 80, 68), (120, 98, 80), (140, 116, 92)],
                       hill=(96, 78, 64), far=(112, 92, 76), city=(76, 62, 56),
                       ground=(110, 96, 66), dark=(90, 78, 54), speck=(130, 114, 82), path=(140, 124, 98),
                       tint=(0.82, 0.74, 0.68)),
    "fire": dict(sky=[(38, 20, 30), (70, 30, 34), (116, 46, 38), (170, 76, 44), (214, 122, 58)],
                 hill=(62, 30, 32), far=(92, 40, 38), city=(46, 22, 28),
                 ground=(80, 60, 44), dark=(62, 44, 34), speck=(104, 76, 50), path=(120, 92, 66),
                 tint=(0.92, 0.62, 0.50)),
}


def background(theme):
    t = THEMES[theme]
    im = new()
    banded_sky(im, t["sky"])
    hills(im, t["hill"], t["far"], t["city"])
    ground(im, t["ground"], t["dark"], t["speck"], t["path"])
    return im


def tinted_house(theme):
    house = Image.open(os.path.join(CUTS, "Cut02_HouseDay", "Cutscene02_House_px.png")).convert("RGBA")
    r, g, b = THEMES[theme]["tint"]
    out = new()
    for y in range(H):
        for x in range(W):
            p = house.getpixel((x, y))
            if p[3]:
                out.putpixel((x, y), (int(p[0] * r), int(p[1] * g), int(p[2] * b), p[3]))
    return out


# ── weather / disaster effects ────────────────────────────────────────────────────────

def rain(heavy):
    """320x360: two copies of a seamless 320x180 tile, scrolled down one tile height."""
    rng = random.Random(11 if heavy else 12)
    tile = new()
    drops, slant = (520, 3) if heavy else (300, 1)
    col = (190, 205, 215, 150) if heavy else (175, 195, 215, 130)
    for _ in range(drops):
        x, y = rng.randrange(W), rng.randrange(H)
        for i in range(6 if heavy else 5):
            put_wrap(tile, x - (i * slant) // 3, y + i, col)
    out = new(W, H * 2)
    out.paste(tile, (0, 0))
    out.paste(tile, (0, H))
    return out


def wind():
    rng = random.Random(21)
    tile = new()
    for _ in range(26):
        x, y, ln = rng.randrange(W), rng.randrange(10, 150), rng.randrange(14, 40)
        for i in range(ln):
            alpha = 120 if 3 < i < ln - 3 else 60
            put_wrap(tile, x + i, y, (230, 238, 240, alpha))
    out = new(W * 2, H)
    out.paste(tile, (0, 0))
    out.paste(tile, (W, 0))
    return out


def leaves():
    rng = random.Random(22)
    tile = new()
    cols = [(86, 130, 60, 255), (120, 150, 70, 255), (150, 110, 60, 255)]
    for _ in range(40):
        x, y = rng.randrange(W), rng.randrange(20, 160)
        c = rng.choice(cols)
        for dx, dy in [(0, 0), (1, 0), (1, 1), (2, 1)] if rng.random() < 0.5 else [(0, 1), (1, 0), (1, 1), (2, 0)]:
            put_wrap(tile, x + dx, y + dy, c)
    out = new(W * 2, H)
    out.paste(tile, (0, 0))
    out.paste(tile, (W, 0))
    return out


def flash():
    im = new()
    rect(im, 0, 0, W, H, (235, 240, 255, 255))
    return im


def water():
    """400x90 floodwater; rises from below the stage up to the house's windows."""
    rng = random.Random(31)
    im = new(400, 90)
    body, deep, foam, glint = (72, 86, 100), (56, 68, 84), (150, 166, 176), (190, 204, 210)
    for y in range(90):
        for x in range(400):
            top = 4 + int(2 * math.sin(x * 0.18) + math.sin(x * 0.05))
            if y < top:
                continue
            c = foam if y - top < 1 else (deep if y > 40 and (x + y) % 2 == 0 else body)
            im.putpixel((x, y), a(c, 235))
    for _ in range(90):   # glints and floating bits
        x, y = rng.randrange(400), rng.randrange(10, 90)
        ln = rng.randrange(3, 9)
        for i in range(ln):
            put(im, x + i, y, a(glint, 200))
    for _ in range(10):
        x, y = rng.randrange(400), rng.randrange(8, 40)
        rect(im, x, y, x + rng.randrange(5, 12), y + 2, (96, 70, 50, 255))   # floating planks
    return im


def cracks(rng_seed=41, region=(HORIZON + 6, H), count=7, color=(30, 22, 26, 255), xs=(20, 300)):
    """Bold zig-zag fissures (2 px wide, a lit lower lip) with small side branches."""
    rng = random.Random(rng_seed)
    im = new()

    def fissure(x, y, steps, dx):
        for _ in range(steps):
            if not (xs[0] - 10 <= x <= xs[1] + 10):
                break
            put(im, x, y, color)
            put(im, x + 1, y, color)
            put(im, x, y + 1, (150, 126, 96, 220))   # lit edge
            x += dx
            y += rng.choice([-1, 0, 1, 1]) if y < region[1] - 4 else -1
            if rng.random() < 0.06:
                fissure(x, y, rng.randrange(5, 12), -dx)

    for _ in range(count):
        fissure(rng.randrange(*xs), rng.randrange(*region), rng.randrange(30, 70), rng.choice([-1, 1]))
    return im


def dust():
    rng = random.Random(51)
    im = new()
    for _ in range(16):
        cx, cy, r = rng.randrange(0, W), rng.randrange(92, 130), rng.randrange(6, 16)
        for y in range(cy - r, cy + r):
            for x in range(cx - r * 2, cx + r * 2):
                d = ((x - cx) / 2) ** 2 + (y - cy) ** 2
                if d < r * r and (x + y) % 2 == 0:
                    put(im, x, y, (170, 150, 120, 150))
    return im


def flames(frame):
    rng = random.Random(61 + frame)
    im = new()
    spots = [(118, 70), (150, 46), (205, 62), (232, 74), (128, 112), (60, 128), (270, 124), (180, 100)]
    cols = [(255, 214, 90, 255), (250, 150, 50, 255), (214, 70, 40, 255)]
    for (sx, sy) in spots:
        width = rng.randrange(8, 16)
        for x in range(sx - width // 2, sx + width // 2):
            hgt = int((width / 2 - abs(x - sx)) * 2.2) + rng.randrange(0, 6) + (3 if (x + frame) % 3 == 0 else 0)
            for i in range(hgt):
                c = cols[0] if i < hgt * 0.35 else cols[1] if i < hgt * 0.7 else cols[2]
                put(im, x, sy - i + hgt // 2, c)
    return im


def glow():
    im = new()
    for y in range(H):
        alpha = int(150 * (y / H) ** 1.6)
        for x in range(W):
            if (x + y) % 2 == 0 or y > 120:
                im.putpixel((x, y), (255, 120, 40, alpha))
    return im


def smoke():
    """Plumes rising from the roof fires, widening and leaning with the wind as they climb,
    fading out near the top. Static art; the cutscene drifts and breathes it."""
    rng = random.Random(71)
    im = new()
    core, mid, rim = (40, 32, 36), (60, 50, 52), (86, 74, 72)
    for (px, py) in [(150, 40), (122, 64), (210, 58), (238, 70)]:
        y = py
        x = float(px)
        k = 0
        while y > -10:
            r = 4 + k * 0.55
            alpha = int(190 * max(0.15, min(1.0, (y + 10) / (py + 10))))
            for yy in range(int(y - r), int(y + r) + 1):
                for xx in range(int(x - r * 1.3), int(x + r * 1.3) + 1):
                    d = ((xx - x) / 1.3) ** 2 + (yy - y) ** 2
                    if d < r * r:
                        shade = rim if yy < y - r * 0.5 else (core if d < (r * 0.45) ** 2 else mid)
                        if im.getpixel((xx, yy))[3] if 0 <= xx < W and 0 <= yy < H else False:
                            continue   # keep the plume in front solid, no muddy overlaps
                        put(im, xx, yy, a(shade, alpha))
            y -= 4 + rng.random() * 2
            x += 1.6 + rng.random()   # wind pushes it right
            k += 1
    return im


def embers():
    rng = random.Random(81)
    tile = new()
    for _ in range(70):
        x, y = rng.randrange(W), rng.randrange(H)
        put_wrap(tile, x, y, rng.choice([(255, 200, 80, 255), (255, 140, 50, 255)]))
        if rng.random() < 0.3:
            put_wrap(tile, x + 1, y, (255, 120, 40, 200))
    out = new(W, H * 2)
    out.paste(tile, (0, 0))
    out.paste(tile, (0, H))
    return out


# ── aftermath overlays (drawn over the intro's wrecked-house background, Cut05) ────────

def after(theme):
    """The dialog box covers the stage below y~116, so everything that matters sits on the
    house (x 105-245, y 40-116) and the strip of yard beside it."""
    rng = random.Random(91)
    im = new()
    if theme == "flood":
        for y in range(100, H):     # brown water up past the doorstep, with glints
            for x in range(W):
                if (x + y) % 2 == 0 or y > 104:
                    im.putpixel((x, y), (92, 82, 64, 205))
        for _ in range(70):
            x, y = rng.randrange(W), rng.randrange(102, 116)
            for i in range(rng.randrange(3, 8)):
                put(im, x + i, y, (160, 150, 120, 220))
        for y in range(94, 99):     # mud line left on the walls
            for x in range(105, 246):
                if rng.random() < 0.6:
                    im.putpixel((x, y), (70, 56, 42, 220))
        for _ in range(6):          # floating planks
            x, y = rng.randrange(0, 300), rng.randrange(102, 114)
            rect(im, x, y, x + rng.randrange(6, 12), y + 2, (110, 80, 56, 255))
    elif theme == "typhoon":
        for _ in range(5):          # branches thrown against the house
            x, y = rng.randrange(100, 230), rng.randrange(92, 108)
            ln = rng.randrange(14, 26)
            for i in range(ln):
                put(im, x + i, y + i // 4, (74, 52, 40, 255))
                put(im, x + i, y + i // 4 + 1, OL)
                if i % 3 == 0:
                    put(im, x + i, y + i // 4 - 1, (86, 120, 60, 255))
                    put(im, x + i + 1, y + i // 4 - 2, (100, 140, 70, 255))
        for _ in range(60):         # leaves plastered on the roof and walls
            put(im, rng.randrange(110, 245), rng.randrange(40, 100), rng.choice([(86, 120, 60, 255), (130, 110, 60, 255)]))
        for x0 in (12, 262):        # puddles beside the house
            rect(im, x0, 108, x0 + 30, 111, (90, 104, 120, 220))
            rect(im, x0 + 4, 108, x0 + 10, 109, (150, 166, 180, 230))
        rect(im, 6, 104, 32, 108, (150, 70, 50, 255))    # a roof sheet in the yard
        rect(im, 6, 108, 32, 109, OL)
    elif theme == "earthquake":
        im = cracks(95, (62, 112), 7, xs=(112, 238))      # cracks run up the walls
        for x0 in (90, 248, 20, 286):                    # rubble heaped by the walls
            for _ in range(10):
                bx, by = x0 + rng.randrange(0, 22), 104 + rng.randrange(0, 11)
                rect(im, bx, by, bx + 3, by + 2, rng.choice([(150, 144, 136, 255), (118, 112, 104, 255), (170, 104, 80, 255)]))
                put(im, bx, by + 2, OL)
    elif theme == "fire":
        for _ in range(14):         # soot on the walls and roof
            cx, cy, r = rng.randrange(110, 245), rng.randrange(44, 112), rng.randrange(5, 12)
            for y in range(cy - r, cy + r):
                for x in range(cx - r, cx + r):
                    if (x - cx) ** 2 + (y - cy) ** 2 < r * r:
                        put(im, x, y, (24, 20, 22, 120))
        for _ in range(10):         # ash in the yard
            cx = rng.randrange(0, W)
            rect(im, cx, 106 + rng.randrange(0, 8), cx + rng.randrange(8, 20), 116, (96, 92, 90, 200))
        for _ in range(40):         # embers still drifting
            put(im, rng.randrange(W), rng.randrange(20, 110), (255, 140, 50, 255))
    return im

# ── going back in time ─────────────────────────────────────────────────────────────

def rewind_bg():
    im = new()
    banded_sky(im, [(14, 12, 34), (22, 18, 50), (32, 26, 70), (44, 34, 90), (58, 44, 108)], 0, H)
    rng = random.Random(101)
    for _ in range(90):
        put(im, rng.randrange(W), rng.randrange(H), (220, 214, 255, 255) if rng.random() < 0.7 else (170, 150, 255, 255))
    return im


def spiral():
    im = new(180, 180)
    cx = cy = 90
    for i in range(1400):
        t = i / 1400 * 6 * math.pi
        r = 6 + t * 4.4
        x, y = int(cx + r * math.cos(t)), int(cy + r * math.sin(t))
        if (i // 14) % 2 == 0:
            put(im, x, y, (190, 170, 255, 220))
            put(im, x + 1, y, (150, 130, 230, 160))
    return im


def clock():
    im = new(64, 64)
    for y in range(64):
        for x in range(64):
            d = math.hypot(x - 31.5, y - 31.5)
            if d < 27:
                im.putpixel((x, y), (244, 232, 204, 255))
            elif d < 30:
                im.putpixel((x, y), OL)
    for k in range(12):
        ang = k / 12 * 2 * math.pi
        for rr in (22, 23, 24):
            put(im, int(31.5 + rr * math.cos(ang)), int(31.5 + rr * math.sin(ang)), OL)
    rect(im, 30, 30, 34, 34, OL)
    return im


def hand(length, width):
    im = new(64, 64)
    for i in range(length):
        for w_ in range(width):
            put(im, 31 + w_ - width // 2 + 1, 31 - i, (120, 40, 50, 255) if i > length - 3 else OL)
    return im


def motes():
    rng = random.Random(111)
    tile = new()
    for _ in range(50):
        x, y = rng.randrange(W), rng.randrange(H)
        put_wrap(tile, x, y, (210, 200, 255, 220))
        if rng.random() < 0.4:
            put_wrap(tile, x, y + 1, (160, 140, 255, 150))
    out = new(W, H * 2)
    out.paste(tile, (0, 0))
    out.paste(tile, (0, H))
    return out


# ── town: what the threat left around the wrecked home (16 px / unit) ──────────────────

def wreck(theme):
    rng = random.Random(201)
    im = new(128, 28)
    if theme == "flood":
        for _ in range(7):
            x, y, w_ = rng.randrange(0, 110), rng.randrange(4, 24), rng.randrange(10, 20)
            for yy in range(y, y + 4):
                inset = abs(yy - (y + 1.5)) * 2
                for xx in range(int(x + inset), int(x + w_ - inset)):
                    put(im, xx, yy, (86, 132, 176, 230))
            rect(im, x + 3, y + 1, x + 7, y + 2, (190, 220, 240, 255))
            rect(im, x - 2, y + 4, x + w_ + 2, y + 5, (96, 78, 56, 180))   # mud rim
    elif theme == "typhoon":
        for _ in range(6):
            x, y = rng.randrange(0, 110), rng.randrange(4, 22)
            ln = rng.randrange(10, 18)
            for i in range(ln):
                put(im, x + i, y + i // 5, (98, 66, 44, 255))
                put(im, x + i, y + i // 5 + 1, OL)
                if i % 3 == 0:
                    put(im, x + i, y + i // 5 - 1, (92, 150, 70, 255))
                    put(im, x + i + 1, y + i // 5 - 2, (120, 170, 80, 255))
        for _ in range(14):
            put(im, rng.randrange(128), rng.randrange(28), rng.choice([(92, 150, 70, 255), (170, 130, 60, 255)]))
        rect(im, 90, 16, 104, 19, (170, 80, 56, 255))   # a torn roof sheet
        rect(im, 90, 19, 104, 20, OL)
    elif theme == "earthquake":
        x, y = 8, 14
        for _ in range(110):
            put(im, x, y, OL)
            x += 1
            y = max(2, min(25, y + rng.choice([-1, 0, 0, 1])))
        for _ in range(9):
            bx, by = rng.randrange(0, 120), rng.randrange(4, 24)
            for _ in range(rng.randrange(3, 6)):
                sx, sy = bx + rng.randrange(-4, 4), by + rng.randrange(-2, 2)
                rect(im, sx, sy, sx + 3, sy + 2, rng.choice([(150, 144, 136, 255), (118, 112, 108, 255), (170, 104, 80, 255)]))
                put(im, sx, sy + 2, OL)
    elif theme == "fire":
        for _ in range(8):
            cx, cy, r = rng.randrange(8, 120), rng.randrange(6, 22), rng.randrange(5, 10)
            for yy in range(cy - r // 2, cy + r // 2 + 1):
                for xx in range(cx - r, cx + r):
                    if ((xx - cx) / 2) ** 2 + (yy - cy) ** 2 <= (r / 2) ** 2:
                        put(im, xx, yy, (44, 34, 36, 220) if (xx + yy) % 2 else (96, 90, 88, 210))
        for _ in range(10):
            put(im, rng.randrange(128), rng.randrange(28), (255, 150, 60, 255))
    return im


if __name__ == "__main__":
    for theme in THEMES:
        save(background(theme), f"bg_{theme}")
        save(tinted_house(theme), f"house_{theme}")
        save(after(theme), f"after_{theme}")
        save(wreck(theme), f"wreck_{theme}", RES)
    save(rain(True), "rain_heavy")
    save(rain(False), "rain_light")
    save(wind(), "wind")
    save(leaves(), "leaves")
    save(flash(), "flash")
    save(water(), "water")
    save(cracks(41, (100, 116), 8), "cracks")
    save(dust(), "dust")
    save(flames(0), "flames_a")
    save(flames(1), "flames_b")
    save(glow(), "glow")
    save(smoke(), "smoke")
    save(embers(), "embers")
    save(rewind_bg(), "rewind_bg")
    save(spiral(), "spiral")
    save(clock(), "clock")
    save(hand(22, 2), "hand_long")
    save(hand(14, 3), "hand_short")
    save(motes(), "motes")
    print("done")
