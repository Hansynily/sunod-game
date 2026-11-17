"""Rebuilds the quest props (Assets/Art/Generated/Quests) from their original art in git.

For every prop whose committed original is 22 or 32 px/unit:
  1. snap to the Umaga palette,
  2. shrink to 16 px/unit with a dominant-colour filter (each new pixel takes the colour that
     covers most of it, dark lines win ties), so pixels stay flat and crisp instead of blurry,
  3. run the definition pass (define.py): coloured outline, top-left light, bottom-right shade.
Props listed in make_props.py are drawn by hand instead and are skipped here.

Only works while the committed version is still the original (22/32 px/unit); after that the
props are already done and the script leaves them alone.
Run from the sunod-game folder:  python Tools/art/redefine_quest_props.py
"""
import io, os, re, subprocess, sys, time
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sunod_palette import remap, PALETTE
from define import define

DIR = "Assets/Art/Generated/Quests"
PAL = np.array(PALETTE)
LUM = PAL @ np.array([0.3, 0.59, 0.11])
LUT = {(int(c[0]) << 16) | (int(c[1]) << 8) | int(c[2]): i for i, c in enumerate(PAL)}
try:
    from make_props import HAND_DRAWN
except Exception:
    HAND_DRAWN = set()


# These keep their original pixel count (22 or 32 px/unit) and only get the palette:
#   - people and animals with faces (the same grain as the townsfolk),
#   - boards, sheets and signs whose frames carry content (text, charts, maps, drawings),
#   - symbols and text (digits, speech bubbles, labels, letters).
# Shrinking any of these to 16 px/unit is what blurred faces and made writing unreadable.
PEOPLE = re.compile(
    r"^(kid_|buykid_|songkid_|choirkid_|cust_|judge_|cast\d|friend_|stunt_|lola_|lorna_|exam_cat"
    r"|rabbit_|sparrow_|fish_|crow_|gull_"
    r"|lyrics_|poster_board_|pitch_board_|riverbed_map_|tally_board_|score_|anatomy_poster|newspaper_front"
    r"|page_board_|task_board_|storyboard_|schedule_sheet|mural_|depth_|stencil_|plaque_|pic_|folder_"
    r"|digit_|lcd_|bubble_|label_|tally_\d|done_stamp|sold_banner|stamp_approved|stamp_ok|deadline_sign"
    r"|bone_label|name_tag|time_slip|receipt$|paper_tilt_)")


def git(path):
    return subprocess.run(["git", "show", "HEAD:" + path], capture_output=True).stdout


SCALE = {}
_sf = os.path.join(os.path.dirname(os.path.abspath(__file__)), "props_scale.json")
if os.path.exists(_sf):
    import json
    SCALE = json.load(open(_sf))


def shrink(im, ppu, k=1.0):
    ppu = ppu / k                                  # k times the pixels: the same as a coarser source grid
    a = np.array(remap(im))
    h, w = a.shape[:2]
    key = (a[..., 0].astype(np.int64) << 16) | (a[..., 1].astype(np.int64) << 8) | a[..., 2]
    idx = np.array([LUT.get(int(k), 0) for k in key.ravel()]).reshape(h, w)
    idx[a[..., 3] < 128] = -1
    s = 8
    ow = max(1, round(w * 16 / ppu))
    oh = max(1, round(h * ow / float(w)))
    blk = max(1, int(round(s * w / float(ow))))
    up = np.repeat(np.repeat(idx, s, 0), s, 1)
    pad = np.full((oh * blk, ow * blk), -1)
    hh, ww = min(up.shape[0], oh * blk), min(up.shape[1], ow * blk)
    pad[:hh, :ww] = up[:hh, :ww]
    out = np.zeros((oh, ow, 4), np.uint8)
    for y in range(oh):
        for x in range(ow):
            vals, cnt = np.unique(pad[y * blk:(y + 1) * blk, x * blk:(x + 1) * blk], return_counts=True)
            wgt = cnt.astype(float)
            for i, v in enumerate(vals):
                if v >= 0 and LUM[v] < 70:
                    wgt[i] *= 1.7
            v = vals[wgt.argmax()]
            if v >= 0:
                out[y, x, :3] = PAL[v]
                out[y, x, 3] = 255
    return Image.fromarray(out, "RGBA")


def soft(im):
    al = np.array(im)[..., 3]
    vis = (al > 0).sum()
    return vis > 0 and ((al > 8) & (al < 247)).sum() / vis > 0.12


done = skipped = people = 0
for f in sorted(os.listdir(DIR)):
    if not f.endswith(".png") or f[:-4] in HAND_DRAWN:
        continue
    path = DIR + "/" + f
    meta = git(path + ".meta").decode("utf-8", "ignore")
    m = re.search(r"spritePixelsToUnits: ([\d.]+)", meta)
    if not m or float(m.group(1)) not in (22.0, 32.0):
        skipped += 1
        continue
    src = Image.open(io.BytesIO(git(path))).convert("RGBA")
    if soft(src):
        skipped += 1        # glows / shadows keep the soft resample they already have
        continue
    if PEOPLE.match(f):
        out = remap(src)
        for attempt in range(20):
            try:
                out.save(path)
                break
            except OSError:
                time.sleep(0.3)
        mt = path + ".meta"
        txt = open(mt, encoding="utf-8").read()
        txt = re.sub(r"spritePixelsToUnits: [\d.]+", "spritePixelsToUnits: %g" % float(m.group(1)), txt)
        open(mt, "w", encoding="utf-8", newline=chr(10)).write(txt)
        people += 1
        continue
    k = SCALE.get(f[:-4], 1)
    out = define(shrink(src, float(m.group(1)), k), grain=False)
    world_w = src.width / float(m.group(1))        # size in the world never changes
    mt = path + ".meta"
    txt = open(mt, encoding="utf-8").read()
    txt = re.sub(r"spritePixelsToUnits: [\d.]+", "spritePixelsToUnits: %g" % (out.width / world_w), txt)
    open(mt, "w", encoding="utf-8", newline=chr(10)).write(txt)
    for attempt in range(20):           # the Editor may hold the file for a moment
        try:
            out.save(path)
            break
        except OSError:
            time.sleep(0.3)
    done += 1
print("props rebuilt:", done, " kept at original grain (people, boards, text):", people, " left alone:", skipped)
