"""Every townsperson's spec. Run:  python townsfolk.py [preview|export] [look ...]

look ids: quest givers use their question code in lower case (r6, r2, ...); roaming
townsfolk keep town0..town7. Colours come from the Umaga palette via tri(ramp, step).
"""
import os, sys
from PIL import Image, ImageDraw
from npc_gen import tri, white, export, idle_frames, walk_frames, sit_frames, C, PAPER, INK

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))  # sunod-game


def P(**kw):
    kw.setdefault("build", "adult")
    kw.setdefault("skin", "tan")
    kw.setdefault("hair", "short")
    kw.setdefault("hair_col", tri("cool", 1))
    kw.setdefault("top", "tshirt")
    kw.setdefault("bottom", "pants")
    return kw


BLACK, DBROWN, BROWN, GREY, WHITEH = tri("cool", 1), tri("brown", 1), tri("brown", 2), tri("warm", 5), tri("warm", 6)
DENIM, NAVY, KHAKI, BLACKP = tri("blue", 3), tri("blue", 2), tri("warm", 4), tri("cool", 1)

PEOPLE = {
    # ── Elementary ──
    "r6": P(name="Aling Rosa", skin="morena", hair="bun", hair_col=DBROWN, top="dress", top_col=tri("pink", 6), bottom="dress",
            bottom_col=tri("pink", 6), extras=[("apron", white()), ("flower",)], shoe_col=tri("teal", 4)),
    "r2": P(name="Mang Ben", build="man", skin="dark", hair="crop", hair_col=GREY, top="sando", top_col=white(), bottom="shorts",
            bottom_col=DENIM, hat="salakot", hat_col=tri("amber", 5), moustache=C["warm"][6], cheeks=False),
    "r8": P(name="Lolo Ador", build="man", skin="tan", hair="receding", hair_col=WHITEH, top="polo", top_col=tri("teal", 3),
            bottom="pants", bottom_col=KHAKI, glasses=True, moustache=C["warm"][6], cheeks=False),
    "s4": P(name="Tita Lorna", skin="light", hair="pony", hair_col=DBROWN, top="tshirt", top_col=tri("lime", 5), bottom="pants",
            bottom_col=tri("cool", 2), extras=[("stripe", tri("lime", 6), 3)], shoe_col=white()),
    "i2": P(name="Kuya Migs", build="man", skin="morena", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("green", 3),
            bottom="shorts", bottom_col=KHAKI, hat="bucket", hat_col=tri("lime", 3), cheeks=False),
    "i3": P(name="Teacher Joy", skin="light", hair="bob", hair_col=BROWN, top="blouse", top_col=tri("cyan", 6), bottom="skirt",
            bottom_col=NAVY, glasses=True, extras=[("collar", white())]),
    "i6": P(name="Nico", build="kid", skin="tan", hair="spiky", hair_col=BLACK, top="tshirt", top_col=tri("orange", 5),
            bottom="shorts", bottom_col=DENIM, extras=[("stripe", tri("orange", 6), 2)]),
    "a6": P(name="Paolo", build="kid", skin="morena", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("red", 5),
            bottom="shorts", bottom_col=tri("cool", 2), extras=[("flower",)] and []),
    "e2": P(name="Jun", build="kid", skin="light", hair="bangs", hair_col=DBROWN, top="tshirt", top_col=tri("amber", 5),
            bottom="shorts", bottom_col=tri("blue", 4), hat="cap", hat_col=tri("blue", 4)),
    "e8": P(name="Mr. Tan", build="man", skin="fair", hair="short", hair_col=BLACK, top="polo", top_col=tri("red", 4), bottom="pants",
            bottom_col=KHAKI, glasses=True, extras=[("apron", tri("amber", 5))], cheeks=False),
    "a3": P(name="Ms. Dela Cruz", skin="tan", hair="long", hair_col=BLACK, top="blouse", top_col=tri("violet", 5), bottom="skirt",
            bottom_col=tri("violet", 2), extras=[("dots", PAPER)]),
    "c2": P(name="Principal Reyes", build="man", skin="tan", hair="receding", hair_col=GREY, top="barong", top_col=white(),
            bottom="pants", bottom_col=BLACKP, glasses=True, cheeks=False),
    "a4": P(name="Bea", build="kid", skin="morena", hair="pony", hair_col=BLACK, top="dress", top_col=tri("violet", 5),
            bottom="dress", bottom_col=tri("violet", 5), extras=[("flower",)]),
    "s6": P(name="Ate Mia", skin="light", hair="long", hair_col=DBROWN, top="uniform", top_col=white(), bottom="skirt",
            bottom_col=tri("blue", 3), extras=[("collar", tri("blue", 4))]),
    "s7": P(name="Toto", build="kid", skin="dark", hair="curly", hair_col=BLACK, top="tshirt", top_col=tri("teal", 5),
            bottom="shorts", bottom_col=KHAKI),
    "c6": P(name="Manang Fe", skin="morena", hair="bun", hair_col=GREY, top="blouse", top_col=tri("green", 5), bottom="skirt",
            bottom_col=tri("brown", 3), hat="kerchief", hat_col=tri("red", 5), extras=[("apron", white())]),
    # ── High School ──
    "r4": P(name="Kuya Dan", build="man", skin="morena", hair="spiky", hair_col=BLACK, top="tshirt", top_col=tri("cool", 2),
            bottom="pants", bottom_col=DENIM, extras=[("towel",)], cheeks=False),
    "a8": P(name="Ate Carla", skin="tan", hair="bob", hair_col=tri("red", 2), top="tshirt", top_col=tri("amber", 6), bottom="pants",
            bottom_col=tri("teal", 3), extras=[("dots", C["pink"][5])], hat="beret", hat_col=tri("red", 4)),
    "a1": P(name="Mr. Santos", build="man", skin="tan", hair="short", hair_col=GREY, top="long", top_col=tri("blue", 2), bottom="pants",
            bottom_col=BLACKP, extras=[("tie", tri("red", 4))], glasses=True, cheeks=False),
    "s2": P(name="Liza", skin="morena", hair="pony", hair_col=BLACK, top="tshirt", top_col=tri("green", 4), bottom="shorts",
            bottom_col=KHAKI, hat="cap", hat_col=tri("green", 3)),
    "i8": P(name="Prof. Lim", build="man", skin="fair", hair="receding", hair_col=GREY, top="long", top_col=tri("warm", 5),
            bottom="pants", bottom_col=tri("brown", 3), glasses=True, beard=C["warm"][5], cheeks=False),
    "c8": P(name="Mang Tonyo", build="man", skin="dark", hair="short", hair_col=BLACK, top="sando", top_col=tri("blue", 5),
            bottom="shorts", bottom_col=tri("cool", 2), hat="cap", hat_col=tri("red", 4), moustache=C["cool"][1], cheeks=False),
    "r1": P(name="Coach Ramon", build="man", skin="morena", hair="crop", hair_col=BLACK, top="jacket", top_col=tri("red", 4),
            bottom="pants", bottom_col=NAVY, extras=[("stripe", white(), 4), ("lanyard", C["amber"][6])], cheeks=False),
    "i1": P(name="Ms. Garcia", skin="fair", hair="bun", hair_col=DBROWN, top="coat", top_col=white(), bottom="skirt",
            bottom_col=tri("cool", 2), glasses=True, extras=[("open", tri("teal", 5))]),
    "c4": P(name="Mrs. Ong", skin="fair", hair="bob", hair_col=BLACK, top="blouse", top_col=tri("pink", 5), bottom="skirt",
            bottom_col=tri("cool", 1), glasses=True, extras=[("collar", white())]),
    "r7": P(name="Joy", skin="tan", hair="pony", hair_col=BROWN, top="tshirt", top_col=tri("cyan", 5), bottom="pants",
            bottom_col=DENIM, extras=[("stripe", white(), 2)]),
    "e1": P(name="Marco", build="man", skin="light", hair="spiky", hair_col=DBROWN, top="polo", top_col=tri("amber", 5),
            bottom="pants", bottom_col=KHAKI, extras=[("apron", tri("red", 4))], cheeks=False),
    "c5": P(name="Aling Nena", skin="dark", hair="bun", hair_col=BLACK, top="long", top_col=tri("lime", 4), bottom="skirt",
            bottom_col=tri("brown", 3), hat="salakot", hat_col=tri("amber", 5)),
    "a2": P(name="Direk Paulo", build="man", skin="tan", hair="long", hair_col=BLACK, top="jacket", top_col=tri("cool", 1),
            bottom="pants", bottom_col=tri("cool", 1), hat="beret", hat_col=tri("red", 3), beard=C["cool"][1], cheeks=False),
    "s8": P(name="Lola Ising", skin="light", hair="bun", hair_col=WHITEH, top="dress", top_col=tri("violet", 4), bottom="dress",
            bottom_col=tri("violet", 4), glasses=True, extras=[("dots", PAPER)]),
    "e4": P(name="Mang Ramil", build="man", skin="morena", hair="short", hair_col=BLACK, top="tshirt", top_col=white(),
            bottom="pants", bottom_col=BLACKP, extras=[("apron", tri("cyan", 4))], moustache=C["cool"][1], cheeks=False),
    "e6": P(name="Ate Joan", skin="light", hair="long", hair_col=tri("brown", 3), top="jacket", top_col=tri("amber", 4),
            bottom="pants", bottom_col=DENIM, extras=[("open", white())]),
    # ── College ──
    "i5": P(name="Dr. Cruz", build="man", skin="tan", hair="short", hair_col=BLACK, top="coat", top_col=white(), bottom="pants",
            bottom_col=tri("cool", 2), glasses=True, extras=[("open", tri("green", 4))], cheeks=False),
    "s1": P(name="Ms. Rivera", skin="morena", hair="long", hair_col=DBROWN, top="blouse", top_col=tri("teal", 5), bottom="skirt",
            bottom_col=tri("cool", 2), extras=[("lanyard", C["blue"][4])]),
    "i7": P(name="Ana", skin="light", hair="pony", hair_col=BLACK, top="coat", top_col=white(), bottom="pants", bottom_col=DENIM,
            glasses=True, extras=[("open", tri("pink", 5))]),
    "a5": P(name="Kevin", build="man", skin="tan", hair="bangs", hair_col=BLACK, top="tshirt", top_col=tri("violet", 3),
            bottom="pants", bottom_col=DENIM, glasses=True, cheeks=False),
    "e3": P(name="Mr. Lee", build="man", skin="fair", hair="short", hair_col=BLACK, top="jacket", top_col=tri("cool", 1),
            bottom="pants", bottom_col=tri("cool", 1), extras=[("open", white()), ("tie", tri("red", 4))], cheeks=False),
    "c7": P(name="Ms. Uy", skin="fair", hair="bun", hair_col=BLACK, top="jacket", top_col=tri("blue", 3), bottom="skirt",
            bottom_col=tri("blue", 2), extras=[("open", white()), ("badge", C["amber"][6])]),
    "e5": P(name="Ms. Ria", skin="tan", hair="bob", hair_col=DBROWN, top="blouse", top_col=tri("red", 6), bottom="pants",
            bottom_col=tri("cool", 1), extras=[("lanyard", C["red"][4])]),
    "c1": P(name="Mr. Go", build="man", skin="fair", hair="receding", hair_col=BLACK, top="long", top_col=tri("cyan", 6),
            bottom="pants", bottom_col=tri("cool", 2), glasses=True, extras=[("tie", tri("blue", 3))], cheeks=False),
    "c3": P(name="Jay", build="man", skin="morena", hair="spiky", hair_col=tri("brown", 3), top="tshirt", top_col=tri("cool", 1),
            bottom="shorts", bottom_col=tri("cool", 2), hat="cap", hat_col=tri("lime", 4), cheeks=False),
    "i4": P(name="Dr. Santos", skin="tan", hair="pony", hair_col=DBROWN, top="coat", top_col=white(), bottom="pants",
            bottom_col=tri("teal", 3), extras=[("open", tri("teal", 4))]),
    "e7": P(name="Agent Mel", skin="morena", hair="long", hair_col=BLACK, top="jacket", top_col=tri("red", 4), bottom="skirt",
            bottom_col=tri("cool", 1), extras=[("open", white())]),
    "s3": P(name="Kuya Joel", build="man", skin="dark", hair="curly", hair_col=BLACK, top="jacket", top_col=tri("green", 3),
            bottom="pants", bottom_col=DENIM, extras=[("open", tri("warm", 6))], cheeks=False),
    "s5": P(name="Aling Cora", skin="morena", hair="bun", hair_col=GREY, top="dress", top_col=tri("cyan", 5), bottom="dress",
            bottom_col=tri("cyan", 5), extras=[("dots", PAPER)]),
    "r5": P(name="Mang Erning", build="man", skin="dark", hair="receding", hair_col=GREY, top="sando", top_col=white(),
            bottom="pants", bottom_col=tri("brown", 2), extras=[("towel",)], moustache=C["warm"][5], cheeks=False),
    "a7": P(name="Direk Bong", build="man", skin="tan", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("cool", 1),
            bottom="pants", bottom_col=KHAKI, hat="cap", hat_col=tri("red", 4), beard=C["cool"][1], cheeks=False),
    "r3": P(name="Engr. Paz", skin="morena", hair="pony", hair_col=BLACK, top="long", top_col=tri("orange", 5), bottom="pants",
            bottom_col=DENIM, hat="hardhat", hat_col=tri("amber", 6), extras=[("stripe", white(), 5)]),
    # ── roaming townsfolk ──
    "town0": P(name="townsfolk", build="man", skin="morena", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("blue", 4),
               bottom="shorts", bottom_col=KHAKI, cheeks=False),
    "town1": P(name="townsfolk", skin="light", hair="long", hair_col=DBROWN, top="blouse", top_col=tri("amber", 6), bottom="skirt",
               bottom_col=tri("green", 3)),
    "town2": P(name="townsfolk", build="kid", skin="tan", hair="short", hair_col=BLACK, top="tshirt", top_col=tri("green", 5),
               bottom="shorts", bottom_col=DENIM),
    "town3": P(name="townsfolk", build="kid", skin="morena", hair="pony", hair_col=BLACK, top="dress", top_col=tri("pink", 5),
               bottom="dress", bottom_col=tri("pink", 5)),
    "town4": P(name="townsfolk", build="man", skin="tan", hair="receding", hair_col=WHITEH, top="polo", top_col=tri("warm", 5),
               bottom="pants", bottom_col=tri("brown", 2), moustache=C["warm"][6], cheeks=False),
    "town5": P(name="townsfolk", skin="morena", hair="bun", hair_col=GREY, top="blouse", top_col=tri("teal", 4), bottom="skirt",
               bottom_col=tri("violet", 2), extras=[("dots", PAPER)]),
    "town6": P(name="townsfolk", build="man", skin="dark", hair="spiky", hair_col=BLACK, top="sando", top_col=tri("red", 5),
               bottom="shorts", bottom_col=tri("cool", 2), cheeks=False),
    "town7": P(name="townsfolk", skin="tan", hair="bob", hair_col=BLACK, top="tshirt", top_col=tri("violet", 5), bottom="pants",
               bottom_col=DENIM, hat="cap", hat_col=tri("cyan", 4)),
    # ── kids that only appear inside quest rooms (families, the parade band, shoppers, story time) ──
    "kid0": P(name="room kid", build="kid", skin="morena", hair="bangs", hair_col=BLACK, top="tshirt", top_col=tri("cyan", 5),
              bottom="shorts", bottom_col=KHAKI),
    "kid1": P(name="room kid", build="kid", skin="light", hair="pony", hair_col=DBROWN, top="dress", top_col=tri("amber", 6),
              bottom="dress", bottom_col=tri("amber", 6), extras=[("dots", PAPER)]),
    "kid2": P(name="room kid", build="kid", skin="dark", hair="curly", hair_col=BLACK, top="tshirt", top_col=tri("red", 5),
              bottom="shorts", bottom_col=tri("blue", 3)),
}


def contact(looks, out, sc=5):
    G = C["green"][4] + (255,)
    per = 4
    rows = (len(looks) + per - 1) // per
    cw, ch = 16 * sc * 4 + 40, 32 * sc + 40
    img = Image.new("RGBA", (per * cw + 20, rows * ch + 20), G)
    d = ImageDraw.Draw(img)
    for i, look in enumerate(looks):
        s = PEOPLE[look]
        ims = [idle_frames(s, v)[0] for v in ("down", "left", "up")]
        ims.append(ims[1].transpose(Image.FLIP_LEFT_RIGHT))
        x = 20 + (i % per) * cw
        y = 20 + (i // per) * ch
        for j, im in enumerate(ims):
            img.alpha_composite(im.resize((16 * sc, 32 * sc), Image.NEAREST), (x + j * 16 * sc, y))
        d.text((x, y + 32 * sc + 2), f"{look} {s['name']}", fill=(20, 20, 20, 255))
    img.convert("RGB").save(out)


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "preview"
    looks = sys.argv[2:] or list(PEOPLE)
    here = os.path.dirname(os.path.abspath(__file__))
    if mode == "preview":
        os.makedirs(os.path.join(here, "out"), exist_ok=True)
        contact(looks, os.path.join(here, "out", "contact.png"))
        print("contact sheet ->", os.path.join(here, "out", "contact.png"))
    elif mode == "export":
        for look in looks:
            export(PEOPLE[look], look, ROOT)
        print("exported", len(looks), "looks")
