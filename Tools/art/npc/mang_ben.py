"""Mang Ben (R2, River Crossing) - storybook townsperson.

An older fisherman: woven salakot, grey hair and moustache, sun-dark skin, white sando,
rolled denim shorts, rubber tsinelas.
"""
import os
from PIL import Image
from npc_style import C, INK, PAPER, LINE, EYE_WHITE, render, put, bob

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out", "ben")
os.makedirs(OUT, exist_ok=True)

RAMP = {
    "K": [C["amber"][4], C["amber"][5], C["amber"][6]],      # salakot (buri straw)
    "G": [C["warm"][4], C["warm"][5], C["warm"][6]],         # grey hair
    "S": [C["skin"][3], C["skin"][4], C["skin"][5]],         # sun-dark skin
    "W": [C["warm"][6], C["warm"][7], PAPER],                # white sando
    "B": [C["blue"][3], C["blue"][4], C["blue"][5]],         # denim shorts
    "L": [C["skin"][3], C["skin"][3], C["skin"][4]],
    "F": [C["cool"][1], C["cool"][2], C["cool"][3]],         # rubber tsinelas
}

FRONT = [
    "................",
    ".......KK.......",
    "......KKKK......",
    "....KKKKKKKK....",
    "..KKKKKKKKKKKK..",
    ".KKKKKKKKKKKKKK.",
    "...GSSSSSSSSG...",
    "...GSSSSSSSSG...",
    "...GSSSSSSSSG...",
    "...SSSSSSSSSS...",
    "...SSSSSSSSSS...",
    "....SSSSSSSS....",
    ".....SSSSSS.....",
    "......SSSS......",
    "...SSWWWWWWSS...",
    "..SSSWWWWWWSSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSBBBBBBBBSS..",
    "...BBBBBBBBBB...",
    "...BBBBBBBBBB...",
    "...BBBB..BBBB...",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    "....FFF..FFF....",
    "................",
    "................",
]
BACK = [
    "................",
    ".......KK.......",
    "......KKKK......",
    "....KKKKKKKK....",
    "..KKKKKKKKKKKK..",
    ".KKKKKKKKKKKKKK.",
    "...GGGGGGGGGG...",
    "...GGGGGGGGGG...",
    "...GGGGGGGGGG...",
    "...GGGGGGGGGG...",
    "...SGGGGGGGGS...",
    "....GGGGGGGG....",
    ".....SSSSSS.....",
    "......SSSS......",
    "...SSWWWWWWSS...",
    "..SSSWWWWWWSSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSWWWWWWWWSS..",
    "..SSBBBBBBBBSS..",
    "...BBBBBBBBBB...",
    "...BBBBBBBBBB...",
    "...BBBB..BBBB...",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    ".....LL..LL.....",
    "....FFF..FFF....",
    "................",
    "................",
]
LEFT = [
    "................",
    "........KK......",
    ".......KKKK.....",
    ".....KKKKKKKK...",
    "...KKKKKKKKKKKK.",
    ".KKKKKKKKKKKKKK.",
    "...SSSSSGGGG....",
    "..SSSSSSGGGG....",
    ".SSSSSSSSGGG....",
    "..SSSSSSSSGG....",
    "..SSSSSSSSG.....",
    "...SSSSSSSS.....",
    "....SSSSSS......",
    "......SSSS......",
    ".....WWSSWW.....",
    ".....WWSSWWW....",
    ".....WWSSWWW....",
    ".....WWSSWWW....",
    ".....WWSSWWW....",
    ".....WWSSWWW....",
    ".....WWSSWWW....",
    ".....BBSSBBB....",
    ".....BBBBBBB....",
    ".....BBBBBBB....",
    ".....BBB.BBB....",
    "......LL.LL.....",
    "......LL.LL.....",
    "......LL.LL.....",
    "......LL.LL.....",
    ".....FFF.FFF....",
    "................",
    "................",
]

FAR, CAST = "KGWB", "SW"


def straw(img, top=1, bottom=5):
    """Woven salakot: a few darker weave rings and a dark tip knob."""
    K = RAMP["K"]
    for y in range(top, bottom + 1):
        for x in range(16):
            p = img.getpixel((x, y))
            if p[3] and p[:3] in [tuple(c) for c in K] and (x + y * 2) % 4 == 0:
                put(img, [(x, y)], K[0])


def front(blink=False):
    im = render(FRONT, RAMP, FAR, CAST)
    straw(im)
    put(im, [(7, 1), (8, 1)], C["amber"][3])                      # tip knob
    put(im, [(4, 6), (5, 6), (6, 6), (7, 6), (8, 6), (9, 6), (10, 6), (11, 6)], RAMP["S"][0])   # brim shade on the face
    S, G = RAMP["S"], RAMP["G"]
    if blink:
        put(im, [(5, 9), (6, 9), (9, 9), (10, 9)], S[0])
    else:
        put(im, [(5, 8), (6, 8), (9, 8), (10, 8)], G[0])          # bushy grey brows
        put(im, [(5, 9), (10, 9)], EYE_WHITE)
        put(im, [(6, 9), (9, 9)], INK)
    put(im, [(7, 10)], S[0])                                      # nose
    put(im, [(5, 11), (6, 11), (7, 11), (8, 11), (9, 11), (10, 11)], G[2])   # moustache
    put(im, [(5, 11), (10, 11)], G[1])
    put(im, [(7, 12), (8, 12)], S[0])                             # smile under it
    put(im, [(4, 10), (11, 10)], C["pink"][4])                    # weathered cheeks
    put(im, [(6, 14), (9, 14)], RAMP["W"][1])                     # sando straps
    return im


def back():
    im = render(BACK, RAMP, FAR, CAST)
    straw(im)
    put(im, [(7, 1), (8, 1)], C["amber"][3])
    G = RAMP["G"]
    put(im, [(5, 7), (8, 8), (10, 7), (6, 9), (9, 10), (4, 9)], G[0])   # short cropped strands
    put(im, [(4, 6), (5, 6), (6, 6), (7, 6), (8, 6), (9, 6), (10, 6), (11, 6)], G[0])  # brim shadow
    return im


def left(blink=False):
    im = render(LEFT, RAMP, FAR, CAST)
    straw(im)
    put(im, [(8, 1), (9, 1)], C["amber"][3])
    S, G = RAMP["S"], RAMP["G"]
    put(im, [(3, 6), (4, 6), (5, 6), (6, 6), (7, 6)], S[0])       # brim shade
    put(im, [(3, 7), (4, 7)], G[0])                               # brow
    if blink:
        put(im, [(3, 8)], S[0])
    else:
        put(im, [(3, 8)], INK); put(im, [(4, 8)], EYE_WHITE)
    put(im, [(1, 8)], S[2])                                       # nose tip
    put(im, [(2, 10), (3, 10), (4, 10)], G[2])                    # moustache, under the nose
    put(im, [(8, 8), (8, 9)], S[0])                               # ear
    return im


def build():
    frames = {
        "down": [front(), front(), bob(front()), bob(front()), front(), front(blink=True)],
        "up": [back(), back(), bob(back()), bob(back()), back(), back()],
        "left": [left(), left(), bob(left()), bob(left()), left(), left(blink=True)],
    }
    frames["right"] = [im.transpose(Image.FLIP_LEFT_RIGHT) for im in frames["left"]]
    for v, lst in frames.items():
        for i, im in enumerate(lst):
            im.save(os.path.join(OUT, f"ben_{v}_{i}.png"))
    return frames


if __name__ == "__main__":
    build()
    print("ok ->", OUT)
