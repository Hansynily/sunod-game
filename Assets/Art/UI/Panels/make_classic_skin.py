"""SUNOD Classic UI skin v2 (2026-11-07): pixel 9-slice art for buttons, panels, cards and
chips, used only in the Classic UI style (vc_ClassicSkin.cs + SunodTheme.tss .sn-classic).

Run:  python -I make_classic_skin.py
Writes Assets/Resources/UIClassic/*.png (small sources; drawn at 3x with point filtering).

Look (after Han's cozy-farm reference): stair-stepped round pixel corners, a thick dark
outline, a light inset line just inside it, banded fills (lighter top band, darker bottom
band) instead of flat colour, a 1px pixel drop shadow, and on buttons a 2-tone bottom lip
(pressed = lip gone, inner shadow on top). Palette = the UI V2 tokens.
"""
import os
from collections import deque
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "Resources", "UIClassic"))
os.makedirs(OUT, exist_ok=True)
CLEAR = (0, 0, 0, 0)
SHADOW = (60, 38, 22, 90)


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def mask(w, h, insets):
    """Rounded pixel rectangle: insets[i] = pixels cut from row i at each corner."""
    m = [[True] * w for _ in range(h)]
    for i, cut in enumerate(insets):
        for x in range(cut):
            for (yy, xx) in [(i, x), (i, w - 1 - x), (h - 1 - i, x), (h - 1 - i, w - 1 - x)]:
                m[yy][xx] = False
    return m


def rings(m):
    """Distance (4-neighbour steps) from outside the shape: 1 = edge pixel."""
    h, w = len(m), len(m[0])
    d = [[0] * w for _ in range(h)]
    q = deque()
    for y in range(h):
        for x in range(w):
            if not m[y][x]:
                continue
            edge = x == 0 or y == 0 or x == w - 1 or y == h - 1 or \
                not m[y - 1][x] or not m[y + 1][x] or not m[y][x - 1] or not m[y][x + 1]
            if edge:
                d[y][x] = 1
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and m[ny][nx] and d[ny][nx] == 0:
                d[ny][nx] = d[y][x] + 1
                q.append((nx, ny))
    return d


def with_shadow(im, w, h, m):
    """Canvas one row taller with a 1px drop shadow under the shape."""
    out = Image.new("RGBA", (w, h + 1), CLEAR)
    px = out.load()
    for y in range(h):
        for x in range(w):
            if m[y][x]:
                px[x, y + 1] = SHADOW
    out.alpha_composite(im, (0, 0))
    return out


# ---------------------------------------------------------------- buttons
BUTTONS = {
    "cream": dict(F=(248, 238, 216), H=(255, 253, 244), R=(234, 218, 188), S=(212, 184, 142), D=(156, 116, 80), O=(84, 56, 38)),
    "green": dict(F=(84, 158, 94), H=(150, 206, 132), R=(72, 140, 82), S=(54, 116, 64), D=(36, 82, 46), O=(26, 54, 32)),
    "gold":  dict(F=(244, 196, 80), H=(255, 236, 158), R=(230, 178, 62), S=(204, 146, 44), D=(146, 96, 28), O=(96, 60, 18)),
    "red":   dict(F=(210, 88, 74), H=(242, 150, 128), R=(194, 76, 64), S=(162, 56, 48), D=(112, 36, 32), O=(72, 24, 22)),
    "night": dict(F=(92, 96, 170), H=(152, 158, 220), R=(82, 86, 156), S=(64, 66, 128), D=(44, 44, 94), O=(26, 26, 60)),
}
BW, BH = 20, 20
B_INS = [3, 1, 1]


def button(p, pressed):
    m = mask(BW, BH, B_INS)
    d = rings(m)
    im = Image.new("RGBA", (BW, BH), CLEAR)
    px = im.load()
    FH = mix(p["F"], p["H"], 0.45)    # light top band
    FL = mix(p["F"], p["S"], 0.35)    # darker low band
    lip_top = BH - 5                  # rows lip_top.. : lip (2 S + 1 D) above the outline row
    for y in range(BH):
        for x in range(BW):
            if not m[y][x]:
                continue
            r = d[y][x]
            if r == 1:
                c = p["O"]
            elif pressed:
                if y <= 3:
                    c = p["S"] if y <= 2 else FL          # pushed in: shadow along the top
                elif y >= BH - 3:
                    c = p["D"] if y == BH - 2 else p["S"]
                elif r == 2 and x >= BW - 3:
                    c = p["R"]
                else:
                    c = p["F"]
            else:
                if y >= lip_top:
                    c = p["D"] if y >= BH - 2 else p["S"]
                elif r == 2 and (y <= 2 or x <= 2):
                    c = p["H"]                              # inset light line, top + left
                elif r == 2 and x >= BW - 3:
                    c = p["R"]
                elif y <= 5:
                    c = FH
                elif y >= lip_top - 3:
                    c = FL
                else:
                    c = p["F"]
            px[x, y] = rgba(c)
    if not pressed:
        px[3, 3] = rgba(p["H"])                             # a glint in the lit corner
    return with_shadow(im, BW, BH, m)


for name, pal in BUTTONS.items():
    button(pal, False).save(os.path.join(OUT, f"btn_{name}.png"))
    button(pal, True).save(os.path.join(OUT, f"btn_{name}_down.png"))

# ---------------------------------------------------------------- panels (wood rim + paper)
PANELS = {
    "panel": dict(O=(66, 42, 28), W=(146, 102, 68), WH=(186, 138, 96), WS=(112, 76, 50), IN=(90, 60, 40),
                  P=(248, 238, 216), PH=(255, 252, 242), PL=(238, 224, 196)),
    "panel_dark": dict(O=(36, 24, 16), W=(98, 66, 46), WH=(132, 94, 66), WS=(72, 48, 32), IN=(46, 32, 22),
                       P=(72, 50, 36), PH=(92, 66, 48), PL=(62, 42, 30)),
}
PW, PH_ = 30, 30
P_INS = [4, 2, 1, 1]


def panel(p):
    m = mask(PW, PH_, P_INS)
    d = rings(m)
    im = Image.new("RGBA", (PW, PH_), CLEAR)
    px = im.load()
    for y in range(PH_):
        for x in range(PW):
            if not m[y][x]:
                continue
            r = d[y][x]
            top_left = y < PH_ // 2 and (y <= x and y <= PW - 1 - x) or (x < PW // 2 and x <= y and x <= PH_ - 1 - y)
            if r == 1:
                c = p["O"]
            elif r == 2:
                c = p["WH"] if top_left else p["W"]
            elif r == 3:
                c = p["W"] if top_left else p["WS"]
            elif r == 4:
                c = p["IN"]
            elif r == 5:
                c = p["PH"] if top_left else p["PL"]     # light inset line on the paper
            elif y >= PH_ - 8:
                c = p["PL"]                              # paper gets a darker low band
            else:
                c = p["P"]
            px[x, y] = rgba(c)
    return with_shadow(im, PW, PH_, m)


for name, pal in PANELS.items():
    panel(pal).save(os.path.join(OUT, f"{name}.png"))

# ---------------------------------------------------------------- cards + chips
SMALL = {
    # card: paper inside a panel, soft outline
    "card": dict(O=(196, 166, 124), H=(255, 255, 250), F=(253, 248, 236), L=(228, 210, 176)),
    "card_sel": dict(O=(46, 108, 60), H=(250, 255, 246), F=(236, 246, 226), L=(170, 210, 156)),
    # chip: small HUD info pill (day, energy), dark outline like the reference's top-right box
    "chip": dict(O=(84, 56, 38), H=(255, 253, 244), F=(248, 238, 216), L=(226, 206, 170)),
}
SW, SH = 14, 14
S_INS = [2, 1]


def small(p):
    m = mask(SW, SH, S_INS)
    d = rings(m)
    im = Image.new("RGBA", (SW, SH), CLEAR)
    px = im.load()
    for y in range(SH):
        for x in range(SW):
            if not m[y][x]:
                continue
            r = d[y][x]
            if r == 1:
                c = p["O"]
            elif y >= SH - 3:
                c = p["L"]                       # low band / lip
            elif r == 2 and (y <= 2 or x <= 2):
                c = p["H"]
            else:
                c = p["F"]
            px[x, y] = rgba(c)
    return with_shadow(im, SW, SH, m)


for name, pal in SMALL.items():
    small(pal).save(os.path.join(OUT, f"{name}.png"))

# ---------------------------------------------------------------- note (board poster, pinned)
NOTE = dict(O=(150, 112, 74), H=(255, 253, 244), F=(250, 242, 222), FL=(238, 224, 194), L=(214, 190, 150),
            PIN=(210, 72, 64), PINH=(246, 150, 136), PIND=(120, 36, 32))
NW, NH = 18, 18


def note(p):
    m = mask(NW, NH, [2, 1])
    d = rings(m)
    im = Image.new("RGBA", (NW, NH), CLEAR)
    px = im.load()
    for y in range(NH):
        for x in range(NW):
            if not m[y][x]:
                continue
            r = d[y][x]
            if r == 1:
                c = p["O"]
            elif y >= NH - 3:
                c = p["L"]
            elif r == 2 and (y <= 2 or x <= 2):
                c = p["H"]
            elif y >= NH - 7:
                c = p["FL"]
            else:
                c = p["F"]
            px[x, y] = rgba(c)
    # two pushpins in the top corners (inside the 5px corner slices)
    for cx in (3, NW - 5):
        for (dx, dy, k) in [(0, 0, "PINH"), (1, 0, "PIN"), (0, 1, "PIN"), (1, 1, "PIND")]:
            px[cx + dx, 2 + dy] = rgba(p[k])
        px[cx + 1, 4] = (60, 38, 22, 120)       # tiny pin shadow
    return with_shadow(im, NW, NH, m)


note(NOTE).save(os.path.join(OUT, "note.png"))

# ---------------------------------------------------------------- badges (Lv / Rec. tags)
BADGES = {
    "badge_green": dict(O=(22, 50, 28), H=(96, 160, 104), F=(46, 108, 60), L=(32, 84, 44)),
    "badge_gold": dict(O=(96, 60, 18), H=(255, 236, 158), F=(244, 196, 80), L=(204, 146, 44)),
    "badge_silver": dict(O=(70, 78, 96), H=(244, 248, 255), F=(206, 214, 228), L=(160, 170, 190)),
}
GW, GH = 10, 10


def badge(p):
    m = mask(GW, GH, [1])
    d = rings(m)
    im = Image.new("RGBA", (GW, GH), CLEAR)
    px = im.load()
    for y in range(GH):
        for x in range(GW):
            if not m[y][x]:
                continue
            r = d[y][x]
            if r == 1:
                c = p["O"]
            elif y >= GH - 3:
                c = p["L"]
            elif r == 2 and y <= 1:
                c = p["H"]
            else:
                c = p["F"]
            px[x, y] = rgba(c)
    return im


for name, pal in BADGES.items():
    badge(pal).save(os.path.join(OUT, f"{name}.png"))

print("wrote", sorted(os.listdir(OUT)))
