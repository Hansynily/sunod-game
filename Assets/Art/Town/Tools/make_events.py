"""Town events, the plaza bulletin board and morning letters (pixel art, 16 px per unit),
written to Resources/TownEvents. Usage: python make_events.py <out_dir>

  event_mark_0/1   the gold-and-violet sparkle over whoever has today's event (2 frames)
  stage            a small wooden stage with a striped skirt (gathering events)
  bunting_0/1      a string of banderitas, two flap frames (fiesta; tiles left-right)
  pole             a bamboo pole the bunting hangs from
  board            the plaza bulletin board with pinned notes
  board_new        a small "!" note that sits on the board when there's news
  letter           an envelope with a red seal, left at the door in the morning
  confetti_0..3    single fiesta confetti bits
"""
from PIL import Image
import os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
os.makedirs(OUT, exist_ok=True)

T = (0, 0, 0, 0)
PAL = {
    '.': T,
    'o': (0x2b, 0x1d, 0x24, 255),   # outline (dark plum, like the town art)
    'y': (0xff, 0xd2, 0x3c, 255),   # gold
    'Y': (0xff, 0xf2, 0xa8, 255),   # gold highlight
    'v': (0x96, 0x6e, 0xff, 255),   # violet
    'V': (0xc8, 0xb4, 0xff, 255),   # violet light
    'w': (0x8a, 0x5a, 0x3c, 255),   # wood
    'W': (0xb0, 0x7a, 0x50, 255),   # wood light
    'd': (0x5e, 0x3b, 0x28, 255),   # wood dark
    'r': (0xd9, 0x3f, 0x3a, 255),   # red
    'R': (0xf2, 0x7b, 0x6e, 255),   # red light
    'b': (0x3f, 0x7f, 0xd9, 255),   # blue
    'g': (0x4f, 0xb0, 0x5a, 255),   # green
    'p': (0xf2, 0x9c, 0xc8, 255),   # pink
    'c': (0xf6, 0xf1, 0xe6, 255),   # paper cream
    'C': (0xd6, 0xcd, 0xbd, 255),   # paper shade
    'k': (0x6b, 0x5d, 0x55, 255),   # ink lines
    'm': (0xc9, 0xa2, 0x5e, 255),   # bamboo
    'M': (0xe3, 0xc4, 0x80, 255),   # bamboo light
    'l': (0x9b, 0x78, 0x50, 255),   # string
}


def draw(rows, name):
    h, w = len(rows), max(len(r) for r in rows)
    im = Image.new("RGBA", (w, h), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), PAL.get(ch, T))
    im.save(os.path.join(OUT, name + ".png"))
    return im


# ── event sparkle, 11 x 11, two frames (pulse) ───────────────────────────────
draw([
    ".....o.....",
    "....oyo....",
    "....oyo....",
    "...oyYyo...",
    ".ooyyYYyyo.",
    "oyYYYvYYYyo",
    ".ooyyYYyyo.",
    "...oyYyo...",
    "....oyo....",
    "....oyo....",
    ".....o.....",
], "event_mark_0")
draw([
    "...........",
    ".....o.....",
    "....oVo....",
    "....oVo....",
    "..ooVvVoo..",
    ".oVVvyvVVo.",
    "..ooVvVoo..",
    "....oVo....",
    "....oVo....",
    ".....o.....",
    "...........",
], "event_mark_1")

# ── stage, 48 x 20 (feet pivot = the bottom) ─────────────────────────────────
top = "o" + "W" * 46 + "o"
plank = "o" + "".join("wwwwwwwd"[(i % 8)] for i in range(46)) + "o"
skirt_a = "o" + "".join(("rrrr" if (i // 4) % 2 == 0 else "cccc")[i % 4] for i in range(46)) + "o"
skirt_b = "o" + "".join(("rrrr" if (i // 4) % 2 == 0 else "CCCC")[i % 4] for i in range(46)) + "o"
stage = ["o" * 48, top, top, plank, plank, "o" * 48]
stage += [skirt_a] * 6 + [skirt_b] * 5
stage += ["o" + "d" * 46 + "o", ".o" + "d" * 4 + "o" + "." * 34 + "o" + "d" * 4 + "o.", "." + "o" * 6 + "." * 34 + "o" * 6 + "."]
draw(stage, "stage")

# ── bunting: 32 px of string with 4 flags; two frames (wind) ────────────────
def bunting(flap):
    colors = "rybg"
    rows = [["." for _ in range(32)] for _ in range(9)]
    for x in range(32):
        rows[1 if x % 16 in range(4, 12) else 0][x] = "l"    # a little sag
    for f in range(4):
        x0 = f * 8 + 1
        c = colors[f]
        top_y = 1 if (x0 % 16) in range(4, 12) else 0
        shape = [6, 6, 4, 4, 2, 2] if not flap else [6, 5, 4, 3, 2, 1]
        for i, w in enumerate(shape):
            y = top_y + 1 + i
            x_start = x0 + (6 - w) // 2 + (1 if flap and i > 2 else 0)
            for dx in range(w):
                edge = dx == 0 or dx == w - 1 or i == len(shape) - 1
                rows[y][x_start + dx] = "o" if edge else c
    return ["".join(r) for r in rows]
draw(bunting(False), "bunting_0")
draw(bunting(True), "bunting_1")

# ── bamboo pole, 4 x 40 ──────────────────────────────────────────────────────
pole = []
for y in range(40):
    if y == 0:
        pole.append(".oo.")
    elif y % 9 == 0:
        pole.append("oddo")
    else:
        pole.append("omMo")
pole.append("oooo")
draw(pole, "pole")

# ── bulletin board, 26 x 30 ──────────────────────────────────────────────────
board = [
    ".oooooooooooooooooooooooo.",
    "oWWWWWWWWWWWWWWWWWWWWWWWWo",
    "owddddddddddddddddddddddwo",
    "owdccccccdddrrrdddcccccdwo",
    "owdckkkkcddrRRrdddckkkcdwo",
    "owdccccccddrrrrdddcccccdwo",
    "owdckkkccdddkddddckkkkcdwo",
    "owdccccccddddddddccccccdwo",
    "owdckkkkcddcccccdckkkccdwo",
    "owdccccccddckkkcdccccccdwo",
    "owddddddddccccccddddddddwo",
    "owdddccccdckkkkcddcccdddwo",
    "owdddcbbcdccccccddcpcdddwo",
    "owdddccccddddddddcccdddwwo",
    "owddddddddddddddddddddddwo",
    "oWWWWWWWWWWWWWWWWWWWWWWWWo",
    ".oooooooooooooooooooooooo.",
] + [".......owo........owo....."] * 12 + [".......ooo........ooo....."]
draw(board, "board")

draw([
    ".ooooooo.",
    "oyyyyyyyo",
    "oyyyrryyo",
    "oyyyrryyo",
    "oyyyrryyo",
    "oyyyyyyyo",
    "oyyyrryyo",
    "oYyyyyyyo",
    ".ooooooo.",
], "board_new")

# ── letter, 12 x 9 ───────────────────────────────────────────────────────────
draw([
    "oooooooooooo",
    "occCccccCcco",
    "ocCcCccCcCco",
    "occcCrrCccco",
    "occccRrccccc".replace("c", "c")[:-1] + "o",
    "occcccccccco",
    "oCccccccccCo",
    "oCCCCCCCCCCo",
    "oooooooooooo",
], "letter")

# ── confetti bits ────────────────────────────────────────────────────────────
for i, c in enumerate("rybp"):
    draw([c + c, c + c], f"confetti_{i}")

print("wrote", len([f for f in os.listdir(OUT) if f.endswith('.png')]), "pngs to", OUT)
