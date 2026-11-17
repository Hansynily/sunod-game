"""Stall art: keepsake icons (+ Lv2 upgrades), stall goods, outfits, hats, trails, and a
sample sheet for review.

Run from the sunod-game folder:   python Tools/art/make_cosmetics.py [--sample-only]

Writes
  Assets/Resources/Loop/memento_<id>.png        new keepsake icons (16x16, like make_loop_icons.py)
  Assets/Resources/Loop/memento_<id>_lv2.png    Lv2: gold edge + a sparkle, every giver keepsake
  Assets/Resources/Shop/*.png                   stall icons (helpers, pockets, coin, keeper)
  Assets/Resources/Cosmetics/outfits.json       outfit palette swaps (read by vc_PlayerCosmetics)
  Assets/Resources/Cosmetics/hat_<id>_<dir>.png hat overlays (32 px/unit, like the player)
  Assets/Resources/Cosmetics/<trail>_<n>.png    trail bits (16 px/unit)
  Tools/art/cosmetics_sample.png                the review sheet

Every colour is snapped to the Umaga palette (sunod_palette.py). Outfits are palette swaps of
the clothes on the player's own frames, below the collar only (the same rule as
vc_PlayerCosmetics.Recolor), so hair that shares a colour with trousers / skirt is untouched.
"""
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from sunod_palette import C, INK, PAPER, remap  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
RES = os.path.join(ROOT, "Assets", "Resources")
LOOP = os.path.join(RES, "Loop")
SHOP = os.path.join(RES, "Shop")
COS = os.path.join(RES, "Cosmetics")
FRAMES = os.path.join(ROOT, "Assets", "Art", "Characters", "Player", "Frames")
USE = os.path.join(RES, "PlayerUse")
for d in (LOOP, SHOP, COS):
    os.makedirs(d, exist_ok=True)

T = (0, 0, 0, 0)


def rgba(c):
    return tuple(c[:3]) + (255,)


def hexc(c):
    return "#%02x%02x%02x" % tuple(c[:3])


# icon colours: palette ramps only (dark -> light index 0..7)
PAL = {
    ".": T, "o": rgba(INK), "w": rgba(PAPER),
    "y": rgba(C["amber"][6]), "Y": rgba(C["amber"][4]), "j": rgba(C["amber"][2]),
    "r": rgba(C["red"][5]), "R": rgba(C["red"][3]),
    "p": rgba(C["pink"][6]), "P": rgba(C["pink"][4]),
    "b": rgba(C["blue"][5]), "B": rgba(C["blue"][3]),
    "c": rgba(C["warm"][6]), "C": rgba(C["warm"][4]),
    "n": rgba(C["brown"][4]), "N": rgba(C["brown"][2]),
    "g": rgba(C["green"][5]), "G": rgba(C["green"][3]),
    "s": rgba(C["cool"][5]), "S": rgba(C["cool"][3]),
    "h": rgba(C["brown"][6]), "H": rgba(C["brown"][4]), "m": rgba(C["brown"][3]),
    "t": rgba(C["teal"][5]), "v": rgba(C["violet"][5]),
    "k": rgba(C["orange"][5]),
}


def grid(rows):
    w = max(len(r) for r in rows)
    im = Image.new("RGBA", (w, len(rows)), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), PAL.get(ch, T))
    return im


# ── keepsake icons (16x16, same build as make_loop_icons.py) ─────────────────
ICONS = {
    "hammer": [  # Papa's Hammer: worn wooden handle, steel head
        "................",
        "......ooooo.....",
        ".....osssSSo....",
        "....oswsssSSoo..",
        "....osssssSSSo..",
        ".....ooonoSSo...",
        "........onoo....",
        ".......onNo.....",
        "......onNo......",
        ".....onNo.......",
        "....onNo........",
        "...onNo.........",
        "..onNo..........",
        "..oNo...........",
        "...o............",
        "................",
    ],
    "notebook": [  # Field Notebook: green cover, elastic band, pencil
        "................",
        "..oooooooooo.k..",
        "..oGgggggggokko.",
        "..oGgggggggoyko.",
        "..oGgcccccgoyyo.",
        "..oGgcCCcCgoyyo.",
        "..oGgggggggoyyo.",
        "..oGrrrrrrroyyo.",
        "..oGgggggggoyyo.",
        "..oGgcccgggoyyo.",
        "..oGgggggggoono.",
        "..oGgggggggo.o..",
        "..oGGGGGGGGo....",
        "..oooooooooo....",
        "................",
        "................",
    ],
    "nametag": [  # Name Tag: red band, a scribbled name, safety pin
        "......sss.......",
        ".....s...s......",
        "..oooooooooooo..",
        "..orrrrrrrrrro..",
        "..orwwrwrwrwro..",
        "..oRRRRRRRRRRo..",
        "..owwwwwwwwwwo..",
        "..owBBwBwBBwwo..",
        "..owBwBBwBwBwo..",
        "..owwwwwwwwwwo..",
        "..ocCCCCCCCCco..",
        "..oooooooooooo..",
        "................",
        "................",
        "................",
        "................",
    ],
    "presspass": [  # Press Pass: lanyard + card with photo
        "....b......b....",
        ".....b....b.....",
        "......b..b......",
        ".......oo.......",
        "....oooyyooo....",
        "....oBBBBBBo....",
        "....owwwwwwo....",
        "....owNNwccо....".replace("о", "o"),
        "....owNnwCCo....",
        "....owwwwwwo....",
        "....owSSSSwo....",
        "....owSSwwwo....",
        "....owwwwwwo....",
        "....oooooooo....",
        "................",
        "................",
    ],
    "jacket": [  # Vintage Jacket: olive bomber, gold buttons
        "................",
        ".....oo..oo.....",
        "...ooGcooocGoo..",
        "..oGgGcoocGgGo..",
        ".oGgggGooGgggGo.",
        ".oGggggyoggggGo.",
        ".oGgoggggggogGo.",
        ".oGgoggyoggogGo.",
        ".oGgoggggggogGo.",
        ".oGgoggyoggogGo.",
        ".oGgoGgggggoGgo.",
        ".oo.oCCCCCCCo.o.",
        "....oooooooo....",
        "................",
        "................",
        "................",
    ],
    "coinpurse": [  # Coin Purse: pink purse, gold kiss-lock clasp
        "................",
        "......o..o......",
        ".....oyooyo.....",
        "....oyYooYyo....",
        "...oooooooooo...",
        "..oPppppppppPo..",
        ".oPppwppppppPPo.",
        ".oPpwppppppppPo.",
        ".oPppppppppppPo.",
        ".oPpppppppppPPo.",
        ".oPPppppppPPPPo.",
        "..oPPPPPPPPPPo..",
        "...oooooooooo...",
        "................",
        "................",
        "................",
    ],
    "banderitas": [  # Banderitas: fiesta bunting on a string
        "................",
        "................",
        "o..............o",
        ".ooo........ooo.",
        "...oooooooooo...",
        "..orro.oyyo.obbo",
        "..orRo.oyYo.obBo",
        "...oro..oyo..obo",
        "...oo...oo....o.",
        "....o....o......",
        "................",
        "..ogo..opo..oyo.",
        "..ogGo.opPo.oyYo",
        "...ogo..opo..oyo",
        "....o....o....o.",
        "................",
    ],
    "shell": [  # Lucky Shell: pink spiral
        "................",
        "................",
        "......oooo......",
        "....oopppPoo....",
        "...opwppppPPo...",
        "..opwpoooopPPo..",
        "..opppoPPoopPo..",
        "..oppoPwpPopPo..",
        "..oppopppoppPo..",
        "...oppoooppPo...",
        "....oopppPPo....",
        "......ooooPPo...",
        "..........ooo...",
        "................",
        "................",
        "................",
    ],
    "luckycoin": [  # Lucky Coin: old gold coin with a square hole
        "................",
        ".....oooooo.....",
        "....oyyyyyYo....",
        "...oywyyyyyYo...",
        "..oywyyyyyyyYo..",
        "..oyyyYooYyyYo..",
        "..oyyyo..oyyYo..",
        "..oyyyo..oyyYo..",
        "..oyyyYooYyyYo..",
        "..oyyyyyyyyyYo..",
        "...oyyyyyyyYo...",
        "....oYYYYYYo....",
        ".....oooooo.....",
        "................",
        "................",
        "................",
    ],
}

GIVER = ["compass", "sneakers", "ribbon", "hammer", "lantern", "locket", "notebook", "jacket",
         "whistle", "nametag", "presspass", "coinpurse"]


def snap(im):
    """Palette snap that keeps alpha (sunod_palette.remap on the colours)."""
    rgb = remap(im.convert("RGBA"))
    return rgb


def lv2(icon):
    """Friendship upgrade look: a 1 px gold rim around the silhouette, a lighter gold glint on
    the outline's top-left, and a 4-point sparkle in the top-right corner."""
    w, h = icon.size
    out = icon.copy()
    gold, gold_hi, gold_lo = rgba(C["amber"][5]), rgba(C["amber"][7]), rgba(C["amber"][3])
    a = icon.load()
    o = out.load()
    for y in range(h):
        for x in range(w):
            if a[x, y][3] != 0:
                continue
            nb = [(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            if any(0 <= nx < w and 0 <= ny < h and a[nx, ny][3] != 0 for nx, ny in nb):
                o[x, y] = gold_lo if (x + y) % 3 == 0 else gold
    # outline pixels on the top-left catch the light
    for y in range(h):
        for x in range(w):
            if a[x, y][:3] == tuple(INK) and (x + y) < w * 0.75:
                if (x > 0 and a[x - 1, y][3] == 0) or (y > 0 and a[x, y - 1][3] == 0):
                    o[x, y] = gold_lo
    # sparkle
    for (sx, sy) in ((13, 0), (13, 2), (12, 1), (14, 1)):
        if 0 <= sx < w and 0 <= sy < h:
            o[sx, sy] = gold_hi
    o[13, 1] = rgba(PAPER)
    return out


def make_icons():
    out = {}
    for name, rows in ICONS.items():
        im = snap(grid(rows))
        im.save(os.path.join(LOOP, f"memento_{name}.png"))
        out[name] = im
    for name in GIVER:
        base = Image.open(os.path.join(LOOP, f"memento_{name}.png")).convert("RGBA")
        up = lv2(base)
        up.save(os.path.join(LOOP, f"memento_{name}_lv2.png"))
        out[name + "_lv2"] = up
        out.setdefault(name, base)
    return out


# ── stall goods ──────────────────────────────────────────────────────────────
SHOP_ICONS = {
    "coin_star": [
        "...oo...",
        "..oyyo..",
        "ooyyyYoo",
        "oyywyyYo",
        ".oyyyYo.",
        ".oyYoYo.",
        "oyo..oYo",
        "oo....oo",
    ],
    "icon_reroll": [  # two cards with a turn arrow
        "................",
        "...oooooo.......",
        "...owwwwoooooo..",
        "...owbbwowwwwo..",
        "...owbbwowrrwo..",
        "...owwwwowrrwo..",
        "...owwwwowwwwo..",
        "...oooooowwwwo..",
        "........oooooo..",
        "..g.............",
        "..gg.......g....",
        "..ggggggggggg...",
        "..gg.......g....",
        "..g.............",
        "................",
        "................",
    ],
    "icon_luckycard": [
        "................",
        "....oooooooo....",
        "....oyyyyyyo....",
        "....oywwwwyo....",
        "....oywyywyo....",
        "....owyyyywo....",
        "....owwyywwo....",
        "....oywyywyo....",
        "....oywwwwyo....",
        "....oyyyyyyo....",
        "....oooooooo....",
        "................",
        "...y......y.....",
        "..yWy....yWy....".replace("W", "w"),
        "...y......y.....",
        "................",
    ],
    "icon_studynotes": [
        "................",
        "...oooooooo.....",
        "...owwwwwwoo....",
        "...owSSSSwwoo...",
        "...owwwwwwwwo...",
        "...owSSSSSwwo...",
        "...owwwwwwwwo.k.",
        "...owSSSwwwwokko",
        "...owwwwwwwwoyko",
        "...owSSSSSwwoyyo",
        "...owwwwwwwwoyyo",
        "...oooooooooono.",
        "..............o.",
        "................",
        "................",
        "................",
    ],
    "pocket_open": [
        "................",
        "................",
        "..oooooooooooo..",
        "..onnnnnnnnnno..",
        "..oNNNNNNNNNNo..",
        "..onnnnnnnnnno..",
        "..onn......nno..",
        "..on........no..",
        "..on........no..",
        "..on........no..",
        "..onn......nno..",
        "...onnnnnnnno...",
        "....oooooooo....",
        "................",
        "................",
        "................",
    ],
    "pocket_locked": [
        "................",
        "................",
        "..oooooooooooo..",
        "..oCCCCCCCCCCo..",
        "..oCCCCCCCCCCo..",
        "..oCC......CCo..",
        "..oC...oo...Co..",
        "..oC..o..o..Co..",
        "..oC.oSSSSo.Co..",
        "..oC.oSooSo.Co..",
        "..oCC.oSSo.CCo..",
        "...oCCCCCCCCo...",
        "....oooooooo....",
        "................",
        "................",
        "................",
    ],
    "keeper_face": [  # Aling Tess: grey bun, warm smile, a sampaguita in her hair
        "................",
        ".....oooooo.....",
        "....osswwsso....",
        "...osSssssSSo...",
        "..osSoooooooSo..",
        "..osocccccccoo..",
        "..ooccoccoccoo..",
        "...occcccccco...",
        "...occpcccpco...",
        "...occcRRRcco...",
        "....occcccco....",
        "...oorrrrrroo...",
        "..orrrwrrwrrro..",
        "..orrrrrrrrrro..",
        "..oooooooooooo..",
        "................",
    ],
}


def make_shop():
    out = {}
    for name, rows in SHOP_ICONS.items():
        im = snap(grid(rows))
        im.save(os.path.join(SHOP, name + ".png"))
        out[name] = im
    return out


# ── outfits: palette swaps of the clothes ───────────────────────────────────
# Source clothes colours on the player frames (white shirt + its shade, the gold tie and its
# pin, the trousers / skirt). Hair and outline colours are never in these lists.
SRC = {
    "boy": {
        "shirt": ["#ffffff", "#ccd2ea", "#ffd080", "#808fff"],
        "lower": ["#1d1e3a", "#2c2e52", "#4e5a8a"],
    },
    "girl": {
        "shirt": ["#ffffff", "#ccd2ea", "#ffd080", "#808fff"],
        "lower": ["#383639", "#272727", "#68636c", "#393a5c"],
    },
}

# outfit -> (shirt light, shirt shade, accent, accent2, lower main, lower dark, lower light)
OUTFITS = {
    # Fiesta Best: barong (pina ecru, gold embroidery, black slacks) / Filipiniana (pink
    # butterfly-sleeve top, gold trim, deep red skirt)
    "outfit_fiesta": {
        "boy": (C["warm"][6], C["warm"][4], C["amber"][4], C["amber"][3], C["cool"][1], C["cool"][0], C["cool"][2]),
        "girl": (C["pink"][7], C["pink"][5], C["amber"][5], C["amber"][4], C["red"][2], C["red"][1], C["red"][3]),
    },
    # Yellow Raincoat: a rain suit, top to bottom
    "outfit_raincoat": {
        "boy": (C["amber"][6], C["amber"][4], C["amber"][3], C["amber"][3], C["amber"][5], C["amber"][3], C["amber"][6]),
        "girl": (C["amber"][6], C["amber"][4], C["amber"][3], C["amber"][3], C["amber"][5], C["amber"][3], C["amber"][6]),
    },
}


def outfit_maps():
    data = {"outfits": []}
    for oid, sides in OUTFITS.items():
        entry = {"id": oid}
        for who, t in sides.items():
            src = SRC[who]
            to_shirt = [t[0], t[1], t[2], t[3]]
            lower_to = [t[4], t[5], t[6], t[4]]
            frm = src["shirt"] + src["lower"]
            to = to_shirt[:len(src["shirt"])] + lower_to[:len(src["lower"])]
            entry[who] = {"from": frm, "to": [hexc(c) for c in to], "lower": src["lower"]}
        data["outfits"].append(entry)
    with open(os.path.join(COS, "outfits.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    return data


def apply_outfit(frame, side):
    """Same rule as vc_PlayerCosmetics.Recolor (image rows run top -> bottom here)."""
    im = frame.convert("RGBA")
    w, h = im.size
    px = im.load()
    frm = [tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) for c in side["from"]]
    to = [tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) for c in side["to"]]
    lower = {tuple(int(c[i:i + 2], 16) for i in (1, 3, 5)) for c in side["lower"]}
    shirt = set(frm) - lower
    m = dict(zip(frm, to))
    collar, waist = None, -1
    for y in range(h):
        run = best = 0
        for x in range(w):
            p = px[x, y]
            run = run + 1 if p[3] and p[:3] in shirt else 0
            best = max(best, run)
        if best >= 4:
            if collar is None:
                collar = y
            waist = max(waist, y)
    if collar is None:
        return im
    for y in range(h):
        for x in range(w):
            p = px[x, y]
            if not p[3] or p[:3] not in m:
                continue
            if p[:3] in lower:
                if y < waist - 1:
                    continue
            elif y < collar:
                continue
            px[x, y] = m[p[:3]] + (p[3],)
    return im


# ── hats: overlays per facing, bottom-centre sits on the head ────────────────
HATS = {
    # salakot: wide woven cone with a finial; same from the front and the back, a little
    # narrower in profile
    "hat_salakot": {
        "down": [
            "...........oo...........",
            "..........ohHo..........",
            ".........ohhHHo.........",
            ".......oohmhmHHoo.......",
            ".....oohhmhhmhHHHoo.....",
            "...oohhmhhmhhmhHHHHoo...",
            ".oohhhhhhhhhhhhhHHHHHoo.",
            "ommmmmmmmmmmmmmmmmmmmmmo",
            ".oooooooooooooooooooooo.",
        ],
        "side": [
            "..........oo..........",
            ".........ohHo.........",
            "........ohhHHo........",
            "......oohmhmHHoo......",
            "....oohhmhhmhHHHoo....",
            "..oohhmhhmhhmhHHHHoo..",
            "oohhhhhhhhhhhhhHHHHHoo",
            "ommmmmmmmmmmmmmmmmmmmo",
            ".oooooooooooooooooooo.",
        ],
    },
}
HAT_SINK = 5   # vc_PlayerCosmetics.HatSink


def make_hats():
    out = {}
    for hid, views in HATS.items():
        down = snap(grid(views["down"]))
        side = snap(grid(views.get("side", views["down"])))
        for d, im in (("down", down), ("up", down), ("left", side), ("right", side.transpose(Image.FLIP_LEFT_RIGHT))):
            im.save(os.path.join(COS, f"{hid}_{d}.png"))
            out[(hid, d)] = im
    return out


def head_top(frame):
    """vc_PlayerCosmetics.HeadTop, in image coords: (centre x, top row)."""
    im = frame.convert("RGBA")
    w, h = im.size
    px = im.load()
    top = next((y for y in range(h) for x in range(w) if px[x, y][3]), 0)
    xs = [x for y in range(top, min(h, top + 7)) for x in range(w) if px[x, y][3]]
    cx = (min(xs) + max(xs) + 1) // 2 if xs else w // 2
    return cx, top


def wear_hat(frame, hat):
    im = frame.convert("RGBA").copy()
    cx, top = head_top(frame)
    bottom = top + HAT_SINK
    canvas = Image.new("RGBA", (im.width + 24, im.height + 12), T)
    canvas.alpha_composite(im, (12, 12))
    canvas.alpha_composite(hat, (12 + cx - hat.width // 2, 12 + bottom - hat.height))
    return canvas


# ── trail bits (16 px/unit) ─────────────────────────────────────────────────
TRAILS = {
    "trail_petals": [[".p.", "pPp", ".p."], ["pp.", "pPp", ".pp"], [".pp", "pP.", "p.."]],
    "dust": [[".s.", "sss", ".s."], ["ss", "ss"], ["s"]],
    "sparkle": [[".y.", "ywy", ".y."], ["y.y", ".w.", "y.y"], [".w.", "w.w", ".w."]],
}


def make_trails():
    for tid, frames in TRAILS.items():
        for i, rows in enumerate(frames):
            snap(grid(rows)).save(os.path.join(COS, f"{tid}_{i}.png"))


# ── the sample sheet ────────────────────────────────────────────────────────

def load_frame(who, kind, facing, i):
    if who == "girl":
        p = os.path.join(FRAMES, "Girl", f"girl_{kind}_{facing}_{i}.png")
    else:
        p = os.path.join(FRAMES, f"player_{kind}_{facing}_{i}.png")
    if kind == "use":
        p = os.path.join(USE, f"{'player' if who == 'boy' else who}_use_{facing}_{i}.png")
    return Image.open(p).convert("RGBA")


def sample_sheet(icons, shop, maps, hats):
    S = 4
    bg = rgba(C["warm"][6])
    sheet = Image.new("RGBA", (1600, 1240), bg)
    d = ImageDraw.Draw(sheet)

    def label(x, y, text, color=INK):
        d.text((x, y), text, fill=rgba(color))

    def put(im, x, y, scale=S):
        sheet.alpha_composite(im.resize((im.width * scale, im.height * scale), Image.NEAREST), (x, y))

    label(20, 12, "SUNOD stall art - sample for review (4x; in game: icons 16px, player 32 px/unit)")

    # keepsakes
    label(20, 40, "New keepsake icons")
    x = 20
    for name in ["hammer", "notebook", "jacket", "nametag", "presspass", "coinpurse", "banderitas", "shell", "luckycoin"]:
        put(icons[name], x, 60)
        label(x, 130, name)
        x += 84
    label(20, 160, "Lv2 (friendship upgrade): gold rim + glint + sparkle, next to Lv1")
    x = 20
    for name in ["compass", "hammer", "lantern", "coinpurse"]:
        put(icons[name], x, 180)
        put(icons[name + "_lv2"], x + 70, 180)
        label(x, 250, name + "  Lv1 / Lv2")
        x += 180
    label(780, 160, "Stall icons")
    x = 780
    for name in ["icon_reroll", "icon_luckycard", "icon_studynotes", "pocket_open", "pocket_locked", "keeper_face"]:
        put(shop[name], x, 180)
        x += 70
    put(shop["coin_star"], x, 196)

    # outfits on the player, every facing + the skill-use pose
    y = 290
    rows = [("Original look (for comparison)", None, "boy", None),
            ("Fiesta Best (boy: barong)", "outfit_fiesta", "boy", None),
            ("Fiesta Best (girl: Filipiniana)", "outfit_fiesta", "girl", None),
            ("Yellow Raincoat + Salakot (boy)", "outfit_raincoat", "boy", "hat_salakot"),
            ("Yellow Raincoat + Salakot (girl)", "outfit_raincoat", "girl", "hat_salakot")]
    for title, oid, who, hat in rows:
        label(20, y, title)
        side = next(o for o in maps["outfits"] if o["id"] == oid)[who] if oid else None
        x = 20
        for facing in ["down", "left", "up", "right"]:
            for kind, i in (("walk", 0), ("walk", 2), ("use", 2)):
                try:
                    fr = load_frame(who, kind, facing, i)
                except FileNotFoundError:
                    continue
                im = apply_outfit(fr, side) if side else fr
                if hat:
                    hk = "up" if facing == "up" else facing if facing in ("left", "right") else "down"
                    im = wear_hat(im, hats[(hat, hk)])
                else:
                    pad = Image.new("RGBA", (im.width + 24, im.height + 12), T)  # same box as a hat frame
                    pad.alpha_composite(im, (12, 12))
                    im = pad
                put(im, x, y + 14, 2)
                x += im.width * 2 + 2
            x += 14   # gap between facings
        y += 14 + 60 * 2 + 18
    label(20, y, "Facings left to right: down, left, up, right - each as walk 0, walk 2, skill-use pose.")

    sheet.save(os.path.join(HERE, "cosmetics_sample.png"))


# ══ The rest of the stall (approved sample 2026-11-17) ═══════════════════════

OUTFITS.update({
    # Elementary: white polo, green collar tie, green shorts / skirt (public-school green)
    "outfit_elem": {
        "boy": (PAPER, C["cool"][6], C["green"][4], C["green"][3], C["green"][2], C["green"][1], C["green"][3]),
        "girl": (PAPER, C["cool"][6], C["green"][4], C["green"][3], C["green"][2], C["green"][1], C["green"][3]),
    },
    # High School: school-blue shirt, red tie, charcoal slacks / skirt
    "outfit_hs": {
        "boy": (C["blue"][6], C["blue"][4], C["red"][3], C["red"][2], C["cool"][2], C["cool"][1], C["cool"][3]),
        "girl": (C["blue"][6], C["blue"][4], C["red"][3], C["red"][2], C["cool"][2], C["cool"][1], C["cool"][3]),
    },
    # College: maroon polo with gold trim, dark slacks / skirt
    "outfit_college": {
        "boy": (C["red"][3], C["red"][2], C["amber"][5], C["amber"][4], C["cool"][1], C["cool"][0], C["cool"][2]),
        "girl": (C["red"][3], C["red"][2], C["amber"][5], C["amber"][4], C["cool"][1], C["cool"][0], C["cool"][2]),
    },
    # Camisa de Chino: light collarless shirt, brown rolled trousers / skirt
    "outfit_farmer": {
        "boy": (C["warm"][7], C["warm"][5], C["brown"][4], C["brown"][3], C["brown"][3], C["brown"][2], C["brown"][4]),
        "girl": (C["warm"][7], C["warm"][5], C["brown"][4], C["brown"][3], C["brown"][3], C["brown"][2], C["brown"][4]),
    },
})


def _canvas(w, h):
    return Image.new("RGBA", (w, h), T)


def _outline(im):
    """1 px INK outline around the opaque shape (pixel-art style)."""
    w, h = im.size
    src = im.load()
    out = im.copy()
    o = out.load()
    for y in range(h):
        for x in range(w):
            if src[x, y][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3]:
                    o[x, y] = rgba(INK)
                    break
    return out


def hat_from(fn, w, h):
    """Draw a hat with fn(put) on an inner canvas, then outline + palette snap."""
    im = _canvas(w + 2, h + 2)
    px = im.load()

    def put(x, y, c):
        if 0 <= x < w and 0 <= y < h:
            px[x + 1, y + 1] = rgba(c)
    fn(put, w, h)
    return snap(_outline(im))


def _ellipse_rows(w, h, put, colour):
    cx, cy = (w - 1) / 2, (h - 1) / 2
    for y in range(h):
        for x in range(w):
            if ((x - cx) / (w / 2)) ** 2 + ((y - cy) / (h / 2)) ** 2 <= 1.0:
                put(x, y, colour(x, y))


def hardhat(side):
    def f(put, w, h):
        # dome
        def dome(x, y):
            if (x, y) in ((6, 1), (7, 1), (6, 2)):
                return C["amber"][7]          # shine
            if x == w // 2 and y < 5:
                return C["amber"][5]          # centre ridge
            return C["amber"][6] if x < w * 0.65 else C["amber"][4]
        for y in range(6):
            for x in range(3, w - 3):
                cx = (w - 1) / 2
                if ((x - cx) / ((w - 6) / 2)) ** 2 + ((y - 5.5) / 5.5) ** 2 <= 1.0:
                    put(x, y, dome(x, y))
        # brim (sticks out to the front = left in profile)
        lo, hi = (0, w - 3) if side else (1, w - 1)
        for x in range(lo, hi):
            put(x, 6, C["amber"][4])
    return hat_from(f, 20, 7)


def banig_hat(side):
    weave = [C["teal"][4], C["pink"][5], C["amber"][6], C["warm"][6]]

    def f(put, w, h):
        cx = (w - 1) / 2
        for y in range(4):            # crown, woven checks
            for x in range(int(cx - 5), int(cx + 6)):
                put(x, y, weave[((x // 2) + y) % 4] if y > 0 else C["warm"][5])
        for y in (4, 5):              # wide flat brim
            for x in range(w):
                put(x, y, weave[((x // 2) + y) % 4] if (x + y) % 3 else C["brown"][4])
    return hat_from(f, 22 if not side else 20, 6)


def sampaguita(side):
    def f(put, w, h):
        # a ring of leaves with white sampaguita buds, sitting on the crown of the head
        for x in range(w):
            y = 2 if 2 < x < w - 3 else 3
            put(x, y, C["green"][3])
            put(x, y + 1, C["green"][2])
        for i, x in enumerate(range(1, w - 1, 3)):
            y = 1 if 2 < x < w - 3 else 2
            put(x, y, PAPER)
            put(x + 1, y, PAPER)
            put(x, y + 1, C["amber"][6] if i % 2 == 0 else PAPER)
    return hat_from(f, 16, 5)


def cap(view):
    def f(put, w, h):
        cx = (w - 1) / 2
        for y in range(5):
            for x in range(2, w - 2):
                if ((x - cx) / ((w - 4) / 2)) ** 2 + ((y - 4.5) / 4.5) ** 2 <= 1.0:
                    put(x, y, C["blue"][4] if x < cx + 3 else C["blue"][3])
        put(int(cx), 0, C["blue"][2])   # button
        if view == "down":              # brim towards you: a lighter band with the patch
            for x in range(3, w - 3):
                put(x, 5, C["blue"][5])
            put(int(cx) - 1, 3, C["amber"][6]); put(int(cx), 3, C["amber"][6])
        elif view == "up":              # the back: strap gap
            put(int(cx), 4, C["cool"][5]); put(int(cx) + 1, 4, C["cool"][5])
        else:                           # profile: brim out to the front (left)
            for x in range(0, 5):
                put(x, 5, C["blue"][5])
    return hat_from(f, 16, 6)


def make_more_hats():
    out = {}
    sets = {
        "hat_hardhat": {"down": hardhat(False), "up": hardhat(False), "left": hardhat(True)},
        "hat_banig": {"down": banig_hat(False), "up": banig_hat(False), "left": banig_hat(True)},
        "hat_sampaguita": {"down": sampaguita(False), "up": sampaguita(False), "left": sampaguita(True)},
        "hat_cap": {"down": cap("down"), "up": cap("up"), "left": cap("left")},
    }
    for hid, v in sets.items():
        v["right"] = v["left"].transpose(Image.FLIP_LEFT_RIGHT)
        for d, im in v.items():
            im.save(os.path.join(COS, f"{hid}_{d}.png"))
            out[(hid, d)] = im
    return out


TRAILS.update({
    "trail_leaves": [[".g.", "gGg", "..G"], ["gg.", ".gG", "..."], [".G", "gg"]],
    "trail_sparkles": [[".y.", "ywy", ".y."], ["w"], ["y.y", ".y.", "y.y"]],
    "trail_bubbles": [[".bb.", "bwcb", "bccb", ".bb."], [".b.", "bwb", ".b."], ["bb", "bb"]],
})


# ── home add-ons: drawn at 16 px/unit, placed around the rebuilt home ─────────
FONT = {
    "S": ["###", "#..", "###", "..#", "###"], "A": [".#.", "#.#", "###", "#.#", "#.#"],
    "R": ["##.", "#.#", "##.", "#.#", "#.#"], "I": ["###", ".#.", ".#.", ".#.", "###"],
    "-": ["...", "...", "##.", "...", "..."],
}


def home_garden():
    w, h = 38, 16
    im = _canvas(w, h)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 6, w - 1, h - 1], fill=rgba(C["brown"][4]))             # wooden border
    d.rectangle([1, 7, w - 2, h - 2], fill=rgba(C["brown"][2]))             # soil
    px = im.load()
    for x in range(2, w - 2, 2):
        px[x, 10] = rgba(C["brown"][1]); px[x + 1, 13] = rgba(C["brown"][1])  # furrows
    for i, x in enumerate(range(3, w - 4, 5)):
        if i % 2 == 0:   # pechay: a fan of leaves
            for (dx, dy, c) in ((0, 0, 5), (1, -1, 5), (-1, -1, 4), (0, -2, 4), (2, -2, 3), (-2, -2, 3), (1, -3, 5), (-1, -3, 4)):
                px[x + dx, 9 + dy] = rgba(C["green"][c])
        else:            # tomato vine on a stick, red fruit
            for y in range(1, 10):
                px[x, y] = rgba(C["brown"][5])
            for (dx, dy) in ((1, 3), (-1, 5), (1, 7), (-1, 2)):
                px[x + dx, dy] = rgba(C["green"][4])
            for (dx, dy) in ((1, 4), (-1, 6)):
                px[x + dx, dy] = rgba(C["red"][4])
    return snap(_outline(im))


def home_capiz():
    w, h = 14, 16
    im = _canvas(w, h)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w - 1, h - 1], fill=rgba(C["brown"][3]))
    px = im.load()
    pearl = [C["warm"][7], C["pink"][7], C["cool"][7], C["warm"][6]]
    for gy in range(4):
        for gx in range(3):
            x0, y0 = 1 + gx * 4, 1 + gy * 4 - (1 if gy else 0)
            for y in range(y0, min(y0 + 3, h - 1)):
                for x in range(x0, x0 + 3):
                    px[x, y] = rgba(pearl[(gx + gy * 2 + (x + y) // 3) % 4])
            px[x0, y0] = rgba(PAPER)  # glint
    return snap(_outline(im))


def home_hammock():
    w, h = 44, 22
    im = _canvas(w, h)
    px = im.load()
    for y in range(2, h):                       # two posts
        for x in (1, 2, w - 3, w - 2):
            px[x, y] = rgba(C["brown"][4] if x in (1, w - 3) else C["brown"][3])
    stripes = [C["red"][4], C["amber"][6], C["teal"][4], C["amber"][6]]
    for x in range(3, w - 3):                   # the sagging woven bed
        t = (x - 3) / (w - 7)
        sag = int(round(7 * (1 - (2 * t - 1) ** 2)))
        for k in range(3):
            px[x, 6 + sag + k] = rgba(stripes[((x // 3) + k) % 4] if k < 2 else C["red"][2])
    for x in range(3, 7):                       # ropes to the posts
        px[x, 5 + (x - 3) // 2] = rgba(C["warm"][4])
        px[w - 1 - x, 5 + (x - 3) // 2] = rgba(C["warm"][4])
    return snap(_outline(im))


def home_sarisari():
    text = "SARI-SARI"
    w = 4 * len(text) + 3
    h = 11
    im = _canvas(w, h)
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w - 1, 8], fill=rgba(C["red"][4]))
    d.rectangle([0, 8, w - 1, 8], fill=rgba(C["red"][2]))
    px = im.load()
    x = 2
    for ch in text:
        for gy, row in enumerate(FONT[ch]):
            for gx, c in enumerate(row):
                if c == "#":
                    px[x + gx, 2 + gy] = rgba(PAPER)
        x += 4
    for hx in (3, w - 4):                       # hanging hooks
        px[hx, 9] = rgba(C["cool"][4]); px[hx, 10] = rgba(C["cool"][4])
    return snap(_outline(im))


def make_home_addons():
    out = {"home_garden": home_garden(), "home_capiz": home_capiz(),
           "home_hammock": home_hammock(), "home_sarisari": home_sarisari()}
    for k, im in out.items():
        im.save(os.path.join(COS, k + ".png"))
    return out


# ── stall tiles for every cosmetic (Resources/Shop/icon_<id>) ────────────────

def _fit(im, size=24):
    """Centre on a square canvas (no scaling: the UI scales it up, point-filtered)."""
    s = max(size, im.width, im.height)
    c = _canvas(s, s)
    c.alpha_composite(im, ((s - im.width) // 2, (s - im.height) // 2))
    return c


def make_style_tiles(maps, hats, homes):
    who = "boy"
    for o in maps["outfits"]:
        fr = load_frame(who, "walk", "down", 0)
        im = apply_outfit(fr, o[who])
        bbox = im.getbbox()
        _fit(im.crop(bbox), 0).save(os.path.join(SHOP, f"icon_{o['id']}.png"))
    for hid in {k[0] for k in hats}:
        _fit(hats[(hid, "down")], 24).save(os.path.join(SHOP, f"icon_{hid}.png"))
    for tid in ("trail_petals", "trail_leaves", "trail_sparkles", "trail_bubbles"):
        c = _canvas(16, 16)
        for i, (x, y) in enumerate(((2, 9), (7, 4), (11, 10))):
            bit = Image.open(os.path.join(COS, f"{tid}_{i}.png")).convert("RGBA")
            c.alpha_composite(bit, (x, y))
        c.save(os.path.join(SHOP, f"icon_{tid}.png"))
    cat = Image.open(os.path.join(RES, "Morning", "cat_sit.png")).convert("RGBA")
    _fit(cat.crop(cat.getbbox()), 0).save(os.path.join(SHOP, "icon_pet_cat.png"))
    for k, im in homes.items():
        _fit(im, 0).save(os.path.join(SHOP, f"icon_{k}.png"))


def full_sheet(maps, hats):
    """Every outfit x hat on both avatars, down/left/up/right walk 0 (review sheet)."""
    ids = [o["id"] for o in maps["outfits"]]
    hat_ids = sorted({k[0] for k in hats})
    sheet = Image.new("RGBA", (1700, 120 * (len(ids) + len(hat_ids)) + 40), rgba(C["warm"][6]))
    d = ImageDraw.Draw(sheet)
    y = 10
    for oid in ids + hat_ids:
        d.text((10, y), oid, fill=rgba(INK))
        x = 10
        for who in ("boy", "girl"):
            for facing in ("down", "left", "up", "right"):
                fr = load_frame(who, "walk", facing, 0)
                if oid in ids:
                    side = next(o for o in maps["outfits"] if o["id"] == oid)[who]
                    im = apply_outfit(fr, side)
                    pad = _canvas(im.width + 24, im.height + 12)
                    pad.alpha_composite(im, (12, 12))
                    im = pad
                else:
                    im = wear_hat(fr, hats[(oid, facing)])
                sheet.alpha_composite(im.resize((im.width * 2, im.height * 2), Image.NEAREST), (x, y + 12))
                x += im.width * 2 + 4
            x += 30
        y += 120
    sheet.save(os.path.join(HERE, "cosmetics_all.png"))


if __name__ == "__main__":
    sample_only = "--sample-only" in sys.argv
    icons = make_icons()
    shop = make_shop()
    maps = outfit_maps()
    hats = make_hats()
    make_trails()
    if sample_only:
        sample_sheet(icons, shop, maps, hats)
    else:
        hats.update(make_more_hats())
        homes = make_home_addons()
        make_style_tiles(maps, hats, homes)
        full_sheet(maps, hats)
    print("done")
