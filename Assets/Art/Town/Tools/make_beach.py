"""Beach props for the town builder (pixel art, 16 px per unit), written next to the other
town sprites as <name>_base.png (+ <name>_top.png for parts drawn over the player).
Usage: python make_beach.py <Assets/Art/Town/Sprites>

  umbrella_red / umbrella_blue   striped beach umbrella (top = canopy, base = pole + shadow)
  towel_red / towel_blue         beach towel lying on the sand (walk-over)
  sandcastle                     a little sandcastle with a flag
  shell1 / shell2 / starfish     tiny shore decor (walk-over)
  boat                           a small banca (outrigger) that sits on the water
  beach_hut                      a nipa-roof snack hut
"""
from PIL import Image
import os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(OUT, exist_ok=True)

T = (0, 0, 0, 0)
PAL = {
    '.': T,
    'o': (0x2b, 0x1d, 0x24, 255),   # outline
    'r': (0xd9, 0x3f, 0x3a, 255), 'R': (0xf2, 0x7b, 0x6e, 255),
    'b': (0x3f, 0x7f, 0xd9, 255), 'B': (0x8c, 0xb8, 0xf2, 255),
    'w': (0xf6, 0xf1, 0xe6, 255), 'W': (0xd6, 0xcd, 0xbd, 255),
    'y': (0xff, 0xd2, 0x3c, 255),
    's': (0xe8, 0xc9, 0x8a, 255), 'S': (0xc9, 0xa2, 0x5e, 255), 'd': (0xa8, 0x82, 0x48, 255),   # sand tones
    'p': (0xf2, 0x9c, 0xa8, 255), 'P': (0xe0, 0x70, 0x84, 255),
    'g': (0x8a, 0x5a, 0x3c, 255), 'G': (0xb0, 0x7a, 0x50, 255), 'k': (0x5e, 0x3b, 0x28, 255),   # wood
    'n': (0xc9, 0xa2, 0x4e, 255), 'N': (0xe3, 0xc4, 0x70, 255),   # nipa straw
    'x': (0x00, 0x00, 0x00, 60),    # soft shadow
    'c': (0x4f, 0xb0, 0x5a, 255),
}


def img(rows):
    h, w = len(rows), max(len(r) for r in rows)
    im = Image.new("RGBA", (w, h), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), PAL.get(ch, T))
    return im


def save(name, base, top=None):
    img(base).save(os.path.join(OUT, name + "_base.png"))
    if top is not None:
        img(top).save(os.path.join(OUT, name + "_top.png"))


# ── umbrella: canopy 28 x 12 (top), pole + shadow 28 x 14 (base) ────────────
def umbrella(c, C):
    top = [
        "...........oooooo...........",
        "........ooo" + c * 2 + "ww" + c * 2 + "ooo........",
        "......oo" + c * 3 + "ww" + c * 2 + "ww" + c * 3 + "oo......",
        "....oo" + c * 4 + "www" + c * 2 + "www" + c * 4 + "oo....",
        "...o" + c * 5 + "wwww" + c * 2 + "wwww" + c * 5 + "o...",
        "..o" + c * 5 + "wwwww" + c * 2 + "wwwww" + c * 4 + "o..",
        ".o" + c * 6 + "wwwww" + c * 2 + "wwwww" + c * 5 + "o.",
        ".o" + C * 6 + "WWWWW" + C * 2 + "WWWWW" + C * 5 + "o.",
        "o" + "o" * 26 + "o",
        "..............o.............",
        "..............o.............",
        "..............o.............",
    ]
    base = ["..............o............."] * 8 + [
        "...........xxxoxxx..........",
        ".........xxxxxoxxxxx........",
        "........xxxxxxxxxxxxx.......",
        "........xxxxxxxxxxxxx.......",
        ".........xxxxxxxxxxx........",
        "...........xxxxxxx..........",
    ]
    return base, [r[:28].ljust(28, '.') for r in top]


b, t = umbrella('r', 'R'); save("umbrella_red", b, t)
b, t = umbrella('b', 'B'); save("umbrella_blue", b, t)


# ── towels 12 x 7 ────────────────────────────────────────────────────────────
def towel(c, C):
    return [
        "oooooooooooo",
        "o" + c + C + c + C + c + C + c + C + c + C + "o",
        "o" + "w" * 10 + "o",
        "o" + c * 10 + "o",
        "o" + "w" * 10 + "o",
        "o" + C + c + C + c + C + c + C + c + C + c + "o",
        "oooooooooooo",
    ]
save("towel_red", towel('r', 'R'))
save("towel_blue", towel('b', 'B'))

# ── sandcastle 14 x 12 ───────────────────────────────────────────────────────
save("sandcastle", [
    "......o.......",
    "......oyy.....",
    "......oy......",
    "......o.......",
    ".....oSo......",
    "..o.osSso.o...",
    ".oSooSsSoosSo.",
    ".oSsSssssSssSo",
    ".osssskksssso.",
    "osssssKkssssso".replace("K", "k"),
    "oSSSSSSSSSSSSo",
    ".oooooooooooo.",
])

# ── tiny decor ───────────────────────────────────────────────────────────────
save("shell1", [".ooo.", "opPpo", "oPpPo", ".ooo."])
save("shell2", ["..o..", ".owo.", "owWwo", ".ooo."])
save("starfish", ["..o..", "..oyo", "oyyyo", ".oyo.", "o.o.o"])

# ── banca (outrigger boat) 36 x 12, sits on the water ────────────────────────
save("boat", [
    "......................o.............",
    ".....................owo............",
    "....................owwwo...........",
    "...................owwwwwo..........",
    "....................oogoo...........",
    "o..............oooooogooooo.......o.",
    "oo...........ooGGGGGGGGGGGGGoo...oo.",
    ".ooooooooooooobbbbbbbbbbbbbbbboooo..",
    "..o.........obbbbbbbbbbbbbbbbbo..o..",
    "..oooooooooooowwwwwwwwwwwwwwwoooooo.",
    "...............ooooooooooooo........",
    "....................................",
])

# ── nipa hut 48 x 32: roof (top) over a bamboo stall (base) ──────────────────
roof = []
for y in range(14):
    pad = max(0, 13 - y)
    inner = 48 - 2 * pad
    if inner <= 2:
        roof.append("." * 48); continue
    fill = "".join(("N" if (x + y) % 4 == 0 else "n") for x in range(inner - 2))
    row = "." * pad + "o" + fill + "o" + "." * pad
    roof.append(row[:48].ljust(48, "."))
roof.append("o" * 48)
base = []
for y in range(17):
    if y in (0, 1):
        base.append(".o" + "G" * 44 + "o.")
    elif y < 10:
        base.append(".og" + "".join(("k" if x % 9 == 0 else ".") for x in range(42)) + "go.")
    elif y == 10:
        base.append(".o" + "G" * 44 + "o.")
    elif y < 15:
        base.append(".og" + "g" * 42 + "go.")
    else:
        base.append(".oooooooooooooooooooooooooooooooooooooooooooooo.")
save("beach_hut", base, roof)

# ── shore foam, 16 x 6: a white wave line at the top of the first sea tile (tiles left-right) ─
F = {'.': T, 'f': (0xf6, 0xfb, 0xff, 255), 'F': (0xc8, 0xe6, 0xf5, 255), 'l': (0x7f, 0xc4, 0xe0, 255)}
for i, rows in enumerate([
    ["ff....ffff....ff", "FfffFFffffFFfffF", ".FFlll.FF.lllFF.", "..l........l....", "................", "................"],
    ["..ffff....ffff..", "fFFffffFFffffFFf", "Fl.FFlll.FFlll.F", ".....l.......l..", "................", "................"],
]):
    im = Image.new("RGBA", (16, 6), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), F.get(ch, T))
    im.save(os.path.join(OUT, f"foam{i}_base.png"))

# ── bridge / pier deck, 16 x 16 tiles: plank_h = boards across (walk up/down), plank_v = boards up/down (walk left/right) ─
P = {'o': (0x5e, 0x3b, 0x28, 255), 'a': (0xb0, 0x7a, 0x50, 255), 'b': (0x9a, 0x68, 0x42, 255), 'n': (0x3a, 0x26, 0x1c, 255)}
def plank(horizontal):
    im = Image.new("RGBA", (16, 16), T)
    for y in range(16):
        for x in range(16):
            u, v = (y, x) if horizontal else (x, y)       # u: across the boards, v: along them
            board = u // 4
            if u % 4 == 3:
                c = P['o']                                  # gap between boards
            else:
                c = P['a'] if (board % 2 == 0) else P['b']
                if u % 4 == 0 and v % 8 != 7: c = (min(c[0] + 18, 255), min(c[1] + 14, 255), min(c[2] + 10, 255), 255)
                if v % 16 in (2, 13) and u % 4 == 1: c = P['n']   # nail heads
            im.putpixel((x, y), c)
    return im
plank(True).save(os.path.join(OUT, "plank_h_base.png"))
plank(False).save(os.path.join(OUT, "plank_v_base.png"))

print("beach props written to", OUT)
