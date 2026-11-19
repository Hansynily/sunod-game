"""Parametric townsfolk generator - the storybook style (see npc_style.py).

A character is a small spec (build, skin, hair, top, bottom, hat, extras). This module turns
it into material maps for down / up / left (right is mirrored), shades them with the shared
rules and adds faces and details. It also animates: walk (6), idle (6) and sit (6 per side).

Export (per look, same layout the game already loads - see vc_NpcSprites):
  Resources/TownLife/walk_<look>.png  384x32  right(0-5) up(6-11) left(12-17) down(18-23)
  Resources/TownLife/idle_<look>.png  384x32  same order
  Resources/TownLife/sit_<look>.png   384x32  12 frames of 32x32: facing right (0-5), left (6-11)
  Art/Town/NPC/npc_<look>_f0..5.png   16x32   idle facing down (quest givers' body frames)
"""
import os
from PIL import Image
from npc_style import C, INK, PAPER, LINE, EYE_WHITE, W, H, put

SKIN = {"fair": 6, "light": 5, "tan": 5, "morena": 4, "dark": 3}
SKIN_RAMP = {"fair": "skin", "light": "skin", "tan": "skin", "morena": "skin", "dark": "skin"}


def tri(name, i):
    """3 tones (shadow, base, light) around step i of a palette ramp."""
    r = C[name] if name in C else None
    if r is None:
        raise KeyError(name)
    i = max(1, min(6, i))
    return [r[i - 1], r[i], r[i + 1]]


def white():
    return [C["warm"][6], C["warm"][7], PAPER]


# ── geometry ────────────────────────────────────────────────────────────────────
# Head rows (front view): row -> (x0, x1) inclusive. `top` shifts everything for kids.
HEAD_F = {0: (5, 10), 1: (4, 11), 2: (3, 12), 3: (3, 12), 4: (3, 12), 5: (3, 12), 6: (3, 12),
          7: (3, 12), 8: (4, 11), 9: (5, 10)}
HEAD_S = {0: (6, 10), 1: (5, 11), 2: (4, 12), 3: (4, 12), 4: (4, 12), 5: (3, 12), 6: (3, 12),
          7: (3, 11), 8: (4, 11), 9: (5, 10)}
NECK_F, NECK_S = (6, 9), (6, 8)

BUILDS = {
    # head top row, neck row, shoulder row, hand row, hip row (first bottom row), leg row, feet row
    "adult": dict(head=3, neck=13, sh=14, hand=21, hip=22, leg=25, feet=29, wide=False),
    "man":   dict(head=3, neck=13, sh=14, hand=21, hip=22, leg=25, feet=29, wide=True),
    "kid":   dict(head=8, neck=18, sh=19, hand=24, hip=24, leg=26, feet=29, wide=False),
}


class Canvas:
    def __init__(self):
        self.m = [["."] * W for _ in range(H)]

    def set(self, x, y, k):
        if 0 <= x < W and 0 <= y < H:
            self.m[y][x] = k

    def get(self, x, y):
        return self.m[y][x] if 0 <= x < W and 0 <= y < H else "."

    def row(self, y, x0, x1, k):
        for x in range(x0, x1 + 1):
            self.set(x, y, k)

    def rect(self, x0, y0, x1, y1, k):
        for y in range(y0, y1 + 1):
            self.row(y, x0, x1, k)

    def rows(self):
        return ["".join(r) for r in self.m]


# ── body ─────────────────────────────────────────────────────────────────────────

def body_front(cv, s, b, legs="stand", arm=0, back=False):
    g = BUILDS[b]
    wide = g["wide"]
    sh, hand, hip, leg, feet = g["sh"], g["hand"], g["hip"], g["leg"], g["feet"]
    kid = b == "kid"
    tx0, tx1 = (5, 10) if kid else (4, 11)
    top, bot = s["top"], s["bottom"]
    # torso
    cv.rect(tx0, sh, tx1, hip - 1, "T")
    cv.row(sh, tx0 + 1, tx1 - 1, "T")
    # arms (sleeve 'T' or 'A' long sleeve / skin 'S')
    ax = [(tx0 - 2, tx0 - 1), (tx1 + 1, tx1 + 2)] if wide else [(tx0 - 1, tx0 - 1), (tx1 + 1, tx1 + 1)]
    sleeve = {"sando": 0, "tshirt": 2, "blouse": 2, "polo": 2, "dress": 2, "uniform": 2,
              "long": 99, "coat": 99, "barong": 99, "jacket": 99}[top]
    for side, (a0, a1) in enumerate(ax):
        d = arm if side == 0 else -arm
        end = hand - (1 if d > 0 else 0) + (1 if d < 0 else 0)
        for y in range(sh + 1, end + 1):
            k = "T" if (y - sh) <= sleeve and y < end else "S"
            if top == "sando" and y == sh + 1:
                k = "S"
            cv.row(y, a0, a1, k)
    # bottom
    if bot in ("dress",):
        cv.rect(tx0, hip, tx1, leg - 1, "T")
        cv.row(leg - 1, tx0 - 1, tx1 + 1, "T")
    elif bot == "skirt":
        cv.rect(tx0, hip, tx1, leg - 1, "B")
        cv.row(leg - 1, tx0 - 1, tx1 + 1, "B")
    else:  # pants / shorts
        cv.rect(tx0, hip, tx1, leg - 1, "B")
    if top in ("coat", "barong") and not kid:      # long top hangs over the hips
        cv.rect(tx0, hip, tx1, hip + 1, "T")
    # legs
    lx = [(5, 6), (9, 10)] if not kid else [(6, 6), (9, 9)]
    lift = {"stand": (0, 0), "l": (1, 0), "r": (0, 1)}[legs]
    if back:
        lift = (lift[1], lift[0])
    for i, (x0, x1) in enumerate(lx):
        up = lift[i]
        k = "B" if bot == "pants" else "L"
        for y in range(leg, feet - up):
            cv.row(y, x0, x1, k)
        cv.row(feet - up, x0 - (1 if i == 0 else 0), x1 + (1 if i == 1 else 0), "F")


def body_side(cv, s, b, legs="stand", arm=0):
    g = BUILDS[b]
    sh, hand, hip, leg, feet = g["sh"], g["hand"], g["hip"], g["leg"], g["feet"]
    kid = b == "kid"
    tx0, tx1 = (6, 9) if kid else (5, 10)
    if g["wide"]:
        tx0, tx1 = 5, 11
    top, bot = s["top"], s["bottom"]
    cv.rect(tx0, sh, tx1, hip - 1, "T")
    # near arm in front of the torso
    ax = tx0 + 2 + (-1 if arm > 0 else 1 if arm < 0 else 0)
    sleeve = {"sando": 0, "tshirt": 2, "blouse": 2, "polo": 2, "dress": 2, "uniform": 2,
              "long": 99, "coat": 99, "barong": 99, "jacket": 99}[top]
    for y in range(sh + 1, hand + 1):
        k = "A" if (y - sh) <= sleeve and y < hand else "S"
        cv.set(ax, y, k)
    if bot in ("dress", "skirt"):
        k = "T" if bot == "dress" else "B"
        cv.rect(tx0, hip, tx1, leg - 1, k)
        cv.row(leg - 1, tx0 - 1, tx1 + 1, k)
    else:
        cv.rect(tx0, hip, tx1, leg - 1, "B")
    if top in ("coat", "barong") and not kid:
        cv.rect(tx0, hip, tx1, hip + 1, "T")
    k = "B" if bot == "pants" else "L"
    pos = {"stand": [(6, 7), (8, 9)], "stride": [(5, 6), (9, 10)], "pass": [(7, 8), (7, 8)]}[legs]
    if kid:
        pos = {"stand": [(7, 7), (8, 8)], "stride": [(6, 6), (9, 9)], "pass": [(7, 8), (7, 8)]}[legs]
    for (x0, x1) in pos:
        for y in range(leg, feet):
            cv.row(y, x0, x1, k)
        cv.row(feet, x0 - 1, x1, "F")


def head(cv, s, b, view):
    g = BUILDS[b]
    t = g["head"]
    rows = HEAD_S if view == "left" else HEAD_F
    for dy, (x0, x1) in rows.items():
        cv.row(t + dy, x0, x1, "S")
    nx = NECK_S if view == "left" else NECK_F
    cv.row(g["neck"], nx[0], nx[1], "S")
    if view == "left":
        cv.set(2, t + 6, "S")                                  # nose


def hair(cv, s, b, view):
    st = s["hair"]
    t = BUILDS[b]["head"]
    rows = HEAD_S if view == "left" else HEAD_F
    if st == "none":
        return

    def fill_head(r0, r1, cond=lambda x, y: True):
        for dy in range(r0, r1 + 1):
            if dy in rows:
                x0, x1 = rows[dy]
                for x in range(x0, x1 + 1):
                    if cond(x, dy):
                        cv.set(x, t + dy, "H")

    if view == "up":
        last = {"long": 12, "bob": 8, "bun": 8, "pony": 8}.get(st, 7)
        fill_head(0, min(9, last))
        if st == "long":
            cv.rect(3, t + 8, 12, t + 12, "H")
        if st == "bun":
            cv.rect(6, t - 2, 9, t - 1, "H")
        if st == "pony":
            cv.rect(7, t + 8, 8, t + 13, "H")
        if st == "spiky":
            for x in (4, 6, 8, 10):
                cv.set(x, t - 1, "H")
        if st in ("receding",):
            pass
        return

    if view == "down":
        hl = {"short": 3, "spiky": 3, "bun": 3, "pony": 3, "long": 3, "bob": 3, "bangs": 4,
              "receding": 2, "curly": 3, "crop": 2}[st]
        fill_head(0, hl - 1)
        if st == "receding":
            for x in (5, 6, 7, 8, 9, 10):
                cv.set(x, t + 0, "S")
        # sides
        side_to = {"long": 9, "bob": 7, "bun": 5, "pony": 5, "curly": 6, "bangs": 6}.get(st, 4)
        for dy in range(hl, side_to + 1):
            if dy in rows:
                x0, x1 = rows[dy]
                cv.set(x0, t + dy, "H"); cv.set(x1, t + dy, "H")
        if st == "long":
            cv.rect(2, t + 4, 2, t + 12, "H"); cv.rect(13, t + 4, 13, t + 12, "H")
            cv.rect(3, t + 8, 3, t + 12, "H"); cv.rect(12, t + 8, 12, t + 12, "H")
        if st == "bob":
            cv.rect(2, t + 3, 2, t + 7, "H"); cv.rect(13, t + 3, 13, t + 7, "H")
        if st == "curly":
            for x in (2, 13):
                cv.rect(x, t + 1, x, t + 6, "H")
            for x in range(4, 12, 2):
                cv.set(x, t - 1, "H")
        if st == "bun":
            cv.rect(6, t - 2, 9, t - 1, "H")
        if st == "spiky":
            for x in (4, 6, 8, 10):
                cv.set(x, t - 1, "H")
        return

    # left (facing left): hair over the back of the head, face to the left
    hl = {"short": 3, "spiky": 3, "bun": 3, "pony": 3, "long": 3, "bob": 3, "bangs": 4,
          "receding": 2, "curly": 3, "crop": 2}[st]
    fill_head(0, hl - 1)
    back_from = {"long": 6, "bob": 6, "bun": 7, "pony": 7, "curly": 6, "bangs": 6, "receding": 8,
                 "crop": 8, "short": 7, "spiky": 7}[st]
    last = {"long": 9, "bob": 8, "curly": 8}.get(st, 7)
    fill_head(hl, last, lambda x, y: x >= back_from)
    if st == "long":
        cv.rect(9, t + 7, 12, t + 12, "H")
    if st == "bun":
        cv.rect(8, t - 2, 11, t - 1, "H")
    if st == "pony":
        cv.rect(12, t + 3, 13, t + 9, "H")
    if st == "curly":
        cv.rect(13, t + 1, 13, t + 6, "H")
    if st == "spiky":
        for x in (5, 7, 9, 11):
            cv.set(x, t - 1, "H")


def hat(cv, s, b, view):
    hw = s.get("hat")
    if not hw:
        return
    t = BUILDS[b]["head"]
    if hw == "salakot":
        cv.rect(7, t - 2, 8, t - 2, "K"); cv.rect(6, t - 1, 9, t - 1, "K")
        cv.rect(4, t, 11, t, "K"); cv.rect(2, t + 1, 13, t + 1, "K")
        cv.row(t + 2, 1, 14, "K")
    elif hw == "cap":
        cv.rect(4, t - 1, 11, t + 1, "K"); cv.row(t - 2, 5, 10, "K")
        if view == "down":
            cv.row(t + 2, 3, 12, "K")
        elif view == "left":
            cv.row(t + 2, 1, 7, "K")
    elif hw == "hardhat":
        cv.row(t - 2, 5, 10, "K"); cv.rect(4, t - 1, 11, t + 1, "K"); cv.row(t + 2, 2, 13, "K")
    elif hw == "beret":
        cv.row(t - 2, 5, 11, "K"); cv.rect(3, t - 1, 12, t + 1, "K"); cv.set(8, t - 3, "K")
    elif hw == "bucket":
        cv.rect(4, t - 2, 11, t + 1, "K"); cv.row(t + 2, 2, 13, "K")
    elif hw == "kerchief":
        cv.rect(3, t - 1, 12, t + 1, "K"); cv.row(t - 2, 5, 10, "K")
        if view == "up":
            cv.rect(7, t + 2, 8, t + 3, "K")


def build_map(s, view, legs="stand", arm=0):
    b = s["build"]
    cv = Canvas()
    if view == "left":
        body_side(cv, s, b, legs, arm)
    else:
        body_front(cv, s, b, legs, arm, back=(view == "up"))
    head(cv, s, b, view)
    hair(cv, s, b, view)
    hat(cv, s, b, view)
    return cv.rows()


# ── shading (same rules as npc_style.render, with per-character ramps) ─────────────

def shade(rows, ramp, far="HKTBA", cast="STA"):
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
            if up not in (".", k) and k in cast and not (k == "T" and up == "S"):
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
            if l in "LFB" and r in "LFB" and l != "." and r != "." and y >= 25:
                continue
            px[x, y] = LINE + (255,)
    return img


def ramps(s):
    sk = s.get("skin", "tan")
    R = {
        "S": tri("skin", SKIN[sk]),
        "L": tri("skin", SKIN[sk] - 1),
        "H": s["hair_col"],
        "T": s["top_col"],
        "A": s["top_col"],
        "B": s["bottom_col"],
        "F": s.get("shoe_col", tri("cool", 2)),
        "K": s.get("hat_col", tri("amber", 5)),
    }
    if s["top"] in ("long", "coat", "barong", "jacket"):
        R["A"] = s["top_col"]
    return R


# ── faces and extras ──────────────────────────────────────────────────────────

def face(img, s, view, blink=False):
    b = s["build"]
    t = BUILDS[b]["head"]
    R = ramps(s)
    S, Hh = R["S"], R["H"]
    brow = Hh[0] if s["hair"] != "none" else S[0]
    ey = t + 5
    if s.get("hat") in ("salakot", "hardhat", "bucket"):
        for x in range(4, 12) if view == "down" else range(3, 9):
            if img.getpixel((x, t + 3))[:3] in [tuple(c) for c in S]:
                put(img, [(x, t + 3)], S[0])                  # brim shadow on the brow
    mood = s.get("mood", "smile")
    if view == "down" and mood in ("happy", "nod") and not blink:
        put(img, [(5, ey - 1), (6, ey - 1), (9, ey - 1), (10, ey - 1)], brow)
        put(img, [(4, ey), (7, ey), (8, ey), (11, ey)], S[1])
        put(img, [(5, ey), (6, ey), (9, ey), (10, ey)], INK)    # closed, curved happy eyes
        blink = True
        happy_eyes = True
    else:
        happy_eyes = False
    if view == "down" and happy_eyes:
        pass
    elif view == "down":
        if blink:
            put(img, [(5, ey), (6, ey), (9, ey), (10, ey)], S[0])
        else:
            put(img, [(5, ey - 1), (6, ey - 1), (9, ey - 1), (10, ey - 1)], brow)
            put(img, [(5, ey), (10, ey)], EYE_WHITE)
            put(img, [(6, ey), (9, ey)], INK)
        if s.get("glasses"):
            gc = s.get("glasses_col", C["cool"][1])
            put(img, [(4, ey), (7, ey), (8, ey), (11, ey)], gc)
        if s.get("cheeks", True):
            put(img, [(4, ey + 1), (11, ey + 1)], C["pink"][5 if s.get("skin") in ("morena", "dark") else 6])
        put(img, [(7, ey + 1)], S[0])                          # nose
        if s.get("moustache"):
            put(img, [(5, ey + 2), (6, ey + 2), (7, ey + 2), (8, ey + 2), (9, ey + 2), (10, ey + 2)], s["moustache"])
        elif mood in ("happy", "sing"):
            put(img, [(7, ey + 2), (8, ey + 2)], C["red"][3])    # open mouth
            put(img, [(7, ey + 3), (8, ey + 3)], C["red"][4])
        elif mood == "sad":
            put(img, [(7, ey + 2), (8, ey + 2)], S[0])
            put(img, [(6, ey + 3), (9, ey + 3)], S[0])         # frown
            put(img, [(5, ey - 1), (10, ey - 1)], S[1])        # brows tilt up in the middle
            put(img, [(5, ey + 1), (10, ey + 1)], C["blue"][6])  # a tear-ish shine
        elif mood == "neutral":
            put(img, [(7, ey + 3), (8, ey + 3)], S[0])
        else:
            put(img, [(6, ey + 2), (9, ey + 2)], S[0])
            put(img, [(7, ey + 3), (8, ey + 3)], S[0])         # smile
        if s.get("beard"):
            put(img, [(5, ey + 3), (6, ey + 3), (9, ey + 3), (10, ey + 3), (6, ey + 4), (7, ey + 4), (8, ey + 4), (9, ey + 4)], s["beard"])
    elif view == "left":
        put(img, [(3, ey - 1), (4, ey - 1)], brow)
        if blink:
            put(img, [(3, ey)], S[0])
        else:
            put(img, [(3, ey)], INK); put(img, [(4, ey)], EYE_WHITE)
        if s.get("glasses"):
            put(img, [(2, ey), (5, ey)], s.get("glasses_col", C["cool"][1]))
        put(img, [(2, ey + 1)], S[2])                          # nose tip
        if s.get("moustache"):
            put(img, [(2, ey + 2), (3, ey + 2), (4, ey + 2)], s["moustache"])
        put(img, [(8, ey), (8, ey + 1)], S[0])                 # ear
        if s.get("cheeks", True):
            put(img, [(5, ey + 2)], C["pink"][5 if s.get("skin") in ("morena", "dark") else 6])
        if s.get("beard"):
            put(img, [(3, ey + 3), (4, ey + 3), (5, ey + 3), (4, ey + 4)], s["beard"])
    # hair shine
    if s["hair"] not in ("none",) and view in ("down", "up"):
        hx = [(4, t + 1), (5, t), (6, t)] if not s.get("hat") else []
        for p in hx:
            if img.getpixel(p)[:3] == tuple(Hh[1]) or img.getpixel(p)[:3] == tuple(Hh[2]):
                put(img, [p], Hh[2])


def extras(img, s, view):
    b = s["build"]
    g = BUILDS[b]
    sh, hip = g["sh"], g["hip"]
    for ex in s.get("extras", []):
        kind = ex[0]
        if kind == "apron" and view in ("down", "left"):
            col = ex[1]
            if view == "down":
                for y in range(sh + 4, hip + 2):
                    for x in range(6, 10):
                        if img.getpixel((x, y))[3]:
                            put(img, [(x, y)], col[1] if x < 9 else col[0])
            else:
                for y in range(sh + 4, hip + 2):
                    if img.getpixel((4 if b != "kid" else 5, y))[3]:
                        put(img, [(4 if b != "kid" else 5, y)], col[1])
        elif kind == "tie" and view == "down":
            put(img, [(7, sh + 1), (8, sh + 1)], ex[1][0])
            for y in range(sh + 2, sh + 6):
                put(img, [(7, y)], ex[1][1]); put(img, [(8, y)], ex[1][0])
        elif kind == "collar" and view == "down":
            put(img, [(6, sh), (9, sh)], ex[1][2]); put(img, [(7, sh), (8, sh)], ex[1][1])
        elif kind == "open" and view == "down":                # open coat over an inner shirt
            for y in range(sh + 1, hip + 2):
                put(img, [(7, y), (8, y)], ex[1][1] if y > sh + 1 else ex[1][2])
        elif kind == "stripe" and view in ("down", "up", "left"):
            y = sh + ex[2]
            for x in range(16):
                p = img.getpixel((x, y))
                if p[3] and p[:3] in [tuple(c) for c in s["top_col"]]:
                    put(img, [(x, y)], ex[1][1])
        elif kind == "dots" and view in ("down", "up", "left"):
            for (x, y) in ((5, sh + 3), (9, sh + 5), (6, sh + 7), (10, sh + 2), (4, sh + 8), (8, hip + 1), (11, hip + 2)):
                if 0 <= y < H and img.getpixel((x, y))[:3] in [tuple(c) for c in s["top_col"]]:
                    put(img, [(x, y)], ex[1])
        elif kind == "towel" and view == "down":
            for y in range(sh, sh + 5):
                put(img, [(10, y)], white()[1]); put(img, [(11, y)], white()[0])
        elif kind == "lanyard" and view == "down":
            put(img, [(6, sh + 1), (9, sh + 1), (7, sh + 2), (8, sh + 2)], ex[1])
            put(img, [(7, sh + 3), (8, sh + 3), (7, sh + 4), (8, sh + 4)], PAPER)
        elif kind == "badge" and view == "down":
            put(img, [(5, sh + 2)], ex[1])
        elif kind == "belt" and view in ("down", "left"):
            for x in range(16):
                p = img.getpixel((x, hip))
                if p[3] and p[:3] != tuple(LINE):
                    put(img, [(x, hip)], ex[1])
        elif kind == "crown" and view in ("down", "up", "left"):
            t = g["head"]
            put(img, [(5, t - 2), (7, t - 2), (9, t - 2), (11, t - 2)], C["amber"][6])
            for x in range(5, 12):
                put(img, [(x, t - 1)], C["amber"][5])
            put(img, [(8, t - 1)], C["red"][5])
        elif kind == "leafcrown" and view in ("down", "up", "left"):
            t = g["head"]
            for x in range(3, 13):
                put(img, [(x, t)], C["green"][4] if x % 2 else C["lime"][5])
            put(img, [(6, t - 1), (10, t - 1)], C["pink"][6])
        elif kind == "cape" and view == "up":
            for y in range(g["sh"], g["hip"] + 2):
                for x in range(4, 12):
                    if img.getpixel((x, y))[3] and img.getpixel((x, y))[:3] != tuple(LINE):
                        put(img, [(x, y)], ex[1][1] if x < 9 else ex[1][0])
        elif kind == "flower" and view in ("down", "up", "left"):
            t = g["head"]
            x = {"down": 11, "up": 4, "left": 11}[view]
            put(img, [(x, t), (x + 1, t)], PAPER); put(img, [(x, t + 1)], C["amber"][6])


# ── frames ───────────────────────────────────────────────────────────────────

def sprite(s, view, legs="stand", arm=0, blink=False):
    lg = legs
    if view == "left":
        lg = {"stand": "stand", "l": "stride", "r": "pass"}.get(legs, legs)
    rows = build_map(s, view, lg, arm)
    img = shade(rows, ramps(s))
    extras(img, s, view)
    face(img, s, view, blink)
    return img


def dip(img, by=1, split=25):
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(img.crop((0, split, W, H)), (0, split))
    out.alpha_composite(img.crop((0, 0, W, split)), (0, by))
    return out


def walk_frames(s, view):
    if view == "left":
        seq = [("stand", 0, 0), ("stride", 1, 1), ("pass", 0, 0), ("stand", 0, 0), ("stride", -1, 1), ("pass", 0, 0)]
        return [dip(sprite(s, view, l, a), b) if b else sprite(s, view, l, a) for l, a, b in seq]
    seq = [("stand", 0, 0), ("l", 1, 0), ("l", 1, 1), ("stand", 0, 0), ("r", -1, 0), ("r", -1, 1)]
    return [dip(sprite(s, view, l, a), b) if b else sprite(s, view, l, a) for l, a, b in seq]


def idle_frames(s, view):
    base = sprite(s, view)
    return [base, base, dip(base), dip(base), base, sprite(s, view, blink=(view != "up"))]


def sit_frames(s):
    """Seated, facing left, in a 32x32 frame (feet at the bottom). Right = mirrored."""
    g = BUILDS[s["build"]]
    up = sprite(s, "left")
    seat = up.crop((0, 0, W, g["hip"] + 1))                    # head + torso + hips
    shut = sprite(s, "left", blink=True).crop((0, 0, W, g["hip"] + 1))
    R = ramps(s)
    frames = []
    for i in range(6):
        src = shut if i == 5 else seat
        f = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        drop = 32 - 6 - src.height                             # thighs + shins under the hips
        f.alpha_composite(src, (8, drop + (1 if i in (2, 3) else 0)))
        hy = drop + src.height
        legk = R["B"] if s["bottom"] == "pants" else R["L"]
        for y in (hy, hy + 1):                                 # thighs forward
            for x in range(6, 18):
                f.putpixel((x, y), legk[1 if y == hy else 0] + (255,))
        for y in range(hy + 2, 31):                            # shins down
            for x in (6, 7):
                f.putpixel((x, y), legk[0 if x == 7 else 1] + (255,))
        for x in (4, 5, 6, 7):
            f.putpixel((x, 31), R["F"][1] + (255,))
        if s["bottom"] in ("dress", "skirt"):
            kk = R["T"] if s["bottom"] == "dress" else R["B"]
            for x in range(9, 18):
                f.putpixel((x, hy), kk[1] + (255,))
        # outline around the new legs
        px = f.load()
        filled = {(x, y) for y in range(32) for x in range(32) if px[x, y][3]}
        for (x, y) in list(filled):
            if y < hy:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (x + dx, y + dy)
                if 0 <= q[0] < 32 and 0 <= q[1] < 32 and q not in filled:
                    px[q] = LINE + (255,)
        frames.append(f)
    return frames


def export(s, look, root):
    """Writes the game's sheets for one look."""
    walk = {v: walk_frames(s, v) for v in ("down", "up", "left")}
    idle = {v: idle_frames(s, v) for v in ("down", "up", "left")}
    for d in (walk, idle):
        d["right"] = [im.transpose(Image.FLIP_LEFT_RIGHT) for im in d["left"]]
    tl = os.path.join(root, "Assets", "Resources", "TownLife")
    for name, d in (("walk", walk), ("idle", idle)):
        sheet = Image.new("RGBA", (W * 24, H), (0, 0, 0, 0))
        for i, v in enumerate(("right", "up", "left", "down")):
            for j, im in enumerate(d[v]):
                sheet.alpha_composite(im, ((i * 6 + j) * W, 0))
        sheet.save(os.path.join(tl, f"{name}_{look}.png"))
    sit_l = sit_frames(s)
    sit_r = [im.transpose(Image.FLIP_LEFT_RIGHT) for im in sit_l]
    sheet = Image.new("RGBA", (32 * 12, 32), (0, 0, 0, 0))
    for j, im in enumerate(sit_r + sit_l):
        sheet.alpha_composite(im, (j * 32, 0))
    sheet.save(os.path.join(tl, f"sit_{look}.png"))
    npc = os.path.join(root, "Assets", "Art", "Town", "NPC")
    for j, im in enumerate(idle["down"]):
        im.save(os.path.join(npc, f"npc_{look}_f{j}.png"))
    return walk, idle, sit_l
