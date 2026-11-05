"""Pixel icons for the loop screens (main menu): white silhouettes for the menu buttons
(the menu tints them) and small coloured keepsake / Spark icons. 16x16 each.
Usage: python make_loop_icons.py   (writes next to this file)"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
T = (0, 0, 0, 0)
PAL = {
    '.': T,
    'w': (255, 255, 255, 255),
    'o': (0x2b, 0x1d, 0x24, 255),   # outline
    'y': (255, 214, 90, 255), 'Y': (214, 160, 50, 255),     # gold
    'r': (214, 70, 60, 255), 'R': (150, 40, 44, 255),       # red
    'p': (176, 140, 255, 255), 'P': (110, 80, 200, 255),    # violet
    'b': (110, 170, 230, 255), 'B': (60, 110, 170, 255),    # blue
    'c': (244, 232, 204, 255), 'C': (200, 180, 150, 255),   # cream
    'n': (150, 100, 64, 255), 'N': (98, 66, 44, 255),       # brown
    'g': (120, 190, 90, 255), 'G': (70, 130, 60, 255),      # green
    's': (180, 180, 190, 255), 'S': (120, 120, 132, 255),   # steel
    'k': (255, 150, 60, 255),                               # flame
}


RES = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Resources", "Loop"))
os.makedirs(RES, exist_ok=True)


def draw(name, rows):
    im = Image.new("RGBA", (16, 16), T)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            im.putpixel((x, y), PAL.get(ch, T))
    # keepsake icons are loaded at runtime (Resources); menu icons are referenced by the USS
    im.save(os.path.join(RES if name.startswith(("memento_", "stall_")) else HERE, name + ".png"))


# menu silhouettes (tinted by the menu style)
draw("icon_goback", [
    "................",
    "......wwwww.....",
    "....ww.....ww...",
    "...w.........w..",
    "..w...........w.",
    ".www..........w.",
    "wwwww.........w.",
    ".www....w.....w.",
    "..w.....w.....w.",
    "........w....w..",
    "........www.w...",
    "...w.......w....",
    "....ww...ww.....",
    "......www.......",
    "................",
    "................",
])

# keepsakes
draw("memento_compass", [
    "................",
    ".....oooooo.....",
    "....ossssssо....".replace("о", "o"),
    "...osccccccso...",
    "..osccccrcccso..",
    "..osccccrrccso..",
    "..osccccrrccso..",
    "..oscccoooccso..",
    "..osccсbbcccso..".replace("с", "c"),
    "..osccсbbcccso..".replace("с", "c"),
    "..osccccbcccso..",
    "...osccccccso...",
    "....ossssssо....".replace("о", "o"),
    ".....oooooo.....",
    "................",
    "................",
])
draw("memento_ribbon", [
    "................",
    "..ooo......ooo..",
    ".orrro....orrro.",
    ".orRrro..orrRro.",
    ".orRRrrooorRRro.",
    "..orRrrRRrrRro..",
    "...orrRrrRrro...",
    "....oorRRroo....",
    "....orRoorRo....",
    "...orRo..orRo...",
    "...orro..orro...",
    "..orRo....oRro..",
    "..ooo......ooo..",
    "................",
    "................",
    "................",
])
draw("memento_locket", [
    "......oooo......",
    ".....oy..yo.....",
    ".....oy..yo.....",
    "......oyyo......",
    "....oooyyooo....",
    "...oyyyyyyyyo...",
    "..oyyppppppyyo..",
    "..oyppPPPPppyo..",
    "..oyppPpPPppyo..",
    "..oyppPPPPppyo..",
    "..oyyppppppyyo..",
    "...oyyyyyyyyo...",
    "....oYYYYYYo....",
    ".....oooooo.....",
    "................",
    "................",
])
draw("memento_lantern", [
    "......oooo......",
    ".....oNNNNo.....",
    "....oooooooo....",
    "....onyyyyno....",
    "...onykkkkyno...",
    "...onykyykyno...",
    "...onykyykyno...",
    "...onykkkkyno...",
    "...onyyyyyyno...",
    "....oooooooo....",
    "....oNNNNNNo....",
    ".....oooooo.....",
    "................",
    "................",
    "................",
    "................",
])
draw("memento_sneakers", [
    "................",
    "................",
    "................",
    "....ooooo.......",
    "...obbbbbo......",
    "...obwbwbo......",
    "...obbbbbbooo...",
    "...obwbwbbbbbo..",
    "..obbbbbbbbbbbo.",
    "..obbbbbbBBBBbo.",
    "..occcccccccccо.".replace("о", "o"),
    "..oooooooooooo..",
    "................",
    "................",
    "................",
    "................",
])
draw("memento_whistle", [
    "................",
    "................",
    "..........oo....",
    ".........oyo....",
    "....oooooyyo....",
    "...osssssyyoooo.",
    "..osSSSSSsyyyyyo",
    ".osSssssSSsoooo.",
    ".osSsoosSSso....",
    ".osSsoosSSso....",
    ".osSssssSSo.....",
    "..osSSSSSo......",
    "...oooooo.......",
    "................",
    "................",
    "................",
])

# sign over the keepsake stall in town (shown in the world, so it's coloured)
draw("stall_sign", [
    "................",
    ".....oooooo.....",
    "....oo....oo....",
    "....o......o....",
    "..oooooooooooo..",
    "..oyyyyyyyyyyo..",
    "..oyoyyyyyyoyo..",
    "..oyyyyyyyyyyo..",
    "..oyyyyYYyyyyo..",
    "..oyyyYyyYyyyo..",
    "..oyyyyYYyyyyo..",
    "..oYyyyyyyyyYo..",
    "...oYYYYYYYYo...",
    "....oooooooo....",
    "................",
    "................",
])
print("done")
