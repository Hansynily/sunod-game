"""Works out how many pixels each quest prop should be drawn with, from how big the rooms
actually show it.

A room often shows a prop bigger or smaller than its natural size (the pond is stretched
4.5x). Drawn at 16 px/unit and then stretched, its pixels come out the wrong size. So each
prop is drawn with k times the pixels and imported at 16*k px/unit: its size in the world is
exactly the same (nothing moves, no prefab changes, safe for props that scripts spawn or
swap), but on screen its pixels match everything else.

k = the scale most rooms use for that prop. Frames or states of one prop share one k.
Input: a dump of every prop instance (made in the Editor, see ART_REWORK_LOG.md).
Output: Tools/art/props_scale.json, read by make_props.py and redefine_quest_props.py.

    python Tools/art/plan_prop_scales.py <dump.tsv>
"""
import collections, glob, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = "Assets/Art/Generated/Quests"
BOX = json.load(open(os.path.join(HERE, "props_bbox.json")))
PAIRS = [("set_blank", "painted_set"), ("stamp_up", "stamp_down"),
         ("generator_off", "generator_on_0", "generator_on_1"), ("grinder_dull", "grinder_0", "grinder_1"),
         ("warn_red", "warn_dark", "warn_green")]   # states of one prop whose names do not line up
SMALL_STEMS = {"box", "jar", "pc", "rack", "tray", "radio", "kit"}   # one-word stems that are one prop


def main(dump):
    uses = collections.defaultdict(list)          # sprite -> scales the rooms use
    for line in open(dump, encoding="utf-8"):
        f = line.rstrip("\n").split("\t")
        if f[0] != "I":
            continue
        name, wx, wy, mode = f[2], float(f[5]), float(f[6]), f[7]
        if mode != "Simple" or abs(wx - wy) > 0.03 or wx <= 0:
            continue                               # stretched unevenly or tiled: cannot be matched
        uses[name].append(round(wx * 20) / 20.0)

    names = sorted(os.path.basename(p)[:-4] for p in glob.glob(os.path.join(DIR, "*.png")))
    nameset = set(names)

    def stem(n):
        return n.rsplit("_", 1)[0] if "_" in n else n

    # Frames and states of one prop must share k. Names tell us which belong together:
    #   pot_0, pot_1 (number or single-letter endings), drum + drum_dented (the bare name exists),
    #   fit_slot_empty + fit_slot_full (a two-word stem with a few members).
    # One-word stems with many unrelated members (toy_*, sample_*, part_*) are not families.
    by_stem = collections.defaultdict(set)
    for n in names:
        by_stem[stem(n)].add(n)
    family = {}
    for n in names:
        s = stem(n)
        tail = n[len(s) + 1:] if n != s else ""
        mates = {m for m in by_stem[s] if m != s}
        if tail and (tail.isdigit() or len(tail) == 1):
            fam = {m for m in mates if m[len(s) + 1:].isdigit() or len(m[len(s) + 1:]) == 1}
            if s in nameset:
                fam.add(s)
        elif tail and s in nameset:
            fam = mates | {s}
        elif tail and "_" in s and len(mates) <= 5:
            fam = set(mates)
        elif tail and s in SMALL_STEMS:
            fam = set(mates)
        else:
            fam = {n}
        if n in nameset and n in by_stem and any(m != n for m in by_stem[n]):
            fam |= by_stem[n]
        fam.add(n)
        family[n] = fam
    for n in names:                                  # make it symmetric
        for m in list(family[n]):
            family[m] = family[m] | family[n]
    for group in PAIRS:
        both = set()
        for m in group:
            both |= family[m]
        for m in both:
            family[m] = both

    plan = {}
    for n in names:
        scales = [s for m in family[n] for s in uses.get(m, [])]
        if not scales:
            continue
        count = collections.Counter(scales)
        best = max(count.items(), key=lambda kv: (kv[1], -abs(kv[0] - 1.0)))[0]
        if abs(best - 1.0) <= 0.12:
            continue
        if best < 1.0:
            # Shrinking only pays off for big props; small ones (digits, labels) would turn to mush.
            small = min(min(BOX[m]["size"]) for m in family[n] if m in BOX) * best < 10
            if small:
                continue
        plan[n] = best
    json.dump(plan, open(os.path.join(HERE, "props_scale.json"), "w"), indent=0, sort_keys=True)
    by_k = collections.defaultdict(list)
    for n, k in plan.items():
        by_k[k].append(n)
    for k in sorted(by_k):
        print("x%-5s %s" % (k, " ".join(sorted(by_k[k]))))
    print(len(plan), "props get a matching pixel count")


if __name__ == "__main__":
    main(sys.argv[1])
