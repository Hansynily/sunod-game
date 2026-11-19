"""Redraws the people inside the quest rooms in the storybook style.

Each file in Assets/Art/Generated/Quests keeps its name, canvas size, pivot and import
settings, so every room and quest script keeps working and nothing moves. Only the drawing
changes. Run:  python quest_people.py [preview|write]
(The exam cat is an animal and keeps its art on purpose.)
"""
import os, sys
from PIL import Image, ImageDraw
import npc_gen as G
from npc_gen import tri, white, C, PAPER, INK, LINE, W, H
from townsfolk import P, PEOPLE, BLACK, DBROWN, BROWN, GREY, WHITEH, DENIM, NAVY, KHAKI, BLACKP

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
QDIR = os.path.join(ROOT, "Assets", "Art", "Generated", "Quests")


def kid(**kw):
    kw["build"] = "kid"
    return P(**kw)


def with_mood(s, mood):
    t = dict(s); t["mood"] = mood
    return t


# ── the cast ──────────────────────────────────────────────────────────────────────
BUY = [kid(name="buyer a", skin="tan", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("green", 5), bottom="shorts", bottom_col=DENIM),
       kid(name="buyer b", skin="morena", hair="pony", hair_col=BLACK, top="tshirt", top_col=tri("pink", 6), bottom="skirt", bottom_col=tri("blue", 3)),
       kid(name="buyer c", skin="light", hair="spiky", hair_col=DBROWN, top="tshirt", top_col=tri("amber", 5), bottom="shorts", bottom_col=tri("cool", 2))]
CUST = [kid(name="customer a", skin="tan", hair="short", hair_col=BLACK, top="polo", top_col=tri("cool", 3), bottom="pants", bottom_col=NAVY, cheeks=False),
        kid(name="customer b", skin="light", hair="bun", hair_col=WHITEH, top="blouse", top_col=tri("red", 5), bottom="skirt", bottom_col=tri("violet", 2), glasses=True),
        kid(name="customer c", skin="morena", hair="crop", hair_col=BLACK, top="tshirt", top_col=tri("teal", 4), bottom="pants", bottom_col=KHAKI, moustache=C["cool"][1], cheeks=False)]
JUDGE = [kid(name="judge a", skin="fair", hair="receding", hair_col=GREY, top="jacket", top_col=tri("blue", 2), bottom="pants", bottom_col=tri("cool", 1),
             extras=[("open", white()), ("tie", tri("red", 4))], glasses=True, cheeks=False),
         kid(name="judge b", skin="tan", hair="bun", hair_col=WHITEH, top="blouse", top_col=tri("violet", 4), bottom="skirt", bottom_col=tri("cool", 1), glasses=True),
         kid(name="judge c", skin="morena", hair="bob", hair_col=BLACK, top="jacket", top_col=tri("teal", 3), bottom="pants", bottom_col=tri("cool", 1),
             extras=[("open", white())])]
CHOIR = [kid(name="choir d", skin="tan", hair="short", hair_col=BLACK, top="uniform", top_col=white(), bottom="shorts", bottom_col=NAVY, extras=[("collar", tri("blue", 4))]),
         kid(name="choir e", skin="light", hair="long", hair_col=DBROWN, top="uniform", top_col=white(), bottom="skirt", bottom_col=NAVY, extras=[("collar", tri("blue", 4))]),
         kid(name="choir f", skin="morena", hair="pony", hair_col=BLACK, top="uniform", top_col=white(), bottom="skirt", bottom_col=NAVY, extras=[("collar", tri("blue", 4))])]
SONG = [kid(name="song a", skin="tan", hair="spiky", hair_col=BLACK, top="tshirt", top_col=tri("blue", 5), bottom="shorts", bottom_col=KHAKI),
        kid(name="song b", skin="morena", hair="bob", hair_col=BLACK, top="tshirt", top_col=tri("amber", 6), bottom="skirt", bottom_col=tri("red", 4)),
        kid(name="song c", skin="light", hair="short", hair_col=BROWN, top="tshirt", top_col=tri("lime", 5), bottom="shorts", bottom_col=DENIM)]
LOSTKID = kid(name="kid", skin="tan", hair="pony", hair_col=BLACK, top="tshirt", top_col=tri("red", 5), bottom="skirt", bottom_col=DENIM, extras=[("flower",)])
FRIEND = kid(name="friend", skin="morena", hair="short", hair_col=BLACK, top="jacket", top_col=tri("green", 3), bottom="pants", bottom_col=DENIM,
             extras=[("open", tri("warm", 6))], cheeks=False)
CAST = [P(name="king", build="man", skin="tan", hair="short", hair_col=BLACK, top="barong", top_col=tri("red", 4), bottom="pants", bottom_col=tri("cool", 1),
          extras=[("crown",), ("cape", tri("red", 3))], beard=C["cool"][1], cheeks=False),
        P(name="forest spirit", skin="light", hair="long", hair_col=tri("green", 2), top="dress", top_col=tri("green", 5), bottom="dress", bottom_col=tri("green", 5),
          extras=[("leafcrown",)]),
        P(name="princess", skin="morena", hair="long", hair_col=BLACK, top="dress", top_col=tri("pink", 5), bottom="dress", bottom_col=tri("pink", 5),
          extras=[("crown",), ("dots", PAPER)])]
STUNT = P(name="stunt double", build="man", skin="tan", hair="short", hair_col=BLACK, top="jacket", top_col=tri("cool", 1), bottom="pants",
          bottom_col=tri("cool", 1), hat="cap", hat_col=tri("red", 4), extras=[("stripe", tri("red", 5), 3)], cheeks=False)
LORNA = PEOPLE["s4"]
LOLA = PEOPLE["s8"]


# ── poses ─────────────────────────────────────────────────────────────────────────

def render(s, view="down", legs="stand", arm=0, blink=False, edit=None):
    lg = {"stand": "stand", "l": "stride", "r": "pass"}.get(legs, legs) if view == "left" else legs
    rows = G.build_map(s, view, lg, arm)
    if edit:
        rows = edit(rows, s)
    img = G.shade(rows, G.ramps(s))
    G.extras(img, s, view)
    G.face(img, s, view, blink)
    return img


def arms_up(rows, s):
    g = G.BUILDS[s["build"]]
    m = [list(r) for r in rows]
    for x in range(16):
        for y in range(g["sh"] + 1, g["hand"] + 2):
            if m[y][x] in "SA" and (x <= 3 or x >= 12):
                m[y][x] = "."
    for x in (3, 12):
        for y in range(g["head"] - 1, g["sh"] + 2):
            m[y][x] = "S" if y < g["head"] + 1 else ("T" if m[y][x] == "." or y >= g["sh"] else "S")
    return ["".join(r) for r in m]


def squat(img, s, by=5):
    g = G.BUILDS[s["build"]]
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(img.crop((0, g["feet"] - 1, W, H)), (0, g["feet"] - 1))
    upper = img.crop((0, 0, W, g["leg"] + 1))
    out.alpha_composite(upper, (0, by))
    return out


def seated_front(img, s):
    """Sitting on a bench, seen from the front: the shins tuck away, the knees show."""
    g = G.BUILDS[s["build"]]
    leg_rows = g["feet"] - g["leg"]
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.alpha_composite(img.crop((0, g["feet"], W, H)), (0, g["feet"]))
    out.alpha_composite(img.crop((0, 0, W, g["leg"] + 1)), (0, leg_rows))
    return out


def hold(img, s, col):
    """Holds something small in front with both hands."""
    g = G.BUILDS[s["build"]]
    y = g["hand"] - 2
    d = ImageDraw.Draw(img)
    d.rectangle((6, y - 1, 10, y + 2), fill=LINE + (255,))
    d.rectangle((7, y, 9, y + 1), fill=col[1] + (255,))
    img.putpixel((7, y), col[2] + (255,))
    for x in (5, 11):
        img.putpixel((x, y + 1), G.ramps(s)["S"][1] + (255,))
    return img


def ball(s, turn=0):
    """The stunt roll: tucked into a ball, a quarter turn per frame."""
    R = G.ramps(s)
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cy = 24
    d.ellipse((3, cy - 6, 12, cy + 4), fill=LINE + (255,))
    d.ellipse((4, cy - 5, 11, cy + 3), fill=R["T"][1] + (255,))
    d.ellipse((5, cy - 4, 8, cy - 1), fill=R["T"][2] + (255,))
    spots = [(9, cy - 4), (10, cy), (7, cy + 2), (5, cy - 1)]
    hx, hy = spots[turn % 4]
    d.rectangle((hx - 1, hy - 1, hx, hy), fill=R["K"][1] + (255,))           # red cap going round
    return im


def cane(img, x=13):
    for y in range(17, 30):
        img.putpixel((x, y), C["brown"][2] + (255,))
    img.putpixel((x - 1, 17), C["brown"][2] + (255,))
    return img


def chair_behind(fig):
    out = Image.new("RGBA", fig.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(out)
    w = fig.width
    x0, x1 = w // 2 - 7, w // 2 + 6
    d.rectangle((x0, 12, x1, 27), fill=LINE + (255,))
    d.rectangle((x0 + 1, 13, x1 - 1, 26), fill=C["brown"][3] + (255,))
    for y in range(14, 26, 3):
        d.line((x0 + 2, y, x1 - 2, y), fill=C["brown"][2] + (255,))
    d.rectangle((x0, 26, x1, 28), fill=C["red"][4] + (255,))
    d.line((x0 + 1, 29, x0 + 1, 31), fill=LINE + (255,)); d.line((x1 - 1, 29, x1 - 1, 31), fill=LINE + (255,))
    out.alpha_composite(fig)
    return out


# ── canvas fitting ───────────────────────────────────────────────────────────────

def kid24(img):
    return img.crop((0, 7, 16, 31))


def fit(img, w, h=32):
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(img, ((w - 16) // 2, h - 32))
    return out


def idle6(s):
    return G.idle_frames(s, "down")


# ── the file list ────────────────────────────────────────────────────────────────

def build():
    f = {}
    for i, s in enumerate(BUY):
        k = "abc"[i]
        f[f"buykid_{k}_0"] = kid24(render(s))
        f[f"buykid_{k}_1"] = kid24(hold(render(with_mood(s, "happy")), s, tri("amber", 5)))
    for i, s in enumerate(CUST):
        k = "abc"[i]
        f[f"cust_{k}_0"] = kid24(render(with_mood(s, "neutral")))
        f[f"cust_{k}_1"] = kid24(render(with_mood(s, "happy")))
    for i, s in enumerate(JUDGE):
        k = "abc"[i]
        f[f"judge_{k}_0"] = kid24(render(with_mood(s, "neutral")))
        f[f"judge_{k}_1"] = kid24(render(with_mood(s, "nod")))
        f[f"judge_{k}_2"] = kid24(render(with_mood(s, "happy")))
    for i, s in enumerate(CHOIR):
        k = "def"[i]
        f[f"choirkid_{k}_0"] = kid24(render(s))
        f[f"choirkid_{k}_1"] = kid24(render(with_mood(s, "sing")))
    for i, s in enumerate(SONG):
        k = "abc"[i]
        f[f"songkid_{k}_0"] = kid24(render(s))
        f[f"songkid_{k}_1"] = kid24(render(with_mood(s, "sing")))
    walk = G.walk_frames(LOSTKID, "left")
    for i, j in enumerate((0, 1, 3, 4)):
        f[f"kid_walk_{i}"] = kid24(walk[j])
    f["kid_happy_0"] = kid24(render(with_mood(LOSTKID, "happy")))
    f["kid_happy_1"] = kid24(G.dip(render(with_mood(LOSTKID, "happy"))))
    f["kid_sad_0"] = kid24(render(with_mood(LOSTKID, "sad")))
    f["kid_sad_1"] = kid24(G.dip(render(with_mood(LOSTKID, "sad"))))
    for i in range(4):
        im = render(LOSTKID, blink=(i == 3))
        f[f"kid_hold_{i}"] = kid24(hold(G.dip(im) if i in (1, 2) else im, LOSTKID, tri("brown", 4)))
    f["friend_stand"] = kid24(render(with_mood(FRIEND, "neutral")))
    f["friend_sit_sad"] = kid24(seated_front(render(with_mood(FRIEND, "sad")), FRIEND))
    f["friend_sit_up"] = kid24(seated_front(render(FRIEND), FRIEND))
    for i, s in enumerate(CAST):
        for j, im in enumerate(idle6(s)):
            f[f"cast{i}_c_f{j}"] = im
    lw = G.walk_frames(LOLA, "down")
    for j, im in enumerate(lw):
        f[f"lola_f{j}"] = fit(cane(im), 24)
    f["lola_sit"] = chair_behind(fit(seated_front(render(LOLA), LOLA), 24))
    f["lorna_stand"] = fit(render(LORNA), 20)
    f["lorna_up"] = fit(render(with_mood(LORNA, "happy"), edit=arms_up), 20)
    f["lorna_side"] = fit(render(LORNA, view="left"), 20)
    f["lorna_toe"] = fit(squat(render(LORNA, blink=True), LORNA, by=6), 20)
    sr = G.walk_frames(STUNT, "left")
    f["stunt_run_0"] = kid24(sr[1].resize((16, 32)))
    f["stunt_run_1"] = kid24(sr[4])
    f["stunt_land"] = kid24(squat(render(with_mood(STUNT, "happy")), STUNT, by=5))
    for i in range(4):
        f[f"stunt_roll_{i}"] = kid24(ball(STUNT, i))
    return f


def check_sizes(f):
    bad = []
    for name, im in f.items():
        p = os.path.join(QDIR, name + ".png")
        old = Image.open(p)
        if old.size != im.size:
            bad.append((name, old.size, im.size))
    return bad


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "preview"
    f = build()
    bad = check_sizes(f)
    if bad:
        print("size mismatch:", bad)
        sys.exit(1)
    if mode == "write":
        for name, im in f.items():
            im.save(os.path.join(QDIR, name + ".png"))
        print("wrote", len(f), "sprites")
    else:
        sc = 5
        names = sorted(f)
        cols = 12
        rows = (len(names) + cols - 1) // cols
        sheet = Image.new("RGBA", (cols * 26 * sc, rows * 36 * sc), C["green"][4] + (255,))
        for i, n in enumerate(names):
            im = f[n]
            sheet.alpha_composite(im.resize((im.width * sc, im.height * sc), Image.NEAREST),
                                  ((i % cols) * 26 * sc, (i // cols) * 36 * sc + (32 - im.height) * sc))
        out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "quest_people.png")
        sheet.convert("RGB").save(out)
        print(len(f), "sprites ->", out)
