"""
SUNOD UI v2 - original pixel-art frames (9-slice), same warm palette as v1.

Look: dark-ink outline -> wooden frame band (light top edge, dark bottom edge)
-> cream face with a 1px highlight. Notched corners instead of round "pillow"
corners, so it no longer reads like the Sprout Lands pack.

Overwrites the v1 PNGs in place (same file names, so every USS keeps working):
  panel_cream, panel_wood, panel_dark, dialog, slot, button, button_pressed,
  button_wide, button_wide_pressed, bar_bg
and adds: chip.png (small HUD info chip, e.g. "Energy 4/4"), icon_energy.png.

Run: python make_ui_v2.py   (borders are printed; keep SunodTheme.tss in sync)
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

T = (0, 0, 0, 0)
INK = (58, 40, 30, 255)          # outline
WOOD_HI = (205, 150, 98, 255)    # frame top edge
WOOD = (176, 120, 78, 255)       # frame band
WOOD_DK = (138, 88, 58, 255)     # frame bottom edge / lip
CREAM_HI = (250, 238, 208, 255)
CREAM = (238, 218, 178, 255)
CREAM_DK = (222, 196, 152, 255)  # face shading line
PRESS = (226, 200, 156, 255)     # pressed face
DUSK = (92, 70, 84, 255)
DUSK_HI = (122, 98, 112, 255)
DUSK_DK = (66, 48, 60, 255)
GOLD = (255, 204, 66, 255)
GOLD_HI = (255, 238, 150, 255)
GOLD_SH = (220, 130, 40, 255)


def new(w, h): return Image.new("RGBA", (w, h), T)


def notched(img, x0, y0, x1, y1, col, notch):
    """Filled rect with square notches cut from each corner (notch x notch)."""
    px = img.load()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if min(x - x0, x1 - x) < notch and min(y - y0, y1 - y) < notch:
                continue
            px[x, y] = col


def hline(img, x0, x1, y, col):
    for x in range(x0, x1 + 1): img.putpixel((x, y), col)


def frame(w, h, face, face_hi, band=WOOD, band_hi=WOOD_HI, band_dk=WOOD_DK, lip=2, band_w=2, face_line=None):
    """Outline + wooden band + face. lip = extra dark rows at the bottom (depth)."""
    img = new(w, h)
    notched(img, 0, 0, w - 1, h - 1, INK, 2)
    notched(img, 1, 1, w - 2, h - 2, band_dk, 1)                    # bottom edge colour shows as the lip
    notched(img, 1, 1, w - 2, h - 2 - lip, band, 1)
    hline(img, 2, w - 3, 1, band_hi)                                 # light top edge of the frame
    b = 1 + band_w
    notched(img, b, b, w - 1 - b, h - 1 - b - lip, INK, 1)           # inner outline
    notched(img, b + 1, b + 1, w - 2 - b, h - 2 - b - lip, face, 0)
    hline(img, b + 1, w - 2 - b, b + 1, face_hi)                     # face highlight
    if face_line:
        hline(img, b + 1, w - 2 - b, h - 2 - b - lip, face_line)     # face bottom shade
    # rivets on the frame corners
    for (x, y) in ((2, 2), (w - 3, 2)):
        img.putpixel((x, y), band_hi)
    return img


def save(img, name, border):
    img.save(os.path.join(HERE, name))
    print(f"{name:26s} {img.size}  border LBRT={border}")


# panels
save(frame(24, 24, CREAM, CREAM_HI, lip=2, face_line=CREAM_DK), "panel_cream.png", (6, 8, 6, 6))
TAN = (220, 185, 138, 255); TAN_HI = (232, 207, 166, 255); TAN_DK = (204, 166, 118, 255)
save(frame(32, 32, TAN, TAN_HI, lip=3, band_w=2, face_line=TAN_DK), "panel_wood.png", (10, 10, 10, 10))
# title plate: dark wood face for light text (dialog titles, banners)
save(frame(20, 16, WOOD, WOOD_HI, band=WOOD_DK, band_hi=WOOD, band_dk=INK, lip=2, band_w=1), "plate.png", (5, 6, 5, 5))
save(frame(24, 24, DUSK, DUSK_HI, band=DUSK_DK, band_hi=DUSK, band_dk=INK, lip=2), "panel_dark.png", (6, 8, 6, 6))
save(frame(30, 30, CREAM, CREAM_HI, lip=3, band_w=2, face_line=CREAM_DK), "dialog.png", (10, 8, 10, 10))

# slot: inset look (dark inner rim on top, light on bottom)
s = frame(24, 24, CREAM_DK, CREAM_DK, lip=1)
hline(s, 4, 19, 4, WOOD_DK)
save(s, "slot.png", (6, 6, 6, 6))

# buttons: chunky + simple (outline, cream face, highlight, wood lip) - no inner frame,
# so the whole face is free for the label. Pressed = lip gone, face 3px lower and darker.
PLANK = (222, 172, 116, 255); PLANK_HI = (240, 200, 146, 255); PLANK_SH = (196, 144, 92, 255); PLANK_PR = (204, 154, 100, 255)
GRAIN = (208, 158, 104, 255)

def button(w, pressed):
    """Wooden plank: ink outline, warm wood face with a light top edge, grain line, nails, dark lip."""
    h = 26
    img = new(w, h)
    top = 3 if pressed else 0
    notched(img, 0, top, w - 1, h - 1, INK, 2)
    if not pressed:
        notched(img, 1, 1, w - 2, h - 2, WOOD_DK, 1)          # lip
    face_bot = h - 5 if not pressed else h - 2
    notched(img, 1, top + 1, w - 2, face_bot, PLANK_PR if pressed else PLANK, 1)
    hline(img, 2, w - 3, top + 1, PLANK if pressed else PLANK_HI)
    hline(img, 2, w - 3, face_bot, PLANK_SH)
    # faint grain line through the middle (stays in the stretchable centre area)
    gy = top + (face_bot - top) // 2 + 2
    for x in range(5, w - 5):
        if x % 7 not in (0, 1): img.putpixel((x, gy), GRAIN)
    # nails in the corners of the face
    for nx in (3, w - 4):
        img.putpixel((nx, top + 3), WOOD_DK)
        img.putpixel((nx, face_bot - 2), WOOD_DK)
    return img

save(button(24, False), "button.png", (6, 8, 6, 6))
save(button(24, True), "button_pressed.png", (6, 8, 6, 6))
save(button(40, False), "button_wide.png", (8, 8, 8, 6))
save(button(40, True), "button_wide_pressed.png", (8, 8, 8, 6))

# bar background (sunken track)
bar = new(12, 7)
notched(bar, 0, 0, 11, 6, INK, 1)
notched(bar, 1, 1, 10, 5, WOOD_DK, 0)
hline(bar, 1, 10, 1, INK)
save(bar, "bar_bg.png", (3, 3, 3, 3))

# chip: small flat info tag for HUD numbers (no frame band, just outline + face + lip)
chip = new(20, 16)
notched(chip, 0, 0, 19, 15, INK, 2)
notched(chip, 1, 1, 18, 14, WOOD_DK, 1)
notched(chip, 1, 1, 18, 12, CREAM, 1)
hline(chip, 2, 17, 1, CREAM_HI)
save(chip, "chip.png", (5, 5, 5, 5))

# energy icon: little lightning bolt
bolt_rows = [
    "....KKK",
    "...KYYK",
    "..KYYK.",
    ".KYYKKK",
    "KYYYYYK",
    "KKKYYK.",
    "..KYK..",
    ".KYK...",
    ".KK....",
]
icon = new(7, 9)
for y, r in enumerate(bolt_rows):
    for x, ch in enumerate(r):
        if ch == "K": icon.putpixel((x, y), INK)
        elif ch == "Y": icon.putpixel((x, y), GOLD if y > 2 else GOLD_HI)
save(icon, "icon_energy.png", (0, 0, 0, 0))

# preview sheet
names = ["panel_cream", "panel_wood", "panel_dark", "dialog", "slot", "button", "button_pressed",
         "button_wide", "button_wide_pressed", "bar_bg", "chip", "icon_energy"]
ims = [Image.open(os.path.join(HERE, n + ".png")) for n in names]
Z = 6
W = sum(i.width * Z + 12 for i in ims)
sheet = Image.new("RGBA", (W, 34 * Z), (70, 110, 70, 255))
x = 0
for i in ims:
    sheet.alpha_composite(i.resize((i.width * Z, i.height * Z), Image.NEAREST), (x, 0))
    x += i.width * Z + 12
sheet.save(os.path.join(os.environ.get("PREVIEW_DIR", HERE), "_preview_v2.png"))


# ---- skill cards (replace the old SkillSlotUIKit art) --------------------
def card(face, face_hi, band=WOOD, band_hi=WOOD_HI, band_dk=WOOD_DK, lip=3):
    return frame(24, 28, face, face_hi, band=band, band_hi=band_hi, band_dk=band_dk, lip=lip, band_w=2, face_line=CREAM_DK)

card_n = card(CREAM, CREAM_HI)
card_p = new(24, 28); card_p.alpha_composite(frame(24, 25, PRESS, CREAM, lip=0, band_w=2), (0, 3))
card_s = card(CREAM_HI, (255, 248, 225, 255), band=GOLD_SH, band_hi=GOLD, band_dk=(170, 96, 30, 255))
GREY = (196, 188, 176, 255); GREY_HI = (214, 207, 196, 255)
card_d = card(GREY, GREY_HI, band=(150, 132, 120, 255), band_hi=(170, 152, 140, 255), band_dk=(118, 100, 92, 255))
for im, n in ((card_n, "card.png"), (card_p, "card_pressed.png"), (card_s, "card_selected.png"), (card_d, "card_disabled.png")):
    save(im, n, (6, 9, 6, 6))

# round inset ring behind skill icons
ring = new(24, 24)
import math
for y in range(24):
    for x in range(24):
        d = math.hypot(x - 11.5, y - 11.5)
        if d <= 11.5: ring.putpixel((x, y), INK)
        if d <= 10.5: ring.putpixel((x, y), WOOD_DK if y < 12 else WOOD_HI)
        if d <= 9.5: ring.putpixel((x, y), CREAM_DK)
        if d <= 9.5 and y > 13 and d > 8.5: ring.putpixel((x, y), CREAM)
save(ring, "icon_ring.png", (0, 0, 0, 0))

# small number/level badge
badge = new(14, 12)
notched(badge, 0, 0, 13, 11, INK, 2)
notched(badge, 1, 1, 12, 10, GOLD_SH, 1)
notched(badge, 1, 1, 12, 8, GOLD, 1)
hline(badge, 2, 11, 1, GOLD_HI)
save(badge, "badge.png", (4, 4, 4, 4))
