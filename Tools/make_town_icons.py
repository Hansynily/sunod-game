"""Small pixel-art icons for the town (11x15 speech bubbles matching marker_*.png, plus bits).
Run from the sunod-game folder:  python Tools/make_town_icons.py"""
import os
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets/Resources/TownLife")
BUBBLE = [
    "..OOOOOOO..",
    ".OFFFFFFFO.",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    "OFFFFFFFFFO",
    ".OFFFFFFFO.",
    "..OOOOOOO..",
    "....OOO....",
    "....OOO....",
    ".....O.....",
]
# glyphs drawn inside the 9x9 interior (rows 1..9, cols 1..9) with 'G'
GLYPHS = {
    # not completed (failed / turned down): a soft "x"
    "missed": [".........", ".........", "..G...G..", "...G.G...", "....G....", "...G.G...", "..G...G..", ".........", "........."],
    "heart":  [".........", ".........", "..GG.GG..", ".GGGGGGG.", ".GGGGGGG.", "..GGGGG..", "...GGG...", "....G....", "........."],
    "note":   [".........", "....GGGG.", "....G..G.", "....G..G.", "....G..G.", "..GGG.GG.", ".GGGG.GG.", "..GG.....", "........."],
    "hi":     [".........", ".........", ".G..G..G.", ".G..G....", ".GGGG..G.", ".G..G..G.", ".G..G..G.", ".........", "........."],
    "dots":   [".........", ".........", ".........", ".........", ".G..G..G.", ".........", ".........", ".........", "........."],
    "sun":    [".........", "....G....", ".G..G..G.", "..GGGGG..", "GGGGGGGGG", "..GGGGG..", ".G..G..G.", "....G....", "........."],
}
STYLE = {  # outline, fill, glyph
    "missed": ((120, 60, 40), (250, 196, 160), (170, 70, 50)),
    "heart":  ((110, 40, 60), (255, 236, 240), (230, 70, 100)),
    "note":   ((60, 50, 100), (240, 236, 255), (100, 80, 190)),
    "hi":     ((90, 60, 20), (255, 246, 220), (150, 90, 30)),
    "dots":   ((80, 80, 80), (245, 245, 245), (110, 110, 110)),
    "sun":    ((140, 90, 10), (255, 248, 220), (240, 170, 30)),
}


def bubble(name):
    o, f, g = STYLE[name]
    im = Image.new("RGBA", (11, 15), (0, 0, 0, 0))
    for y, row in enumerate(BUBBLE):
        for x, c in enumerate(row):
            if c == "O": im.putpixel((x, y), o + (255,))
            elif c == "F": im.putpixel((x, y), f + (255,))
    for y, row in enumerate(GLYPHS[name]):
        for x, c in enumerate(row):
            if c == "G": im.putpixel((x + 1, y + 1), g + (255,))
    return im


def sprite(rows, palette, name):
    h, w = len(rows), len(rows[0])
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c in palette: im.putpixel((x, y), palette[c])
    im.save(os.path.join(OUT, name + ".png"))


os.makedirs(OUT, exist_ok=True)
for n in GLYPHS:
    bubble(n).save(os.path.join(OUT, f"emote_{n}.png"))

# a ball for the field games, a picnic mat, a kite and a bird (2 frames)
sprite([".OOO.", "OWWRO", "OWRRO", "ORRWO", ".OOO."],
       {"O": (60, 40, 40, 255), "W": (250, 250, 250, 255), "R": (220, 60, 60, 255)}, "ball")
sprite(["RRWWRRWWRRWWRRWWRRWW", "RRWWRRWWRRWWRRWWRRWW", "WWRRWWRRWWRRWWRRWWRR", "WWRRWWRRWWRRWWRRWWRR",
        "RRWWRRWWRRWWRRWWRRWW", "RRWWRRWWRRWWRRWWRRWW", "WWRRWWRRWWRRWWRRWWRR", "WWRRWWRRWWRRWWRRWWRR",
        "RRWWRRWWRRWWRRWWRRWW", "RRWWRRWWRRWWRRWWRRWW", "WWRRWWRRWWRRWWRRWWRR", "WWRRWWRRWWRRWWRRWWRR"],
       {"R": (214, 80, 80, 255), "W": (246, 236, 220, 255)}, "picnic_mat")
sprite(["....O....", "...OYO...", "..OYYYO..", ".OYYRYYO.", "OYYRRRYYO", ".OYYRYYO.", "..OYYYO..", "...OYO...", "....O....",
        "....T....", "...T.....", "....T....", ".....T...", "....T...."],
       {"O": (90, 40, 30, 255), "Y": (255, 210, 70, 255), "R": (230, 80, 60, 255), "T": (240, 240, 240, 255)}, "kite")
sprite(["O.....O", ".O...O.", "..OOO..", "...O..."], {"O": (50, 45, 60, 255)}, "bird_0")
sprite([".......", "OOOOOOO", "..OOO..", "...O..."], {"O": (50, 45, 60, 255)}, "bird_1")
print("icons written to", OUT)

# tutorial pointer: a chunky down arrow (cream with brown outline + gold shade)
POINTER = [
    "....OOOOOO....",
    "....OWWWYO....",
    "....OWWWYO....",
    "....OWWWYO....",
    "....OWWWYO....",
    "....OWWWYO....",
    "OOOOOWWWYOOOOO",
    "OWWWWWWWYYYYYO",
    ".OWWWWWWYYYYO.",
    "..OWWWWWYYYO..",
    "...OWWWWYYO...",
    "....OWWWYO....",
    ".....OWYO.....",
    "......OO......",
]
pal = {"O": (74, 44, 26, 255), "W": (255, 246, 214, 255), "Y": (240, 184, 60, 255)}
ptr_out = os.path.join(os.path.dirname(OUT), "UIPointer")
os.makedirs(ptr_out, exist_ok=True)
im = Image.new("RGBA", (len(POINTER[0]), len(POINTER)), (0, 0, 0, 0))
for y, row in enumerate(POINTER):
    for x, c in enumerate(row):
        if c in pal: im.putpixel((x, y), pal[c])
im.save(os.path.join(ptr_out, "pointer_arrow.png"))
