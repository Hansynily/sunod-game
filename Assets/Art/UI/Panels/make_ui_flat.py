"""
SUNOD UI v3 - FLAT frames (9-slice). Replaces the beveled v2 look (wood band, light top
edge, dark lip, rivets) with one solid fill and a thin 1px soft outline with softly
rounded corners. Same file names and sizes as v2, so every USS / slice border keeps working.

Overwrites: panel_cream, panel_wood, panel_dark, dialog, slot, button, button_pressed,
button_wide, button_wide_pressed, card, card_pressed, card_disabled, card_selected,
chip, plate, bar_bg.

Run: python make_ui_flat.py
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

T = (0, 0, 0, 0)
INK = (160, 118, 84, 255)       # soft caramel outline (dark brown read too heavy); ~3:1 on the cream faces

CREAM = (247, 236, 214, 255)
WOOD = (228, 194, 146, 255)      # flat warm tan (was the wood band)
DARK = (70, 76, 96, 255)         # slate instead of brown-black
SLOT = (234, 216, 182, 255)
BUTTON = (246, 228, 194, 255)
BUTTON_PRESSED = (226, 200, 156, 255)
CARD = (246, 234, 210, 255)
CARD_PRESSED = (226, 206, 170, 255)
CARD_DISABLED = (206, 198, 186, 255)
GOLD = (230, 150, 50, 255)
BAR = (70, 76, 96, 255)


def flat(w, h, face, outline=INK, line=1):
    """Solid face, `line`-px outline, 1px corner cut (outer) so corners read rounded."""
    img = Image.new("RGBA", (w, h), T)
    px = img.load()
    for y in range(h):
        for x in range(w):
            dx = min(x, w - 1 - x)
            dy = min(y, h - 1 - y)
            if dx == 0 and dy == 0:
                continue                       # rounded outer corner
            if dx < line or dy < line:
                px[x, y] = outline
            elif dx == line and dy == line:
                px[x, y] = outline             # round the inner corner too
            else:
                px[x, y] = face
    return img


def save(img, name):
    img.save(os.path.join(HERE, name))
    print(f"{name:26s} {img.size}")


save(flat(24, 24, CREAM), "panel_cream.png")
save(flat(32, 32, WOOD), "panel_wood.png")
save(flat(24, 24, DARK, outline=(110, 118, 142, 255)), "panel_dark.png")
save(flat(30, 30, CREAM), "dialog.png")
save(flat(24, 24, SLOT), "slot.png")
save(flat(24, 26, BUTTON), "button.png")
save(flat(24, 26, BUTTON_PRESSED), "button_pressed.png")
save(flat(40, 26, BUTTON), "button_wide.png")
save(flat(40, 26, BUTTON_PRESSED), "button_wide_pressed.png")
save(flat(24, 28, CARD), "card.png")
save(flat(24, 28, CARD_PRESSED), "card_pressed.png")
save(flat(24, 28, CARD_DISABLED), "card_disabled.png")
save(flat(24, 28, CARD, outline=GOLD, line=2), "card_selected.png")
save(flat(20, 16, CREAM), "chip.png")
save(flat(20, 16, CREAM), "plate.png")
save(flat(12, 7, BAR, line=1), "bar_bg.png")
