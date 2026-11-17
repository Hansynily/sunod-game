"""Aling Rosa v6 - a different direction: cozy storybook.

 - one warm brown outline around everything (not black, not coloured per part)
 - flat 3-tone cel shading, light from the top-left
 - a friendly face: small eyes, a real smile, happy closed eyes when she blinks
 - pink daster + apron, and she fans herself with a pamaypay (the fan waves in her idle)
16x32, same sheet size as the townsfolk.
"""
import os, sys
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))   # Tools/art (palette)
from sunod_palette import C, INK, PAPER  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "rosa")
os.makedirs(OUT, exist_ok=True)
W, H = 16, 32
LINE = C["brown"][0]

# shadow, base, light
RAMP = {
    "H": [C["brown"][1], C["brown"][2], C["brown"][3]],
    "S": [C["skin"][4], C["skin"][5], C["skin"][6]],
    "D": [C["pink"][5], C["pink"][6], C["pink"][7]],
    "A": [C["warm"][5], C["warm"][6], PAPER],          # apron
    "E": [C["pink"][4], C["pink"][5], C["pink"][6]],   # ruffle hem
    "P": [C["amber"][4], C["amber"][5], C["amber"][6]],  # buri fan
    "L": [C["skin"][3], C["skin"][4], C["skin"][5]],
    "F": [C["teal"][3], C["teal"][4], C["teal"][5]],
    "X": [C["warm"][6], PAPER, PAPER],                 # sampaguita
}

FRONT = [
    "................",
    "......HHH.......",
    ".....HHHHHX.....",
    "....HHHHHHHH....",
    "...HHHHHHHHHH...",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHSSSSSSHHH..",
    "..HHSSSSSSSSHH..",
    "..HSSSSSSSSSSH..",
    "..HSSSSSSSSSSH..",
    "..HSSSSSSSSSSHPP",
    "...SSSSSSSSSSPPP",
    "....SSSSSSSS.PP.",
    "......SSSS...P..",
    "....DDDDDDDDSS..",
    "...DDDDDDDDDDS..",
    "..SDDDDDDDDDDD..",
    "..SDDAAAAAADDD..",
    "..SDDAAAAAADDD..",
    "..SDDAAAAAADD...",
    "..SSDAAAAAADD...",
    "...DDAAAAAADD...",
    "...DDAAAAAADD...",
    "..DDDDDDDDDDDD..",
    "..EEEEEEEEEEEE..",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    "....FFF..FFF....",
    "................",
    "................",
]
# fan tilted the other way (idle wave)
FRONT_FAN2 = list(FRONT)
FRONT_FAN2[11] = "..HSSSSSSSSSSH.P"
FRONT_FAN2[12] = "...SSSSSSSSSSPPP"
FRONT_FAN2[13] = "....SSSSSSSS.PPP"
FRONT_FAN2[14] = "......SSSS...P.."

BACK = [
    "................",
    ".......HHH......",
    ".....XHHHHH.....",
    "....HHHHHHHH....",
    "...HHHHHHHHHH...",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "..HHHHHHHHHHHH..",
    "...HHHHHHHHHHPPP",
    "....HHHHHHHH.PP.",
    "......SSSS...P..",
    "....DDDDDDDDSS..",
    "...DDDDDDDDDDS..",
    "..SDDDDDDDDDDD..",
    "..SDDDDDAADDDD..",
    "..SDDDDDAADDDD..",
    "..SDDDDDDDDDD...",
    "..SSDDDDDDDDD...",
    "...DDDDDDDDDD...",
    "...DDDDDDDDDD...",
    "..DDDDDDDDDDDD..",
    "..EEEEEEEEEEEE..",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    "....FFF..FFF....",
    "................",
    "................",
]
LEFT = [
    "................",
    "........HHH.....",
    ".......HHHHHX...",
    ".....HHHHHHHH...",
    "....HHHHHHHHHH..",
    "...HHHHHHHHHHHPP",
    "...HHHHHHHHHHPPP",
    "...SSSHHHHHHHHP.",
    "..SSSSSSHHHHHH..",
    ".SSSSSSSSHHHHH..",
    "..SSSSSSSSHHHH..",
    "..SSSSSSSSSHHH..",
    "...SSSSSSSSSH...",
    "....SSSSSSSS....",
    "......SSSS......",
    ".....DDDDDDD....",
    "....DDDDDDDDD...",
    "....DDDDDDDDD...",
    "...AADDSDDDDD...",
    "...AADDSDDDDD...",
    "...AADDSDDDDD...",
    "...AADDSDDDD....",
    "...AADDDDDDD....",
    "...AADDDDDDD....",
    "...DDDDDDDDDD...",
    "...EEEEEEEEEE...",
    "......LL.LL.....",
    "......LL.LL.....",
    "......LL.LL.....",
    ".....FFF.FFF....",
    "................",
    "................",
]


def check(rows):
    assert len(rows) == H, len(rows)
    for i, r in enumerate(rows):
        assert len(r) == W, (i, r)
    return [list(r) for r in rows]


def render(rows):
    m = check(rows)
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    get = lambda x, y: m[y][x] if 0 <= x < W and 0 <= y < H else "."
    for y in range(H):
        for x in range(W):
            k = m[y][x]
            if k == ".":
                continue
            r = RAMP[k]
            up, lf, dn, rt = get(x, y - 1), get(x - 1, y), get(x, y + 1), get(x + 1, y)
            v = 1
            if x >= W // 2 + 2 and k in "HDE":
                v = 0
            if up not in (".", k) and k in "SDA":
                v = 0                                   # cast shadow under hair / sleeves
            if dn == "." or rt == ".":
                v = 0
            if (up == "." or lf == ".") and v == 1 or (up == "." and lf == "."):
                v = 2
            img.putpixel((x, y), r[v] + (255,))
    px = img.load()
    for y in range(H):                                   # one warm outline all around
        for x in range(W):
            if m[y][x] == "." and any(get(x + dx, y + dy) != "." for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                if get(x - 1, y) in "LF" and get(x + 1, y) in "LF" and get(x - 1, y) != "." and get(x + 1, y) != ".":
                    continue
                px[x, y] = LINE + (255,)
    return img


def put(img, pts, col):
    for x, y in pts:
        img.putpixel((x, y), col + (255,))


def face_front(img, happy=False):
    S, Hh, P = RAMP["S"], RAMP["H"], RAMP["P"]
    put(img, [(4, 5), (5, 4), (6, 4)], Hh[2])                     # shine
    if happy:
        put(img, [(4, 10), (6, 10), (9, 10), (11, 10)], LINE)     # ^ ^
        put(img, [(5, 9), (10, 9)], LINE)
    else:
        put(img, [(5, 9), (6, 9), (9, 9), (10, 9)], Hh[0])        # brows
        put(img, [(5, 10), (10, 10)], C["warm"][7])               # eye whites
        put(img, [(6, 10), (9, 10)], INK)                         # pupils
    put(img, [(3, 11), (4, 11), (11, 11), (12, 11)], C["pink"][6])  # cheeks
    put(img, [(6, 11), (9, 11)], C["brown"][2])                  # smile
    put(img, [(7, 12), (8, 12)], C["brown"][2])
    # fan weave
    for x in range(13, 16):
        for y in range(11, 14):
            if img.getpixel((x, y))[:3] in [tuple(c) for c in P] and (x + y) % 2:
                put(img, [(x, y)], P[0])


def apron(img, back=False):
    A, D = RAMP["A"], RAMP["D"]
    if not back:
        put(img, [(6, 21), (7, 21), (8, 21), (9, 21)], A[0])      # pocket
        put(img, [(6, 18), (9, 18)], C["pink"][5])                 # straps
        for x, y in ((3, 17), (11, 18), (4, 23), (11, 23), (3, 24), (12, 24)):
            if img.getpixel((x, y))[:3] in [tuple(c) for c in D]:
                put(img, [(x, y)], PAPER)
    else:
        for x, y in ((5, 17), (10, 18), (5, 21), (10, 22), (7, 23), (4, 24), (12, 24)):
            if img.getpixel((x, y))[:3] in [tuple(c) for c in D]:
                put(img, [(x, y)], PAPER)


def build():
    frames = {}
    for i, (rows, happy) in enumerate(((FRONT, False), (FRONT_FAN2, False), (FRONT, False), (FRONT_FAN2, False), (FRONT, False), (FRONT, True))):
        im = render(rows); face_front(im, happy); apron(im)
        frames[f"down_{i}"] = im
    im = render(BACK); apron(im, back=True); frames["up"] = im
    im = render(LEFT)
    S, Hh = RAMP["S"], RAMP["H"]
    put(im, [(5, 5), (6, 4)], Hh[2])
    put(im, [(3, 8), (4, 8)], Hh[0])                              # brow
    put(im, [(3, 9)], INK); put(im, [(4, 9)], C["warm"][7])       # eye: pupil forward, white behind
    put(im, [(1, 9)], S[2])                                       # nose tip in the light
    put(im, [(3, 12)], C["brown"][2])                             # small smile, under the nose not on the lip
    put(im, [(6, 11)], C["pink"][6]); put(im, [(9, 9), (9, 10)], S[0])
    frames["left"] = im
    frames["right"] = im.transpose(Image.FLIP_LEFT_RIGHT)
    for k, v in frames.items():
        v.save(os.path.join(OUT, f"rosa_{k}.png"))
    return frames


if __name__ == "__main__":
    build()
    print("ok ->", OUT)
