"""
SUNOD original pixel-art UI panels (9-slice) + quest stars.

Generates small native-resolution PNGs next to this script and a contact-sheet
preview (_preview.png). Palette is sampled from the old Sprout Lands sprites so the
look stays consistent, but every shape here is drawn from scratch.

Run:  python make_panels.py
Borders (L, B, R, T) are written to panel_import.json - keep them in sync with
SunodTheme.tss (-unity-slice-left/bottom/right/top).
"""
import json
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- palette
T = (0, 0, 0, 0)
CREAM_HI = (243, 229, 194, 255)   # highlight
CREAM = (232, 207, 166, 255)      # main cream fill
TAN = (220, 185, 138, 255)        # wood fill / pressed face
TAN_DK = (196, 154, 108, 255)     # lip / bottom edge / grain
BROWN = (170, 121, 89, 255)       # deep lip / inner shadow
OUTLINE = (144, 98, 93, 255)      # outline (warm purple-brown)
INK = (72, 52, 34, 255)           # dark text brown
INK_DK = (46, 34, 24, 255)        # darkest brown
SHADOW = (72, 52, 34, 80)         # soft drop shadow

# dusky purple-brown overlay panel
DUSK_HI = (128, 102, 116, 255)
DUSK = (100, 78, 92, 255)
DUSK_DK = (78, 58, 72, 255)
DUSK_LINE = (46, 32, 40, 255)

# stars
GOLD_HI = (255, 240, 150, 255)
GOLD = (255, 204, 66, 255)
GOLD_SH = (234, 138, 46, 255)
STAR_LINE = INK
DIM_HI = (220, 196, 160, 255)
DIM = (196, 168, 132, 255)
DIM_SH = (170, 140, 110, 255)
GLOW = (255, 226, 110)


# ---------------------------------------------------------------- helpers
def new(w, h):
    return Image.new("RGBA", (w, h), T)


def rrect(img, x0, y0, x1, y1, col, chamfer):
    """Filled rect (inclusive coords) with pixel-chamfered corners.
    chamfer 2 cuts a 3-pixel L on each corner, chamfer 1 cuts the single corner pixel."""
    px = img.load()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            dx = min(x - x0, x1 - x)
            dy = min(y - y0, y1 - y)
            if dx + dy < chamfer:
                continue
            px[x, y] = col


def hline(img, x0, x1, y, col):
    px = img.load()
    for x in range(x0, x1 + 1):
        if px[x, y][3] > 0:
            px[x, y] = col


def vline(img, x, y0, y1, col):
    px = img.load()
    for y in range(y0, y1 + 1):
        if px[x, y][3] > 0:
            px[x, y] = col


def raised_body(img, x0, y0, x1, y1, face, hi, lip, lip_dk, line, lip_h, shadow=True):
    """Outlined chamfered block: top/left highlight, darker bottom lip of lip_h rows."""
    if shadow:
        rrect(img, x0 + 1, y0 + 1, x1, y1 + 1, SHADOW, 2)
    rrect(img, x0, y0, x1, y1, line, 2)
    rrect(img, x0 + 1, y0 + 1, x1 - 1, y1 - 1, face, 1)
    # lip (bottom depth)
    for i in range(lip_h):
        y = y1 - 1 - i
        col = lip_dk if (i == 0 and lip_h > 1) else lip
        hline(img, x0 + 1, x1 - 1, y, col)
    # top + left highlight (inside outline)
    hline(img, x0 + 2, x1 - 2, y0 + 1, hi)
    vline(img, x0 + 1, y0 + 2, y1 - 1 - lip_h, hi)
    # re-cut the inner corners so they stay round
    px = img.load()
    for (cx, cy) in ((x0 + 1, y0 + 1), (x1 - 1, y0 + 1), (x0 + 1, y1 - 1), (x1 - 1, y1 - 1)):
        px[cx, cy] = line


# ---------------------------------------------------------------- panels
def panel_cream():
    w, h = 24, 24
    img = new(w, h)
    raised_body(img, 0, 0, w - 2, h - 2, CREAM, CREAM_HI, TAN_DK, TAN_DK, OUTLINE, 2)
    return img, (6, 8, 6, 6)


def panel_wood():
    w, h = 32, 32
    img = new(w, h)
    raised_body(img, 0, 0, w - 2, h - 2, TAN, CREAM, TAN_DK, BROWN, OUTLINE, 2)
    # second bevel row under the top highlight
    hline(img, 2, w - 4, 2, CREAM)
    # grain lines live inside the top/bottom slices so stretching never thickens them
    hline(img, 3, w - 5, 6, TAN_DK)
    hline(img, 3, w - 5, h - 8, TAN_DK)
    # little plank breaks in the corner cells only
    px = img.load()
    for (x, y) in ((6, 6), (w - 8, 6), (5, h - 8), (w - 9, h - 8)):
        px[x, y] = TAN
    return img, (10, 10, 10, 10)


def panel_dark():
    w, h = 24, 24
    img = new(w, h)
    raised_body(img, 0, 0, w - 2, h - 2, DUSK, DUSK_HI, DUSK_DK, DUSK_DK, DUSK_LINE, 2)
    return img, (6, 8, 6, 6)


def slot():
    w, h = 24, 24
    img = new(w, h)
    rrect(img, 0, 0, w - 1, h - 1, OUTLINE, 2)
    rrect(img, 1, 1, w - 2, h - 2, CREAM_HI, 1)      # light bottom/right rim
    rrect(img, 1, 1, w - 3, h - 3, BROWN, 1)         # dark top/left rim (inset shadow)
    rrect(img, 2, 2, w - 3, h - 3, TAN_DK, 0)
    rrect(img, 3, 3, w - 3, h - 3, CREAM, 0)         # lighter fill
    px = img.load()
    px[w - 2, 2] = CREAM_HI
    px[2, h - 2] = CREAM_HI
    return img, (6, 6, 6, 6)


def dialog():
    # body starts at x=5; the tail lives in the TOP-LEFT corner cell (rows 4..12),
    # so it never stretches. Borders: L10 B8 R6 T14.
    w, h = 30, 30
    bx = 5
    img = new(w, h)
    raised_body(img, bx, 0, w - 2, h - 2, CREAM, CREAM_HI, TAN_DK, TAN_DK, OUTLINE, 2)
    px = img.load()
    tip = 8
    for y in range(4, 13):
        x0 = abs(y - tip)
        if y in (4, 12):
            for x in range(x0, bx + 1):
                px[x, y] = OUTLINE
            continue
        px[x0, y] = OUTLINE
        for x in range(x0 + 1, bx + 2):
            px[x, y] = CREAM
        if y < tip:
            px[x0 + 1, y] = CREAM_HI       # lit upper slope
        elif y > tip:
            px[x0 + 1, y] = TAN_DK         # shaded lower slope
    # soft shadow under the lower slope
    for y in range(9, 14):
        x = abs(y - tip) + (0 if y < 13 else -1)
        if px[x, y + 1][3] == 0:
            px[x, y + 1] = SHADOW
    return img, (10, 8, 6, 14)


def button(w, h, face, hi, pressed):
    img = new(w, h)
    if not pressed:
        raised_body(img, 0, 0, w - 1, h - 1, face, hi, TAN_DK, BROWN, OUTLINE, 3, shadow=False)
    else:
        raised_body(img, 0, 2, w - 1, h - 1, face, hi, TAN_DK, BROWN, OUTLINE, 1, shadow=False)
    return img


def bar_bg():
    w, h = 12, 7
    img = new(w, h)
    rrect(img, 0, 0, w - 1, h - 1, OUTLINE, 1)
    rrect(img, 1, 1, w - 2, h - 2, INK, 0)
    hline(img, 1, w - 2, 1, INK_DK)      # inner shadow top
    hline(img, 1, w - 2, h - 2, (98, 72, 56, 255))  # faint bottom lift
    return img, (3, 3, 3, 3)


# ---------------------------------------------------------------- stars
def star_mask():
    # hand-built, symmetric 5-point star; '#' outline, h highlight, f fill, s shade
    rows = [
        "................",
        ".......##.......",
        "......#hh#......",
        "......#hf#......",
        ".....#hffs#.....",
        ".#####hffs#####.",
        ".#hhhhfffffffs#.",
        "..#hfffffffss#..",
        "...#ffffffss#...",
        "....#fffffs#....",
        "....#fffffs#....",
        "...#fffsfffs#...",
        "...#ffs##ffs#...",
        "..#fs#..#fss#...",
        "..###....####...",
        "................",
    ]
    return rows


def star(fill, hi, sh, line):
    img = new(16, 16)
    px = img.load()
    cmap = {"#": line, "h": hi, "f": fill, "s": sh}
    for y, row in enumerate(star_mask()):
        for x, c in enumerate(row):
            if c in cmap:
                px[x, y] = cmap[c]
    return img


def star_glow():
    base = star(GOLD, GOLD_HI, GOLD_SH, STAR_LINE)
    a = base.split()[3].load()
    img = new(16, 16)
    px = img.load()
    for y in range(16):
        for x in range(16):
            if a[x, y]:
                continue
            best = 99
            for yy in range(max(0, y - 2), min(16, y + 3)):
                for xx in range(max(0, x - 2), min(16, x + 3)):
                    if a[xx, yy]:
                        d = max(abs(xx - x), abs(yy - y))
                        best = min(best, d)
            if best == 1:
                px[x, y] = GLOW + (170,)
            elif best == 2 and (x + y) % 2 == 0:
                px[x, y] = GLOW + (80,)
    return img


# ---------------------------------------------------------------- preview
def nine_slice(img, border, W, H):
    L, B, R, Tp = border
    w, h = img.size
    xs = [(0, L, L), (L, w - R, W - L - R), (w - R, w, R)]
    ys = [(0, Tp, Tp), (Tp, h - B, H - Tp - B), (h - B, h, B)]
    out = new(W, H)
    oy = 0
    for (y0, y1, th) in ys:
        ox = 0
        for (x0, x1, tw) in xs:
            if tw > 0 and th > 0:
                cell = img.crop((x0, y0, x1, y1)).resize((tw, th), Image.NEAREST)
                out.paste(cell, (ox, oy))
            ox += tw
        oy += th
    return out


def preview(entries, stars, path):
    S = 4
    sizes = [None, (40, 24), (80, 40), (28, 56)]
    tiles = []
    for name, img, border in entries:
        row = [img.resize((img.width * S, img.height * S), Image.NEAREST)]
        for sz in sizes[1:]:
            W = max(sz[0], border[0] + border[2] + 1)
            H = max(sz[1], border[1] + border[3] + 1)
            if name.startswith("bar"):
                W, H = sz[0], 7
            n = nine_slice(img, border, W, H)
            row.append(n.resize((W * S, H * S), Image.NEAREST))
        tiles.append(row)
    pad = 12
    width = max(sum(t.width for t in r) + pad * (len(r) + 1) for r in tiles)
    height = sum(max(t.height for t in r) + pad for r in tiles) + pad + 16 * S * 2 + pad
    sheet = Image.new("RGBA", (width, height), (94, 122, 84, 255))  # grassy bg
    y = pad
    for r in tiles:
        x = pad
        for t in r:
            sheet.alpha_composite(t, (x, y))
            x += t.width + pad
        y += max(t.height for t in r) + pad
    # stars, on cream and on dark bg
    x = pad
    strip = Image.new("RGBA", (len(stars) * 18 * 2 * S, 16 * S * 2), CREAM)
    for i, (n, im) in enumerate(stars):
        z = im.resize((64, 64), Image.NEAREST)
        strip.alpha_composite(z, (i * 72, 0))
        dark = Image.new("RGBA", (64, 64), DUSK)
        dark.alpha_composite(z)
        strip.alpha_composite(dark, (i * 72, 64))
    # glow behind a full star
    combo = star_glow()
    combo.alpha_composite(star(GOLD, GOLD_HI, GOLD_SH, STAR_LINE))
    z = combo.resize((64, 64), Image.NEAREST)
    strip.alpha_composite(z, (len(stars) * 72, 0))
    dark = Image.new("RGBA", (64, 64), DUSK)
    dark.alpha_composite(z)
    strip.alpha_composite(dark, (len(stars) * 72, 64))
    sheet.alpha_composite(strip.crop((0, 0, min(strip.width, width - pad), strip.height)), (x, y))
    sheet.save(path)


# ---------------------------------------------------------------- main
def main():
    entries = []

    def save(name, img, border):
        img.save(os.path.join(HERE, name + ".png"))
        entries.append((name, img, border))

    img, b = panel_cream(); save("panel_cream", img, b)
    img, b = panel_wood(); save("panel_wood", img, b)
    img, b = panel_dark(); save("panel_dark", img, b)
    img, b = slot(); save("slot", img, b)
    img, b = dialog(); save("dialog", img, b)
    save("button", button(24, 26, CREAM, CREAM_HI, False), (6, 8, 6, 6))
    save("button_pressed", button(24, 26, CREAM, CREAM_HI, True), (6, 8, 6, 6))
    save("button_wide", button(40, 26, CREAM, CREAM_HI, False), (8, 8, 8, 6))
    save("button_wide_pressed", button(40, 26, CREAM, CREAM_HI, True), (8, 8, 8, 6))
    img, b = bar_bg(); save("bar_bg", img, b)

    stars = [
        ("star_full", star(GOLD, GOLD_HI, GOLD_SH, STAR_LINE)),
        ("star_empty", star(DIM, DIM_HI, DIM_SH, STAR_LINE)),
        ("star_glow", star_glow()),
    ]
    for n, im in stars:
        im.save(os.path.join(HERE, n + ".png"))

    manifest = {
        "note": "Import as Sprite (Single), Point filter, no compression, no mipmaps, "
                "pixelsPerUnit 16. border = Unity spriteBorder order (L, B, R, T) in pixels.",
        "textureType": "Sprite",
        "spriteMode": "Single",
        "filterMode": "Point",
        "textureCompression": "Uncompressed",
        "mipmaps": False,
        "alphaIsTransparency": True,
        "pixelsPerUnit": 16,
        "files": [
            {"path": "Assets/Art/UI/Panels/%s.png" % n, "size": list(im.size),
             "border_LBRT": list(bd)}
            for n, im, bd in entries
        ] + [
            {"path": "Assets/Art/UI/Panels/%s.png" % n, "size": [16, 16],
             "border_LBRT": [0, 0, 0, 0]}
            for n, _ in stars
        ],
    }
    with open(os.path.join(HERE, "panel_import.json"), "w") as f:
        json.dump(manifest, f, indent=2)

    preview(entries, stars, os.path.join(HERE, "_preview.png"))
    print("ok:", [e[0] for e in entries] + [s[0] for s in stars])


if __name__ == "__main__":
    main()
