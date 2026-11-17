"""Props drawn by hand at 16 px/unit (not filtered from the old art). Umaga palette.

Every prop keeps its old canvas size and sits in the same box inside it (props_bbox.json), so
nothing moves in the rooms. Light comes from the top-left: lit top and left edges, shaded
bottom and right edges, a dark outline in the prop's own colour.

Add a drawer to DRAW and the prop is skipped by redefine_quest_props.py.
Run from the sunod-game folder:  python Tools/art/make_props.py
"""
import json, os, re, sys, time
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sunod_palette import C, INK, PAPER

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = "Assets/Art/Generated/Quests"
BOX = json.load(open(os.path.join(HERE, "props_bbox.json")))
B, K, W7 = C["brown"], C["cool"], C["warm"]

# How many pixels each prop is drawn with (props_scale.json, from plan_prop_scales.py): a prop
# the rooms show at k times its natural size is drawn with k times the pixels and imported at
# 16*k px/unit. Its size in the world does not change; its pixels come out the same size as
# everyone else's.
_SCALE_FILE = os.path.join(HERE, "props_scale.json")
SCALE = json.load(open(_SCALE_FILE)) if os.path.exists(_SCALE_FILE) else {}


def grid_size(name):
    """Canvas size and content box for a prop at its planned pixel count (k >= 1 only)."""
    info = BOX[name]
    k = max(1.0, SCALE.get(name, 1))
    w0, h0 = info["size"]
    w = int(round(w0 * k))
    f = w / float(w0)                               # the factor actually used, so nothing distorts
    h = max(1, int(round(h0 * f)))
    return (w, h), [int(round(v * f)) for v in info["bbox"]], f


class P:
    """A small pixel canvas. Coordinates are relative to the prop's content box."""

    def __init__(self, name):
        size, box, _ = grid_size(name)
        self.im = Image.new("RGBA", size, (0, 0, 0, 0))
        self.ox, self.oy = box[0], box[1]
        self.w, self.h = max(1, box[2] - box[0]), max(1, box[3] - box[1])

    def px(self, x, y, c):
        x, y = int(x) + self.ox, int(y) + self.oy
        if 0 <= x < self.im.width and 0 <= y < self.im.height:
            self.im.putpixel((x, y), tuple(c) + (255,))

    def clear(self, x, y):
        x, y = int(x) + self.ox, int(y) + self.oy
        if 0 <= x < self.im.width and 0 <= y < self.im.height:
            self.im.putpixel((x, y), (0, 0, 0, 0))

    def rect(self, x0, y0, x1, y1, c):
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                self.px(x, y, c)

    def block(self, x0, y0, x1, y1, ramp, i=4, round_=False):
        """A lit solid: outline, lit top/left edge, shaded bottom/right edge."""
        r = C[ramp] if isinstance(ramp, str) else ramp
        lo = lambda k: r[max(0, min(7, k))]
        self.rect(x0, y0, x1, y1, lo(i - 3))
        if x1 - x0 >= 2 and y1 - y0 >= 2:
            self.rect(x0 + 1, y0 + 1, x1 - 1, y1 - 1, lo(i))
            self.rect(x0 + 1, y0 + 1, x1 - 1, y0 + 1, lo(i + 1))
            self.rect(x0 + 1, y0 + 1, x0 + 1, y1 - 1, lo(i + 1))
            self.rect(x0 + 1, y1 - 1, x1 - 1, y1 - 1, lo(i - 1))
            self.rect(x1 - 1, y0 + 1, x1 - 1, y1 - 1, lo(i - 1))
        if round_:
            for cx, cy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
                self.clear(cx, cy)

    def oval(self, x0, y0, x1, y1, ramp, i=4):
        """A lit, outlined ellipse."""
        r = C[ramp] if isinstance(ramp, str) else ramp
        lo = lambda k: r[max(0, min(7, k))]
        cx, cy, rx, ry = (x0 + x1) / 2.0, (y0 + y1) / 2.0, (x1 - x0 + 1) / 2.0, (y1 - y0 + 1) / 2.0
        inside = lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0
        for y in range(int(y0), int(y1) + 1):
            for x in range(int(x0), int(x1) + 1):
                if not inside(x, y):
                    continue
                edge = not (inside(x - 1, y) and inside(x + 1, y) and inside(x, y - 1) and inside(x, y + 1))
                d = ((x - cx) / rx + (y - cy) / ry)        # -: top-left, +: bottom-right
                if edge:
                    c = lo(i - 3)
                elif d < -0.55:
                    c = lo(i + 1)
                elif d > 0.6:
                    c = lo(i - 1)
                else:
                    c = lo(i)
                self.px(x, y, c)


# ---------------------------------------------------------------- floorboards
def boards(name, light, mid, dark, nails=False):
    p = P(name)
    p.rect(0, 0, 15, 15, B[1])
    for i, x0 in enumerate((1, 6, 11)):
        x1 = x0 + 3
        p.rect(x0, 1, x1, 14, mid)
        p.rect(x0, 1, x0, 14, light)
        p.rect(x1, 1, x1, 14, dark)
        p.rect(x0, 1, x1, 1, light)
        p.rect(x0, 14, x1, 14, dark)
        for gy in ((4, 10), (7, 12), (3, 9))[i]:
            p.px(x0 + 1, gy, dark); p.px(x0 + 2, gy + 1, dark)
        if nails:
            for ny in (2, 13):
                p.px(x0 + 1, ny, K[5]); p.px(x0 + 2, ny, K[2])
    return p


def floor_fresh():
    return boards("floor_fresh", B[7], B[6], B[5])


def floor_nailed():
    return boards("floor_nailed", B[6], B[5], B[4], nails=True)


def floor_new():
    return boards("floor_new", B[6], B[5], B[4], nails=True)


def floor_broken():
    p = boards("floor_broken", B[4], B[3], B[2])
    p.rect(6, 6, 9, 9, INK)
    for x, y, c in ((6, 5, B[1]), (8, 5, B[1]), (9, 6, B[2]), (7, 10, B[1]), (9, 10, B[1]), (7, 5, B[5]), (8, 10, B[5])):
        p.px(x, y, c)
    for x, y in ((2, 3), (3, 4), (2, 5), (3, 6)):
        p.px(x, y, B[1])
    p.rect(11, 11, 14, 11, B[1]); p.rect(11, 10, 14, 10, B[5])
    p.px(12, 3, K[3]); p.px(13, 13, K[3])
    return p


def plank_stack():
    p = P("plank_stack")
    for i, (x0, y0) in enumerate(((1, 6), (0, 3), (2, 0))):
        p.rect(x0, y0, x0 + 16, y0 + 3, INK)
        p.rect(x0 + 1, y0, x0 + 15, y0 + 2, B[5])
        p.rect(x0 + 1, y0, x0 + 15, y0, B[7])
        p.rect(x0 + 1, y0 + 2, x0 + 15, y0 + 2, B[4])
        p.rect(x0 + 14, y0, x0 + 15, y0 + 2, B[6])
        p.px(x0 + 15, y0 + 1, B[3])
        p.px(x0 + 5 + i * 2, y0 + 1, B[4]); p.px(x0 + 9 - i, y0 + 1, B[4])
    return p


# ---------------------------------------------------------------- crates, boxes, containers
def crate():
    p = P("crate"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 5)
    p.rect(2, 2, w - 2, h - 2, B[4])
    for x in range(3, w - 2, 3):
        p.rect(x, 2, x, h - 2, B[3])
    for k in range(2, min(w, h) - 1):                    # cross brace
        p.px(k, k, B[6]); p.px(w - k, k, B[6])
    for x, y in ((1, 1), (w - 1, 1), (1, h - 1), (w - 1, h - 1)):
        p.px(x, y, K[5])
    return p


def barrel():
    p = P("barrel"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 4, round_=True)
    p.rect(1, 0, w - 1, 1, B[6]); p.rect(2, 1, w - 2, 1, B[3])          # open top rim
    for x in range(3, w - 1, 3):
        p.rect(x, 3, x, h - 2, B[3])
    for y in (3, h - 3):
        p.rect(0, y, w, y, K[2]); p.rect(1, y, w - 2, y, K[4]); p.px(2, y, K[6])
    return p


def box(name, mark):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "skin", 5)
    p.block(0, 0, w, 4, "skin", 6)                                        # flaps
    p.rect(w // 2 - 1, 0, w // 2 + 1, 6, W7[7]); p.rect(w // 2 + 1, 1, w // 2 + 1, 6, W7[5])   # tape
    p.rect(w - 7, h - 6, w - 2, h - 3, PAPER); p.rect(w - 6, h - 5, w - 3, h - 5, mark)
    p.px(2, h - 3, C["skin"][3]); p.px(3, h - 2, C["skin"][3])
    return p


def bin_blue():
    p = P("bin_blue"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "blue", 4)
    p.block(0, 0, w, 3, "blue", 5)
    p.rect(w // 2 - 3, 6, w // 2 + 3, 8, PAPER); p.rect(w // 2 - 2, 7, w // 2 + 1, 7, C["blue"][2])
    p.rect(2, h - 2, w - 2, h - 2, C["blue"][2])
    return p


def bucket():
    p = P("bucket"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "cool", 4)
    p.clear(0, h); p.clear(w, h); p.px(0, h - 1, K[1]); p.px(w, h - 1, K[1])
    p.rect(1, 1, w - 1, 2, C["cyan"][5]); p.rect(2, 1, 4, 1, C["cyan"][7])
    p.rect(0, 0, w, 0, K[6]); p.px(0, 0, K[2]); p.px(w, 0, K[2])
    p.rect(1, h // 2 + 1, w - 1, h // 2 + 1, K[2])
    return p


def cash_box():
    p = P("cash_box"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, "teal", 3)
    p.rect(1, 6, w - 1, 6, C["teal"][1])
    p.rect(w // 2 - 2, h - 3, w // 2 + 2, h - 3, K[1])                    # slot
    for x, c in ((2, C["amber"][5]), (4, C["amber"][6]), (7, C["amber"][5]), (w - 3, C["green"][5])):
        p.rect(x, 2, x + 1, 3, c); p.px(x, 2, PAPER)
    return p


def toolbox():
    p = P("toolbox"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, "red", 4)
    p.rect(1, 5, w - 1, 5, C["red"][2])
    p.rect(w // 2 - 3, 0, w // 2 + 3, 0, K[2]); p.rect(w // 2 - 3, 1, w // 2 - 3, 3, K[2]); p.rect(w // 2 + 3, 1, w // 2 + 3, 3, K[2])
    p.rect(w // 2 - 2, 1, w // 2 + 2, 1, K[5])
    p.rect(w // 2 - 1, 5, w // 2, 6, C["amber"][6])
    return p


def school_bag():
    p = P("school_bag"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "blue", 4, round_=True)
    p.rect(1, 2, w - 1, 4, C["blue"][5]); p.rect(1, 5, w - 1, 5, C["blue"][2])
    p.rect(w // 2 - 1, 4, w // 2, 6, C["amber"][6])
    p.rect(2, 0, w - 2, 0, C["blue"][1])
    return p


def toy_box(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, ramp, 4)
    p.rect(0, 2, w, 3, C[ramp][2]); p.rect(1, 2, w - 1, 2, C[ramp][6])
    p.rect(w // 2 - 1, 2, w // 2, h - 1, PAPER)                           # ribbon
    p.rect(w // 2 - 2, 0, w // 2 - 1, 1, PAPER); p.rect(w // 2 + 1, 0, w // 2 + 2, 1, PAPER)
    return p


def paint_cans():
    p = P("paint_cans"); w, h = p.w - 1, p.h - 1
    for i, ramp in enumerate(("red", "blue", "amber")):
        x0 = i * (w // 3) + (1 if i else 0)
        x1 = x0 + w // 3 - 1
        p.block(x0, 2, x1, h, "cool", 5)
        p.rect(x0 + 1, 1, x1 - 1, 3, C[ramp][4]); p.rect(x0 + 1, 1, x1 - 2, 1, C[ramp][6])
        p.px(x0 + 1, 4, C[ramp][4]); p.px(x0 + 1, 5, C[ramp][4])           # a drip
        p.rect(x0 + 1, h - 3, x1 - 1, h - 3, K[3])
    return p


def snack_jars():
    p = P("snack_jars"); w, h = p.w - 1, p.h - 1
    p.block(0, h - 2, w, h, "brown", 5)
    for i, ramp in enumerate(("amber", "pink", "orange")):
        x0 = 1 + i * ((w - 1) // 3)
        p.block(x0, 2, x0 + 3, h - 3, "warm", 7)
        p.rect(x0, 1, x0 + 3, 2, C["red"][4]); p.rect(x0 + 1, 1, x0 + 2, 1, C["red"][6])
        p.rect(x0 + 1, h - 6, x0 + 2, h - 4, C[ramp][5]); p.px(x0 + 1, h - 6, C[ramp][7])
    return p


# ---------------------------------------------------------------- furniture
def bench():       # a work table with two drawers
    p = P("bench"); w, h = p.w - 1, p.h - 1
    p.block(2, 4, 6, h, "brown", 3); p.block(w - 6, 4, w - 2, h, "brown", 3)      # legs
    p.block(5, 5, w - 5, h - 4, "brown", 4)                                      # apron
    for x0 in (w // 2 - 15, w // 2 + 2):
        p.block(x0, 7, x0 + 13, h - 6, "brown", 5)
        p.rect(x0 + 5, 9, x0 + 8, 10, C["amber"][6]); p.rect(x0 + 5, 10, x0 + 8, 10, C["amber"][3])
    p.block(0, 0, w, 5, "brown", 6)                                              # top slab
    p.rect(1, 4, w - 1, 4, B[4])
    for x in range(6, w - 4, 9):
        p.px(x, 2, B[5]); p.px(x + 1, 2, B[5]); p.px(x + 4, 3, B[5])
    return p


def bookshelf():
    p = P("bookshelf"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 3)
    p.rect(2, 2, w - 2, h - 2, B[1])
    rows = 3
    rh = (h - 3) // rows
    cols = ("red", "blue", "amber", "green", "pink", "teal", "orange", "violet")
    for r in range(rows):
        y1 = 2 + (r + 1) * rh - 1
        p.rect(1, y1 + 1, w - 1, y1 + 1, B[5]) if r < rows - 1 else None
        for i, x in enumerate(range(2, w - 1)):
            ramp = cols[(i * 3 + r * 5) % len(cols)]
            top = y1 - rh + 2 + ((i + r) % 3 == 0)
            p.rect(x, top, x, y1, C[ramp][4 if (i + r) % 2 else 5])
            p.px(x, top, C[ramp][6])
    return p


def cabinet():
    p = P("cabinet"); w, h = p.w - 1, p.h - 1
    p.block(1, h - 3, 5, h, "brown", 2); p.block(w - 5, h - 3, w - 1, h, "brown", 2)   # feet
    p.block(0, 0, w, h - 2, "brown", 4)
    p.block(0, 0, w, 3, "brown", 5)                                              # cornice
    mid = w // 2
    for x0, x1 in ((2, mid - 1), (mid + 1, w - 2)):
        p.block(x0, 5, x1, h - 4, "brown", 5)
        p.rect(x0 + 2, 7, x1 - 2, h - 6, B[4])
        p.rect(x0 + 2, 7, x1 - 2, 7, B[3]); p.rect(x0 + 2, 7, x0 + 2, h - 6, B[3])
        p.rect(x0 + 3, h - 6, x1 - 2, h - 6, B[6])
    p.rect(mid - 2, h // 2, mid - 2, h // 2 + 2, C["amber"][6]); p.rect(mid + 2, h // 2, mid + 2, h // 2 + 2, C["amber"][6])
    return p


def canteen_counter():
    p = P("canteen_counter"); w, h = p.w - 1, p.h - 1
    p.block(0, 4, w, h, "red", 4)
    for x in range(3, w - 5, 8):
        p.block(x, 8, x + 5, h - 3, "red", 5)
    p.rect(1, h - 1, w - 1, h - 1, C["red"][2])
    p.block(0, 0, w, 6, "warm", 7)
    p.rect(1, 5, w - 1, 5, W7[5]); p.rect(1, 1, w - 1, 1, PAPER)
    return p


def dock():        # a short plank jetty
    p = P("dock"); w, h = p.w - 1, p.h - 1
    p.block(1, h - 4, 3, h, "brown", 2); p.block(w - 3, h - 4, w - 1, h, "brown", 2)
    p.block(0, 0, w, h - 3, "brown", 5)
    for y in range(3, h - 4, 3):
        p.rect(1, y, w - 1, y, B[3]); p.rect(1, y + 1, w - 1, y + 1, B[6])
    for x in (2, w - 2):
        p.px(x, 1, K[5])
    return p


def easel():
    p = P("easel"); w, h = p.w - 1, p.h - 1
    for x0, lean in ((1, 1), (w - 3, -1)):                                        # two front legs
        for y in range(0, h + 1):
            x = x0 + lean * (y * 3 // h)
            p.rect(x, y, x + 2, y, B[5]); p.px(x, y, B[6]); p.px(x + 2, y, B[2])
    p.block(w // 2 - 1, h // 2, w // 2 + 1, h - 3, "brown", 3)                     # back leg
    p.block(0, 2, w, 5, "brown", 5)                                              # top bar
    p.block(1, h // 2 - 2, w - 1, h // 2 + 1, "brown", 6)                         # tray
    p.block(w // 2 - 2, 0, w // 2 + 2, 3, "brown", 4)
    return p


def stall():
    p = P("stall"); w, h = p.w - 1, p.h - 1
    ah = max(4, h // 3)                                                             # awning height
    sw = max(2, w // 8)                                                             # stripe width
    p.block(1, ah - 1, 3, h, "brown", 3); p.block(w - 3, ah - 1, w - 1, h, "brown", 3)   # posts
    p.block(0, h // 2 + 2, w, h, "brown", 5)                                        # counter
    for x in range(3, w - 2, 4):
        p.rect(x, h // 2 + 5, x, h - 2, B[3])
    p.block(0, h // 2, w, h // 2 + 3, "brown", 6)                                   # counter top
    for i, ramp in enumerate(("amber", "green", "red", "orange", "pink")):          # goods
        x = 3 + i * ((w - 6) // 5)
        p.oval(x, h // 2 - 3, x + max(2, w // 10), h // 2, ramp, 5)
    for x in range(0, w + 1):                                                       # striped awning
        red = (x // sw) % 2 == 0
        p.rect(x, 0, x, ah, C["red"][4] if red else PAPER)
        p.px(x, 1, C["red"][6] if red else PAPER)
        p.px(x, ah, C["red"][2] if red else W7[5])
        if (x % sw) == sw // 2:
            p.px(x, ah + 1, C["red"][3] if red else W7[6])                          # scallops
    p.rect(0, 0, w, 0, C["red"][1])
    return p


def shelf(name, seed):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.rect(2, 2, w - 2, h - 2, K[1])
    rows = 3
    rh = (h - 2) // rows
    ramps = ("skin", "blue", "red", "green", "amber", "warm", "teal")
    for r in range(rows):
        y1 = 1 + (r + 1) * rh
        p.rect(1, y1, w - 1, y1, B[5]); p.rect(1, y1 + 1, w - 1, y1 + 1, B[3]) if y1 + 1 < h else None
        x = 3
        k = seed + r * 3
        while x < w - 5:
            ramp = ramps[k % len(ramps)]
            bw = 4 + (k % 3)
            bh = min(rh - 3, 3 + (k * 7 % 4))
            p.block(x, y1 - bh, x + bw, y1 - 1, ramp, 5)
            if bw >= 5:
                p.rect(x + 2, y1 - bh + 2, x + bw - 2, y1 - bh + 2, PAPER)
            x += bw + 2
            k += 1
    return p


def toy_shelf():
    p = P("toy_shelf"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "pink", 5)
    p.rect(2, 2, w - 2, h - 2, PAPER)
    p.rect(1, h // 2, w - 1, h // 2, C["pink"][4])
    for i, (ramp, y) in enumerate((("red", 3), ("amber", 3), ("blue", 3), ("green", h // 2 + 2), ("skin", h // 2 + 2))):
        x = 3 + (i % 3) * 3
        p.block(x, y, x + 2, y + 2 + (i % 2), ramp, 5)
    return p


def menu_board():
    p = P("menu_board"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 4)
    p.rect(2, 2, w - 2, h - 2, C["teal"][1])
    p.rect(2, 2, w - 2, 2, C["teal"][0])
    for i, y in enumerate(range(4, h - 3, 3)):
        p.rect(4, y, w - 9 + (i % 2) * 2, y, PAPER)
        p.rect(w - 6, y, w - 4, y, C["amber"][6])
    p.rect(3, h - 1, 6, h - 1, PAPER)                                            # chalk on the ledge
    return p


def music_stand():
    p = P("music_stand"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.rect(m, h // 2, m, h - 2, K[3]); p.px(m - 1, h // 2 + 2, K[5])
    p.rect(m - 3, h - 1, m + 3, h - 1, K[2]); p.rect(m - 2, h - 2, m + 2, h - 2, K[4])
    p.block(0, 0, w, h // 2 + 1, "cool", 3)
    p.rect(2, 2, w - 2, h // 2 - 1, PAPER)
    for y in (3, 5, 7):
        if y < h // 2 - 1:
            p.rect(3, y, w - 3, y, K[5])
    p.px(4, 4, INK); p.px(7, 6, INK); p.px(w - 4, 4, INK)
    return p


def calculator_big():
    p = P("calculator_big"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3, round_=True)
    p.block(2, 2, w - 2, 8, "green", 6)
    p.rect(3, 3, w - 3, 7, C["green"][7]); p.rect(w - 9, 4, w - 4, 6, C["green"][3])
    bw = (w - 5) // 4
    for r in range(4):
        for c in range(4):
            x0, y0 = 3 + c * bw, 11 + r * 4
            ramp = "orange" if c == 3 else "warm"
            p.rect(x0, y0, x0 + bw - 2, y0 + 2, C[ramp][6]); p.rect(x0, y0, x0 + bw - 2, y0, C[ramp][7])
            p.rect(x0, y0 + 2, x0 + bw - 2, y0 + 2, C[ramp][4])
    return p


def chart_board():
    p = P("chart_board"); w, h = p.w - 1, p.h - 1
    p.block(2, h - 6, 4, h, "brown", 3); p.block(w - 4, h - 6, w - 2, h, "brown", 3)
    p.block(0, 0, w, h - 4, "warm", 5)
    p.rect(2, 2, w - 2, h - 6, PAPER)
    p.rect(4, 4, 4, h - 8, K[2]); p.rect(4, h - 8, w - 4, h - 8, K[2])            # axes
    for i, y in enumerate((5, 8, 11)):
        if y < h - 8:
            p.px(5, y, C["blue"][4]); p.px(6, y, C["blue"][4])
    return p


def price_sign():
    p = P("price_sign"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 1, h // 2, m + 1, h, "brown", 3)
    p.block(0, 0, w, h // 2 + 1, "brown", 4)
    p.rect(2, 2, w - 2, h // 2 - 1, PAPER)
    for i, ramp in enumerate(("red", "blue", "amber")):
        x = 3 + i * ((w - 5) // 3)
        p.rect(x, 3, x + 1, h // 2 - 2, C[ramp][4]); p.px(x, 3, C[ramp][6])
    return p


def sign_class():
    p = P("sign_class"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 1, 8, m + 1, h, "skin", 4)
    p.block(0, 0, w, 10, "cool", 2)
    p.rect(2, 2, w - 2, 8, PAPER)
    p.rect(m, 3, m, 7, C["blue"][4]); p.rect(m - 2, 5, m + 2, 5, C["blue"][4]); p.px(m - 1, 4, C["blue"][4]); p.px(m + 1, 4, C["blue"][4])
    return p


def shade_awning():
    p = P("shade_awning"); w, h = p.w - 1, p.h - 1
    p.block(1, 6, 3, h, "brown", 4); p.block(w - 3, 6, w - 1, h, "brown", 4)
    p.block(0, 0, w, 6, "cool", 3)
    p.rect(1, 1, w - 1, 1, K[5])
    for x in range(1, w, 4):                                                      # scalloped hem
        p.rect(x, 7, x + 1, 8, K[2]); p.px(x, 7, K[3])
    return p


def exercise_mat():
    p = P("exercise_mat"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "blue", 5, round_=True)
    for x in range(5, w - 2, 6):
        p.rect(x, 1, x, h - 1, C["blue"][3]); p.rect(x + 1, 1, x + 1, h - 2, C["blue"][6])
    return p


def notebook():
    p = P("notebook"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "red", 3)
    m = w // 2
    for x0, x1 in ((1, m - 1), (m + 1, w - 1)):
        p.rect(x0, 0, x1, h - 2, PAPER); p.rect(x0, h - 2, x1, h - 2, W7[5])
        for y in range(3, h - 3, 3):
            p.rect(x0 + 2, y, x1 - 2, y, C["blue"][5])
    p.rect(m, 0, m, h - 1, W7[4])
    return p


def notes():
    p = P("notes"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "warm", 7)
    p.rect(1, 1, w - 1, h - 1, PAPER); p.rect(1, h - 1, w - 1, h - 1, W7[5])
    for y in range(3, h - 1, 3):
        p.rect(2, y, w - 2 - (y % 2), y, K[3])
    p.px(w - 2, h - 2, C["blue"][4])
    return p


def storybook():
    p = P("storybook"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "amber", 4)
    p.rect(1, 1, 2, h - 1, C["amber"][2])                                          # spine
    p.rect(4, 2, w - 2, h // 2, PAPER); p.rect(5, 3, w - 3, h // 2 - 1, C["green"][5]); p.px(w - 4, 3, C["amber"][6])
    p.rect(4, h // 2 + 2, w - 2, h // 2 + 2, PAPER); p.rect(4, h - 2, w - 4, h - 2, PAPER)
    return p


# ---------------------------------------------------------------- toys and instruments
def teddy(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    hw = max(6, int(w * 0.62))                                                     # head width
    hx = (w - hw) // 2
    hh = int(h * 0.42)
    p.oval(hx - 1, 0, hx + 2, 3, "brown", 4); p.oval(hx + hw - 2, 0, hx + hw + 1, 3, "brown", 4)   # ears
    p.oval(1, hh - 1, w - 1, h - 1, "brown", 4)                                     # body
    p.oval(0, h - 4, 3, h, "brown", 5); p.oval(w - 3, h - 4, w, h, "brown", 5)        # feet
    p.oval(hx, 1, hx + hw, hh + 1, "brown", 5)                                      # head
    m = w // 2
    p.oval(m - 2, hh - 3, m + 2, hh, "skin", 6)                                     # muzzle
    p.px(m, hh - 2, INK); p.px(m - 2, hh - 4, INK); p.px(m + 2, hh - 4, INK)
    p.rect(m - 1, hh + 1, m + 1, hh + 2, C["red"][4]); p.px(m - 2, hh + 1, C["red"][5]); p.px(m + 2, hh + 1, C["red"][5])
    p.oval(m - 2, hh + 4, m + 2, h - 3, "skin", 6) if h > 18 else None             # tummy
    return p


def toy_blocks():
    p = P("toy_blocks"); w, h = p.w - 1, p.h - 1
    s = (w - 1) // 2
    p.block(0, h - s, s, h, "red", 4); p.block(s + 1, h - s, w, h, "blue", 4)
    p.block(s // 2, h - 2 * s - 1, s // 2 + s, h - s - 1, "amber", 5)
    for x, y in ((s // 2, h - s // 2), (s + 1 + s // 2, h - s // 2), (s, h - s - 1 - s // 2)):
        p.px(x, y, PAPER); p.px(x - 1, y, PAPER)
    return p


def drum(name, dented):
    p = P(name); w, h = p.w - 1, p.h - 1
    bh = h - 3
    p.block(0, 1, w, bh, "red", 4)
    p.rect(0, 0, w, 1, PAPER); p.rect(0, 2, w, 2, W7[5]); p.px(0, 0, W7[4]); p.px(w, 0, W7[4])     # skin + rim
    p.rect(0, bh, w, bh, W7[6])
    for x in range(1, w, 2):                                                       # lacing
        p.px(x, 4 + (x // 2) % 2 * 2, PAPER); p.px(x, 5, PAPER)
    p.rect(2, bh + 1, 2, h, B[3]); p.rect(w - 2, bh + 1, w - 2, h, B[3])             # stand legs
    if dented:
        p.rect(w // 2 - 1, 0, w // 2 + 1, 1, W7[4]); p.px(w // 2, 2, INK); p.px(w // 2 + 1, 3, INK); p.px(w // 2, 4, INK)
    return p


def rug_round():
    p = P("rug_round"); w, h = p.w - 1, p.h - 1
    e = max(2, w // 8)
    p.oval(0, 0, w, h, "blue", 4)
    p.oval(e, e - 1, w - e, h - e + 1, "amber", 5)
    p.oval(2 * e, 2 * e - 1, w - 2 * e, h - 2 * e + 1, "red", 4)
    p.oval(3 * e, 3 * e - 1, w - 3 * e, h - 3 * e + 1, "amber", 6)
    for k in range(0, w, max(3, w // 8)):                                           # woven fringe marks
        p.px(k, h // 2, C["blue"][6]) if k in (0, w) else None
    return p


def beanbag():
    p = P("beanbag"); w, h = p.w - 1, p.h - 1
    p.oval(0, 1, w, h, "teal", 5)
    p.oval(2, 0, w - 3, h - 3, "teal", 6)
    p.px(w // 2, h // 2 - 1, C["teal"][3]); p.px(w // 2 + 1, h // 2, C["teal"][3])
    return p


def rope_coil():
    p = P("rope_coil"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "skin", 5)
    p.oval(2, 2, w - 2, h - 2, "skin", 4)
    p.oval(w // 2 - 2, h // 2 - 1, w // 2 + 2, h // 2 + 1, "brown", 2)
    for x in range(1, w, 3):
        p.px(x, 1 + (x % 2), C["skin"][3]); p.px(x, h - 1 - (x % 2), C["skin"][3])
    return p


# ---------------------------------------------------------------- outdoors
def bush():
    p = P("bush"); w, h = p.w - 1, p.h - 1
    p.oval(0, 2, w, h, "green", 3)
    p.oval(1, 0, w // 2 + 1, h - 3, "green", 4); p.oval(w // 2 - 1, 1, w - 1, h - 2, "green", 4)
    for x, y in ((3, 2), (4, 3), (w - 4, 3), (w // 2, 4), (2, 5), (w - 3, 6)):       # leaf lights
        p.px(x, y, C["lime"][5]); p.px(x + 1, y, C["lime"][4])
    for x, y in ((w // 2 - 1, h - 2), (3, h - 2), (w - 3, h - 3)):
        p.px(x, y, C["green"][1])
    return p


def rock(name, crack=False):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "cool", 5)
    p.rect(3, 2, w // 2, 2, K[7]); p.px(2, 3, K[7])
    p.rect(w // 2, h - 2, w - 3, h - 2, K[3])
    if crack:
        for k in range(3):
            p.px(w // 2 + k, h // 2 - 1 + k, K[2])
    return p


def slab():
    p = P("slab"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 5)
    p.rect(1, h - 2, w - 1, h - 1, K[3]); p.px(2, 2, K[7]); p.px(3, 2, K[7])
    return p


def log():
    p = P("log"); w, h = p.w - 1, p.h - 1
    p.block(2, 1, w, h, "brown", 4, round_=True)
    for x in range(8, w - 2, 5):
        p.rect(x, 4, x + 2, 4, B[2]); p.rect(x + 2, h - 3, x + 3, h - 3, B[2])
    p.oval(0, 1, 6, h, "skin", 5)                                                  # cut end
    p.oval(2, 3, 4, h - 2, "skin", 4); p.px(3, h // 2 + 1, B[3])
    p.rect(10, 1, 17, 1, C["green"][4]); p.rect(11, 2, 15, 2, C["green"][3]); p.px(12, 1, C["lime"][5])   # moss
    return p


def fence():
    p = P("fence"); w, h = p.w - 1, p.h - 1
    for y in (3, h - 5):
        p.rect(0, y, w, y + 1, B[5]); p.rect(0, y, w, y, B[6]); p.rect(0, y + 2, w, y + 2, B[2])
    for x in (0, w // 2 - 1, w - 2):
        p.block(x, 0, x + 2, h, "brown", 5)
        p.px(x + 1, 0, B[7])
    return p


def pond():
    p = P("pond"); w, h = p.w - 1, p.h - 1
    e = max(1, w // 16)
    p.oval(0, 0, w, h, "brown", 3)                                                  # muddy bank
    p.oval(e, e, w - e, h - e, "blue", 3)
    p.oval(2 * e, 2 * e, w - 2 * e, h - 2 * e, "blue", 5)
    p.oval(w // 4, h // 4, w - w // 4, h - h // 4, "blue", 4)                        # the deep middle
    for fx, fy, ln in ((0.25, 0.22, 0.22), (0.55, 0.36, 0.16), (0.3, 0.6, 0.14), (0.6, 0.74, 0.2), (0.42, 0.48, 0.1)):
        x0, y = int(w * fx), int(h * fy)
        p.rect(x0, y, x0 + int(w * ln), y, C["cyan"][7]); p.rect(x0 + 1, y + 1, x0 + int(w * ln) - 1, y + 1, C["cyan"][5])
    return p


def lily_pad():
    p = P("lily_pad"); w, h = p.w - 1, p.h - 1
    p.oval(0, 1, w, h, "green", 4)
    p.clear(w // 2, 1); p.clear(w // 2, 2); p.px(w // 2, 3, C["green"][1])           # the notch
    p.rect(w // 2 + 2, h // 2 - 1, w // 2 + 3, h // 2, C["pink"][6]); p.px(w // 2 + 2, h // 2 - 1, PAPER); p.px(w // 2 + 3, h // 2, C["amber"][6])
    return p


def reeds():
    p = P("reeds"); w, h = p.w - 1, p.h - 1
    for i, x in enumerate(range(0, w + 1, 2)):
        top = (i * 3) % 4
        p.rect(x, top, x, h, C["green"][3 + i % 2])
        if i % 2 == 0:
            p.rect(x, top, x, top + 2, B[3]); p.px(x, top, B[5])
    return p


def burrow():
    p = P("burrow"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h + 3, "brown", 4)
    p.oval(w // 2 - 3, 2, w // 2 + 3, h + 2, "brown", 1)
    p.rect(w // 2 - 2, 4, w // 2 + 2, h, INK)
    p.px(2, h, C["green"][4]); p.px(w - 2, h, C["green"][4]); p.px(3, 1, B[6])
    return p


def carrot_patch():
    p = P("carrot_patch"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, "brown", 2)
    for x in range(2, w - 1, 3):
        p.rect(x, 3, x, 4, C["orange"][5]); p.rect(x - 1, 0, x - 1, 2, C["green"][4]); p.rect(x + 1, 1, x + 1, 2, C["green"][5]); p.px(x, 1, C["lime"][5])
    return p


def seed_tray():
    p = P("seed_tray"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "brown", 3)
    p.rect(1, 3, w - 1, 4, B[1])
    for x in range(2, w - 1, 2):
        p.px(x, 2, C["green"][5]); p.px(x, 1, C["lime"][5]) if x % 4 == 0 else None
    return p


def watering_can():
    p = P("watering_can"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w - 5, h, "cyan", 4)
    p.rect(1, 3, w - 6, 3, C["cyan"][2])
    for k in range(5):                                                             # spout
        p.px(w - 5 + k, 6 - k, C["cyan"][3]); p.px(w - 5 + k, 7 - k, C["cyan"][2])
    p.rect(w - 1, 0, w, 2, C["cyan"][5])
    p.rect(1, 0, w - 7, 0, C["cyan"][2]); p.px(0, 1, C["cyan"][2]); p.px(w - 6, 1, C["cyan"][2]); p.px(0, 2, C["cyan"][2])   # handle
    return p


# ================================================================ Elementary, batch 2
# Props with several frames or states: every frame is drawn by the same function so they match.

def box_open():
    p = P("box_open"); w, h = p.w - 1, p.h - 1
    p.block(0, 5, w, h, "skin", 5)
    p.rect(1, 6, w - 1, 9, C["skin"][2]); p.rect(2, 7, w - 2, 9, B[1])              # the dark inside
    p.block(0, 0, 3, 7, "skin", 6); p.block(w - 3, 0, w, 7, "skin", 6)               # flaps standing up
    p.rect(w - 7, h - 6, w - 2, h - 3, PAPER); p.rect(w - 6, h - 5, w - 3, h - 5, C["red"][4])
    return p


def bridge(name, fixed):
    p = P(name); w, h = p.w - 1, p.h - 1
    gap0, gap1 = (w // 2 - 6, w // 2 + 6) if not fixed else (1, 0)
    for x in range(0, w + 1):
        if gap0 <= x <= gap1:
            continue
        ragged = (not fixed) and (x in (gap0 - 1, gap1 + 1))
        y0 = 3 + (2 if ragged else 0)
        p.rect(x, y0, x, h - 1, B[5] if (x // 4) % 2 == 0 else B[4])
        p.px(x, y0, B[7]); p.px(x, h - 1, B[2])
        if x % 4 == 3:
            p.rect(x, y0, x, h - 1, B[2])
    for xs in ((0, gap0 - 1), (gap1 + 1, w)) if not fixed else ((0, w),):
        if xs[1] - xs[0] < 2:
            continue
        p.rect(xs[0], 0, xs[1], 1, B[3]); p.rect(xs[0], 0, xs[1], 0, B[6])            # hand rail
        p.rect(xs[0], h, xs[1], h, B[1])
        for x in (xs[0], xs[1]):
            p.rect(x, 0, x, h, B[2])
    if not fixed:
        p.px(gap0 + 1, 7, B[4]); p.px(gap1 - 1, 9, B[4]); p.px(gap0 + 2, 9, B[3])      # splinters
    return p


def pump(name, state):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2 + 1
    p.block(m - 6, h - 3, m + 6, h, "brown", 4)                                     # plinth
    p.block(m - 2, 6, m + 2, h - 3, "cool", 3)                                      # body
    p.block(m - 3, 4, m + 3, 7, "cool", 4)                                          # head
    p.rect(m - 6, 9, m - 3, 10, K[3]); p.rect(m - 6, 9, m - 4, 9, K[5]); p.rect(m - 7, 10, m - 6, 11, K[2])   # spout
    if state == "broken":                                                          # handle hangs loose
        for k in range(6):
            p.px(m + 3 + k // 2, 6 + k, K[2])
        p.px(m, 12, INK); p.px(m + 1, 13, INK); p.px(m, 14, INK)                      # crack
    else:
        for k in range(8):
            p.px(m + 3 + k, 5 - k // 2, K[2]); p.px(m + 3 + k, 4 - k // 2, K[5])
        p.rect(m + 10, 0, m + 11, 1, B[4])
    if state == "bypass":
        for k in range(h - 12):
            p.px(m - 8 - (k % 2), 12 + k, C["cyan"][5])
        p.rect(m - 1, 12, m + 1, 13, C["amber"][5])                                   # patch
    if state == "fixed":
        p.rect(m - 7, 12, m - 6, h - 4, C["cyan"][6]); p.rect(m - 7, 12, m - 7, h - 4, C["cyan"][4])
        p.rect(m - 9, h - 4, m - 4, h - 4, C["cyan"][7])
    return p


def pot(name, stage):
    """stage: 0 bare soil, 1 sprout, 2 young, 3 flowering, -1 wilted, 10/11 small seedlings."""
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    ph = 7 if stage < 10 else 5
    p.block(m - 5, h - ph, m + 5, h, "orange", 3)
    p.clear(m - 5, h); p.clear(m + 5, h); p.px(m - 4, h, C["orange"][1]); p.px(m + 4, h, C["orange"][1])
    p.block(m - 6, h - ph - 1, m + 6, h - ph + 1, "orange", 4)                         # rim
    p.rect(m - 4, h - ph - 1, m + 4, h - ph - 1, B[1])                                 # soil
    G, L = C["green"], C["lime"]
    top = h - ph - 2
    if stage == -1:
        for k in range(5):
            p.px(m + k // 2, top - k, B[3])
        p.rect(m + 2, top - 4, m + 4, top - 3, C["lime"][2]); p.px(m + 4, top - 2, B[3])
        return p
    height = {0: 1, 1: 4, 2: 8, 3: 12, 10: 3, 11: 8}[stage]
    height = min(height, top)
    for k in range(height):
        p.px(m, top - k, G[3])
    for k in range(2, height, 3):                                                   # leaves, alternating sides
        side = 1 if (k // 3) % 2 == 0 else -1
        p.px(m + side, top - k, G[4]); p.px(m + 2 * side, top - k - 1, L[5]); p.px(m + side, top - k - 1, G[5])
    if height >= 1:
        p.px(m, top - height + 1, L[5])
    if stage == 3:
        ty = top - height
        p.rect(m - 1, ty, m + 1, ty + 1, C["pink"][6]); p.px(m, ty, PAPER); p.px(m, ty + 1, C["amber"][6])
    return p


def rabbit(name, ramp, pose):
    p = P(name); w, h = p.w - 1, p.h - 1
    i = 5 if ramp != "skin" else 6
    if pose == "dig":                                                              # only the back shows
        p.oval(0, 1, w, h, ramp, i)
        p.oval(w - 3, 0, w, 3, "warm", 7)
        p.px(1, h, B[2]); p.px(2, h, B[2])
        return p
    lift = 1 if pose == 1 else 0
    p.oval(2, h - 6 - lift, w, h - lift, ramp, i)                                    # body
    p.oval(w - 2, h - 5 - lift, w, h - 3 - lift, "warm", 7)                          # tail
    p.oval(0, h - 8 - lift, 5, h - 3 - lift, ramp, i)                                # head
    ear = 0 if pose == 0 else 1
    p.rect(1 + ear, max(0, h - 11 - lift), 1 + ear, h - 8 - lift, C[ramp][i - 3]); p.rect(3 + ear, max(0, h - 11 - lift), 3 + ear, h - 8 - lift, C[ramp][i - 3])
    p.rect(2 + ear, max(1, h - 10 - lift), 2 + ear, h - 8 - lift, C["pink"][6])
    p.px(1, h - 6 - lift, INK)
    if lift:
        p.px(3, h, C[ramp][i - 3]); p.px(w - 2, h, C[ramp][i - 3])
    return p


def fish(name, ramp, flip):
    p = P(name); w, h = p.w - 1, p.h - 1
    navy = C["blue"][0]
    p.rect(0, 0, w - 2, h, navy)
    p.rect(1, 1, w - 3, h - 1, C[ramp][5]); p.rect(1, 1, w - 4, 1, C[ramp][7])
    p.px(1, 1, PAPER)
    p.rect(w - 1, 0 if flip else 1, w, h if flip else h - 1, navy)                    # tail
    p.px(w, 0 if not flip else h, navy)
    return p


def hammer(name, swung):
    p = P(name); w, h = p.w - 1, p.h - 1
    if not swung:
        p.block(w // 2 - 1, 3, w // 2 + 1, h, "brown", 5)
        p.block(0, 0, w, 4, "cool", 4)
        p.rect(1, 1, 3, 1, K[7])
    else:
        p.block(3, h // 2 - 1, w, h // 2 + 1, "brown", 5)
        p.block(0, 0, 4, h, "cool", 4)
        p.rect(1, 1, 1, 3, K[7])
    return p


def scissors(name, closed):
    p = P(name); w, h = p.w - 1, p.h - 1
    for sy in (0, 1):
        y = 1 if sy == 0 else h - 3
        p.rect(0, y, 2, y + 2, C["red"][4]); p.px(1, y + 1, (0, 0, 0)); p.clear(1, y + 1)     # handle ring
    spread = 0 if closed else 2
    for k in range(3, w + 1):
        p.px(k, h // 2 - spread * (k - 3) // max(1, w - 3), K[5])
        p.px(k, h // 2 + 1 + spread * (k - 3) // max(1, w - 3), K[3])
    p.px(3, h // 2, K[1])
    return p


def magnifier():
    p = P("magnifier"); w, h = p.w - 1, p.h - 1
    for k in range(4):
        p.px(w - k, h - k, B[3]); p.px(w - k - 1, h - k, B[5])
    p.oval(0, 0, w - 3, h - 3, "cool", 3)
    p.oval(1, 1, w - 4, h - 4, "cyan", 6)
    p.px(2, 2, PAPER); p.px(3, 2, PAPER)
    return p


def paintbrush():
    p = P("paintbrush"); w, h = p.w - 1, p.h - 1
    for k in range(w - 2):
        p.px(w - k, k, B[3]); p.px(w - k - 1, k, B[5]) if k else None
    p.rect(1, h - 3, 3, h - 1, K[5])
    p.rect(0, h - 2, 2, h, C["blue"][4]); p.px(0, h, C["blue"][2]); p.px(1, h - 1, C["blue"][6])
    return p


def net():
    p = P("net"); w, h = p.w - 1, p.h - 1
    for k in range(6):
        p.px(w - k, h - k, B[3]); p.px(w - k - 1, h - k, B[5])
    p.oval(0, 0, w - 5, h - 5, "cool", 3)
    for y in range(1, h - 5):
        for x in range(1, w - 5):
            if (x + y) % 2 == 0 and ((x - (w - 5) / 2.0) ** 2 + (y - (h - 5) / 2.0) ** 2) < ((w - 5) / 2.0 - 0.5) ** 2:
                p.px(x, y, K[6])
            elif ((x - (w - 5) / 2.0) ** 2 + (y - (h - 5) / 2.0) ** 2) < ((w - 5) / 2.0 - 0.5) ** 2:
                p.px(x, y, K[4])
    return p


def padlock():
    p = P("padlock"); w, h = p.w - 1, p.h - 1
    p.rect(1, 0, w - 1, 0, K[3]); p.rect(1, 1, 1, 3, K[3]); p.rect(w - 1, 1, w - 1, 3, K[3]); p.px(2, 0, K[6])
    p.block(0, 3, w, h, "amber", 5)
    p.px(w // 2, h - 3, INK); p.px(w // 2, h - 2, INK)
    return p


def key_item():
    p = P("key_item"); w, h = p.w - 1, p.h - 1
    A = C["amber"]
    p.rect(0, 0, 3, h, A[3]); p.rect(1, 1, 2, h - 1, A[6]); p.px(1, 1, (0, 0, 0)); p.clear(1, 1) if h >= 3 else None
    p.rect(3, 1, w, 1, A[5]); p.rect(3, 2, w, 2, A[3]) if h >= 2 else None
    p.rect(w - 1, 2, w - 1, h, A[4]); p.rect(w - 3, 2, w - 3, h - 1, A[4])
    return p


def item_pencil():
    p = P("item_pencil"); w, h = p.w - 1, p.h - 1
    p.rect(0, 2, w, h - 2, C["amber"][5]); p.rect(0, 2, 0, h - 2, C["amber"][7]); p.rect(w, 2, w, h - 2, C["amber"][3])
    p.rect(0, 0, w, 1, C["pink"][5]); p.rect(0, h - 1, w, h - 1, C["skin"][6]); p.px(w // 2, h, INK)
    return p


def item_tape():
    p = P("item_tape"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "amber", 5)
    p.rect(w // 2 - 1, h // 2 - 1, w // 2, h // 2, B[1])
    p.rect(w - 1, 0, w, 1, PAPER)
    return p


def ruler():
    p = P("ruler"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "amber", 6)
    for y in range(2, h - 1, 2):
        p.rect(w - 2 - (1 if y % 4 == 0 else 0), y, w - 1, y, B[2])
    return p


def water_bottle():
    p = P("water_bottle"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "cyan", 6)
    p.rect(1, h // 2, w - 1, h // 2 + 2, C["red"][4]); p.rect(1, h // 2, w - 1, h // 2, C["red"][6])
    p.rect(1, 0, w - 1, 2, C["blue"][3]); p.px(1, 0, C["blue"][5])
    return p


def clipboard():
    p = P("clipboard"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "brown", 5)
    p.rect(2, 3, w - 2, h - 2, PAPER)
    for y in range(5, h - 2, 3):
        p.rect(3, y, w - 3, y, K[4]); p.px(3, y, C["green"][4])
    p.rect(w // 2 - 2, 0, w // 2 + 2, 2, K[5]); p.rect(w // 2 - 2, 2, w // 2 + 2, 2, K[2])
    return p


def paper_flat():
    p = P("paper_flat"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.rect(1, 1, w - 1, h - 1, PAPER)
    for y in range(2, h - 1, 2):
        p.rect(2, y, w - 2, y, C["blue"][4])
    return p


def post_sign(name, kind):
    """A board on a post. kind: snack0 / snack1 / snack2 / 123 / sun."""
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    bh = h * 3 // 5 if kind != "sun" else h // 2
    p.block(m - 1, bh - 1, m + 1, h, "brown", 3)
    if kind == "sun":
        p.oval(0, 0, w, bh, "amber", 6)
        for x, y in ((0, 0), (w, 0), (0, bh), (w, bh)):
            p.px(x, y, C["amber"][4])
        p.px(m - 1, bh // 2, B[2]); p.px(m + 1, bh // 2, B[2])
        return p
    p.block(0, 0, w, bh, "brown", 5 if kind.startswith("snack") else 4)
    if kind == "123":
        p.rect(2, 2, w - 2, bh - 2, PAPER)
        for i, ramp in enumerate(("red", "amber", "green")):
            x = 3 + i * ((w - 5) // 3)
            p.rect(x, 4, x + 1, bh - 4, C[ramp][4]); p.px(x, 3, C[ramp][5])
    elif kind == "snack0":
        for x in range(3, w - 2, 3):
            p.rect(x, 2, x, bh - 2, B[3])
    elif kind == "snack1":
        p.rect(2, 2, w - 2, bh - 2, C["amber"][6])
        for x in range(3, w - 2, 3):
            p.rect(x, 3, x, bh - 3, C["red"][4])
    else:
        p.rect(2, 2, w - 2, bh - 2, C["amber"][6])
        p.rect(4, 4, w - 4, 4, C["red"][4]); p.rect(4, 7, w - 6, 7, C["red"][4])
        p.oval(w - 8, bh - 7, w - 3, bh - 3, "orange", 5)
    return p


def coin_tray(name, full):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 4)
    p.rect(2, 2, w - 2, h - 2, K[1])
    for x in range(3, w - 3, 5):
        if full:
            p.rect(x, 3, x + 2, h - 3, C["amber"][5]); p.px(x, 3, C["amber"][7])
        else:
            p.rect(x, h - 3, x + 2, h - 3, K[3])
    return p


def coin(name, edge_on):
    p = P(name); w, h = p.w - 1, p.h - 1
    A = C["amber"]
    p.rect(0, 0, w, h, A[2])
    if w >= 2 and h >= 2:
        p.rect(1, 1, w - 1, h - 1, A[5]); p.rect(1, 1, 1, h - 1, A[7])
        if not edge_on and w >= 4:
            p.rect(w // 2, 2, w // 2, h - 2, A[3])
    for x, y in ((0, 0), (w, 0), (0, h), (w, h)):
        p.clear(x, y) if not edge_on else None
    return p


def sale_tag():
    p = P("sale_tag"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "red", 4)
    p.clear(0, 1); p.clear(w, 1); p.px(w // 2, 0, K[3]); p.px(w // 2, 2, PAPER)
    p.rect(2, h - 3, w - 2, h - 3, PAPER)
    return p


def cookie_icon():
    p = P("cookie_icon"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "skin", 5)
    p.px(2, 2, B[2]); p.px(w - 2, 3, B[2]); p.px(3, h - 1, B[2])
    return p


def toy_train():
    p = P("toy_train"); w, h = p.w - 1, p.h - 1
    half = w // 2
    p.block(0, h - 7, half - 1, h - 2, "red", 4)                                    # engine
    p.block(0, h - 10, 3, h - 6, "red", 5); p.rect(1, h - 9, 2, h - 8, C["cyan"][7])
    p.rect(half - 3, h - 9, half - 2, h - 7, K[2]); p.px(half - 3, h - 10, K[5])
    p.block(half + 1, h - 6, w, h - 2, "blue", 4)                                   # wagon
    p.rect(half - 1, h - 3, half + 1, h - 3, K[2])
    for x in (1, half - 3, half + 2, w - 2):
        p.oval(x, h - 2, x + 2, h, "cool", 2); p.px(x + 1, h - 1, K[6])
    return p


def toy_spill():
    p = P("toy_spill"); w, h = p.w - 1, p.h - 1
    step = (w + 1) // 3
    for i, ramp in enumerate(("blue", "amber", "red")):
        p.oval(i * step, 0 if i == 1 else 1, i * step + step - 1, h if i != 1 else h - 1, ramp, 5)
    return p


def toy_ball():
    p = P("toy_ball"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "red", 5); p.rect(0, h // 2, w, h // 2, PAPER); p.px(1, 1, PAPER)
    return p


def toy_duck():
    p = P("toy_duck"); w, h = p.w - 1, p.h - 1
    p.oval(0, 2, w, h, "amber", 6); p.oval(w - 3, 0, w, 3, "amber", 6)
    p.px(w, 2, C["orange"][5]); p.px(w - 1, 1, INK)
    return p


def toy_robot():
    p = P("toy_robot"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, 3, "cool", 6); p.block(0, 3, w, h - 1, "cool", 4)
    p.px(1, 1, C["red"][5]); p.px(w - 1, 1, C["red"][5]); p.px(0, h, K[2]); p.px(w, h, K[2])
    return p


def teddy_icon():
    p = P("teddy_icon"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, 2, 2, "brown", 4); p.oval(w - 2, 0, w, 2, "brown", 4)
    p.oval(0, 1, w, h - 2, "brown", 5); p.oval(1, h - 4, w - 1, h, "brown", 4)
    p.px(2, 3, INK); p.px(w - 2, 3, INK); p.px(w // 2, 4, C["skin"][6]); p.px(w // 2, h - 3, C["red"][4])
    return p


def bunting(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    ramps = ("red", "amber", "blue", "green", "pink")
    for x in range(0, w + 1):
        p.px(x, 0 if h < 6 else (1 if 2 < x < w - 2 else 0), B[2])                    # the string
    fw = 4
    for i, x0 in enumerate(range(1, w - fw + 2, fw + 1)):
        r = C[ramps[i % len(ramps)]]
        for k in range(min(fw, h)):
            p.rect(x0 + k // 2, 1 + k, x0 + fw - 1 - (k + 1) // 2, 1 + k, r[5] if k == 0 else r[4])
        p.px(x0, 1, r[6])
    return p


def piece_hill():
    p = P("piece_hill"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, 2 * h + 1, "green", 4)
    for x in range(3, w - 2, 5):
        p.px(x, h - 1 - (x % 3), C["lime"][5]); p.px(x + 1, h - 1 - (x % 3), C["lime"][4])
    return p


def piece_sun():
    p = P("piece_sun"); w, h = p.w - 1, p.h - 1
    for x, y in ((w // 2, 0), (w // 2, h), (0, h // 2), (w, h // 2), (1, 1), (w - 1, 1), (1, h - 1), (w - 1, h - 1)):
        p.px(x, y, C["amber"][4])
    p.oval(1, 1, w - 1, h - 1, "amber", 6)
    return p


def piece_tent():
    p = P("piece_tent"); w, h = p.w - 1, p.h - 1
    m = w // 2
    for y in range(1, h + 1):
        half = max(0, y * m // h)
        for x in range(m - half, m + half + 1):
            stripe = ((x - m + 20) // 2) % 2 == 0
            p.px(x, y, C["red"][4] if stripe else PAPER)
        p.px(m - half, y, C["red"][1]); p.px(m + half, y, C["red"][1])
    p.rect(m - 1, h - 4, m + 1, h, B[1])                                              # doorway
    p.px(m, 0, C["amber"][6]); p.rect(m - half, h, m + half, h, C["red"][1])
    return p


def piece_ticket():
    p = P("piece_ticket"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "pink", 6)
    p.clear(0, h // 2); p.clear(w, h // 2)
    p.px(2, h // 2, C["pink"][3]); p.px(w // 2, h // 2, C["pink"][3]); p.px(w - 2, h // 2, C["pink"][3])
    return p


def piece_wheel():
    p = P("piece_wheel"); w, h = p.w - 1, p.h - 1
    import math
    cx, cy, r = w / 2.0, h / 2.0, w / 2.0 - 1
    p.oval(1, 1, w - 1, h - 1, "amber", 5)
    p.oval(3, 3, w - 3, h - 3, "cyan", 7)
    for a in range(0, 360, 45):
        for t in range(2, int(r)):
            p.px(round(cx + math.cos(math.radians(a)) * t), round(cy + math.sin(math.radians(a)) * t), C["red"][4])
        ramp = ("red", "blue", "green", "pink")[(a // 45) % 4]
        gx, gy = round(cx + math.cos(math.radians(a)) * r), round(cy + math.sin(math.radians(a)) * r)
        p.rect(gx - 1, gy, gx, gy + 1, C[ramp][5]); p.px(gx - 1, gy, C[ramp][7])     # gondolas
    p.rect(round(cx) - 1, round(cy) - 1, round(cx), round(cy), B[2])
    return p


def scrap_pile():
    p = P("scrap_pile"); w, h = p.w - 1, p.h - 1
    for i, (ramp, x0, y0, x1, y1) in enumerate((("red", 0, h - 3, 8, h), ("blue", 5, h - 6, 12, h - 2), ("amber", 9, h - 4, w - 3, h),
                                                 ("pink", 8, 0, 13, 3), ("green", w - 6, h - 3, w, h))):
        p.block(x0, y0, x1, y1, ramp, 5)
    p.rect(3, h - 5, 6, h - 4, PAPER); p.rect(w - 9, h - 6, w - 6, h - 5, PAPER)
    return p


def note_block(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, ramp, 4)
    m = w // 2
    p.rect(m, 2, m, h - 3, PAPER); p.rect(m, 2, m + 2, 2, PAPER); p.rect(m - 1, h - 3, m, h - 2, PAPER)
    return p


DRAW = {
    "box_open": box_open,
    "bridge_broken": lambda: bridge("bridge_broken", False), "bridge_fixed": lambda: bridge("bridge_fixed", True),
    "pump_broken": lambda: pump("pump_broken", "broken"), "pump_bypass": lambda: pump("pump_bypass", "bypass"),
    "pump_fixed": lambda: pump("pump_fixed", "fixed"),
    "pot_0": lambda: pot("pot_0", 0), "pot_1": lambda: pot("pot_1", 1), "pot_2": lambda: pot("pot_2", 2),
    "pot_3": lambda: pot("pot_3", 3), "pot_wilt": lambda: pot("pot_wilt", -1),
    "seedling_small": lambda: pot("seedling_small", 10), "seedling_tall": lambda: pot("seedling_tall", 11),
    "hammer_0": lambda: hammer("hammer_0", False), "hammer_1": lambda: hammer("hammer_1", True),
    "scissors_0": lambda: scissors("scissors_0", False), "scissors_1": lambda: scissors("scissors_1", True),
    "magnifier": magnifier, "paintbrush": paintbrush, "net": net, "padlock": padlock, "key_item": key_item,
    "item_pencil": item_pencil, "item_tape": item_tape, "ruler": ruler, "water_bottle": water_bottle,
    "clipboard": clipboard, "paper_flat": paper_flat,
    "snack_sign_0": lambda: post_sign("snack_sign_0", "snack0"), "snack_sign_1": lambda: post_sign("snack_sign_1", "snack1"),
    "snack_sign_2": lambda: post_sign("snack_sign_2", "snack2"),
    "sign_123": lambda: post_sign("sign_123", "123"), "sign_sun": lambda: post_sign("sign_sun", "sun"),
    "coin_tray_0": lambda: coin_tray("coin_tray_0", True), "coin_tray_1": lambda: coin_tray("coin_tray_1", False),
    "coin_0": lambda: coin("coin_0", False), "coin_1": lambda: coin("coin_1", True),
    "coin_2": lambda: coin("coin_2", False), "coin_3": lambda: coin("coin_3", True),
    "sale_tag": sale_tag, "cookie_icon": cookie_icon,
    "toy_train": toy_train, "toy_spill": toy_spill, "toy_ball": toy_ball, "toy_duck": toy_duck,
    "toy_robot": toy_robot, "teddy_icon": teddy_icon,
    "pennants": lambda: bunting("pennants"), "piece_bunting": lambda: bunting("piece_bunting"),
    "piece_hill": piece_hill, "piece_sun": piece_sun, "piece_tent": piece_tent, "piece_ticket": piece_ticket,
    "piece_wheel": piece_wheel, "scrap_pile": scrap_pile,
    "note_block_0": lambda: note_block("note_block_0", "red"), "note_block_1": lambda: note_block("note_block_1", "amber"),
    "note_block_2": lambda: note_block("note_block_2", "blue"),
    "drum_parade": lambda: drum("drum_parade", False), "drum_dent_half": lambda: drum("drum_dent_half", True),
    "drum_patched": lambda: drum("drum_patched", False),
    "floor_fresh": floor_fresh, "floor_nailed": floor_nailed, "floor_new": floor_new,
    "floor_broken": floor_broken, "plank_stack": plank_stack,
    "crate": crate, "barrel": barrel,
    "box_closed": lambda: box("box_closed", C["red"][4]), "box_closed_b": lambda: box("box_closed_b", C["blue"][4]),
    "bin_blue": bin_blue, "bucket": bucket, "cash_box": cash_box, "toolbox": toolbox, "school_bag": school_bag,
    "toy_box_a": lambda: toy_box("toy_box_a", "blue"), "toy_box_b": lambda: toy_box("toy_box_b", "red"),
    "toy_box_c": lambda: toy_box("toy_box_c", "green"),
    "paint_cans": paint_cans, "snack_jars": snack_jars,
    "bench": bench, "bookshelf": bookshelf, "cabinet": cabinet, "canteen_counter": canteen_counter, "dock": dock,
    "easel": easel, "stall": stall,
    "shelf_supply_a": lambda: shelf("shelf_supply_a", 0), "shelf_supply_b": lambda: shelf("shelf_supply_b", 4),
    "toy_shelf": toy_shelf, "menu_board": menu_board, "music_stand": music_stand, "calculator_big": calculator_big,
    "chart_board": chart_board, "price_sign": price_sign, "sign_class": sign_class, "shade_awning": shade_awning,
    "exercise_mat": exercise_mat, "notebook": notebook, "notes": notes, "storybook": storybook,
    "teddy": lambda: teddy("teddy"), "teddy_big": lambda: teddy("teddy_big"), "toy_blocks": toy_blocks,
    "drum": lambda: drum("drum", False), "drum_dented": lambda: drum("drum_dented", True),
    "rug_round": rug_round, "beanbag": beanbag, "rope_coil": rope_coil,
    "bush": bush, "stone": lambda: rock("stone"), "boulder": lambda: rock("boulder", True), "slab": slab,
    "log": log, "fence": fence, "pond": pond, "lily_pad": lily_pad, "reeds": reeds, "burrow": burrow,
    "carrot_patch": carrot_patch, "seed_tray": seed_tray, "watering_can": watering_can,
}
for _c, _r in (("brown", "brown"), ("gray", "cool"), ("tan", "skin")):
    for _pose in (0, 1, "dig"):
        DRAW["rabbit_%s_%s" % (_c, _pose)] = (lambda n, r, q: lambda: rabbit(n, r, q))("rabbit_%s_%s" % (_c, _pose), _r, _pose)
for _c, _r in (("gold", "amber"), ("orange", "orange"), ("silver", "cool")):
    for _f in (0, 1):
        DRAW["fish_%s_%d" % (_c, _f)] = (lambda n, r, q: lambda: fish(n, r, q))("fish_%s_%d" % (_c, _f), _r, _f == 1)
# ================================================================ High School batch
# Same rules as before: every frame or state of a prop comes from one function.

def barber_chair():
    p = P("barber_chair"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.oval(2, h - 3, w - 2, h, "cool", 4)                                           # round base
    p.block(m - 1, h - 9, m + 1, h - 2, "cool", 5)                                  # column
    p.block(1, h - 8, w - 1, h - 6, "cool", 3)                                      # foot rest bar
    p.block(2, 0, w - 2, 9, "red", 4, round_=True)                                  # back rest
    p.rect(4, 2, w - 4, 2, C["red"][6])
    p.block(1, 8, w - 1, 12, "red", 5)                                              # seat
    p.block(0, 6, 2, 10, "cool", 5); p.block(w - 2, 6, w, 10, "cool", 5)             # arm rests
    return p


def barber_pole(name, phase):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.rect(0, 0, w, h, K[2])
    for y in range(2, h - 1):
        for x in range(1, w):
            v = (x + y + phase * 2) % 8
            c = C["red"][4] if v < 2 else (C["blue"][4] if 4 <= v < 6 else PAPER)
            if x == w - 1:
                c = step_dark(c)
            p.px(x, y, c)
    p.rect(0, 0, w, 1, K[5]); p.rect(1, 0, w - 1, 0, K[7]); p.rect(0, h - 1, w, h, K[4])
    return p


def step_dark(c):
    return tuple(max(0, int(v * 0.82)) for v in c)


def mirror_wall():
    p = P("mirror_wall"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "amber", 4)
    p.rect(2, 2, w - 2, h - 2, C["cyan"][6])
    p.rect(2, 2, w - 2, 2, C["cyan"][4]); p.rect(2, 2, 2, h - 2, C["cyan"][4])
    for k in range(4):                                                             # glints
        p.px(4 + k, 7 - k, PAPER); p.px(5 + k, 8 - k, PAPER)
    p.px(w - 5, h - 6, PAPER); p.px(w - 4, h - 7, PAPER)
    return p


def hair_tonic():
    p = P("hair_tonic"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, "teal", 5)
    p.rect(1, h - 4, w - 1, h - 2, PAPER); p.px(2, h - 3, C["red"][4])
    p.rect(w // 2 - 1, 1, w // 2 + 1, 3, K[4]); p.rect(w // 2 - 1, 0, w // 2 + 1, 0, C["red"][4])
    return p


def chalkboard():
    p = P("chalkboard"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 4)
    p.rect(2, 2, w - 2, h - 3, C["teal"][1]); p.rect(2, 2, w - 2, 2, C["teal"][0])
    for y in (5, 8, 11):
        p.rect(4, y, w // 2 - 2, y, PAPER)
    p.rect(w // 2 + 2, 5, w - 5, 5, PAPER); p.rect(w // 2 + 2, 5, w // 2 + 2, 11, PAPER)   # a diagram
    p.rect(w // 2 + 2, 11, w - 5, 11, PAPER); p.rect(w - 5, 5, w - 5, 11, PAPER)
    p.rect(w // 2 + 5, 8, w - 8, 8, C["amber"][6])
    p.rect(3, h - 2, 6, h - 2, PAPER); p.rect(8, h - 2, 9, h - 2, C["pink"][6])       # chalk on the ledge
    return p


def choir_riser():
    p = P("choir_riser"); w, h = p.w - 1, p.h - 1
    t = (h + 1) // 3
    for i in range(3):
        inset = (2 - i) * (w // 12)
        p.block(inset, i * t, w - inset, min(h, (i + 1) * t + 1), "pink", 4)
        p.rect(inset + 1, i * t + 1, w - inset - 1, i * t + 2, C["pink"][6])
    return p


def committee_table():
    p = P("committee_table"); w, h = p.w - 1, p.h - 1
    p.block(2, h - 5, 5, h, "brown", 3); p.block(w - 5, h - 5, w - 2, h, "brown", 3)
    p.block(0, 1, w, h - 3, "violet", 4)
    p.rect(1, 2, w - 1, 3, C["violet"][6])
    for x in range(6, w - 4, 9):                                                    # cloth folds
        p.rect(x, 5, x, h - 4, C["violet"][2])
    p.rect(w // 2 - 3, 0, w // 2 + 3, 1, PAPER)                                      # a name card
    return p


def conductor_stand():
    p = P("conductor_stand"); w, h = p.w - 1, p.h - 1
    p.block(1, 4, w - 1, h, "brown", 4)
    p.rect(3, 7, w - 3, h - 3, B[3]); p.rect(3, 7, w - 3, 7, B[2])
    p.block(0, 0, w, 5, "brown", 5)
    p.rect(2, 1, w - 2, 3, PAPER); p.rect(3, 2, w - 4, 2, K[4])
    return p


def curtain():
    p = P("curtain"); w, h = p.w - 1, p.h - 1
    R = C["red"]
    for x in range(0, w + 1):
        k = x % 4
        col = R[4] if k == 0 else (R[3] if k in (1, 3) else R[2])
        p.rect(x, 0, x, h, col)
    p.rect(0, 0, w, 2, C["amber"][5]); p.rect(0, 0, w, 0, C["amber"][7]); p.rect(0, 2, w, 2, C["amber"][3])
    p.rect(0, h, w, h, R[1])
    return p


def desk_lamp(name, on):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.oval(1, h - 2, w - 1, h, "cool", 3)
    p.rect(m, 5, m, h - 2, K[3]); p.px(m - 1, 6, K[5])
    p.block(0, 0, w, 5, "teal", 5 if on else 4)
    p.clear(0, 0); p.clear(w, 0)
    if on:
        p.rect(2, 5, w - 2, 6, C["amber"][7]); p.rect(3, 7, w - 3, 7, C["amber"][6])
    else:
        p.rect(2, 5, w - 2, 5, K[2])
    return p


def filewall(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 3)
    cols, rows = 4, 3
    cw, ch = (w - 2) // cols, (h - 2) // rows
    for r in range(rows):
        for c in range(cols):
            x0, y0 = 2 + c * cw, 2 + r * ch
            x1, y1 = x0 + cw - 2, y0 + ch - 2
            if opened and (r * cols + c) % 3 == 1:
                p.rect(x0, y0, x1, y1, B[1])                                        # pulled out: files show
                for fx in range(x0 + 1, x1, 2):
                    p.rect(fx, y0 + 1, fx, y1 - 1, PAPER)
                p.rect(x0, y1, x1, y1, B[5])
            else:
                p.block(x0, y0, x1, y1, "brown", 5)
                p.rect((x0 + x1) // 2 - 1, (y0 + y1) // 2, (x0 + x1) // 2 + 1, (y0 + y1) // 2, C["amber"][6])
    return p


def filing_cabinet():
    p = P("filing_cabinet"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 4)
    dh = (h - 1) // 3
    for i in range(3):
        y0 = 1 + i * dh
        p.block(1, y0, w - 1, y0 + dh - 1, "cool", 5)
        p.rect(w // 2 - 1, y0 + dh // 2, w // 2 + 1, y0 + dh // 2, K[2])
        p.rect(w // 2 - 1, y0 + 1, w // 2 + 1, y0 + 2, PAPER)
    return p


def front_desk():
    p = P("front_desk"); w, h = p.w - 1, p.h - 1
    p.block(0, h // 3, w, h, "brown", 4)
    for x in range(4, w - 3, max(4, w // 6)):
        p.rect(x, h // 3 + 4, x, h - 2, B[3])
    p.block(w // 4, h // 2, w - w // 4, h - 3, "brown", 5)                           # front panel
    p.rect(w // 2 - 3, h // 2 + 3, w // 2 + 3, h // 2 + 5, C["amber"][6])
    p.block(0, h // 3 - 3, w, h // 3 + 2, "brown", 6)                                # top slab
    p.oval(w - 9, h // 3 - 7, w - 4, h // 3 - 3, "amber", 6)                         # bell
    p.rect(3, h // 3 - 5, 8, h // 3 - 3, PAPER)                                      # guest book
    return p


def wait_bench():
    p = P("wait_bench"); w, h = p.w - 1, p.h - 1
    p.block(2, 3, 4, h, "brown", 2); p.block(w - 4, 3, w - 2, h, "brown", 2)
    p.block(0, 0, w, 4, "brown", 5)
    for x in range(5, w - 3, 6):
        p.px(x, 2, B[3])
    return p


def rocking_chair():
    p = P("rocking_chair"); w, h = p.w - 1, p.h - 1
    for k in range(w + 1):                                                         # rockers: a shallow curve
        y = h - (1 if k in (0, 1, w - 1, w) else 0)
        p.px(k, y, B[2])
    p.block(2, 0, w - 2, h // 2, "brown", 4)                                        # back
    for x in range(4, w - 3, 2):
        p.rect(x, 2, x, h // 2 - 2, B[2])
    p.block(1, h // 2, w - 1, h // 2 + 3, "pink", 5)                                # cushion
    p.rect(2, h // 2 + 4, 2, h - 1, B[3]); p.rect(w - 2, h // 2 + 4, w - 2, h - 1, B[3])
    return p


def seat_row():
    p = P("seat_row"); w, h = p.w - 1, p.h - 1
    n = 4
    sw = (w + 1) // n
    for i in range(n):
        x0 = i * sw
        p.block(x0, 0, x0 + sw - 2, h * 2 // 3, "red", 4, round_=True)               # back
        p.rect(x0 + 2, 2, x0 + sw - 4, 2, C["red"][6])
        p.block(x0, h * 2 // 3 - 2, x0 + sw - 2, h - 3, "red", 3)                    # folded seat
        p.rect(x0 + 1, h - 2, x0 + 1, h, K[2]); p.rect(x0 + sw - 3, h - 2, x0 + sw - 3, h, K[2])
    return p


def stage_floor():      # a plain tile of stage planks (the room stretches it over the stage)
    p = P("stage_floor"); w, h = p.w - 1, p.h - 1
    p.rect(0, 0, w, h, B[4])
    ph = max(4, (h + 1) // 8)
    for r, y in enumerate(range(0, h + 1, ph)):
        p.rect(0, y, w, y, B[5]); p.rect(0, y + ph - 1, w, y + ph - 1, B[2])
        sx = (r * 19 + 7) % (w + 1)
        p.rect(sx, y, sx, y + ph - 1, B[2])
        p.rect((sx + 11) % (w + 1), y + ph // 2, (sx + 14) % (w + 1), y + ph // 2, B[3])
    return p


def rack(name, messy):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.rect(1, 1, 2, h, K[3]); p.rect(w - 2, 1, w - 1, h, K[3])                       # posts
    p.rect(0, 0, w, 1, K[4]); p.rect(0, 0, w, 0, K[6])
    p.rect(0, h, 4, h, K[2]); p.rect(w - 4, h, w, h, K[2])
    ramps = ("red", "amber", "blue", "green", "violet", "pink")
    x = 4
    i = 0
    while x < w - 6:
        r = ramps[i % len(ramps)]
        drop = (i * 3 % 4) if messy else 0
        length = (7 + (i * 5 % 5)) if messy else 9
        p.px(x + 1, 2, K[2])                                                        # hanger hook
        p.block(x, 3 + drop, x + 3, min(h - 3, 3 + drop + length), r, 5)
        x += 4 if not messy else 3 + (i % 2)
        i += 1
    if messy:                                                                      # things fallen on the floor
        p.block(5, h - 3, 11, h, "amber", 5); p.block(w - 13, h - 2, w - 7, h, "pink", 5)
    return p


def mannequin(name, styled):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.oval(1, h - 2, w - 1, h, "cool", 3)
    p.rect(m, h // 2 + 2, m, h - 2, K[3])
    p.block(1, 6, w - 1, h // 2 + 3, "warm", 7, round_=True)                         # torso form
    p.rect(m - 1, 4, m + 1, 6, W7[6])                                               # neck
    p.oval(m - 2, 0, m + 2, 4, "warm", 7)                                           # head knob
    if styled:
        p.block(0, 6, w, h // 2 + 4, "violet", 4)
        p.rect(m, 7, m, h // 2 + 3, C["violet"][2]); p.px(m + 2, 9, C["amber"][6]); p.px(m + 2, 12, C["amber"][6])
        p.rect(m - 2, 7, m - 1, 8, PAPER)                                           # collar
        p.rect(m - 4, 0, m + 4, 1, C["red"][4]); p.rect(m - 2, 0, m + 2, 0, C["red"][6])   # hat
    return p


def till():
    p = P("till"); w, h = p.w - 1, p.h - 1
    p.block(0, 4, w, h, "cool", 2)
    p.block(2, 0, w - 2, 5, "cool", 3)
    p.rect(4, 1, w - 4, 3, C["green"][6]); p.rect(w - 8, 2, w - 5, 2, C["green"][3])
    for r in range(2):
        for x in range(3, w - 2, 3):
            p.rect(x, 7 + r * 2, x + 1, 7 + r * 2, PAPER)
    p.rect(1, h - 2, w - 1, h - 2, K[4]); p.rect(w // 2 - 1, h - 1, w // 2 + 1, h - 1, K[5])
    return p


def test_rig():
    p = P("test_rig"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.rect(3, 3, w - 3, h - 4, K[1])
    p.block(w // 2 - 4, h - 9, w // 2 + 4, h - 4, "cool", 5)                         # platform
    p.block(w // 2 - 2, h - 13, w // 2 + 2, h - 9, "amber", 5)                       # the part under test
    p.rect(w // 2, 3, w // 2, h - 14, K[5])                                         # probe arm
    p.px(4, 4, C["green"][6]); p.px(w - 4, 4, C["red"][5])
    return p


def nest_box():
    p = P("nest_box"); w, h = p.w - 1, p.h - 1
    p.block(1, 5, w - 1, h, "brown", 4)
    for x in range(4, w - 2, 4):
        p.rect(x, 7, x, h - 1, B[3])
    p.oval(w // 2 - 3, h - 8, w // 2 + 3, h, "brown", 1)                             # the opening
    p.rect(w // 2 - 2, h - 5, w // 2 + 2, h, INK)
    for y in range(0, 6):                                                          # thatch roof
        half = (w // 2) * (y + 2) // 7
        p.rect(w // 2 - half, y, w // 2 + half, y, C["amber"][5] if y % 2 == 0 else C["amber"][4])
    p.rect(1, 5, w - 1, 5, C["amber"][2])
    return p


def scarecrow():
    p = P("scarecrow"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.rect(m - 1, h // 2, m, h, B[3]); p.px(m - 1, h // 2, B[5])                     # post
    p.rect(1, h // 3 + 1, w - 1, h // 3 + 2, B[4])                                   # cross bar
    p.block(m - 4, h // 3, m + 4, h * 2 // 3 + 1, "blue", 4)                         # shirt
    p.rect(m - 1, h // 3 + 2, m, h * 2 // 3, C["blue"][2])
    p.px(1, h // 3 + 3, C["amber"][6]); p.px(w - 1, h // 3 + 3, C["amber"][6])        # straw hands
    p.oval(m - 3, 2, m + 3, h // 3, "skin", 6)                                      # sack head
    p.px(m - 1, h // 3 - 3, INK); p.px(m + 1, h // 3 - 3, INK)
    p.rect(m - 5, 2, m + 5, 3, C["amber"][5]); p.rect(m - 3, 0, m + 3, 2, C["amber"][6]); p.rect(m - 5, 3, m + 5, 3, C["amber"][3])
    return p


def veg_row():
    p = P("veg_row"); w, h = p.w - 1, p.h - 1
    p.block(0, h // 2, w, h, "brown", 2)
    for i, x in enumerate(range(2, w - 2, max(4, w // 5))):
        ramp = ("orange", "red", "orange", "violet")[i % 4]
        p.rect(x, h // 2 - 1, x + 2, h // 2 + 1, C[ramp][5]); p.px(x, h // 2 - 1, C[ramp][7])
        p.oval(x - 2, h // 2 - 7, x + 4, h // 2 - 1, "green", 4)
        p.px(x, h // 2 - 6, C["lime"][5]); p.px(x + 2, h // 2 - 4, C["lime"][5])
    return p


def rail_piece():
    p = P("rail_piece"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, 3, h, "brown", 3); p.block(w - 3, 0, w, h, "brown", 3)
    p.block(2, 2, w - 2, 5, "brown", 5); p.block(2, h - 5, w - 2, h - 2, "brown", 4)
    return p


def record_box():
    p = P("record_box"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "skin", 5)
    p.rect(1, 1, w - 1, 2, C["skin"][7])
    p.rect(3, 4, w - 3, h - 2, PAPER); p.rect(4, 5, w - 5, 5, K[3]); p.rect(4, h - 3, w - 7, h - 3, K[4])
    return p


def assembly_runner():
    p = P("assembly_runner"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.rect(1, 1, w - 1, h - 1, K[2])
    for x in range(2, w - 1, 4):
        p.rect(x, 1, x, h - 1, K[4])
    p.rect(1, 1, w - 1, 1, K[5])
    return p


def basket(name, handle):
    p = P(name); w, h = p.w - 1, p.h - 1
    top = h // 2
    p.block(0, top, w, h, "skin", 5)
    p.clear(0, h); p.clear(w, h)
    for y in range(top + 2, h, 2):                                                 # weave
        for x in range(1, w, 2):
            p.px(x + (y // 2) % 2, y, C["skin"][3])
    p.rect(0, top, w, top + 1, C["skin"][6]); p.rect(0, top, w, top, C["skin"][3])
    for i, x in enumerate(range(1, w - 2, 3)):                                     # fruit
        p.oval(x, top - 4 + (i % 2), x + 3, top, "red" if i % 3 else "green", 5)
    if handle:
        m = w // 2
        for k in range(-4, 5):
            p.px(m + k, top - 7 + abs(k) * abs(k) // 5, B[3])
    return p


def crate_n(name, pips):
    p = crate_named(name)
    w, h = p.w - 1, p.h - 1
    p.rect(w // 2 - 3, h // 2 - 2, w // 2 + 3, h // 2 + 2, B[2])
    for i in range(pips):
        p.rect(w // 2 - 2 + i * 2, h // 2 - 1, w // 2 - 2 + i * 2, h // 2 + 1, PAPER)
    return p


def crate_named(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 5)
    p.rect(2, 2, w - 2, h - 2, B[4])
    for x in range(3, w - 2, 3):
        p.rect(x, 2, x, h - 2, B[3])
    for x, y in ((1, 1), (w - 1, 1), (1, h - 1), (w - 1, h - 1)):
        p.px(x, y, K[5])
    return p


def kit(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    if not opened:
        p.block(0, 1, w, h, "skin", 5)
        p.block(0, 0, w, 3, "skin", 6)
        p.rect(w // 2 - 1, 0, w // 2, h - 1, C["red"][4]); p.rect(1, h // 2, w - 1, h // 2, C["red"][4])
        return p
    p.block(0, 4, w, h, "skin", 5)
    p.rect(1, 5, w - 1, 7, B[1])
    p.block(0, 0, 2, 5, "skin", 6); p.block(w - 2, 0, w, 5, "skin", 6)
    for i, ramp in enumerate(("blue", "amber", "red")):
        x = 3 + i * ((w - 6) // 3)
        p.block(x, 3, x + 2, 7, ramp, 5)
    return p


def medkit():
    p = P("medkit"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "warm", 7)
    p.rect(w // 2 - 2, 0, w // 2 + 2, 0, K[3]); p.rect(w // 2 - 2, 1, w // 2 - 2, 2, K[3]); p.rect(w // 2 + 2, 1, w // 2 + 2, 2, K[3])
    m, c = w // 2, (h + 2) // 2
    p.rect(m - 1, c - 2, m, c + 2, C["red"][4]); p.rect(m - 3, c - 1, m + 2, c, C["red"][4])
    return p


def parts_bin(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, ramp, 4)
    p.rect(1, 4, w - 1, 5, C[ramp][2])
    for x in range(2, w - 1, 3):                                                   # parts poking out
        p.rect(x, 1 + (x % 2), x + 1, 4, K[5]); p.px(x, 1 + (x % 2), K[7])
    p.rect(w // 2 - 2, h - 3, w // 2 + 2, h - 2, PAPER)
    return p


def shop_bag():
    p = P("shop_bag"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h, "orange", 5)
    p.rect(2, 0, 2, 3, B[3]); p.rect(w - 2, 0, w - 2, 3, B[3]); p.rect(2, 0, w - 2, 0, B[3])
    p.rect(2, 5, w - 2, 7, PAPER); p.px(w // 2, 6, C["red"][4])
    return p


def sample_tray():
    p = P("sample_tray"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "warm", 6)
    for i, ramp in enumerate(("red", "amber", "green", "blue")):
        x = 2 + i * ((w - 3) // 4)
        p.rect(x, 2, x + 2, h - 2, C[ramp][5]); p.px(x, 2, C[ramp][7])
    return p


def tray(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.rect(2, 2, w - 2, h - 2, K[1])
    p.rect(w // 2 - 4, h // 2 - 1, w // 2 + 4, h // 2 + 1, C[ramp][5]); p.rect(w // 2 - 4, h // 2 - 1, w // 2 + 4, h // 2 - 1, C[ramp][7])
    return p


def ledger():
    p = P("ledger"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 5)
    p.rect(1, 1, 2, h - 1, B[3])
    p.rect(4, 2, w - 2, h - 2, PAPER)
    for y in range(4, h - 2, 2):
        p.rect(5, y, w - 3, y, C["orange"][4])
    return p


def radio(name, state):
    p = P(name); w, h = p.w - 1, p.h - 1
    ramp = "orange" if state == "diy" else "blue"
    top = 7
    p.block(0, top, w, h, ramp, 3 if ramp == "blue" else 4)
    ax = w - 6
    for k in range(top):                                                           # antenna
        p.px(ax + (k // 3 if state != "on" else 0), top - 1 - k, K[4])
    p.px(ax + (2 if state != "on" else 0), 0, K[7])
    p.block(2, top + 2, w // 2 - 1, h - 3, "cool", 1)                               # speaker grille
    for y in range(top + 4, h - 3, 2):
        p.rect(4, y, w // 2 - 3, y, K[3])
    dial = C["amber"][7] if state == "on" else K[4]
    p.rect(w // 2 + 1, top + 2, w - 3, top + 5, dial)                               # dial window
    if state == "on":
        p.rect(w // 2 + 4, top + 2, w // 2 + 4, top + 5, C["red"][4])
    p.oval(w // 2 + 1, h - 7, w // 2 + 4, h - 3, "cool", 5); p.oval(w - 7, h - 7, w - 3, h - 3, "cool", 5)   # knobs
    if state == "open":
        p.rect(w // 2 + 1, top + 2, w - 3, h - 3, K[0])                             # panel off: wires
        for k, ramp2 in enumerate(("red", "amber", "green")):
            p.rect(w // 2 + 2 + k * 3, top + 3, w // 2 + 2 + k * 3, h - 5, C[ramp2][5])
    if state == "diy":
        p.rect(2, top + 1, 4, top + 1, K[6]); p.px(w - 3, h - 2, K[6])               # bare screws and tape
        p.rect(w // 2 - 1, top, w // 2 + 2, top + 1, C["warm"][6])
    return p


def robot(name, awake):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 5, 9, m + 5, h - 5, "cool", 4)                                      # body
    p.block(m - 3, 11, m + 3, h - 8, "blue", 4 if not awake else 5)
    p.block(m - 8, 10, m - 6, h - 7, "cool", 3); p.block(m + 6, 10, m + 8, h - 7, "cool", 3)   # arms
    p.block(m - 4, h - 5, m - 1, h, "cool", 3); p.block(m + 1, h - 5, m + 4, h, "cool", 3)      # legs
    p.block(m - 4, 2, m + 4, 9, "cool", 5)                                          # head
    eye = C["cyan"][7] if awake else K[1]
    p.rect(m - 3, 5, m - 2, 6, eye); p.rect(m + 2, 5, m + 3, 6, eye)
    p.rect(m - 1, 8, m + 1, 8, K[2])
    p.rect(m, 0, m, 2, K[3]); p.px(m, 0, C["red"][5] if awake else K[5])
    return p


def microscope():
    p = P("microscope"); w, h = p.w - 1, p.h - 1
    p.block(1, h - 2, w - 1, h, "cool", 2)                                          # foot
    p.rect(w - 4, 4, w - 3, h - 2, K[3])                                           # arm
    p.block(2, h - 6, w - 5, h - 4, "cool", 5)                                      # stage
    for k in range(7):                                                             # tube, leaning
        p.px(w - 6 - k // 2, 3 + k, K[4]); p.px(w - 5 - k // 2, 3 + k, K[2])
    p.rect(w - 7, 1, w - 4, 3, K[5]); p.px(w - 6, 0, K[7])
    p.px(4, h - 7, C["cyan"][6])
    return p


def solder_iron():
    p = P("solder_iron"); w, h = p.w - 1, p.h - 1
    m = h // 2
    p.block(w // 3, m - 1, w, m + 1, "orange", 4)
    p.rect(2, m, w // 3, m, K[5]); p.px(0, m, C["amber"][7]); p.px(1, m, K[6])
    p.px(w, m + 2, K[2]); p.px(w - 1, m + 3, K[2]) if h > 5 else None
    return p


def probe():
    p = P("probe"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 1, 0, m + 1, h - 4, "red", 4)
    p.rect(m, h - 4, m, h, K[5]); p.px(m, h, K[7])
    return p


def part(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    kind = name[5:]
    if kind == "arm":
        p.block(0, h - 3, w, h, "cool", 5); p.block(w - 3, 0, w, h, "cool", 5); p.px(w - 2, 1, K[1])
    elif kind == "cap":
        p.block(1, 0, w - 1, h, "blue", 5); p.rect(2, 2, w - 2, 3, PAPER); p.rect(2, h - 1, w - 2, h - 1, K[3])
    elif kind == "chip":
        p.block(1, 1, w - 1, h - 1, "cool", 1)
        for x in range(2, w - 1, 2):
            p.px(x, 0, K[6]); p.px(x, h, K[6])
        p.px(3, 3, K[5])
    elif kind in ("gear", "gear_bad"):
        ramp = "cool" if kind == "gear" else "pink"
        for x, y in ((w // 2, 0), (w // 2, h), (0, h // 2), (w, h // 2), (1, 1), (w - 1, 1), (1, h - 1), (w - 1, h - 1)):
            p.px(x, y, C[ramp][3])
        p.oval(1, 1, w - 1, h - 1, ramp, 5)
        p.rect(w // 2 - 1, h // 2 - 1, w // 2, h // 2, K[1])
        if kind == "gear_bad":
            for k in range(3):
                p.px(w // 2 + 1 + k, h // 2 - 1 - k, INK)
    elif kind == "knob":
        p.block(0, 1, w, h - 1, "red", 4, round_=True); p.rect(2, 2, w - 3, 2, C["red"][6])
    elif kind == "sensor":
        p.oval(0, 1, w, h, "blue", 4); p.oval(2, 2, w - 2, h - 3, "cyan", 6); p.px(w // 2, 0, K[5])
    else:   # wheel
        p.oval(0, 0, w, h, "cool", 1); p.oval(2, 2, w - 2, h - 2, "cool", 5); p.px(w // 2, h // 2, K[1])
    return p


def boat():
    p = P("boat"); w, h = p.w - 1, p.h - 1
    for y in range(h // 3, h + 1):                                                 # hull, tapering to the keel
        inset = (y - h // 3) * 3 // 2
        p.rect(inset, y, w - inset, y, B[4])
        p.px(inset, y, B[1]); p.px(w - inset, y, B[1])
    p.rect(3, h, w - 3, h, B[1])
    for x in range(6, w - 4, 6):
        p.rect(x, h // 3 + 2, x, h - 2, B[3])
    p.rect(0, h // 3 - 1, w, h // 3 + 1, C["red"][4]); p.rect(0, h // 3 - 1, w, h // 3 - 1, C["red"][6]); p.rect(0, h // 3 + 1, w, h // 3 + 1, C["red"][2])
    p.block(w // 2 - 4, h // 3 - 4, w // 2 + 4, h // 3 - 1, "brown", 5)              # seat
    p.rect(w - 6, 0, w - 6, h // 3 - 2, B[3])                                       # a short mast
    return p


def buoy(name, bob):
    p = P(name); w, h = p.w - 1, p.h - 1
    y0 = 1 if bob else 0
    m = w // 2
    for y in range(y0 + 2, h - 2):
        half = max(1, (y - y0) * (w // 2) // (h - 4))
        col = PAPER if (h - 5) > y > (h - 8) else C["red"][4]
        p.rect(m - half, y, m + half, y, col)
        p.px(m - half, y, C["red"][1]); p.px(m + half, y, C["red"][1])
    p.rect(m, y0, m, y0 + 2, K[3]); p.px(m, y0, C["amber"][7])
    p.rect(0, h - 2, w, h - 1, C["cyan"][5]); p.rect(1, h - 2, 3, h - 2, C["cyan"][7])
    return p


def sparrow(name, pose):
    p = P(name); w, h = p.w - 1, p.h - 1
    if pose == "hurt":                                                             # lying on its side
        p.oval(0, h - 6, w - 3, h, "brown", 5)
        p.oval(w - 7, h - 5, w, h, "brown", 5)
        p.oval(3, h - 3, w - 6, h, "skin", 6)
        p.px(w - 3, h - 3, INK); p.px(w, h - 2, C["amber"][5])
        p.rect(2, h - 7, 5, h - 6, B[3])
        return p
    flying = pose in ("fly_0", "fly_1")
    by = (h - 7) // 2 + 1 if flying else h - 8                                      # top of the body
    p.oval(1, by, w - 4, by + 6, "brown", 5)                                        # body
    p.oval(3, by + 3, w - 6, by + 6, "skin", 6)                                     # belly
    p.oval(w - 7, by - 3, w - 1, by + 2, "brown", 5)                                # head
    p.px(w - 3, by - 1, INK); p.px(w, by, C["amber"][5]); p.px(w - 1, by, C["amber"][5])
    p.rect(0, by + 1, 1, by + 2, B[3])                                             # tail
    if flying:
        up = pose == "fly_0"
        for k in range(4):                                                         # a wing, up or down
            y = by - 1 - k if up else by + 6 + k
            p.rect(3 + k, y, 7 - k // 2, y, B[3] if k else B[4])
    else:
        p.rect(3, by + 2, 7, by + 3, B[3]); p.rect(3, by + 2, 6, by + 2, B[4])        # folded wing
        if pose == "hop":
            p.px(4, h - 1, C["amber"][4]); p.px(8, h, C["amber"][4])                 # one foot up
        else:
            p.px(4, h, C["amber"][4]); p.px(7, h, C["amber"][4])
    if pose == "bandaged":
        p.rect(3, by + 2, 7, by + 3, PAPER); p.px(5, by + 3, C["red"][4])
    if pose == "splint":
        p.rect(2, by + 3, 8, by + 3, C["skin"][7]); p.px(3, by + 2, PAPER); p.px(7, by + 4, PAPER)
    return p


def skeleton(name, stage):
    """stage 0: bare stand. 1: skull. 2: + spine and ribs. 3: + arms and legs."""
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    bone, shade = PAPER, W7[5]
    p.oval(m - 5, h - 2, m + 5, h, "cool", 3)
    p.rect(m, 3, m, h - 2, K[3]); p.px(m - 1, 4, K[5])
    p.rect(m - 2, 2, m + 2, 2, K[3])
    if stage >= 1:
        p.oval(m - 3, 3, m + 3, 9, "warm", 7)
        p.px(m - 2, 6, INK); p.px(m + 2, 6, INK); p.px(m, 8, INK)
        p.rect(m - 1, 9, m + 1, 10, bone)
    if stage >= 2:
        p.rect(m, 11, m, 20, bone)
        for y in (12, 14, 16):
            p.rect(m - 3, y, m + 3, y, bone); p.px(m - 3, y + 1, shade); p.px(m + 3, y + 1, shade)
        p.rect(m - 2, 20, m + 2, 21, bone)
    if stage >= 3:
        for side in (-1, 1):
            p.rect(m + side * 4, 12, m + side * 4, 19, bone); p.px(m + side * 5, 20, bone)
            p.rect(m + side * 2, 22, m + side * 2, h - 4, bone); p.px(m + side * 3, h - 3, bone)
    return p


def jacket_vintage():
    p = P("jacket_vintage"); w, h = p.w - 1, p.h - 1
    p.block(1, 0, w - 1, h, "brown", 4)
    p.block(0, 1, 2, h - 3, "brown", 3); p.block(w - 2, 1, w, h - 3, "brown", 3)      # sleeves
    p.rect(w // 2, 1, w // 2, h - 1, B[2])
    p.px(w // 2 - 2, 1, B[6]); p.px(w // 2 + 2, 1, B[6]); p.px(w // 2 - 1, 2, B[6]); p.px(w // 2 + 1, 2, B[6])   # collar
    p.px(w // 2 + 1, 5, C["amber"][6]); p.px(w // 2 + 1, 8, C["amber"][6])
    return p


def bone_pile():
    p = P("bone_pile"); w, h = p.w - 1, p.h - 1
    for i, (x0, y0, ln) in enumerate(((0, h - 2, w - 6), (4, h - 5, w - 9), (8, h - 8, w - 14))):
        p.rect(x0, y0, x0 + ln, y0 + 1, PAPER); p.rect(x0, y0 + 1, x0 + ln, y0 + 1, W7[5])
        for ex in (x0, x0 + ln):
            p.rect(ex - 0, y0 - 1, ex + 1, y0 + 2, PAPER)
    p.oval(w - 7, h - 6, w, h, "warm", 7); p.px(w - 5, h - 3, INK); p.px(w - 2, h - 3, INK)   # a skull on the end
    return p


def twig():
    p = P("twig"); w, h = p.w - 1, p.h - 1
    p.rect(0, h // 2, w, h // 2, B[3]); p.rect(0, h // 2, w - 2, h // 2, B[4]) if h else None
    p.px(w - 3, max(0, h // 2 - 1), B[4]); p.px(w - 2, 0, B[4]); p.px(2, min(h, h // 2 + 1), C["green"][4])
    return p


def rope_weight():
    p = P("rope_weight"); w, h = p.w - 1, p.h - 1
    m = w // 2
    for y in range(0, h - 4):
        p.px(m, y, C["skin"][5] if y % 2 else C["skin"][3])
    p.block(0, h - 4, w, h, "cool", 3)
    return p


def pile(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    sheets = max(2, (h + 1) // 3)
    for i in range(sheets):
        y1 = h - i * 3
        off = (i % 2)
        p.rect(off, y1 - 2, w - 1 + off if w - 1 + off <= w else w, y1, K[3])
        p.rect(off + 1, y1 - 2, w - 1, y1 - 1, PAPER)
        p.rect(off + 1, y1 - 1, w - 1, y1 - 1, W7[6])
    return p


def slip():
    p = P("slip"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "warm", 7)
    p.rect(1, 1, w - 1, h - 1, PAPER)
    p.rect(2, 2, w - 2, 2, C["violet"][3]); p.rect(2, 4, w - 3, 4, K[4])
    return p


def set_panel(name, painted):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 5)
    if not painted:
        p.rect(2, 2, w - 2, h - 2, K[6])
        p.rect(3, 3, w // 2, 3, K[7])
        return p
    p.rect(2, 2, w - 2, h - 2, C["cyan"][6])                                        # sky
    p.rect(2, 2, w - 2, 3, C["cyan"][7])
    p.oval(w - 8, 3, w - 4, 7, "amber", 6)                                          # sun
    for x in range(2, w - 1):                                                      # hills
        top = h - 6 + ((x * 5) % 7) // 3 - (2 if w // 3 < x < w * 2 // 3 else 0)
        p.rect(x, max(3, top), x, h - 2, C["green"][4])
        p.px(x, max(3, top), C["lime"][5])
    return p


def bandage_roll():
    p = P("bandage_roll"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "warm", 7)
    p.px(w // 2, h // 2, C["red"][4])
    return p


def brush():
    p = P("brush"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.rect(m, 0, m, h - 3, B[4]); p.rect(0, h - 3, w, h - 2, K[5]); p.rect(0, h - 1, w, h, C["red"][4])
    return p


DRAW_HS = {
    "barber_chair": barber_chair, "mirror_wall": mirror_wall, "hair_tonic": hair_tonic, "chalkboard": chalkboard,
    "choir_riser": choir_riser, "committee_table": committee_table, "conductor_stand": conductor_stand,
    "curtain": curtain, "desk_lamp_off": lambda: desk_lamp("desk_lamp_off", False),
    "desk_lamp_on": lambda: desk_lamp("desk_lamp_on", True),
    "filewall_closed": lambda: filewall("filewall_closed", False), "filewall_open": lambda: filewall("filewall_open", True),
    "filing_cabinet": filing_cabinet, "front_desk": front_desk, "wait_bench": wait_bench, "rocking_chair": rocking_chair,
    "seat_row": seat_row, "stage_floor": stage_floor,
    "rack_messy": lambda: rack("rack_messy", True), "rack_neat": lambda: rack("rack_neat", False),
    "mannequin_bare": lambda: mannequin("mannequin_bare", False), "mannequin_styled": lambda: mannequin("mannequin_styled", True),
    "till": till, "test_rig": test_rig, "nest_box": nest_box, "scarecrow": scarecrow, "veg_row": veg_row,
    "rail_piece": rail_piece, "record_box": record_box, "assembly_runner": assembly_runner,
    "basket": lambda: basket("basket", True), "basket_a": lambda: basket("basket_a", False),
    "basket_b": lambda: basket("basket_b", False), "basket_c": lambda: basket("basket_c", False),
    "crate_1": lambda: crate_n("crate_1", 1), "crate_2": lambda: crate_n("crate_2", 2), "crate_3": lambda: crate_n("crate_3", 3),
    "kit_closed": lambda: kit("kit_closed", False), "kit_open": lambda: kit("kit_open", True), "medkit": medkit,
    "parts_bin_a": lambda: parts_bin("parts_bin_a", "blue"), "parts_bin_b": lambda: parts_bin("parts_bin_b", "amber"),
    "parts_bin_c": lambda: parts_bin("parts_bin_c", "red"),
    "shop_bag": shop_bag, "sample_tray": sample_tray,
    "tray_fail": lambda: tray("tray_fail", "red"), "tray_pass": lambda: tray("tray_pass", "green"),
    "ledger": ledger,
    "radio_diy": lambda: radio("radio_diy", "diy"), "radio_on": lambda: radio("radio_on", "on"),
    "radio_open": lambda: radio("radio_open", "open"),
    "robot_0": lambda: robot("robot_0", False), "robot_1": lambda: robot("robot_1", True),
    "microscope": microscope, "solder_iron": solder_iron, "probe": probe,
    "boat": boat, "buoy_0": lambda: buoy("buoy_0", False), "buoy_1": lambda: buoy("buoy_1", True),
    "sparrow_sit": lambda: sparrow("sparrow_sit", "sit"), "sparrow_hop": lambda: sparrow("sparrow_hop", "hop"),
    "sparrow_bandaged": lambda: sparrow("sparrow_bandaged", "bandaged"), "sparrow_splint": lambda: sparrow("sparrow_splint", "splint"),
    "sparrow_hurt_0": lambda: sparrow("sparrow_hurt_0", "hurt"), "sparrow_hurt_1": lambda: sparrow("sparrow_hurt_1", "hurt"),
    "sparrow_fly_0": lambda: sparrow("sparrow_fly_0", "fly_0"), "sparrow_fly_1": lambda: sparrow("sparrow_fly_1", "fly_1"),
    "jacket_vintage": jacket_vintage, "bone_pile": bone_pile, "twig": twig, "rope_weight": rope_weight,
    "pile_l": lambda: pile("pile_l"), "pile_m": lambda: pile("pile_m"), "pile_s": lambda: pile("pile_s"),
    "slip": slip, "set_blank": lambda: set_panel("set_blank", False), "painted_set": lambda: set_panel("painted_set", True),
    "bandage_roll": bandage_roll, "brush": brush,
}
for _i in range(4):
    DRAW_HS["barber_pole_%d" % _i] = (lambda n, k: lambda: barber_pole(n, k))("barber_pole_%d" % _i, _i)
    DRAW_HS["skeleton_%d" % _i] = (lambda n, k: lambda: skeleton(n, k))("skeleton_%d" % _i, _i)
for _k in ("arm", "cap", "chip", "gear", "gear_bad", "knob", "sensor", "wheel"):
    DRAW_HS["part_" + _k] = (lambda n: lambda: part(n))("part_" + _k)
DRAW.update(DRAW_HS)
# ================================================================ College batch

def bell(name, ring):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2 + (1 if ring else 0)
    for y in range(1, h - 1):
        half = 1 + (y * (w // 2 - 1)) // (h - 2)
        p.rect(m - half, y, m + half, y, C["amber"][5])
        p.px(m - half, y, C["amber"][2]); p.px(m + half, y, C["amber"][2]); p.px(m - half + 1, y, C["amber"][7])
    p.rect(m - w // 2 + 1, h - 1, m + w // 2 - 1, h - 1, C["amber"][3])
    p.px(m, 0, C["amber"][3]); p.px(m + (1 if ring else 0), h, C["amber"][2])
    if ring:
        p.px(0, 2, PAPER); p.px(0, 5, PAPER); p.px(w, 3, PAPER)
    return p


def bill_stack():
    p = P("bill_stack"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "green", 4)
    p.rect(2, 2, w - 2, h - 2, C["green"][6]); p.rect(w // 2 - 1, 1, w // 2, h - 1, PAPER)
    return p


def booth(name, ramp, sign):
    p = P(name); w, h = p.w - 1, p.h - 1
    ah = h // 4
    p.block(1, ah - 1, 3, h, "brown", 3); p.block(w - 3, ah - 1, w - 1, h, "brown", 3)   # posts
    p.block(0, h * 2 // 3, w, h, "brown", 5)                                          # counter
    for x in range(4, w - 3, 5):
        p.rect(x, h * 2 // 3 + 4, x, h - 2, B[3])
    p.block(0, h * 2 // 3 - 2, w, h * 2 // 3 + 2, "brown", 6)
    p.block(4, ah + 2, w - 4, h * 2 // 3 - 4, "warm", 7)                              # poster on the back
    p.rect(w // 2 - 3, ah + 5, w // 2 + 3, h * 2 // 3 - 7, sign)
    p.block(0, 0, w, ah, ramp, 4)                                                     # banner
    p.rect(3, ah // 2, w - 3, ah // 2, PAPER)
    return p


def bottle(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, ramp, 5)
    p.rect(1, h // 2, w - 1, h // 2 + 1, PAPER)
    p.rect(w // 2 - 1, 0, w // 2 + 1, 2, K[2]); p.px(w // 2, 0, K[6])
    return p


def campus_tree():
    p = P("campus_tree"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 2, h * 2 // 3 - 2, m + 2, h, "brown", 3)
    p.px(m - 3, h, B[2]); p.px(m + 3, h, B[2])
    p.oval(0, 2, w, h * 2 // 3 + 2, "green", 3)
    p.oval(1, 0, w - 3, h * 2 // 3 - 3, "green", 4)
    for x, y in ((5, 3), (9, 2), (14, 5), (4, 8), (11, 9), (17, 8), (7, 13)):          # leaf clusters catching light
        p.rect(x, y, x + 2, y, C["lime"][5]); p.rect(x, y + 1, x + 1, y + 1, C["lime"][4])
    for x, y in ((6, 16), (13, 15), (18, 13), (3, 13)):
        p.rect(x, y, x + 2, y, C["green"][1])
    return p


def career_board():
    p = P("career_board"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h - 1, "cool", 5)
    p.rect(2, 2, w - 2, h - 3, PAPER)
    cw = (w - 3) // 3
    for i in range(3):
        x0 = 3 + i * cw
        p.rect(x0, 3, x0 + cw - 2, 4, C["blue"][4])
        p.rect(x0, 6, x0 + cw - 2, h - 5, C["blue"][7])
    p.rect(2, h, 4, h, K[2]); p.rect(w - 4, h, w - 2, h, K[2])
    return p


def cash_drawer(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 2)
    if opened:
        p.rect(2, 2, w - 2, h - 2, K[0])
        for i in range(4):
            x0 = 3 + i * ((w - 5) // 4)
            p.rect(x0, 2, x0 + 2, h - 2, C["green"][6] if i < 3 else C["amber"][5])
    else:
        p.rect(1, 1, w - 1, 1, K[4]); p.rect(w // 2 - 2, h // 2, w // 2 + 2, h // 2, K[5])
    return p


def clapboard(name, snapped):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 4, w, h, "cool", 1)
    p.rect(2, 7, w - 2, 7, K[4]); p.rect(2, 9, w - 4, 9, K[4])
    y0 = 2 if snapped else 0
    for x in range(0, w + 1):
        yy = y0 if snapped else max(0, 3 - x // 3)
        p.rect(x, yy, x, yy + 1, PAPER if (x // 2) % 2 == 0 else K[1])
    return p


def coffee_cup():
    p = P("coffee_cup"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w - 2, h, "warm", 7)
    p.rect(1, 1, w - 3, 2, B[2])
    p.rect(w - 1, 3, w, 3, K[4]); p.rect(w, 3, w, h - 2, K[4]); p.rect(w - 1, h - 2, w, h - 2, K[4])
    return p


def crash_pad():
    p = P("crash_pad"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "blue", 4)
    p.rect(1, 1, w - 1, 2, C["blue"][6])
    for x in range(5, w - 2, 6):
        p.rect(x, 3, x, h - 1, C["blue"][2])
    return p


def deadline_clock(name, late):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "cool", 2)
    p.oval(1, 1, w - 1, h - 1, "warm", 7)
    m, c = w // 2, h // 2
    p.rect(m, 2, m, c, INK)
    if late:
        p.rect(m, c, m, h - 3, C["red"][4]); p.px(m, 1, C["red"][4])
    else:
        p.rect(m, c, w - 3, c, C["red"][4])
    return p


def desk_pc(name, screen):
    """A desk with a monitor. screen: idle / busy0 / busy1 / bill / error / game / ok."""
    p = P(name); w, h = p.w - 1, p.h - 1
    dark = name.startswith("pc_")
    ramp = "cool" if dark else "brown"
    top = h * 3 // 5
    p.block(1, top + 2, 3, h, ramp, 2); p.block(w - 3, top + 2, w - 1, h, ramp, 2)       # legs
    p.block(0, top, w, top + 3, ramp, 3 if dark else 5)                                 # desk top
    mx0, mx1 = w // 2 - 6, w // 2 + 6
    p.rect(w // 2 - 1, top - 2, w // 2 + 1, top - 1, K[2]); p.rect(w // 2 - 3, top - 1, w // 2 + 3, top - 1, K[3])   # stand
    p.block(mx0, 0, mx1, top - 3, "cool", 2)
    sx0, sy0, sx1, sy1 = mx0 + 2, 2, mx1 - 2, top - 5
    fills = {"idle": K[0], "busy0": C["blue"][5], "busy1": C["blue"][5], "bill": PAPER, "error": C["red"][4],
             "game": C["green"][3], "ok": C["green"][3]}
    p.rect(sx0, sy0, sx1, sy1, fills[screen])
    if screen in ("busy0", "busy1"):
        off = 0 if screen == "busy0" else 2
        p.rect(sx0 + 1, sy0 + 1 + off % 3, sx1 - 2, sy0 + 1 + off % 3, PAPER); p.rect(sx0 + 1, sy1 - 1 - off % 2, sx1 - 4, sy1 - 1 - off % 2, C["blue"][7])
    elif screen == "bill":
        for y in range(sy0 + 1, sy1, 2):
            p.rect(sx0 + 1, y, sx1 - 1 - (y % 3), y, C["blue"][3])
    elif screen == "error":
        p.rect((sx0 + sx1) // 2, sy0 + 1, (sx0 + sx1) // 2, sy1 - 2, PAPER); p.px((sx0 + sx1) // 2, sy1, PAPER)
    elif screen == "game":
        p.rect(sx0 + 1, sy1 - 1, sx0 + 2, sy1, C["amber"][6]); p.rect(sx1 - 3, sy0 + 1, sx1 - 2, sy0 + 2, C["red"][5]); p.px((sx0 + sx1) // 2, (sy0 + sy1) // 2, PAPER)
    elif screen == "ok":
        p.rect((sx0 + sx1) // 2 - 1, (sy0 + sy1) // 2, (sx0 + sx1) // 2, (sy0 + sy1) // 2, PAPER)
    else:
        p.px(sx0 + 1, sy0 + 1, K[3])
    return p


def dining_chair():
    p = P("dining_chair"); w, h = p.w - 1, p.h - 1
    p.block(1, 0, w - 1, h // 2 + 1, "brown", 4, round_=True)
    p.rect(3, 2, w - 3, h // 2 - 1, B[5])
    p.block(0, h // 2, w, h // 2 + 3, "brown", 5)
    p.rect(1, h // 2 + 4, 1, h, B[2]); p.rect(w - 1, h // 2 + 4, w - 1, h, B[2])
    return p


def dining_table():
    p = P("dining_table"); w, h = p.w - 1, p.h - 1
    p.block(2, 4, 5, h, "brown", 3); p.block(w - 5, 4, w - 2, h, "brown", 3)
    p.block(0, 0, w, 6, "brown", 6)
    p.rect(1, 5, w - 1, 5, B[4])
    for x in range(5, w - 6, 8):
        p.rect(x, 2, x + 3, 3, PAPER); p.px(x + 1, 2, C["cyan"][6])                    # plates
    return p


def director_chair():
    p = P("director_chair"); w, h = p.w - 1, p.h - 1
    for k in range(h // 2, h + 1):                                                   # crossed legs
        t = (k - h // 2) * w // max(1, h - h // 2)
        p.px(t, k, B[3]); p.px(w - t, k, B[3])
    p.rect(0, 0, 1, h // 2, B[4]); p.rect(w - 1, 0, w, h // 2, B[4])
    p.block(1, 1, w - 1, 5, "cool", 1)                                               # canvas back
    p.rect(3, 3, w - 3, 3, PAPER)
    p.block(0, h // 2 - 1, w, h // 2 + 1, "cool", 1)                                 # canvas seat
    return p


def dock_lamp(name, on):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.oval(m - 3, h - 2, m + 3, h, "cool", 2)
    p.rect(m, 5, m, h - 2, K[2]); p.px(m - 1, 7, K[4])
    p.block(m - 3, 0, m + 3, 5, "cool", 2)
    p.rect(m - 2, 2, m + 2, 4, C["amber"][7] if on else K[4])
    if on:
        p.px(m, 3, PAPER)
    return p


def door():
    p = P("door"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 3)
    p.block(2, 2, w - 2, h - 1, "brown", 5)
    p.block(4, 4, w - 4, h // 2 - 1, "brown", 4); p.block(4, h // 2 + 1, w - 4, h - 3, "brown", 4)
    p.rect(w - 4, h // 2, w - 3, h // 2, C["amber"][6])
    return p


def envelope():
    p = P("envelope"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "warm", 7)
    for k in range(w // 2 + 1):
        p.px(k, 1 + k * (h - 2) // max(1, w), W7[5]); p.px(w - k, 1 + k * (h - 2) // max(1, w), W7[5])
    p.px(w // 2, h // 2, C["red"][4])
    return p


def envelope_stack(name, ready):
    p = P(name); w, h = p.w - 1, p.h - 1
    if not ready:
        for i in range(4):
            p.block(i, h - 3 - i * 3, w - 4 + i, h - i * 3, "skin", 6)
        p.rect(w - 3, 2, w, h - 2, C["red"][4])
        return p
    p.block(0, h - 5, w, h, "brown", 5)                                              # a tray, envelopes standing in it
    for i, ramp in enumerate(("red", "green", "blue", "amber", "pink")):
        x = 1 + i * ((w - 2) // 5)
        p.block(x, 2 + (i % 2), x + 2, h - 4, "warm", 7); p.rect(x, 2 + (i % 2), x + 2, 3 + (i % 2), C[ramp][4])
    return p


def film_camera(name, rolling):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    for k in range(h // 2 + 2, h + 1):                                               # tripod
        t = (k - h // 2 - 2) * (w // 2 - 1) // max(1, h - h // 2 - 2)
        p.px(m - t, k, K[2]); p.px(m + t, k, K[2]); p.px(m, k, K[3])
    p.block(m - 5, h // 4 + 1, m + 4, h // 2 + 2, "cool", 2)                          # body
    p.block(m + 4, h // 4 + 3, m + 7, h // 2, "cool", 3)                             # lens
    p.px(m + 7, h // 4 + 4, C["cyan"][6])
    p.oval(m - 6, 0, m - 1, h // 4 + 1, "cool", 2); p.oval(m - 1, 1, m + 3, h // 4 + 1, "cool", 2)   # reels
    if rolling:
        p.px(m - 3, 2, C["red"][5]); p.px(m - 4, h // 4 + 3, C["red"][5])
    else:
        p.px(m - 4, 2, K[5])
    return p


def fit_slot(name, full):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "teal", 3)
    p.block(w // 2 - 3, 2, w // 2 + 3, 5, "cool", 2); p.px(w // 2, 3, C["red"][5] if not full else C["green"][6])
    p.rect(2, h // 2 - 1, w - 2, h // 2 + 3, K[0])
    if full:
        p.rect(3, h // 2, w - 3, h // 2 + 2, K[6]); p.rect(3, h // 2, w - 3, h // 2, PAPER)
    p.rect(1, h - 2, w - 1, h - 2, C["teal"][1])
    return p


def for_sale_sign():
    p = P("for_sale_sign"); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 1, h // 2, m + 1, h, "brown", 3)
    p.block(0, 0, w, h // 2 + 1, "red", 4)
    p.rect(2, 2, w - 2, h // 2 - 1, PAPER)
    for x in range(3, w - 2, 3):
        p.rect(x, 3, x + 1, h // 2 - 2, C["red"][4])
    return p


def fuel_drum():
    p = P("fuel_drum"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "red", 4, round_=True)
    p.rect(0, h // 3, w, h // 3, C["red"][2]); p.rect(0, h * 2 // 3, w, h * 2 // 3, C["red"][2])
    p.rect(w // 2 - 2, h // 2 - 1, w // 2 + 2, h // 2 + 1, C["amber"][6])
    p.rect(2, 0, 4, 0, K[5])
    return p


def gauge(name, high):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "cool", 2); p.oval(1, 1, w - 1, h - 1, "warm", 7)
    m, c = w // 2, h // 2
    if high:
        p.px(m + 1, c - 1, C["red"][4]); p.px(m + 2, c - 2, C["red"][4]); p.px(w - 2, 2, C["red"][5])
    else:
        p.px(m - 1, c - 1, INK); p.px(m - 2, c - 2, INK)
    p.px(m, c, INK)
    return p


def generator(name, state):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 5, w, h, "amber", 4)
    p.rect(1, h - 1, w - 1, h - 1, C["amber"][2])
    p.block(2, 8, w // 2, h - 4, "cool", 2)                                          # vent
    for y in range(10, h - 4, 2):
        p.rect(4, y, w // 2 - 2, y, K[4])
    p.block(w // 2 + 3, 8, w - 3, h // 2 + 3, "cool", 1)                             # panel
    light = K[3] if state == "off" else (C["green"][6] if state == "on0" else C["lime"][6])
    p.rect(w // 2 + 5, 10, w // 2 + 6, 11, light)
    p.px(w - 5, h - 5, C["red"][4])
    p.block(5, 1, 9, 5, "cool", 3)                                                   # exhaust stack
    if state != "off":
        p.px(7 if state == "on0" else 6, 0, K[6])
        p.rect(w // 2 + 8, 10, w - 5, 10, C["amber"][7])
    return p


def gmush(name, ramp, glow):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(m - 1, h // 2, m + 1, h, "warm", 7)                                      # stem
    p.px(m - 2, h, W7[5]); p.px(m + 2, h, W7[5])
    p.oval(0, 1, w, h // 2 + 2, ramp, 6 if glow else 5)                              # cap
    p.rect(1, h // 2 + 2, w - 1, h // 2 + 2, C[ramp][2])
    for x in (3, m, w - 3):
        p.px(x, 3 + (x % 2), PAPER if glow else C[ramp][7])
    if glow:
        p.px(m - 3, 2, PAPER); p.px(m + 2, 4, PAPER)
    return p


def goggles_hook():
    p = P("goggles_hook"); w, h = p.w - 1, p.h - 1
    p.rect(w // 2, 0, w // 2, 2, K[2]); p.px(w // 2 - 1, 0, K[4])
    p.block(0, 3, w // 2 - 1, h, "cool", 2); p.block(w // 2 + 1, 3, w, h, "cool", 2)
    p.rect(1, 4, w // 2 - 2, h - 1, C["cyan"][6]); p.rect(w // 2 + 2, 4, w - 1, h - 1, C["cyan"][6])
    p.rect(w // 2 - 1, 5, w // 2 + 1, 5, K[2])
    return p


def grinder(name, state):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(2, h - 4, w - 2, h, "brown", 4)                                          # bench
    p.block(w // 2 - 4, h // 2 - 2, w // 2 + 4, h - 4, "blue", 3)                    # motor
    stone = "cool"
    lvl = 3 if state == "dull" else 6
    p.oval(0, h // 2 - 7, 9, h // 2 + 2, stone, lvl); p.oval(w - 9, h // 2 - 7, w, h // 2 + 2, stone, lvl)
    p.px(4, h // 2 - 2, K[1]); p.px(w - 5, h // 2 - 2, K[1])
    p.block(0, h // 2 - 10, 9, h // 2 - 8, "orange", 5); p.block(w - 9, h // 2 - 10, w, h // 2 - 8, "orange", 5)   # guards
    if state == "1":
        p.px(2, h // 2 - 5, PAPER); p.px(w - 3, h // 2 - 1, PAPER)                     # spinning glint
    elif state == "0":
        p.px(7, h // 2 - 5, PAPER); p.px(w - 7, h // 2 - 1, PAPER)
    return p


def guide_jig():
    p = P("guide_jig"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 2)
    p.rect(2, 1, w - 2, h - 2, C["orange"][5]); p.rect(2, 1, w - 2, 1, C["orange"][7])
    return p


def jar(name, kind):
    p = P(name); w, h = p.w - 1, p.h - 1
    if kind == "cover":
        p.block(0, 0, w, h, "cool", 1); p.px(w // 2, h // 2, K[5])
        return p
    p.block(0, 2, w, h, "cyan", 7)
    p.rect(1, 0, w - 1, 2, K[5]); p.rect(1, 0, w - 1, 0, K[7])
    fill = {"dark": K[0], "dim": C["teal"][3], "empty": None, "lit": C["teal"][6]}[kind]
    if fill is not None:
        p.rect(1, h // 2, w - 1, h - 1, fill)
        if kind == "lit":
            p.rect(1, h // 2, w - 1, h // 2, PAPER); p.px(w // 2, h - 3, PAPER)
    p.px(1, 3, PAPER)
    return p


def keycard():
    p = P("keycard"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "warm", 7)
    p.rect(1, 1, w - 1, 2, C["blue"][4]); p.rect(1, h - 1, 3, h - 1, C["amber"][5])
    return p


def lab_table():
    p = P("lab_table"); w, h = p.w - 1, p.h - 1
    top = h // 2
    p.block(1, top + 2, w - 1, h, "cool", 5)                                         # cabinet under the top
    p.rect(w // 2, top + 4, w // 2, h - 1, K[3])
    p.px(w // 2 - 3, (top + h) // 2 + 1, K[2]); p.px(w // 2 + 3, (top + h) // 2 + 1, K[2])
    p.block(0, top, w, top + 3, "warm", 7)                                           # white top
    for i, ramp in enumerate(("teal", "red", "amber", "blue")):                      # glassware
        x = 3 + i * ((w - 6) // 4)
        p.block(x, top - 6 - (i % 2) * 2, x + 2, top, "cyan", 7)
        p.rect(x + 1, top - 3, x + 1, top - 1, C[ramp][5])
    p.rect(w - 6, top - 9, w - 5, top, K[3]); p.rect(w - 9, top - 9, w - 5, top - 8, K[3])   # a stand
    return p


def luggage_cart():
    p = P("luggage_cart"); w, h = p.w - 1, p.h - 1
    A = C["amber"]
    p.rect(1, 0, 1, h - 2, A[4]); p.rect(w - 1, 0, w - 1, h - 2, A[4]); p.rect(1, 0, w - 1, 0, A[6])
    p.rect(0, h - 3, w, h - 2, A[3]); p.rect(0, h - 3, w, h - 3, A[6])
    p.block(3, h - 9, 7, h - 3, "blue", 4); p.block(8, h - 12, w - 3, h - 3, "red", 4)
    p.block(4, h - 13, 7, h - 9, "brown", 4)
    p.oval(1, h - 1, 3, h + 1, "cool", 2); p.oval(w - 3, h - 1, w - 1, h + 1, "cool", 2)
    return p


def med_table():
    p = P("med_table"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "brown", 3)
    for x0 in (3, w // 2 + 1):
        p.block(x0, 5, x0 + w // 2 - 5, h - 3, "brown", 4)
        p.rect(x0 + w // 4 - 4, (h + 2) // 2, x0 + w // 4 - 1, (h + 2) // 2, C["amber"][6])
    p.block(0, 0, w, 3, "brown", 5)
    return p


def mortar():
    p = P("mortar"); w, h = p.w - 1, p.h - 1
    p.block(0, 1, w, h, "cool", 5)
    p.clear(0, h); p.clear(w, h)
    p.rect(1, 1, w - 1, 2, C["green"][4]); p.rect(2, 1, 4, 1, C["lime"][5])
    p.rect(0, 0, w, 0, K[7])
    return p


def pestle():
    p = P("pestle"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h - 3, "skin", 5)
    p.block(0, h - 4, w, h, "cool", 5)
    return p


def notice_board():
    p = P("notice_board"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h - 1, "brown", 4)
    p.rect(2, 2, w - 2, h - 3, C["skin"][5])
    for (x, y, ramp) in ((3, 3, "amber"), (9, 3, "cyan"), (14, 4, "pink"), (4, 9, "green"), (11, 9, "warm")):
        p.rect(x, y, x + 3, y + 3, C[ramp][6]); p.px(x + 1, y, C["red"][4])
    p.rect(2, h, 3, h, B[2]); p.rect(w - 3, h, w - 2, h, B[2])
    return p


def office_desk():
    p = P("office_desk"); w, h = p.w - 1, p.h - 1
    top = h // 2
    p.block(1, top + 2, w // 3, h, "brown", 3); p.block(w - w // 3, top + 2, w - 1, h, "brown", 3)
    p.block(0, top, w, top + 3, "brown", 5)
    p.block(w - w // 2, top - h // 3, w - 3, top - 2, "cool", 2)                      # monitor
    p.rect(w - w // 2 + 2, top - h // 3 + 2, w - 5, top - 4, C["blue"][4])
    p.rect(w - w // 4 - 1, top - 2, w - w // 4, top - 1, K[2])
    p.rect(2, top - 2, w // 3, top - 1, PAPER); p.rect(3, top - 3, w // 3 - 1, top - 3, W7[6])   # papers
    return p


def paint_roller():
    p = P("paint_roller"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w - 1, 3, "cyan", 7)
    p.rect(w, 1, w, 4, K[3]); p.rect(w // 2, 4, w, 4, K[3]); p.rect(w // 2, 4, w // 2, 6, K[3])
    p.block(w // 2 - 1, 6, w // 2 + 1, h, "brown", 4)
    return p


def photo_frame(name, alt):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "amber", 4)
    p.rect(2, 2, w - 2, h - 2, C["cyan"][7])
    p.rect(2, h - 4, w - 2, h - 2, C["green"][4])
    for i, ramp in enumerate(("red", "blue", "amber")):
        x = 3 + i * ((w - 6) // 3) + (1 if alt else 0)
        p.rect(x, h - 6, x + 1, h - 3, C[ramp][4]); p.px(x, h - 7, C["skin"][6])
    if alt:
        p.px(w - 4, 3, C["amber"][6])
    return p


def prop_cart():
    p = P("prop_cart"); w, h = p.w - 1, p.h - 1
    p.block(0, 3, w, h - 3, "brown", 4)
    for x in range(4, w - 2, 5):
        p.rect(x, 5, x, h - 5, B[3])
    for i, ramp in enumerate(("red", "amber", "blue")):
        x = 2 + i * ((w - 4) // 3)
        p.block(x, 0, x + 3, 4, ramp, 5)
    p.oval(1, h - 3, 5, h, "cool", 1); p.oval(w - 5, h - 3, w - 1, h, "cool", 1)
    return p


def ramp(name):
    p = P(name); w, h = p.w - 1, p.h - 1
    for x in range(0, w + 1):
        top = h - (x * h) // max(1, w)
        p.rect(x, top, x, h, B[5] if (x // 4) % 2 == 0 else B[4])
        p.px(x, top, B[7])
        if x % 4 == 3:
            p.rect(x, top, x, h, B[3])
    p.rect(0, h, w, h, B[1]); p.rect(w, 0, w, h, B[2])
    return p


def receipt_printer(name, printing):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, h // 2 - 1, w, h, "warm", 7)
    p.rect(3, h // 2 + 2, w - 3, h // 2 + 3, K[1])
    p.rect(1, h - 2, w - 1, h - 2, W7[5])
    if printing:
        p.block(4, 0, w - 4, h // 2 + 2, "warm", 7)
        p.rect(6, 2, w - 6, 2, K[3]); p.rect(6, 4, w - 8, 4, K[3])
    return p


def sample_cabinet(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    if opened:
        p.block(0, 0, w - 3, h, "blue", 2)
        p.rect(2, 2, w - 5, h - 2, C["blue"][3])
        p.block(w - 3, 2, w, h - 2, "cyan", 7)                                       # the door swung open
        p.px(w - 6, h // 2, C["amber"][6])
        return p
    p.block(0, 0, w, h, "cyan", 6)
    p.rect(2, 2, w - 2, h - 2, C["cyan"][7])
    rows = 3
    for r in range(rows):
        y = 2 + (r + 1) * ((h - 4) // rows)
        p.rect(2, y, w - 2, y, K[5])
        for i, rmp in enumerate(("red", "amber", "blue", "green")):
            x = 3 + i * ((w - 6) // 4)
            p.rect(x, y - 3, x + 1, y - 1, C[rmp][5])
    p.rect(w // 2, 2, w // 2, h - 2, K[5])
    return p


def sample_dish():
    p = P("sample_dish"); w, h = p.w - 1, p.h - 1
    p.oval(0, 0, w, h, "cyan", 7)
    p.oval(2, 1, w - 2, h - 1, "warm", 7)
    p.px(w // 2 - 2, h // 2, C["red"][5]); p.px(w // 2 + 1, h // 2 - 1, C["green"][5]); p.px(w // 2 + 3, h // 2 + 1, C["amber"][5])
    return p


def stamp(name, down):
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    off = 2 if down else 0
    p.oval(m - 2, off, m + 2, off + 3, "brown", 3)                                   # knob
    p.rect(m - 1, off + 3, m + 1, h - 5, B[3])
    p.block(0, h - 5, w, h - 2, "brown", 4)
    p.rect(0, h - 1, w, h, C["red"][3] if not down else C["red"][5])
    return p


def sticky(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, ramp, 6)
    p.rect(2, 2, w - 2, 2, C[ramp][3]); p.rect(2, 4, w - 3, 4, C[ramp][3])
    p.px(w, h, C[ramp][3])
    return p


def suitcase():
    p = P("suitcase"); w, h = p.w - 1, p.h - 1
    p.block(0, 2, w, h, "brown", 4)
    p.rect(w // 2 - 2, 0, w // 2 + 2, 0, B[2]); p.rect(w // 2 - 2, 1, w // 2 - 2, 2, B[2]); p.rect(w // 2 + 2, 1, w // 2 + 2, 2, B[2])
    p.rect(2, 3, 2, h - 1, C["amber"][5]); p.rect(w - 2, 3, w - 2, h - 1, C["amber"][5])
    return p


def support_door(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 5)
    p.rect(1, 1, w - 1, 3, C["teal"][5])                                             # sign strip
    if opened:
        p.rect(3, 5, w - 3, h, C["amber"][7])                                        # warm light from inside
        p.block(2, 5, 6, h, "brown", 4)
    else:
        p.block(3, 5, w - 3, h, "brown", 5)
        p.rect(5, 8, w - 5, h // 2, B[4]); p.px(w - 5, h // 2 + 4, C["amber"][6])
    return p


def teller_window():
    p = P("teller_window"); w, h = p.w - 1, p.h - 1
    p.block(0, h // 2, w, h, "brown", 4)
    for x in range(5, w - 3, 8):
        p.block(x, h // 2 + 4, x + 5, h - 3, "brown", 5)
    p.block(0, h // 2 - 2, w, h // 2 + 2, "brown", 6)
    p.block(0, 0, w, 3, "amber", 4)
    for i in range(3):                                                               # glass panes
        x0 = 2 + i * ((w - 3) // 3)
        x1 = x0 + (w - 3) // 3 - 2
        p.rect(x0, 4, x1, h // 2 - 3, C["cyan"][7]); p.rect(x0, 4, x1, 4, C["cyan"][5])
        p.rect(x0 + 2, 6, x0 + 4, 6, PAPER)
        p.rect(x0 + (x1 - x0) // 2 - 2, h // 2 - 4, x0 + (x1 - x0) // 2 + 2, h // 2 - 3, K[1])   # pass slot
        p.rect(x1 + 1, 3, x1 + 1, h // 2 - 3, C["amber"][3])
    return p


def thermometer():
    p = P("thermometer"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h - 2, "warm", 7)
    p.rect(w // 2, 2, w // 2, h - 3, C["red"][4])
    p.oval(0, h - 3, w, h, "red", 4)
    return p


def tube(name, ramp):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cyan", 7)
    p.clear(0, h); p.clear(w, h)
    if ramp:
        p.rect(1, h // 2, w - 1, h - 1, C[ramp][5]); p.rect(1, h // 2, w - 1, h // 2, C[ramp][7])
    p.rect(0, 0, w, 0, K[6])
    return p


def tube_stand():
    p = P("tube_stand"); w, h = p.w - 1, p.h - 1
    p.block(0, h - 4, w, h, "cool", 3)
    p.block(0, h // 2 - 1, w, h // 2 + 1, "brown", 4)
    for x in range(3, w - 2, 4):
        p.rect(x, h // 2 - 1, x + 1, h // 2 + 1, B[1])
    p.rect(1, h // 2 + 2, 1, h - 4, K[2]); p.rect(w - 1, h // 2 + 2, w - 1, h - 4, K[2])
    return p


def typewriter(name, struck):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, h // 2 - 1, w, h, "teal", 1)
    for r in range(2):
        for x in range(3, w - 2, 3):
            p.px(x + r, h - 4 + r * 2, PAPER if not (struck and x == 9 and r == 0) else C["amber"][6])
    p.block(2, 1, w - 2, h // 2, "cool", 3)                                          # carriage
    p.rect(4 + (1 if struck else 0), 0, w - 5 + (1 if struck else 0), h // 2 - 2, PAPER)
    p.rect(6, 2, w - 8, 2, K[2])
    return p


def vault_door(name, opened):
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "cool", 3)
    p.oval(2, 2, w - 2, h - 2, "cool", 5)
    p.oval(6, 6, w - 6, h - 6, "cool", 4)
    m, c = w // 2, h // 2
    import math
    turn = 22 if opened else 0
    for a in range(0, 360, 45):
        for t in range(2, 6):
            p.px(round(m + math.cos(math.radians(a + turn)) * t), round(c + math.sin(math.radians(a + turn)) * t), K[2])
    p.oval(m - 1, c - 1, m + 1, c + 1, "cool", 2)
    p.px(4, 4, K[7] if opened else K[5])
    for x, y in ((2, 2), (w - 2, 2), (2, h - 2), (w - 2, h - 2)):
        p.px(x, y, K[6])
    return p


def velvet_rope():
    p = P("velvet_rope"); w, h = p.w - 1, p.h - 1
    for x in (1, w - 1):
        p.rect(x - 1, 1, x + 1, h, C["amber"][4]); p.rect(x - 1, 1, x - 1, h, C["amber"][6]); p.px(x, 0, C["amber"][7])
        p.rect(x - 1 if x > 1 else 0, h, x + 1 if x < w - 1 else w, h, C["amber"][2])
    for x in range(3, w - 2):
        t = (x - 3) / max(1.0, w - 6.0)
        y = 3 + int(4 * (1 - (2 * t - 1) ** 2))
        p.px(x, y, C["red"][4]); p.px(x, y + 1, C["red"][2])
    return p


def vise():
    p = P("vise"); w, h = p.w - 1, p.h - 1
    p.block(2, h - 3, w - 2, h, "blue", 3)
    p.block(1, 3, 5, h - 2, "blue", 4); p.block(w - 6, 3, w - 2, h - 2, "blue", 4)     # jaws
    p.rect(5, 5, w - 6, 6, K[5])                                                     # screw
    p.rect(w - 2, 5, w, 5, K[4]); p.rect(w, 2, w, 8, K[3])                            # handle
    p.rect(6, 2, w - 7, 3, K[6])
    return p


def water_cooler():
    p = P("water_cooler"); w, h = p.w - 1, p.h - 1
    p.block(0, h // 2 - 1, w, h, "warm", 7)
    p.px(2, h // 2 + 2, C["blue"][4]); p.px(w - 2, h // 2 + 2, C["red"][4])
    p.rect(2, h // 2 + 4, w - 2, h // 2 + 5, K[4])
    p.block(1, 0, w - 1, h // 2 - 1, "cyan", 6, round_=True)                          # bottle
    p.rect(2, 2, 2, h // 2 - 3, PAPER); p.rect(2, h // 4, w - 2, h // 4, C["cyan"][4])
    return p


def window_big():
    p = P("window_big"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h - 1, "brown", 4)
    p.rect(2, 2, w - 2, h - 3, C["cyan"][6])
    p.rect(2, h - 6, w - 2, h - 3, C["green"][4]); p.rect(2, h - 6, w - 2, h - 6, C["lime"][5])
    p.oval(w - 7, 3, w - 3, 7, "amber", 6)
    p.rect(w // 2, 2, w // 2, h - 3, B[5]); p.rect(2, h // 2, w - 2, h // 2, B[5])
    p.rect(0, h, w, h, B[2])
    return p


DRAW_COL = {
    "bell_0": lambda: bell("bell_0", False), "bell_1": lambda: bell("bell_1", True), "bill_stack": bill_stack,
    "booth_art": lambda: booth("booth_art", "amber", C["brown"][3]),
    "booth_health": lambda: booth("booth_health", "red", C["red"][4]),
    "booth_science": lambda: booth("booth_science", "blue", C["teal"][5]),
    "bottle_blue": lambda: bottle("bottle_blue", "blue"), "bottle_green": lambda: bottle("bottle_green", "green"),
    "bottle_red": lambda: bottle("bottle_red", "red"), "bottle_yellow": lambda: bottle("bottle_yellow", "amber"),
    "campus_tree": campus_tree, "career_board": career_board,
    "cash_drawer_0": lambda: cash_drawer("cash_drawer_0", False), "cash_drawer_1": lambda: cash_drawer("cash_drawer_1", True),
    "clapboard_open": lambda: clapboard("clapboard_open", False), "clapboard_snap": lambda: clapboard("clapboard_snap", True),
    "coffee_cup": coffee_cup, "crash_pad": crash_pad,
    "deadline_clock_0": lambda: deadline_clock("deadline_clock_0", False), "deadline_clock_1": lambda: deadline_clock("deadline_clock_1", True),
    "desk_pc_idle": lambda: desk_pc("desk_pc_idle", "idle"), "desk_pc_busy_0": lambda: desk_pc("desk_pc_busy_0", "busy0"),
    "desk_pc_busy_1": lambda: desk_pc("desk_pc_busy_1", "busy1"),
    "pc_bill": lambda: desk_pc("pc_bill", "bill"), "pc_error": lambda: desk_pc("pc_error", "error"),
    "pc_game": lambda: desk_pc("pc_game", "game"), "pc_ok": lambda: desk_pc("pc_ok", "ok"),
    "dining_chair": dining_chair, "dining_table": dining_table, "director_chair": director_chair,
    "dock_lamp_off": lambda: dock_lamp("dock_lamp_off", False), "dock_lamp_on": lambda: dock_lamp("dock_lamp_on", True),
    "door": door, "envelope": envelope,
    "envelope_stack": lambda: envelope_stack("envelope_stack", False), "envelopes_ready": lambda: envelope_stack("envelopes_ready", True),
    "film_camera_0": lambda: film_camera("film_camera_0", False), "film_camera_1": lambda: film_camera("film_camera_1", True),
    "fit_slot_empty": lambda: fit_slot("fit_slot_empty", False), "fit_slot_full": lambda: fit_slot("fit_slot_full", True),
    "for_sale_sign": for_sale_sign, "fuel_drum": fuel_drum,
    "gauge_0": lambda: gauge("gauge_0", True), "gauge_1": lambda: gauge("gauge_1", False),
    "generator_off": lambda: generator("generator_off", "off"), "generator_on_0": lambda: generator("generator_on_0", "on0"),
    "generator_on_1": lambda: generator("generator_on_1", "on1"),
    "goggles_hook": goggles_hook,
    "grinder_0": lambda: grinder("grinder_0", "0"), "grinder_1": lambda: grinder("grinder_1", "1"),
    "grinder_dull": lambda: grinder("grinder_dull", "dull"), "guide_jig": guide_jig,
    "jar_cover": lambda: jar("jar_cover", "cover"), "jar_dark": lambda: jar("jar_dark", "dark"),
    "jar_dim": lambda: jar("jar_dim", "dim"), "jar_empty": lambda: jar("jar_empty", "empty"), "jar_lit": lambda: jar("jar_lit", "lit"),
    "keycard": keycard, "lab_table": lab_table, "luggage_cart": luggage_cart, "med_table": med_table,
    "mortar": mortar, "pestle": pestle, "notice_board": notice_board, "office_desk": office_desk, "paint_roller": paint_roller,
    "photo_frame_0": lambda: photo_frame("photo_frame_0", False), "photo_frame_1": lambda: photo_frame("photo_frame_1", True),
    "prop_cart": prop_cart, "ramp_big": lambda: ramp("ramp_big"), "ramp_small": lambda: ramp("ramp_small"),
    "receipt_printer_0": lambda: receipt_printer("receipt_printer_0", False),
    "receipt_printer_1": lambda: receipt_printer("receipt_printer_1", True),
    "sample_cabinet": lambda: sample_cabinet("sample_cabinet", False),
    "sample_cabinet_open": lambda: sample_cabinet("sample_cabinet_open", True), "sample_dish": sample_dish,
    "stamp_down": lambda: stamp("stamp_down", True), "stamp_up": lambda: stamp("stamp_up", False),
    "sticky_0": lambda: sticky("sticky_0", "amber"), "sticky_1": lambda: sticky("sticky_1", "pink"),
    "sticky_2": lambda: sticky("sticky_2", "green"),
    "suitcase": suitcase,
    "support_door_0": lambda: support_door("support_door_0", False), "support_door_1": lambda: support_door("support_door_1", True),
    "teller_window": teller_window, "thermometer": thermometer,
    "tube_blue": lambda: tube("tube_blue", "blue"), "tube_green": lambda: tube("tube_green", "green"),
    "tube_pale": lambda: tube("tube_pale", None), "tube_red": lambda: tube("tube_red", "red"), "tube_stand": tube_stand,
    "typewriter_0": lambda: typewriter("typewriter_0", False), "typewriter_1": lambda: typewriter("typewriter_1", True),
    "vault_door_0": lambda: vault_door("vault_door_0", False), "vault_door_1": lambda: vault_door("vault_door_1", True),
    "velvet_rope": velvet_rope, "vise": vise, "water_cooler": water_cooler, "window_big": window_big,
}
for _n, _r in (("a", "cyan"), ("b", "violet"), ("c", "teal")):
    for _g in (0, 1):
        DRAW_COL["gmush_%s_%d" % (_n, _g)] = (lambda n, r, g: lambda: gmush(n, r, g))("gmush_%s_%d" % (_n, _g), _r, _g == 1)
DRAW.update(DRAW_COL)
# ================================================================ Boards with states, and props drawn at a stretched shape

def piano(name, lit):
    """lit: indexes of the white keys that are pressed (drawn in amber)."""
    p = P(name); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "brown", 4)
    p.block(0, 0, w, 2, "brown", 5)                                                 # lid
    p.rect(w // 2 - 7, 4, w // 2 + 7, 8, PAPER); p.rect(w // 2 - 7, 8, w // 2 + 7, 8, W7[5])   # sheet music
    for x in range(w // 2 - 5, w // 2 + 6, 3):
        p.px(x, 5 + (x % 2), INK); p.px(x + 1, 6, K[3])
    ky = h * 2 // 5
    p.rect(0, ky - 1, w, ky + 5, B[1])
    keys = (w - 1) // 2
    for i in range(keys):                                                          # white keys, two pixels each
        x = 1 + i * 2
        col = C["amber"][6] if i in lit else PAPER
        p.rect(x, ky, x, ky + 4, col); p.px(x, ky + 4, C["amber"][3] if i in lit else W7[5])
        if i % 7 not in (2, 6):                                                    # black keys between them
            p.rect(x + 1, ky, x + 1, ky + 2, INK)
        else:
            p.rect(x + 1, ky, x + 1, ky + 4, W7[4])
    p.block(2, ky + 8, w - 2, h - 2, "brown", 3)                                    # lower panel
    p.rect(4, ky + 10, w - 4, ky + 10, B[5])
    p.px(w // 2 - 2, h - 1, C["amber"][6]); p.px(w // 2 + 2, h - 1, C["amber"][6])    # pedals
    p.block(0, h - 3, 2, h, "brown", 2); p.block(w - 2, h - 3, w, h, "brown", 2)
    return p


def story_book(name, stage):
    """0: open. 1: a page turning. 2: open, with a marked line."""
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(0, 1, w, h, "brown", 2)
    for x0, x1 in ((1, m - 1), (m + 1, w - 1)):
        p.rect(x0, 0, x1, h - 1, PAPER); p.rect(x0, h - 1, x1, h - 1, W7[5])
        for y in range(2, h - 2, 2):
            p.rect(x0 + 1, y, x1 - 1, y, K[4])
    p.rect(m, 0, m, h, W7[4])
    if stage == 1:                                                                 # the right page lifts
        for k in range(0, m - 1):
            p.rect(m + 1 + k, max(0, -2 + k // 2), m + 1 + k, h - 2 - k // 2, PAPER)
        p.rect(m + 1, 0, m + 1, h - 2, W7[5]); p.rect(m + 2, h - 1, w - 1, h - 1, W7[6])
    if stage == 2:
        p.rect(m + 2, 3, w - 2, 5, C["amber"][6])
    return p


def ledger_open(name, stage):
    """0: blank pages. 1: entries written. 2: entries checked."""
    p = P(name); w, h = p.w - 1, p.h - 1
    m = w // 2
    p.block(1, h - 5, w - 1, h, "brown", 4)                                         # the desk stand
    p.block(0, 0, w, h - 4, "cool", 1)
    for x0, x1 in ((1, m - 1), (m + 1, w - 1)):
        p.rect(x0, 1, x1, h - 5, PAPER); p.rect(x0, h - 5, x1, h - 5, W7[5])
        if stage >= 1:
            for y in range(3, h - 6, 3):
                p.rect(x0 + 1, y, x1 - 2 - (y % 2), y, C["blue"][4])
    p.rect(m, 1, m, h - 5, W7[4])
    if stage == 2:
        cx, cy = w - 5, h - 8
        p.px(cx - 1, cy, C["green"][4]); p.px(cx, cy + 1, C["green"][4]); p.px(cx + 1, cy, C["green"][4]); p.px(cx + 2, cy - 1, C["green"][4]); p.px(cx + 3, cy - 2, C["green"][4])
    return p


# ---- props that rooms stretch wider than tall: one sprite per stretched shape, drawn at that shape ----
STRETCHED = {                       # new sprite: (source sprite, x scale, y scale)
    "rug_oval": ("rug_round", 3.2, 2.2),
    "carpet_runner": ("rug_round", 7.5, 1.6),
    "curtain_valance": ("curtain", 8.0, 0.6),
    "curtain_tall": ("curtain", 2.3, 3.9),
}
for _new, (_src, _sx, _sy) in STRETCHED.items():
    _w, _h = BOX[_src]["size"]
    _b = BOX[_src]["bbox"]
    BOX[_new] = {"size": [int(round(_w * _sx)), int(round(_h * _sy))],
                 "bbox": [int(round(_b[0] * _sx)), int(round(_b[1] * _sy)), int(round(_b[2] * _sx)), int(round(_b[3] * _sy))]}


def rug_oval():
    p = P("rug_oval"); w, h = p.w - 1, p.h - 1
    e = max(2, h // 8)
    p.oval(0, 0, w, h, "blue", 4)
    p.oval(e, e - 1, w - e, h - e + 1, "amber", 5)
    p.oval(2 * e, 2 * e - 1, w - 2 * e, h - 2 * e + 1, "red", 4)
    p.oval(3 * e + 2, 3 * e - 1, w - 3 * e - 2, h - 3 * e + 1, "amber", 6)
    return p


def carpet_runner():
    p = P("carpet_runner"); w, h = p.w - 1, p.h - 1
    p.block(0, 0, w, h, "red", 3)
    p.rect(2, 2, w - 2, h - 2, C["red"][4])
    p.rect(3, 3, w - 3, 3, C["amber"][5]); p.rect(3, h - 3, w - 3, h - 3, C["amber"][5])       # gold bands
    p.rect(3, 3, 3, h - 3, C["amber"][5]); p.rect(w - 3, 3, w - 3, h - 3, C["amber"][5])
    for x in range(10, w - 8, 12):                                                           # a row of diamonds
        c = h // 2
        for k in range(-3, 4):
            p.rect(x + k, c - (3 - abs(k)), x + k, c + (3 - abs(k)), C["amber"][6] if abs(k) < 2 else C["blue"][4])
    for y in range(1, h, 2):                                                                 # fringe on both ends
        p.px(0, y, C["amber"][7]); p.px(w, y, C["amber"][7])
    return p


def curtain_shape(name, valance):
    p = P(name); w, h = p.w - 1, p.h - 1
    R = C["red"]
    if valance:                                                                              # the strip across the top
        p.rect(0, 0, w, h, R[3])
        p.rect(0, 0, w, 1, C["amber"][5]); p.rect(0, 0, w, 0, C["amber"][7])
        for x in range(0, w + 1):
            drop = h - 2 + (1 if (x % 12) in (4, 5, 6, 7) else 0) + (1 if (x % 12) in (5, 6) else 0)
            p.rect(x, 2, x, min(h, drop), R[4] if (x % 12) < 6 else R[3])
            p.px(x, min(h, drop), C["amber"][5])
            for y in range(min(h, drop) + 1, h + 1):
                p.clear(x, y)
        return p
    for x in range(0, w + 1):                                                                # a hanging drape
        k = x % 6
        col = R[4] if k in (0, 1) else (R[3] if k in (2, 5) else R[2])
        p.rect(x, 0, x, h, col)
    p.rect(0, 0, w, 3, C["amber"][5]); p.rect(0, 0, w, 0, C["amber"][7]); p.rect(0, 3, w, 3, C["amber"][3])
    p.rect(0, h - 1, w, h - 1, C["amber"][5]); p.rect(0, h, w, h, R[1])
    p.rect(0, 0, 0, h, R[1]); p.rect(w, 0, w, h, R[1])
    return p


DRAW_BOARDS = {
    "piano_0": lambda: piano("piano_0", ()), "piano_1": lambda: piano("piano_1", (2, 4)),
    "piano_2": lambda: piano("piano_2", (9, 11)),
    "story_book_0": lambda: story_book("story_book_0", 0), "story_book_1": lambda: story_book("story_book_1", 1),
    "story_book_2": lambda: story_book("story_book_2", 2),
    "ledger_0": lambda: ledger_open("ledger_0", 0), "ledger_1": lambda: ledger_open("ledger_1", 1),
    "ledger_2": lambda: ledger_open("ledger_2", 2),
    "rug_oval": rug_oval, "carpet_runner": carpet_runner,
    "curtain_valance": lambda: curtain_shape("curtain_valance", True),
    "curtain_tall": lambda: curtain_shape("curtain_tall", False),
}
DRAW.update(DRAW_BOARDS)
# Animals keep their original art (2026-11-13: the redrawn rabbits lost their look).
# redefine_quest_props.py restores them at their original pixel grain.
for _n in [n for n in DRAW if re.match(r"(rabbit_|sparrow_|fish_)", n)]:
    del DRAW[_n]
HAND_DRAWN = set(DRAW)

def shrink_exact(im, w, h):
    """Dominant-colour shrink to an exact size (each new pixel takes the colour covering most of it)."""
    import numpy as np
    a = np.array(im.convert("RGBA"))
    s = 6
    big = np.repeat(np.repeat(a, s * w // im.width + 1, 1), s * h // im.height + 1, 0)
    big = np.array(Image.fromarray(big, "RGBA").resize((w * s, h * s), Image.NEAREST))
    out = np.zeros((h, w, 4), np.uint8)
    for y in range(h):
        for x in range(w):
            blk = big[y * s:(y + 1) * s, x * s:(x + 1) * s].reshape(-1, 4)
            cols, cnt = np.unique(blk, axis=0, return_counts=True)
            wgt = cnt.astype(float)
            for i, c in enumerate(cols):
                if c[3] > 0 and int(c[0]) + int(c[1]) + int(c[2]) < 200:
                    wgt[i] *= 1.6                    # dark outlines win ties
            c = cols[wgt.argmax()]
            if c[3] >= 128:
                out[y, x] = (c[0], c[1], c[2], 255)
    return Image.fromarray(out, "RGBA")


if __name__ == "__main__":
    for name, fn in DRAW.items():
        im = fn().im
        k = SCALE.get(name, 1)
        w0 = BOX[name]["size"][0]
        if k < 1:                                   # shown smaller than drawn: shrink, keeping pixels crisp
            w = max(1, int(round(w0 * k)))
            im = shrink_exact(im, w, max(1, int(round(im.height * w / float(im.width)))))
        meta = os.path.join(DIR, name + ".png.meta")
        if not os.path.exists(meta):                # a new sprite: same import settings as the one it replaces
            import uuid
            src = open(os.path.join(DIR, STRETCHED[name][0] + ".png.meta"), encoding="utf-8").read()
            src = re.sub(r"guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, src, count=1)
            open(meta, "w", encoding="utf-8", newline=chr(10)).write(src)
        txt = open(meta, encoding="utf-8").read()
        txt = re.sub(r"spritePixelsToUnits: [\d.]+", "spritePixelsToUnits: %g" % (16.0 * im.width / w0), txt)
        open(meta, "w", encoding="utf-8", newline=chr(10)).write(txt)
        for attempt in range(30):               # the Editor may hold a file for a moment
            try:
                im.save(os.path.join(DIR, name + ".png"))
                break
            except OSError:
                time.sleep(0.3)
    # contact sheet for review
    names = sorted(DRAW)
    cols, cell = 12, 60
    sheet = Image.new("RGBA", (cols * cell * 3, ((len(names) + cols - 1) // cols) * cell * 3), (120, 160, 90, 255))
    for i, n in enumerate(names):
        im = Image.open(os.path.join(DIR, n + ".png"))
        k = 3 if max(im.size) <= 56 else 2
        im = im.resize((im.width * k, im.height * k), Image.NEAREST)
        sheet.alpha_composite(im, ((i % cols) * cell * 3 + 6, (i // cols) * cell * 3 + 6))
    os.makedirs("../previews/art_rework", exist_ok=True)
    sheet.save("../previews/art_rework/hand_props.png")
    print("hand-drawn props:", len(DRAW))
