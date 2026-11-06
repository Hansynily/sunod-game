"""
SUNOD UI V2 - joystick sprites (2026-11-07), soft cozy cards look.

Redraws the two joystick PNGs IN PLACE (same paths, same pixel sizes, so the scenes'
Image references and the .meta import settings stay untouched):
  ../Menu/joystick_base.png  36x36  cream ring (~85% opacity), brown outline, 4 brown chevrons
  ../Menu/joystick_knob.png  18x18  cream knob, brown outline, darker bottom edge for depth

Flat pixel art: hard edges, no blur, no glow (UI_V2.md rule 9). Colours = SunodTheme.tss tokens.

Run: python make_joystick.py
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
MENU = os.path.normpath(os.path.join(HERE, "..", "Menu"))

T = (0, 0, 0, 0)
CREAM = (248, 238, 216)        # --sn-cream
CREAM_HI = (253, 248, 236)     # --sn-cream-hi
CREAM_LO = (236, 220, 190)     # --sn-cream-lo
PAPER = (222, 200, 162)        # --sn-paper-line
BROWN = (139, 98, 66)          # --sn-brown


def rgba(c, a=255): return (c[0], c[1], c[2], a)


def dist(x, y, size):
    """Distance from a pixel centre to the sprite centre."""
    c = size / 2.0
    return ((x + 0.5 - c) ** 2 + (y + 0.5 - c) ** 2) ** 0.5


def make_base(size=36):
    img = Image.new("RGBA", (size, size), T)
    px = img.load()
    r = size / 2.0
    ring = rgba(CREAM, 217)            # ~85 %
    inner = rgba(CREAM_HI, 92)         # see-through middle so the world still shows
    edge = rgba(BROWN, 235)
    inner_edge = rgba(PAPER, 200)
    for y in range(size):
        for x in range(size):
            d = dist(x, y, size)
            if d > r - 0.5:
                continue
            if d > r - 1.6:
                px[x, y] = edge                 # outer brown outline
            elif d > r - 4.6:
                # cream band; bottom rows a touch darker for the thicker-bottom-edge feel
                px[x, y] = rgba(CREAM_LO, 217) if (y > size * 0.72 and d > r - 2.6) else ring
            elif d > r - 5.6:
                px[x, y] = inner_edge           # thin line between band and middle
            else:
                px[x, y] = inner

    # 4 small chevrons pointing out from the centre: rows 2, 4, 6 px wide (the sprite has an
    # even size, so the 2-px core straddles the centre and every arrow stays symmetric)
    c = size // 2                      # 18: pixels 17|18 straddle the centre
    tip = 6                            # tip row, counted from the edge
    arrow = rgba(BROWN, 255)

    def put(x, y):
        if 0 <= x < size and 0 <= y < size: px[x, y] = arrow

    for off, hw in ((0, 1), (1, 2), (2, 3)):    # (row offset from tip, half-width)
        span = range(c - hw, c + hw)
        for s in span:
            put(s, tip + off)                       # up
            put(s, size - 1 - tip - off)            # down
            put(tip + off, s)                       # left
            put(size - 1 - tip - off, s)            # right
    return img


def make_knob(size=18):
    img = Image.new("RGBA", (size, size), T)
    px = img.load()
    r = size / 2.0
    for y in range(size):
        for x in range(size):
            d = dist(x, y, size)
            if d > r - 0.5:
                continue
            if d > r - 1.6:
                px[x, y] = rgba(BROWN)                    # outline
            elif y >= size - 5 and d > r - 3.4:
                px[x, y] = rgba(PAPER)                    # darker bottom edge (depth)
            elif y >= size - 5:
                px[x, y] = rgba(CREAM_LO)
            else:
                px[x, y] = rgba(CREAM)
    # small flat highlight, top-left
    for x, y in ((5, 4), (6, 4), (4, 5), (5, 5), (4, 6)):
        px[x, y] = rgba(CREAM_HI)
    return img


if __name__ == "__main__":
    base = make_base()
    knob = make_knob()
    base.save(os.path.join(MENU, "joystick_base.png"))
    knob.save(os.path.join(MENU, "joystick_knob.png"))
    print("wrote", os.path.join(MENU, "joystick_base.png"), base.size)
    print("wrote", os.path.join(MENU, "joystick_knob.png"), knob.size)
