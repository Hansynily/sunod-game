"""Outdoor room kit for vc_RoomDressing (forest / river quest rooms), 16 px per unit, Umaga palette.

Writes Assets/Resources/RoomShell/outdoor/*.png (+ .meta: point filter, no compression,
16 px/unit; ground pieces centre pivot, standing props bottom pivot).
Run from the sunod-game folder:  python Tools/art/make_outdoor_kit.py
"""
import os, re, random, uuid
from PIL import Image, ImageDraw
from sunod_palette import C, INK, PAPER

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "RoomShell", "outdoor")
META_TPL = os.path.join(ROOT, "Assets", "Resources", "RoomShell", "deck_c.png.meta")
os.makedirs(OUT, exist_ok=True)

GRASS_DARK = (92, 146, 40)       # a shade under the rooms' baked grass (113,170,52)
GRASS_LIGHT = (143, 182, 86)
DIRT = [C["brown"][3], C["brown"][4], C["brown"][5], C["brown"][6]]
SPRITES = {}                       # name -> (image, bottom_pivot)


def new(w, h):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))


def px(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), tuple(c)[:3] + (255,))


def outline(im, col):
    """1 px outline around the opaque shape, in `col`."""
    a = im.load()
    pts = []
    for y in range(im.height):
        for x in range(im.width):
            if a[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if 0 <= q[0] < im.width and 0 <= q[1] < im.height and a[q][3] == 255 and a[q][:3] != tuple(col)[:3]:
                    pts.append((x, y)); break
    for p in pts:
        px(im, p[0], p[1], col)
    return im


def add(name, im, bottom=False):
    SPRITES[name] = (im, bottom)


# ── dirt path (16 variants: letters = sides with no path neighbour) ─────────────────
def path_tile(t, b, l, r, seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for y in range(16):
        for x in range(16):
            edge = (t and y < 2) or (b and y > 13) or (l and x < 2) or (r and x > 13)
            corner = (t and l and x + y < 5) or (t and r and (15 - x) + y < 5) or (b and l and x + (15 - y) < 5) or (b and r and (15 - x) + (15 - y) < 5)
            if corner:
                continue
            if edge and rnd.random() < 0.45:
                continue
            c = DIRT[1] if rnd.random() < 0.7 else DIRT[2]
            if edge:
                c = DIRT[0] if rnd.random() < 0.5 else DIRT[1]
            px(im, x, y, c)
    for _ in range(3):                                 # pebbles
        x, y = rnd.randrange(3, 13), rnd.randrange(3, 13)
        px(im, x, y, DIRT[3]); px(im, x + 1, y + 1, DIRT[0])
    return im


def make_path():
    for t in (0, 1):
        for b in (0, 1):
            for l in (0, 1):
                for r in (0, 1):
                    name = ("t" if t else "") + ("b" if b else "") + ("l" if l else "") + ("r" if r else "")
                    add("path_" + (name or "c"), path_tile(t, b, l, r, hash(name) & 0xffff))


# ── ground details (centre pivot, 16x16) ──────────────────────────────────────────
def tuft(seed, light=False):
    rnd = random.Random(seed)
    im = new(16, 16)
    base = 6 + rnd.randrange(-1, 2)
    cols = [C["green"][3], C["green"][4], C["lime"][5]] if not light else [C["lime"][4], C["lime"][5], C["lime"][6]]
    for k in range(5):
        x = base + k * 2 - 4 + rnd.randrange(0, 2)
        hgt = rnd.randrange(3, 6)
        for i in range(hgt):
            px(im, x + (1 if i > hgt - 2 and k % 2 else 0), 12 - i, cols[min(2, i // 2)])
    return im


def flowers(col, seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for _ in range(4):
        x, y = rnd.randrange(2, 13), rnd.randrange(3, 13)
        px(im, x, y + 1, C["green"][3])
        for dx, dy in ((0, -1), (-1, 0), (1, 0), (0, 0)):
            px(im, x + dx, y + dy, col if (dx, dy) != (0, 0) else C["amber"][6])
    return im


def clover(seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for _ in range(9):
        x, y = rnd.randrange(1, 15), rnd.randrange(1, 15)
        px(im, x, y, GRASS_LIGHT if rnd.random() < 0.6 else C["lime"][6])
        px(im, x + 1, y, GRASS_DARK)
    return im


def leaves(seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for _ in range(7):
        x, y = rnd.randrange(1, 14), rnd.randrange(1, 14)
        c = rnd.choice([C["orange"][5], C["amber"][5], C["brown"][4], C["red"][5]])
        px(im, x, y, c); px(im, x + 1, y, c); px(im, x, y + 1, C["brown"][3])
    return im


def moss(seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for _ in range(26):
        x, y = rnd.randrange(0, 16), rnd.randrange(0, 16)
        if (x - 8) ** 2 + (y - 8) ** 2 < 50:
            px(im, x, y, rnd.choice([C["green"][2], C["green"][3], C["teal"][3]]))
    return im


def pebbles(seed):
    rnd = random.Random(seed)
    im = new(16, 16)
    for _ in range(4):
        x, y = rnd.randrange(2, 13), rnd.randrange(2, 13)
        px(im, x, y, C["warm"][5]); px(im, x + 1, y, C["warm"][4]); px(im, x, y + 1, C["warm"][3])
    return im


def make_details():
    for i in range(3):
        add(f"tuft_{i}", tuft(10 + i))
        add(f"tuftl_{i}", tuft(20 + i, light=True))
        add(f"clover_{i}", clover(30 + i))
        add(f"leaves_{i}", leaves(40 + i))
        add(f"moss_{i}", moss(50 + i))
    add("flowers_y", flowers(C["amber"][6], 61))
    add("flowers_w", flowers(PAPER, 62))
    add("flowers_p", flowers(C["pink"][6], 63))
    add("flowers_v", flowers(C["violet"][6], 64))
    add("pebbles_0", pebbles(70)); add("pebbles_1", pebbles(71))
    # tape X on the film set floor
    im = new(16, 16)
    for i in range(10):
        px(im, 3 + i, 3 + i, C["amber"][6]); px(im, 12 - i, 3 + i, C["amber"][6])
    add("tape_x", im)
    # flat stepping stone
    im = new(16, 16)
    d = ImageDraw.Draw(im)
    d.ellipse((2, 4, 13, 12), fill=C["warm"][3] + (255,))
    d.ellipse((2, 3, 13, 10), fill=C["warm"][5] + (255,))
    d.ellipse((4, 4, 9, 7), fill=C["warm"][6] + (255,))
    add("stone_flat", im)
    # lily pad (for water edges)
    im = new(16, 16)
    d = ImageDraw.Draw(im)
    d.ellipse((3, 5, 12, 12), fill=C["green"][3] + (255,))
    d.ellipse((3, 4, 12, 11), fill=C["green"][4] + (255,))
    d.polygon([(8, 8), (12, 6), (12, 9)], fill=(0, 0, 0, 0))
    px(im, 6, 6, C["pink"][6]); px(im, 7, 6, C["pink"][7]); px(im, 6, 5, C["pink"][6])
    add("lily", im)
    # exercise mat (ground)
    im = new(32, 16)
    d = ImageDraw.Draw(im)
    d.rectangle((2, 4, 29, 11), fill=C["teal"][3] + (255,))
    d.rectangle((2, 4, 29, 10), fill=C["teal"][4] + (255,))
    d.line((2, 4, 29, 4), fill=C["teal"][5] + (255,))
    for x in range(5, 29, 6):
        d.line((x, 5, x, 10), fill=C["teal"][3] + (255,))
    add("mat", im)


# ── standing props (bottom pivot) ─────────────────────────────────────────────────
def prop_tallgrass(seed):
    rnd = random.Random(seed)
    im = new(16, 20)
    for k in range(7):
        x = 2 + k * 2 + rnd.randrange(0, 2)
        h = rnd.randrange(9, 17)
        for i in range(h):
            c = C["green"][3] if i < 4 else C["lime"][4] if i < 10 else C["lime"][5]
            px(im, x + (1 if i > h - 4 and k % 2 else 0), 19 - i, c)
        if rnd.random() < 0.4:
            px(im, x, 19 - h, C["amber"][6])
    return im


def prop_reeds(seed):
    rnd = random.Random(seed)
    im = new(16, 22)
    for k in range(5):
        x = 3 + k * 2 + rnd.randrange(0, 2)
        h = rnd.randrange(12, 20)
        for i in range(h):
            px(im, x, 21 - i, C["green"][3] if i < 6 else C["lime"][4])
        if k % 2 == 0:
            for i in range(3):
                px(im, x, 21 - h - i, C["brown"][3]); px(im, x + 1, 21 - h - i, C["brown"][2])
    return im


def prop_bush(w, h, cols, berries=None, seed=1):
    rnd = random.Random(seed)
    im = new(w, h)
    d = ImageDraw.Draw(im)
    d.ellipse((1, 3, w - 2, h - 1), fill=cols[0] + (255,))
    d.ellipse((2, 2, w - 4, h - 3), fill=cols[1] + (255,))
    d.ellipse((4, 3, w // 2, h // 2), fill=cols[2] + (255,))
    for _ in range(w):
        x, y = rnd.randrange(3, w - 3), rnd.randrange(4, h - 3)
        if im.getpixel((x, y))[3]:
            px(im, x, y, cols[0] if rnd.random() < 0.6 else cols[2])
    if berries:
        for _ in range(5):
            x, y = rnd.randrange(4, w - 4), rnd.randrange(5, h - 3)
            if im.getpixel((x, y))[3]:
                px(im, x, y, berries)
    return outline(im, cols[0] if not berries else C["green"][2])


def prop_fern(seed):
    rnd = random.Random(seed)
    im = new(20, 14)
    for k, ang in enumerate((-3, -2, -1, 1, 2, 3)):
        for i in range(8):
            x = 10 + ang * i // 2
            y = 13 - i + abs(ang) * i // 4
            px(im, x, y, C["green"][4] if i < 5 else C["lime"][5])
            if i % 2:
                px(im, x + 1, y, C["green"][3])
    return im


def prop_stump():
    im = new(16, 14)
    d = ImageDraw.Draw(im)
    d.rectangle((3, 5, 12, 12), fill=C["brown"][3] + (255,))
    d.rectangle((3, 5, 6, 12), fill=C["brown"][4] + (255,))
    d.ellipse((3, 2, 12, 7), fill=C["brown"][5] + (255,))
    d.ellipse((5, 3, 10, 6), fill=C["brown"][6] + (255,))
    px(im, 7, 4, C["brown"][4]); px(im, 8, 5, C["brown"][4])
    for x in (2, 13):
        px(im, x, 12, C["brown"][3])
    return outline(im, C["brown"][1])


def prop_mushrooms(cap, glow=False):
    im = new(16, 12)
    for (x, h) in ((4, 5), (9, 7), (12, 4)):
        for i in range(h):
            px(im, x, 11 - i, C["warm"][6]); px(im, x + 1, 11 - i, C["warm"][5])
        for dx in range(-2, 4):
            px(im, x + dx, 11 - h, cap[1])
        for dx in range(-1, 3):
            px(im, x + dx, 10 - h, cap[2])
        px(im, x, 11 - h, PAPER if not glow else cap[2])
    im = outline(im, cap[0])
    return im


def prop_fence():
    im = new(16, 14)
    d = ImageDraw.Draw(im)
    for x in (1, 7, 13):                               # bamboo posts
        d.rectangle((x, 1, x + 1, 13), fill=C["amber"][5] + (255,))
        px(im, x, 1, C["amber"][6]); px(im, x, 6, C["amber"][4]); px(im, x + 1, 9, C["amber"][4])
    for y in (4, 9):
        d.line((0, y, 15, y), fill=C["amber"][4] + (255,))
    return outline(im, C["amber"][2])


def prop_garden_bed(seed):
    rnd = random.Random(seed)
    im = new(32, 16)
    d = ImageDraw.Draw(im)
    d.rectangle((1, 5, 30, 14), fill=C["brown"][2] + (255,))
    d.rectangle((2, 6, 29, 12), fill=C["brown"][3] + (255,))
    for y in (7, 10):
        d.line((3, y, 28, y), fill=C["brown"][2] + (255,))
    for x in range(4, 28, 4):                          # sprouts
        yy = rnd.choice((7, 10))
        px(im, x, yy - 1, C["lime"][5]); px(im, x - 1, yy - 2, C["green"][4]); px(im, x + 1, yy - 2, C["lime"][6])
    d.line((1, 14, 30, 14), fill=C["brown"][1] + (255,))
    return outline(im, C["brown"][1])


def prop_cone():
    im = new(10, 12)
    d = ImageDraw.Draw(im)
    d.polygon([(5, 1), (8, 10), (2, 10)], fill=C["orange"][5] + (255,))
    d.line((3, 6, 7, 6), fill=PAPER + (255,))
    d.rectangle((1, 10, 9, 11), fill=C["orange"][3] + (255,))
    return outline(im, C["orange"][2])


def prop_sign(text_col):
    im = new(14, 20)
    d = ImageDraw.Draw(im)
    d.rectangle((6, 8, 7, 19), fill=C["brown"][3] + (255,))
    d.rectangle((1, 2, 12, 9), fill=C["brown"][5] + (255,))
    d.rectangle((1, 2, 12, 3), fill=C["brown"][6] + (255,))
    for x in range(3, 11, 2):
        px(im, x, 6, text_col)
    return outline(im, C["brown"][1])


def prop_crate():
    im = new(14, 14)
    d = ImageDraw.Draw(im)
    d.rectangle((1, 3, 12, 13), fill=C["amber"][4] + (255,))
    d.rectangle((1, 3, 12, 5), fill=C["amber"][5] + (255,))
    d.line((1, 3, 12, 13), fill=C["amber"][3] + (255,))
    d.line((1, 8, 12, 8), fill=C["amber"][3] + (255,))
    return outline(im, C["brown"][1])


def prop_rope():
    im = new(14, 8)
    d = ImageDraw.Draw(im)
    d.ellipse((1, 1, 12, 7), fill=C["amber"][5] + (255,))
    d.ellipse((4, 3, 9, 5), fill=C["amber"][3] + (255,))
    d.arc((1, 1, 12, 7), 0, 360, fill=C["amber"][4] + (255,))
    return outline(im, C["brown"][1])


def prop_basket():
    im = new(14, 10)
    d = ImageDraw.Draw(im)
    d.rectangle((2, 3, 11, 9), fill=C["amber"][4] + (255,))
    for x in range(2, 12, 2):
        d.line((x, 3, x, 9), fill=C["amber"][5] + (255,))
    for (x, c) in ((4, C["red"][5]), (6, C["orange"][5]), (8, C["green"][4]), (9, C["violet"][4])):
        px(im, x, 2, c); px(im, x, 1, c)
    return outline(im, C["brown"][1])


def prop_logseat():
    im = new(26, 12)
    d = ImageDraw.Draw(im)
    d.rectangle((3, 3, 22, 10), fill=C["brown"][3] + (255,))
    d.rectangle((3, 3, 22, 5), fill=C["brown"][4] + (255,))
    d.ellipse((19, 2, 25, 10), fill=C["brown"][5] + (255,))
    d.ellipse((21, 4, 23, 8), fill=C["brown"][6] + (255,))
    return outline(im, C["brown"][1])


def prop_glow():
    im = new(20, 20)
    d = ImageDraw.Draw(im)
    for r, a in ((9, 40), (6, 70), (3, 110)):
        d.ellipse((10 - r, 13 - r, 10 + r, 13 + r), fill=C["cyan"][7] + (a,))
    return im


def make_props():
    for i in range(3):
        add(f"tallgrass_{i}", prop_tallgrass(80 + i), True)
    add("reeds_0", prop_reeds(90), True); add("reeds_1", prop_reeds(91), True)
    add("bush_round", prop_bush(22, 16, [C["green"][2], C["green"][3], C["green"][4]], seed=3), True)
    add("bush_berry", prop_bush(20, 15, [C["green"][2], C["green"][3], C["green"][4]], C["red"][5], seed=4), True)
    add("bush_flower", prop_bush(20, 15, [C["green"][2], C["green"][3], C["lime"][5]], C["pink"][6], seed=5), True)
    add("fern", prop_fern(6), True)
    add("stump", prop_stump(), True)
    add("mush_red", prop_mushrooms([C["red"][2], C["red"][5], C["red"][6]]), True)
    add("mush_brown", prop_mushrooms([C["brown"][1], C["brown"][4], C["brown"][5]]), True)
    add("mush_glow", prop_mushrooms([C["teal"][3], C["cyan"][6], C["cyan"][7]], glow=True), True)
    add("glow", prop_glow(), True)
    add("fence", prop_fence(), True)
    add("garden_bed", prop_garden_bed(7), True)
    add("cone", prop_cone(), True)
    add("sign", prop_sign(C["brown"][2]), True)
    add("crate", prop_crate(), True)
    add("rope", prop_rope(), True)
    add("basket", prop_basket(), True)
    add("logseat", prop_logseat(), True)


def write():
    tpl = open(META_TPL, encoding="utf-8").read()
    for name, (im, bottom) in SPRITES.items():
        p = os.path.join(OUT, name + ".png")
        im.save(p)
        m = p + ".meta"
        if os.path.exists(m):
            s = open(m, encoding="utf-8").read()
        else:
            s = re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, tpl, count=1)
        s = re.sub(r"alignment: \d+", "alignment: %d" % (7 if bottom else 0), s, count=1)
        open(m, "w", encoding="utf-8").write(s)
    print(len(SPRITES), "sprites ->", OUT)


if __name__ == "__main__":
    make_path(); make_details(); make_props(); write()
