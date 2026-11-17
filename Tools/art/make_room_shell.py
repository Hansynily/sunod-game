"""Draws the wall tiles that turn an indoor quest room into a real building interior.

16x16 tiles, Umaga palette, written to Assets/Resources/RoomShell (loaded by vc_RoomDressing):
  <theme>_top.png      upper half of the back wall (plaster + crown moulding)
  <theme>_window.png   upper half with a capiz-style window
  <theme>_bot.png      lower half (wainscot + baseboard)
  wood_a/b.png         plank floor (homes and shops)
  tile_a/b.png         light floor tile, tinted per room in the game
  rug_<part>.png       9 pieces of an area rug (tl t tr l c r bl b br), tinted per room
  deck_<part>.png      9 pieces of a raised plank stage for the outdoor fair rooms
  dapple.png           a soft patch of morning sun for outdoor rooms
  cap.png              the top of a wall seen from above
  shade.png            soft shadow the back wall drops on the floor
Themes: house (cream plaster, wood wainscot), school (cream plaster, green band).

Run from the sunod-game folder:  python Tools/art/make_room_shell.py
"""
import os, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sunod_palette import C, INK, PAPER

OUT = "Assets/Resources/RoomShell"
os.makedirs(OUT, exist_ok=True)
T = 16
THEMES = {
    "house":  dict(plaster=C["warm"][7], plaster_s=C["warm"][6], trim=C["brown"][4], trim_d=C["brown"][2],
                   band=C["brown"][4], band_l=C["brown"][5], band_d=C["brown"][3], base=C["brown"][1]),
    "school": dict(plaster=C["warm"][7], plaster_s=C["warm"][6], trim=C["green"][3], trim_d=C["green"][2],
                   band=C["green"][4], band_l=C["green"][5], band_d=C["green"][3], base=C["green"][1]),
}


def tile(fill):
    return Image.new("RGBA", (T, T), fill + (255,))


def rect(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            im.putpixel((x, y), c + (255,))


def top(t, window=False):
    im = tile(t["plaster"])
    rect(im, 0, 0, 15, 1, t["trim_d"])          # where the wall meets the ceiling
    rect(im, 0, 2, 15, 3, t["trim"])
    rect(im, 0, 4, 15, 4, t["plaster_s"])
    for x, y in ((3, 8), (11, 6), (7, 12), (13, 13), (1, 14)):   # plaster texture
        im.putpixel((x, y), t["plaster_s"] + (255,))
    if window:
        rect(im, 2, 6, 13, 15, t["trim_d"])     # frame
        rect(im, 3, 7, 12, 14, C["cyan"][7])
        for x in (5, 6, 9, 10):                  # capiz lattice
            pass
        for gx in (6, 9):
            rect(im, gx, 7, gx, 14, t["trim"])
        for gy in (9, 12):
            rect(im, 3, gy, 12, gy, t["trim"])
        for x, y in ((3, 7), (4, 7), (3, 8), (7, 10), (10, 13)):  # morning light on the panes
            im.putpixel((x, y), PAPER + (255,))
    return im


def bot(t):
    im = tile(t["band"])
    rect(im, 0, 0, 15, 0, t["band_l"])          # rail
    rect(im, 0, 1, 15, 1, t["band_d"])
    for x in (3, 7, 11, 15):                     # plank seams
        rect(im, x, 2, x, 11, t["band_d"])
    rect(im, 0, 12, 15, 14, t["base"])          # baseboard
    rect(im, 0, 12, 15, 12, t["band_d"])
    rect(im, 0, 15, 15, 15, INK)
    return im


for name, t in THEMES.items():
    top(t).save(os.path.join(OUT, name + "_top.png"))
    top(t, True).save(os.path.join(OUT, name + "_window.png"))
    bot(t).save(os.path.join(OUT, name + "_bot.png"))

cap = tile(C["violet"][1])
cap.save(os.path.join(OUT, "cap.png"))

shade = Image.new("RGBA", (T, T), (0, 0, 0, 0))
for y, a in enumerate((110, 80, 55, 30, 14)):
    for x in range(T):
        shade.putpixel((x, y), INK + (a,))
shade.save(os.path.join(OUT, "shade.png"))

white = Image.new("RGBA", (4, 4), (255, 255, 255, 255))
white.save(os.path.join(OUT, "white.png"))
print("room shell tiles written to", OUT)


# ---- floors -------------------------------------------------------------------------------
def wood(shift):
    im = tile(C["brown"][5])
    for row in range(4):                      # four planks per tile
        y = row * 4
        rect(im, 0, y + 3, 15, y + 3, C["brown"][4])
        sx = (shift + row * 7) % 16           # staggered butt joints
        rect(im, sx, y, sx, y + 2, C["brown"][4])
        im.putpixel(((sx + 5) % 16, y + 1), C["brown"][6] + (255,))
        im.putpixel(((sx + 11) % 16, y), C["brown"][6] + (255,))
    return im


def floor_tile(speck):
    base, grout, lite = (240, 236, 228), (212, 206, 196), (250, 248, 242)
    im = tile(base)
    rect(im, 0, 15, 15, 15, grout)
    rect(im, 15, 0, 15, 15, grout)
    rect(im, 0, 0, 14, 0, lite)
    rect(im, 0, 0, 0, 14, lite)
    for x, y in speck:
        im.putpixel((x, y), grout + (255,))
    return im


def rug(part):
    body, edge, line, dot = (236, 232, 224), (176, 170, 164), (208, 202, 194), (218, 213, 205)
    im = tile(body)
    im.putpixel((7, 7), dot + (255,)); im.putpixel((8, 8), dot + (255,))
    im.putpixel((8, 7), line + (255,)); im.putpixel((7, 8), line + (255,))
    L, R, U, D = "l" in part, "r" in part, "t" in part, "b" in part
    if U:
        rect(im, 0, 0, 15, 0, edge); rect(im, 0, 2, 15, 3, line)
    if D:
        rect(im, 0, 15, 15, 15, edge); rect(im, 0, 12, 15, 13, line)
    if L:
        rect(im, 0, 0, 0, 15, edge); rect(im, 2, 0, 3, 15, line)
    if R:
        rect(im, 15, 0, 15, 15, edge); rect(im, 12, 0, 13, 15, line)
    if U:
        rect(im, 0, 1, 15, 1, body)
    if D:
        rect(im, 0, 14, 15, 14, body)
    if L:
        rect(im, 1, 0, 1, 15, body)
    if R:
        rect(im, 14, 0, 14, 15, body)
    if U:
        rect(im, 0, 0, 15, 0, edge)
    if D:
        rect(im, 0, 15, 15, 15, edge)
    if L:
        rect(im, 0, 0, 0, 15, edge)
    if R:
        rect(im, 15, 0, 15, 15, edge)
    return im


wood(3).save(os.path.join(OUT, "wood_a.png"))
wood(10).save(os.path.join(OUT, "wood_b.png"))
floor_tile(((4, 9),)).save(os.path.join(OUT, "tile_a.png"))
floor_tile(((10, 4), (6, 12))).save(os.path.join(OUT, "tile_b.png"))
for part in ("tl", "t", "tr", "l", "c", "r", "bl", "b", "br"):
    rug(part).save(os.path.join(OUT, "rug_" + part + ".png"))
print("floors written")


# ---- outdoor rooms ------------------------------------------------------------------------
def deck(part):
    """A raised plank stage: lit rim on top and left, a front face along the bottom."""
    br = C["brown"]
    im = tile(br[5])
    for row in range(4):
        y = row * 4
        rect(im, 0, y + 3, 15, y + 3, br[4])
        sx = (5 + row * 7) % 16
        rect(im, sx, y, sx, y + 2, br[4])
        im.putpixel(((sx + 6) % 16, y + 1), br[6] + (255,))
    if "t" in part:
        rect(im, 0, 0, 15, 0, br[2]); rect(im, 0, 1, 15, 1, br[7])
    if "l" in part:
        rect(im, 0, 0, 0, 15, br[2]); rect(im, 1, 1 if "t" in part else 0, 1, 15, br[7])
    if "r" in part:
        rect(im, 15, 0, 15, 15, br[2]); rect(im, 14, 1 if "t" in part else 0, 14, 15, br[4])
    if "b" in part:                           # the front face of the platform
        rect(im, 0, 10, 15, 10, br[7])
        rect(im, 0, 11, 15, 14, br[3])
        for x in (3, 9, 14):
            rect(im, x, 11, x, 14, br[2])
        rect(im, 0, 15, 15, 15, INK)
        if "l" in part:
            rect(im, 0, 10, 0, 15, br[1])
        if "r" in part:
            rect(im, 15, 10, 15, 15, br[1])
    return im


for part in ("tl", "t", "tr", "l", "c", "r", "bl", "b", "br"):
    deck(part).save(os.path.join(OUT, "deck_" + part + ".png"))

# sun dapple: a stepped, dithered blob (white, tinted and faded in the game)
DW, DH = 28, 18
dap = Image.new("RGBA", (DW, DH), (0, 0, 0, 0))
for y in range(DH):
    for x in range(DW):
        d = ((x - DW / 2.0 + 0.5) / (DW / 2.0)) ** 2 + ((y - DH / 2.0 + 0.5) / (DH / 2.0)) ** 2
        if d < 0.35:
            a = 255
        elif d < 0.65:
            a = 170
        elif d < 0.95:
            a = 255 if (x + y) % 2 == 0 else 0     # dithered rim
            a = 110 if a else 0
        else:
            a = 0
        dap.putpixel((x, y), (255, 255, 255, a))
dap.save(os.path.join(OUT, "dapple.png"))
print("outdoor pieces written")
