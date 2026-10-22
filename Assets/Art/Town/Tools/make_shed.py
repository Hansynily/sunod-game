"""Pixel-art sleeping shed for the SUNOD towns, in the cabin's palette.
Writes shed_base.png (bottom 18 px, sorts with the player) and shed_top.png (roof),
plus shed_sign.png (a small hanging "Zz" board)."""
from PIL import Image
import sys, os

OUT = sys.argv[1]
SP = sys.argv[2] if len(sys.argv) > 2 else OUT

OL  = (0x47, 0x2d, 0x3c, 255)   # outline
DK  = (0x5e, 0x36, 0x43, 255)
MD  = (0x7a, 0x44, 0x4a, 255)
LT  = (0xa0, 0x5b, 0x53, 255)
HI  = (0xbf, 0x79, 0x58, 255)
GL  = (0xee, 0xa1, 0x60, 255)   # lantern glow
PL  = (0xf4, 0xcc, 0xa1, 255)   # pale
GS  = (0x30, 0xa2, 0xc4, 255)   # glass
GS2 = (0x28, 0xcc, 0xdf, 255)
SH  = (0, 0, 0, 70)             # soft shadow
CLR = (0, 0, 0, 0)

W, H = 38, 40
ROOF_H = 21      # roof rows 0..20
im = Image.new("RGBA", (W, H), CLR)
px = im.load()

def put(x, y, c):
    if 0 <= x < W and 0 <= y < H:
        px[x, y] = c

cx = W // 2  # 19
# ── roof: a gable, eaves overhang the walls by 2 px each side ─────────────
for y in range(ROOF_H):
    half = 3 + int(y * (cx - 1) / (ROOF_H - 1))     # half-width grows to the eaves
    x0, x1 = cx - half, cx + half - 1
    for x in range(x0, x1 + 1):
        row = (y - 2) // 3
        seam = (y - 2) % 3 == 2
        if x == x0 or x == x1 or y == ROOF_H - 1:
            c = OL
        elif seam:
            c = DK
        else:
            # plank tone alternates per row, with staggered joints
            c = LT if row % 2 == 0 else MD
            if (x + (row % 2) * 4) % 8 == 0:
                c = DK
        put(x, y, c)
# ridge beam + top light edge
for y in range(0, ROOF_H - 1):
    put(cx - 1, y, HI if y > 0 else OL)
    put(cx, y, LT if y > 0 else OL)
put(cx - 1, 0, OL); put(cx, 0, OL)
for y in range(1, ROOF_H - 1):
    half = 3 + int(y * (cx - 1) / (ROOF_H - 1))
    put(cx - half + 1, y, HI)                # sunlit left eave edge

# ── walls ────────────────────────────────────────────────────────────────
wx0, wx1 = 3, W - 4                          # 3..34
for y in range(ROOF_H, H - 1):
    for x in range(wx0, wx1 + 1):
        if x == wx0 or x == wx1:
            c = OL
        else:
            board = (x - wx0 - 1) // 3
            c = DK if (x - wx0 - 1) % 3 == 2 else (MD if board % 2 else LT)
        put(x, y, c)
# shade under the eaves
for x in range(wx0 + 1, wx1):
    put(x, ROOF_H, DK)
    put(x, ROOF_H + 1, MD if px[x, ROOF_H + 1] == LT else px[x, ROOF_H + 1])
# sill / ground line
for x in range(wx0, wx1 + 1):
    put(x, H - 1, OL)
for x in range(wx0 - 1, wx1 + 2):
    if px[x, H - 1][3] == 0:
        put(x, H - 1, SH)

# ── door (centred) with a crescent moon ──────────────────────────────────
dx0, dx1, dy0 = cx - 5, cx + 4, ROOF_H + 4   # 14..23, from y=25
for y in range(dy0, H - 1):
    for x in range(dx0, dx1 + 1):
        if x in (dx0, dx1) or y == dy0:
            c = HI if y > dy0 else OL
        else:
            c = OL if (x - dx0) % 4 == 0 else DK
        put(x, y, c)
for x in range(dx0, dx1 + 1):
    put(x, dy0, OL)
# moon cut-out
for (mx, my) in [(cx - 1, dy0 + 3), (cx, dy0 + 3), (cx - 2, dy0 + 4), (cx - 2, dy0 + 5),
                 (cx - 1, dy0 + 6), (cx, dy0 + 6)]:
    put(mx, my, PL)
# handle
put(dx1 - 2, dy0 + 8, GL)

# ── little window on the right, warm light inside ────────────────────────
wx, wy = dx1 + 4, ROOF_H + 4
for y in range(wy, wy + 6):
    for x in range(wx, wx + 6):
        edge = x in (wx, wx + 5) or y in (wy, wy + 5)
        put(x, y, OL if edge else (GL if (x + y) % 5 else PL))
put(wx + 2, wy + 1, PL)
for x in range(wx, wx + 6):
    put(x, wy + 6, HI)                       # sill

# ── lantern on the left of the door ──────────────────────────────────────
lx, ly = dx0 - 5, ROOF_H + 3
put(lx + 2, ly - 1, OL)
for x in range(lx, lx + 5): put(x, ly, OL)                          # cap
for y in range(ly + 1, ly + 5):
    for x in range(lx + 1, lx + 4):
        put(x, y, OL if x in (lx + 1, lx + 3) else (PL if y == ly + 2 else GL))
for x in range(lx + 1, lx + 4): put(x, ly + 5, OL)
for (gx, gy) in [(lx, ly + 2), (lx + 4, ly + 2), (lx, ly + 3), (lx + 4, ly + 3)]:
    if px[gx, gy] in (LT, MD): put(gx, gy, HI)                       # warm spill on the boards

# ── firewood stack at the left foot: round log ends ─────────────────────
for (fx, fy) in [(wx0 + 1, H - 5), (wx0 + 5, H - 5), (wx0 + 3, H - 8)]:
    ring = [(fx + 1, fy), (fx + 2, fy), (fx, fy + 1), (fx + 3, fy + 1), (fx, fy + 2), (fx + 3, fy + 2), (fx + 1, fy + 3), (fx + 2, fy + 3)]
    for (x, y) in ring: put(x, y, OL)
    for (x, y) in [(fx + 1, fy + 1), (fx + 2, fy + 1), (fx + 1, fy + 2), (fx + 2, fy + 2)]: put(x, y, PL)
    put(fx + 2, fy + 2, HI)

base = im.crop((0, ROOF_H - 3, W, H))       # 18 px: lower eave edge + walls
top = im.crop((0, 0, W, ROOF_H - 3))         # 18 px roof
# The base sorts with the player; the top draws over them (like the cabin).
base.save(os.path.join(OUT, "shed_base.png"))
top.save(os.path.join(OUT, "shed_top.png"))

# ── "Zz" hanging sign (11 x 9) ───────────────────────────────────────────
s = Image.new("RGBA", (11, 9), CLR)
sp = s.load()
for x in range(11):
    for y in range(2, 9):
        sp[x, y] = OL if x in (0, 10) or y in (2, 8) else LT
sp[2, 0] = OL; sp[2, 1] = OL; sp[8, 0] = OL; sp[8, 1] = OL      # ropes
for (x, y) in [(2, 3), (3, 3), (4, 3), (5, 3), (4, 4), (3, 5), (2, 6), (3, 6), (4, 6), (5, 6)]:
    sp[x, y] = PL                                                   # big Z
for (x, y) in [(7, 5), (8, 5), (8, 6), (7, 7), (8, 7)]:
    sp[x, y] = PL                                                   # small z
sp[7, 6] = LT
s.save(os.path.join(OUT, "shed_sign.png"))

# preview
prev = Image.new("RGBA", (W + 20, H + 12), (120, 170, 90, 255))
prev.alpha_composite(im, (2, 10))
prev.alpha_composite(s, (W + 6, 4))
prev.resize((prev.width * 8, prev.height * 8), Image.NEAREST).save(os.path.join(SP, "shed_preview.png"))
print("ok", base.size, top.size)

# ── open door overlay (10 x 14): drawn over the closed door while the player goes in / out ──
INSIDE = (0x2a, 0x1c, 0x24, 255)
FLOOR = (0x4a, 0x30, 0x38, 255)
d = Image.new("RGBA", (10, 14), CLR)
dp = d.load()
for y in range(14):
    for x in range(10):
        if y == 0:
            dp[x, y] = OL
        elif x in (0, 9):
            dp[x, y] = HI                                   # door frame
        elif x in (1, 2):
            dp[x, y] = DK if y > 1 else OL                  # the door leaf, swung inward
        else:
            dp[x, y] = FLOOR if y >= 11 else INSIDE         # dark room, a little floor light
dp[2, 7] = GL                                               # handle catching the light
for x in range(4, 8):
    dp[x, 3] = MD                                           # the cot's edge inside
    dp[x, 4] = PL if x in (5, 6) else MD                    # pillow
d.save(os.path.join(OUT, "shed_door_open.png"))
print("door ok")
