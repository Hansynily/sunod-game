"""Builds the town sprite set for the life-stage towns.

Crops buildings/props from the Super Retro Collection atlas and splits each into
  <name>_base.png  (bottom strip - drawn below the player, gets the collider)
  <name>_top.png   (everything above - drawn over the player so you can walk behind it)
Also generates small originals (quest markers, signboards, playground pieces) and
recolored NPC idle sheets from the Modern Tiles characters.

Run from the sunod-game folder:  python Assets/Art/Town/Tools/town_art.py
"""
import colorsys, json, os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # Assets/Art/Town
OUT = os.path.join(ROOT, "Sprites")
NPC_OUT = os.path.join(ROOT, "NPC")
ATLAS = "Assets/Gif/Super_Retro_Collection/Resources/Environments/original_atlas.png"
CHARS = "Assets/Art/Packs/Modern tiles_Free/Characters_free"
os.makedirs(OUT, exist_ok=True)
os.makedirs(NPC_OUT, exist_ok=True)

A = Image.open(ATLAS).convert("RGBA")
MAGENTA = (255, 0, 255)


def clean(im):
    """Magenta filler -> transparent, then trim to content."""
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if r > 240 and g < 20 and b > 240:
                px[x, y] = (0, 0, 0, 0)
    bb = im.getbbox()
    return im.crop(bb) if bb else im


# name: (x0, y0, x1, y1, base_px)  base_px = rows from the bottom that sit under the player
CROPS = {
    # houses (4-5 tiles wide, roof + wall row)
    "house_red":        (1296, 16, 1360, 64, 16),
    "house_red_window": (1360, 16, 1440, 64, 16),
    "house_peach":      (1440, 16, 1520, 64, 16),
    "house_peach2":     (1296, 64, 1360, 112, 16),
    "house_red2":       (1360, 64, 1440, 112, 16),
    "house_peach3":     (1440, 64, 1520, 112, 16),
    "house_slate":      (1296, 112, 1360, 160, 16),
    "house_red_tall":   (1360, 112, 1440, 176, 16),
    "house_peach_tall": (1440, 112, 1520, 176, 16),
    "shop_reddoor":     (1296, 160, 1360, 208, 16),
    "house_slate_tall": (1360, 176, 1440, 240, 16),
    "house_slate2":     (1440, 176, 1520, 224, 16),
    "house_slate3":     (1440, 224, 1520, 272, 16),
    # big public buildings
    "hall_small":       (1296, 384, 1360, 442, 22),
    "hall_small2":      (1296, 448, 1360, 506, 22),
    "hall_deck":        (1360, 386, 1440, 466, 30),
    "hall_grand":       (1440, 398, 1520, 482, 30),
    "cabin":            (1449, 341, 1509, 400, 18),
    # market stalls / tents
    "stall_green":      (1296, 276, 1346, 336, 18),
    "stall_orange":     (1360, 276, 1410, 336, 18),
    "stall_blue":       (1436, 275, 1490, 336, 18),
    "tent_red":         (1298, 517, 1375, 576, 16),
    "tent_green":       (1298, 581, 1375, 640, 16),
    "tent_blue":        (1298, 645, 1375, 704, 16),
    # trees / plants
    "tree_green":       (371, 18, 414, 63, 8),
    "tree_pine":        (419, 18, 459, 64, 8),
    "tree_round":       (705, 146, 751, 192, 8),
    "tree_pink":        (705, 194, 751, 240, 8),
    "tree_palm":        (352, 197, 389, 237, 8),
    "bush":             (304, 65, 320, 80, 15),
    "flower_red":       (292, 147, 302, 158, 11),
    "flower_white":     (307, 148, 317, 158, 10),
    "flower_blue":      (323, 147, 333, 158, 11),
    "mushroom":         (321, 162, 335, 174, 12),
    # ground flora (no collider, drawn under the player)
    "tuft1": (16, 59, 23, 64, 999), "tuft2": (82, 54, 89, 60, 999), "tuft3": (38, 65, 44, 70, 999),
    "tuft4": (102, 66, 109, 72, 999), "tuft5": (23, 102, 30, 107, 999), "tuft6": (88, 114, 95, 119, 999),
    "tuft7": (97, 131, 104, 137, 999), "tuft8": (130, 182, 137, 188, 999),
    "tuft_wide1": (133, 23, 141, 29, 999), "tuft_wide2": (18, 90, 26, 96, 999),
    "tuft_wide3": (53, 114, 61, 120, 999), "tuft_wide4": (53, 151, 61, 157, 999),
    "flowers_white": (275, 148, 285, 157, 999),
    "mush_green": (275, 161, 287, 173, 999), "mush_purple": (290, 160, 303, 175, 999),
    "mush_orange": (305, 162, 319, 175, 999), "mush_brown": (337, 162, 350, 174, 999),
    "bloom_orange": (272, 176, 287, 192, 999), "bloom_blue": (288, 176, 303, 192, 999),
    "bloom_pink": (304, 176, 319, 192, 999), "clover": (336, 177, 352, 192, 999),
    "tiny_flower1": (275, 194, 282, 200, 999), "tiny_flower2": (291, 194, 298, 200, 999),
    "tiny_flower3": (307, 194, 314, 200, 999),
    "rock1": (66, 338, 80, 351, 999), "rock2": (82, 340, 96, 351, 999), "rock3": (98, 337, 112, 351, 999),
    "rock_moss1": (66, 386, 80, 399, 999), "rock_moss2": (98, 385, 112, 399, 999),
    "pebble1": (82, 292, 89, 297, 999), "pebble2": (98, 290, 104, 295, 999),
    # ground variation patches (drawn flat under everything)
    "grass_dark1":      (17, 69, 48, 103, 999),
    "grass_dark2":      (52, 70, 79, 93, 999),
    "grass_dark3":      (49, 97, 78, 112, 999),
    "grass_light1":     (66, 113, 94, 137, 999),
    "grass_light2":     (20, 114, 60, 143, 999),
    "grass_light3":     (102, 115, 140, 137, 999),
    # street props
    "lamp_blue":        (1728, 370, 1744, 398, 5),
    "lamp_red":         (1747, 370, 1758, 398, 5),
    "lamp_white":       (1762, 370, 1774, 398, 5),
    "plant_pot":        (1745, 298, 1759, 320, 6),
    "plant_pot2":       (1761, 298, 1775, 320, 6),
    "bench":            (1556, 459, 1581, 478, 19),
    "bench_long":       (1650, 482, 1677, 494, 12),
    "crates":           (1554, 482, 1584, 511, 29),
    "fence_h":          (656, 48, 672, 80, 32),
    "fence_v":          (672, 64, 688, 80, 16),
}

meta = {}


def save_split(name, im, base_px):
    im = im.copy()
    w, h = im.size
    base_px = min(base_px, h)
    base = im.crop((0, h - base_px, w, h))
    base.save(os.path.join(OUT, f"{name}_base.png"))
    entry = {"w": w, "h": h, "base": base_px}
    if base_px < h:
        im.crop((0, 0, w, h - base_px)).save(os.path.join(OUT, f"{name}_top.png"))
        entry["top"] = True
    meta[name] = entry


for name, (x0, y0, x1, y1, base_px) in CROPS.items():
    save_split(name, clean(A.crop((x0, y0, x1, y1))), base_px)

# ---------------------------------------------------------------- tiny pixel font
FONT = {
    "A": ["010", "101", "111", "101", "101"], "B": ["110", "101", "110", "101", "110"],
    "C": ["011", "100", "100", "100", "011"], "D": ["110", "101", "101", "101", "110"],
    "E": ["111", "100", "110", "100", "111"], "F": ["111", "100", "110", "100", "100"],
    "G": ["011", "100", "101", "101", "011"], "H": ["101", "101", "111", "101", "101"],
    "I": ["111", "010", "010", "010", "111"], "J": ["001", "001", "001", "101", "010"],
    "K": ["101", "101", "110", "101", "101"], "L": ["100", "100", "100", "100", "111"],
    "M": ["10001", "11011", "10101", "10001", "10001"], "N": ["1001", "1101", "1011", "1001", "1001"],
    "O": ["010", "101", "101", "101", "010"], "P": ["110", "101", "110", "100", "100"],
    "Q": ["010", "101", "101", "110", "011"], "R": ["110", "101", "110", "101", "101"],
    "S": ["011", "100", "010", "001", "110"], "T": ["111", "010", "010", "010", "010"],
    "U": ["101", "101", "101", "101", "111"], "V": ["101", "101", "101", "101", "010"],
    "W": ["10001", "10001", "10101", "11011", "10001"], "X": ["101", "101", "010", "101", "101"],
    "Y": ["101", "101", "010", "010", "010"], "Z": ["111", "001", "010", "100", "111"],
    " ": ["0", "0", "0", "0", "0"], "'": ["1", "1", "0", "0", "0"], "&": ["0110", "1000", "0101", "1010", "0101"],
}


def text_width(t):
    return sum(len(FONT[c][0]) + 1 for c in t) - 1


def draw_text(d, x, y, t, col):
    for c in t:
        g = FONT[c]
        for gy, row in enumerate(g):
            for gx, bit in enumerate(row):
                if bit == "1":
                    d.point((x + gx, y + gy), fill=col)
        x += len(g[0]) + 1


def signboard(name, text):
    tw = text_width(text)
    w, h = tw + 8, 11
    im = Image.new("RGBA", (w, h + 6), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([2, h, 3, h + 5], fill=(92, 58, 38, 255))           # posts
    d.rectangle([w - 4, h, w - 3, h + 5], fill=(92, 58, 38, 255))
    d.rectangle([0, 0, w - 1, h - 1], fill=(72, 44, 30, 255))        # outline
    d.rectangle([1, 1, w - 2, h - 2], fill=(196, 140, 86, 255))      # board
    d.line([1, h - 2, w - 2, h - 2], fill=(150, 98, 58, 255))
    draw_text(d, 4, 3, text, (60, 34, 22, 255))
    save_split(name, im, 6)


SIGNS = {
    "sign_school": "SCHOOL", "sign_toys": "TOYS", "sign_canteen": "CANTEEN", "sign_park": "PARK",
    "sign_fair": "SCHOOL FAIR", "sign_garden": "GARDEN", "sign_market": "MARKET", "sign_barber": "BARBER",
    "sign_thrift": "THRIFT SHOP", "sign_hs": "HIGH SCHOOL", "sign_hall": "TOWN HALL", "sign_radio": "RADIO CLUB",
    "sign_campus": "COLLEGE", "sign_lab": "LAB", "sign_hotel": "HOTEL", "sign_bank": "BANK",
    "sign_port": "PORT", "sign_cafe": "NET CAFE", "sign_office": "OFFICE", "sign_clinic": "CLINIC",
    "sign_workshop": "WORKSHOP", "sign_realty": "HOUSES FOR SALE", "sign_paper": "CAMPUS PAPER",
    "sign_home": "HOME", "sign_studio": "FILM STUDIO", "sign_choir": "CHOIR", "sign_river": "RIVER",
}
for n, t in SIGNS.items():
    signboard(n, t)


# ---------------------------------------------------------------- generated props
def outline_rect(d, box, fill, edge):
    d.rectangle(box, fill=edge)
    d.rectangle([box[0] + 1, box[1] + 1, box[2] - 1, box[3] - 1], fill=fill)


def gen_swing():
    im = Image.new("RGBA", (32, 26), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    wood, dark = (176, 112, 64, 255), (84, 50, 30, 255)
    for x in (2, 28):
        d.line([x, 25, x + 1, 2], fill=dark, width=3); d.line([x, 25, x + 1, 2], fill=wood, width=1)
    d.rectangle([2, 1, 30, 4], fill=dark); d.rectangle([3, 2, 29, 3], fill=wood)
    for sx in (9, 19):
        d.line([sx, 4, sx, 17], fill=(120, 120, 130, 255)); d.line([sx + 4, 4, sx + 4, 17], fill=(120, 120, 130, 255))
        d.rectangle([sx - 1, 17, sx + 5, 19], fill=(200, 60, 50, 255))
    save_split("swing", im, 5)


def gen_slide():
    im = Image.new("RGBA", (30, 26), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([2, 4, 4, 25], fill=(90, 90, 100, 255)); d.rectangle([10, 4, 12, 25], fill=(90, 90, 100, 255))
    for y in range(8, 25, 4):
        d.line([2, y, 12, y], fill=(150, 150, 160, 255))
    d.rectangle([1, 2, 13, 5], fill=(60, 120, 200, 255))
    d.polygon([(13, 3), (28, 22), (28, 25), (13, 7)], fill=(240, 190, 40, 255), outline=(150, 100, 20, 255))
    save_split("slide", im, 5)


def gen_flagpole():
    im = Image.new("RGBA", (14, 34), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rectangle([2, 1, 3, 31], fill=(200, 200, 210, 255)); d.rectangle([0, 31, 5, 33], fill=(110, 110, 120, 255))
    d.rectangle([4, 2, 13, 4], fill=(40, 70, 180, 255)); d.rectangle([4, 5, 13, 7], fill=(210, 40, 40, 255))
    d.polygon([(4, 2), (8, 4), (4, 7)], fill=(250, 250, 250, 255)); d.point((5, 4), fill=(250, 210, 40, 255))
    save_split("flagpole", im, 3)


def gen_drumstage():
    im = Image.new("RGBA", (40, 22), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    outline_rect(d, [0, 10, 39, 21], (176, 112, 64, 255), (84, 50, 30, 255))
    for x in range(4, 38, 6):
        d.line([x, 11, x, 20], fill=(150, 92, 52, 255))
    d.ellipse([14, 0, 26, 12], fill=(200, 50, 50, 255), outline=(90, 20, 20, 255))
    d.ellipse([16, 1, 24, 5], fill=(240, 230, 210, 255))
    save_split("drum_stage", im, 12)


def gen_garden_bed():
    im = Image.new("RGBA", (32, 16), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    outline_rect(d, [0, 2, 31, 15], (120, 76, 46, 255), (70, 40, 24, 255))
    for x in range(4, 30, 6):
        d.line([x, 4, x, 12], fill=(60, 150, 60, 255)); d.point((x - 1, 6), fill=(90, 190, 80, 255)); d.point((x + 1, 8), fill=(90, 190, 80, 255))
    save_split("garden_bed", im, 16)


def gen_dock():
    im = Image.new("RGBA", (48, 16), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    outline_rect(d, [0, 2, 47, 13], (170, 120, 76, 255), (84, 50, 30, 255))
    for x in range(6, 46, 6):
        d.line([x, 3, x, 12], fill=(130, 88, 54, 255))
    for x in (2, 44):
        d.rectangle([x, 12, x + 2, 15], fill=(84, 50, 30, 255))
    save_split("dock", im, 16)


def gen_marker(name, glyph, fill, edge):
    im = Image.new("RGBA", (11, 15), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, 10, 11], radius=3, fill=edge)
    d.rounded_rectangle([1, 1, 9, 10], radius=2, fill=fill)
    d.polygon([(3, 11), (7, 11), (5, 14)], fill=edge)
    if glyph == "!":
        d.rectangle([4, 2, 6, 6], fill=edge); d.rectangle([4, 8, 6, 9], fill=edge)
    elif glyph == "check":
        d.line([2, 5, 4, 8, 8, 2], fill=edge, width=2)
    elif glyph == "dots":
        for x in (2, 5, 8):
            d.point((x, 6), fill=edge); d.point((x, 5), fill=edge)
    im.save(os.path.join(OUT, f"{name}.png"))
    meta[name] = {"w": 11, "h": 15, "base": 15, "marker": True}


gen_swing(); gen_slide(); gen_flagpole(); gen_drumstage(); gen_garden_bed(); gen_dock()
gen_marker("marker_quest", "!", (255, 214, 64, 255), (90, 50, 10, 255))
gen_marker("marker_done", "check", (140, 220, 120, 255), (30, 80, 30, 255))
gen_marker("marker_soon", "dots", (200, 200, 200, 255), (90, 90, 90, 255))

# ---------------------------------------------------------------- NPC recolors
def hue_shift(im, shift, sat=1.0, keep_skin=True):
    im = im.copy(); px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
            skin = 0.02 < h < 0.11 and s > 0.25 and l > 0.45
            if keep_skin and skin:
                continue
            if s < 0.08:   # outlines / greys stay put
                continue
            h = (h + shift) % 1.0
            s = max(0.0, min(1.0, s * sat))
            r2, g2, b2 = colorsys.hls_to_rgb(h, l, s)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return im


npcs = []
VARIANTS = [(0.0, 1.0), (0.33, 1.1), (0.6, 1.0), (0.85, 0.9)]
for cname in ("Adam", "Alex", "Amelia", "Bob"):
    sheet = Image.open(os.path.join(CHARS, f"{cname}_idle_anim_16x16.png")).convert("RGBA")
    down = sheet.crop((18 * 16, 0, 24 * 16, 32))   # frames 18-23 face the camera
    for vi, (shift, sat) in enumerate(VARIANTS):
        out = hue_shift(down, shift, sat) if vi else down
        key = f"npc_{cname.lower()}_{vi}"
        for f in range(6):   # one file per frame -> simple Single-sprite import
            out.crop((f * 16, 0, f * 16 + 16, 32)).save(os.path.join(NPC_OUT, f"{key}_f{f}.png"))
        npcs.append(key)

with open(os.path.join(ROOT, "town_sprites.json"), "w") as f:
    json.dump({"sprites": meta, "npcs": npcs}, f, indent=1)
print(len(meta), "sprites,", len(npcs), "npc sheets")
