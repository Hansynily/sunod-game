"""Skill-use pose for the player (boy + girl), built from the original frames.

The body is untouched. One arm swings out, then up, with a small hop and a sparkle at the hand:
  frame 0  arm out to the side
  frame 1  arm raised, hop 1 px
  frame 2  arm raised, hop 2 px, small sparkle
  frame 3  arm raised, back on the ground, bigger sparkle
Made for all four facings (left is the mirror of right). Written to
Assets/Resources/PlayerUse as <who>_use_<dir>_<n>.png and loaded by vc_SpriteFrameAnimator.

Run from the sunod-game folder:  python Tools/art/make_player_use.py
(after enhance_player.py, since it starts from the frames on disk)
"""
import os, re, uuid
from PIL import Image

SRC = "Assets/Art/Characters/Player/Frames"
OUT = "Assets/Resources/PlayerUse"
INK = (28, 23, 38, 255)
SKIN = (244, 204, 165, 255)
SKIN_S = (213, 157, 100, 255)
GLOW = (255, 208, 128, 255)
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)
os.makedirs(OUT, exist_ok=True)

# which frame each facing starts from: (boy, girl)
BASE = {
    "right": ("player_idle_right_0.png", "Girl/girl_idle_right_0.png"),
    "up":    ("player_idle_up_0.png", "Girl/girl_idle_up_0.png"),
    "down":  ("player_walk_down_2.png", "Girl/girl_idle_down_0.png"),
}


def arm(im, stage):
    """Redraws the right-hand arm. stage 0 = out to the side, 1 = raised."""
    px = im.load()
    y_hand = 35
    ax = max(x for x in range(im.width) if px[x, y_hand][3])      # outer edge of the hand
    def put(dx, y, c):
        x = ax + dx
        if 0 <= x < im.width and 0 <= y < im.height:
            px[x, y] = c
    for y in range(34, 38):                                         # take the hanging arm away
        for dx in (-2, -1, 0):
            put(dx, y, CLEAR)
    put(-2, 34, INK)                                                # close the armpit
    if stage == 0:
        for dx, c in ((-2, SKIN_S), (-1, SKIN), (0, SKIN), (1, INK)):
            put(dx, 33, c)
        for dx in (-1, 0):
            put(dx, 32, INK); put(dx, 34, INK)
    else:
        put(-2, 33, SKIN_S); put(-1, 33, INK)
        put(-1, 32, SKIN); put(0, 32, INK)
        put(-1, 31, INK); put(0, 31, SKIN); put(1, 31, INK)
        put(0, 30, INK); put(1, 30, SKIN); put(2, 30, INK)
        put(0, 29, INK); put(1, 29, SKIN); put(2, 29, INK)
        put(1, 28, INK)
    return ax


def lift(im, n):
    out = Image.new("RGBA", im.size, CLEAR)
    out.paste(im, (0, -n))
    return out


def sparkle(im, ax, y, big):
    px = im.load()
    cx, cy = ax + 3, y
    pts = [(0, 0, WHITE), (-1, 0, GLOW), (1, 0, GLOW), (0, -1, GLOW), (0, 1, GLOW)]
    if big:
        pts += [(-2, 0, GLOW), (2, 0, GLOW), (0, -2, GLOW), (0, 2, GLOW), (-3, -3, GLOW), (3, 3, GLOW), (3, -3, WHITE), (-3, 3, WHITE)]
    for dx, dy, c in pts:
        x, yy = cx + dx, cy + dy
        if 0 <= x < im.width and 0 <= yy < im.height and px[x, yy][3] == 0:
            px[x, yy] = c


def frames(base):
    f0 = base.copy(); arm(f0, 0)
    up = base.copy(); ax = arm(up, 1)
    f1 = lift(up, 1)
    f2 = lift(up, 2); sparkle(f2, ax, 24, False)
    f3 = up.copy(); sparkle(f3, ax, 25, True)
    return [f0, f1, f2, f3]


def write(name, im, meta_from):
    path = os.path.join(OUT, name)
    im.save(path)
    if not os.path.exists(path + ".meta"):                          # same import settings as the frame it came from
        txt = open(meta_from + ".meta", encoding="utf-8").read()
        txt = re.sub(r"guid: [0-9a-f]+", "guid: " + uuid.uuid4().hex, txt, count=1)
        open(path + ".meta", "w", encoding="utf-8", newline="\n").write(txt)


n = 0
sheet = Image.new("RGBA", (40 * 16, 48 * 2), (214, 160, 110, 255))
for wi, who in enumerate(("player", "girl")):
    col = 0
    for d in ("down", "right", "up", "left"):
        src = os.path.join(SRC, BASE["right" if d == "left" else d][wi])
        base = Image.open(src).convert("RGBA")
        fs = frames(base)
        if d == "left":
            fs = [f.transpose(Image.FLIP_LEFT_RIGHT) for f in fs]
        for i, f in enumerate(fs):
            write("%s_use_%s_%d.png" % (who, d, i), f, src)
            sheet.alpha_composite(f, (col * 40, wi * 48)); col += 1
            n += 1
os.makedirs("../previews/art_rework", exist_ok=True)
sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save("../previews/art_rework/player_use.png")
print("use frames written:", n)
