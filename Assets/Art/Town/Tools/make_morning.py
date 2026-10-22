"""Morning town critters and props (pixel art, 16 px per unit), written to Resources/Morning.
Chickens (stand / peck / walk), a ginger cat (sleep / sit / stretch / walk), and a laundry
line with clothes in two flap frames. Usage: python make_morning.py <out_dir> [preview_dir]"""
from PIL import Image
import os, sys

OUT = sys.argv[1]
PREV = sys.argv[2] if len(sys.argv) > 2 else OUT
os.makedirs(OUT, exist_ok=True)

T = (0, 0, 0, 0)
OL = (0x2b, 0x1d, 0x24, 255)       # outline (dark plum, like the town art)
W1 = (0xf6, 0xf1, 0xe6, 255)       # chicken white
W2 = (0xd6, 0xcd, 0xbd, 255)       # chicken shade
RD = (0xd9, 0x3f, 0x3a, 255)       # comb
OR = (0xf0, 0x9a, 0x2a, 255)       # beak / legs
G1 = (0xf0, 0x9a, 0x45, 255)       # cat ginger
G2 = (0xc4, 0x6a, 0x2b, 255)       # cat stripes / shade
G3 = (0xfb, 0xd8, 0xa6, 255)       # cat muzzle / belly
PK = (0xf2, 0x9c, 0xa8, 255)       # nose / ear
WD = (0x8a, 0x5a, 0x3c, 255)       # wood post
WL = (0xb0, 0x7a, 0x50, 255)
RP = (0xe8, 0xdc, 0xc4, 255)       # rope

PAL = {'.': T, 'o': OL, 'w': W1, 's': W2, 'r': RD, 'y': OR, 'g': G1, 'd': G2, 'c': G3, 'p': PK}

def draw(rows, name, pal=PAL):
    h, w = len(rows), max(len(r) for r in rows)
    im = Image.new("RGBA", (w, h), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), pal.get(ch, T))
    im.save(os.path.join(OUT, name + ".png"))
    return im

# ── chicken (facing right), 11 x 10 ──────────────────────────────────────────
chicken_stand = [
    "......r....",
    ".....rro...",
    "....owwo...",
    "....owwoyy.",
    ".oo.owwwo..",
    "owwoowwwwo.",
    "owswwwwswo.",
    ".owsssssso.",
    "..oowwwoo..",
    "....y..y...",
]
chicken_peck = [
    "...........",
    "...........",
    ".oo........",
    "owwoo......",
    "owswwwor...",
    ".owsssworr.",
    "..oowwwwoyy",
    "....oswwo..",
    ".....ooo...",
    "....y..y...",
]
chicken_walk = [
    "......r....",
    ".....rro...",
    "....owwo...",
    "....owwoyy.",
    ".oo.owwwo..",
    "owwoowwwwo.",
    "owswwwwswo.",
    ".owsssssso.",
    "..oowwwoo..",
    "...y....y..",
]
frames = [draw(chicken_stand, "chicken_stand"), draw(chicken_peck, "chicken_peck"), draw(chicken_walk, "chicken_walk")]

# ── cat (facing right) ───────────────────────────────────────────────────────
cat_sleep = [   # curled up, 12 x 7
    "............",
    "...oooooo...",
    "..oggdggdo..",
    ".ogdggggggo.",
    "ogggdggpcgo.",
    "oddgggggcco.",
    ".oooooooooo.",
]
cat_sit = [     # sitting, looking right, 10 x 11
    "......o..o",
    "......opop",
    ".....ogggo",
    ".....ogcgc",
    "......oppo",
    "....oggggo",
    "...ogdgggo",
    "..ogggdgco",
    "o.ogdgggco",
    "dooggggcco",
    ".ooooooooo",
]
cat_stretch = [  # front down, tail up, 14 x 8
    "o.............",
    "do..........o.",
    ".do.......oopo",
    "..oooooooogggo",
    "..ogdgdgdgcgco",
    "..oggggggggppo",
    "...oo.....oo..",
    "...oo.....oo..",
]
cat_walk = [     # walking, 13 x 8
    "o............",
    "do........o.o",
    ".doooooooopop",
    "..ogdgdgdgggo",
    "..oggggggcgco",
    "..ogggggccppo",
    "...oo...oo...",
    "..o.o..o..o..",
]
cats = [draw(cat_sleep, "cat_sleep"), draw(cat_sit, "cat_sit"), draw(cat_stretch, "cat_stretch"), draw(cat_walk, "cat_walk")]

# ── laundry line, 48 x 20, two flap frames ───────────────────────────────────
CLOTHES = [  # (x, width, height, main, shade)
    (7, 7, 8, (0x6f, 0xa8, 0xdc, 255), (0x4d, 0x7f, 0xb2, 255)),   # blue shirt
    (16, 5, 9, (0xf4, 0xf0, 0xe6, 255), (0xcf, 0xc8, 0xb8, 255)),  # white towel
    (23, 6, 7, (0xe0, 0x6a, 0x5a, 255), (0xb4, 0x4c, 0x40, 255)),  # red dress
    (31, 5, 6, (0xf2, 0xc9, 0x4c, 255), (0xc9, 0x9f, 0x2c, 255)),  # yellow shorts
    (38, 4, 8, (0x8b, 0xc3, 0x7a, 255), (0x63, 0x98, 0x55, 255)),  # green towel
]

def laundry(frame):
    w, h = 48, 20
    im = Image.new("RGBA", (w, h), T)
    px = im.load()
    for y in range(2, h):                      # posts
        for x in (1, 2, w - 3, w - 2):
            px[x, y] = OL if x in (1, w - 2) or y == h - 1 else (WL if x == 2 else WD)
    for x in range(1, w - 1):                  # cap
        pass
    for x in (0, 1, 2, 3):
        px[x, 2] = OL; px[w - 1 - x, 2] = OL
    rope = []
    for x in range(3, w - 3):                  # sagging rope
        sag = int(round(2.2 * (1 - ((x - w / 2) / (w / 2 - 3)) ** 2)))
        y = 3 + sag
        px[x, y] = RP
        rope.append(y)
    for i, (cx, cw, ch, main, shade) in enumerate(CLOTHES):
        top = rope[cx - 3] + 1
        sway = (1 if (i + frame) % 2 == 0 else 0)
        for y in range(ch):
            off = sway if y > ch // 2 else 0
            for x in range(cw):
                xx = cx + x + off
                edge = x == 0 or x == cw - 1 or y == ch - 1
                px[xx, top + y] = OL if edge else (shade if (x + y) % 5 == 0 or y > ch - 3 else main)
        px[cx + 1, top - 1] = OL               # pegs
        px[cx + cw - 2, top - 1] = OL
    im.save(os.path.join(OUT, "laundry_%d.png" % frame))
    return im

lines = [laundry(0), laundry(1)]

# ── preview ──────────────────────────────────────────────────────────────────
prev = Image.new("RGBA", (120, 52), (110, 170, 85, 255))
x = 2
for im in frames: prev.alpha_composite(im, (x, 2)); x += 14
for im in cats: prev.alpha_composite(im, (x if x < 100 else 2, 2 if x < 100 else 16)); x += 16
prev.alpha_composite(lines[0], (2, 30)); prev.alpha_composite(lines[1], (60, 30))
prev.resize((prev.width * 6, prev.height * 6), Image.NEAREST).save(os.path.join(PREV, "morning_preview.png"))
print("ok")
