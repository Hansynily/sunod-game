# Skill Slot UI Kit — Sprout Lands Palette (Unity-ready)

Generic skill-slot components, palette-matched to the Sprout Lands UI pack.
Not tied to any specific skill system (RIASEC or otherwise) — label text is
left to TextMeshPro so you can plug in whatever skill name you're using.

## Contents
- `backgrounds/skill_card_[normal|hover|pressed|disabled].png` — the card background, 240×320, 9-slice ready
- `backgrounds/icon_ring.png` — circular icon holder, 128×128
- `backgrounds/level_badge.png` — small pill for the level number, 160×64, 9-slice ready
- `backgrounds/progress_track.png` / `progress_fill.png` — 180×40 bar pieces, 9-slice ready
- `icons/icon_generic_skill.png` — plain white star silhouette, 128×128, placeholder icon (tint or replace per skill)

All transparent PNGs, exported at pixel-crisp resolution.

## Palette (sampled from the Sprout Lands reference)
- Outline (main border): `#AA7959`
- Outline (inner/dark): `#645552`
- Highlight (top-left bevel): `#F3E5C2`
- Fill (base cream): `#E8CFA6`
- Shadow (bottom-right bevel): `#C49A6C`
- Track (unfilled bar / off state): `#8C7369`
- Accent green (fill bar / active state): `#C0D470`

## Import settings
1. Texture Type: **Sprite (2D and UI)**, Sprite Mode: **Single**
2. Filter Mode: **Point (no filter)** — keeps pixel edges crisp
3. Sprite Editor → set 9-slice border:
   - `skill_card_*`: L/R/T/B = **32**
   - `level_badge`: L/R/T/B = **20**
   - `progress_track` / `progress_fill`: L/R = **18**, T/B = **16**

## Building a skill slot
1. `skill_card_normal` (Image, Sliced) as the root background. Wire hover/pressed/disabled via Sprite Swap if it's a Button, or leave static if it's just a display panel.
2. Center `icon_ring` near the top, with `icon_generic_skill` (or your own icon) as a child Image on top, tinted per skill category.
3. Below that, a TextMeshPro label for the skill name.
4. `level_badge` with a TextMeshPro "LV. X" on top.
5. `progress_track` at the bottom, with `progress_fill` as a child Image whose **width** you drive from code (0–100%) — Image Type: Filled, Fill Method: Horizontal works well for this, or just resize the RectTransform.

## Notes
- Icon is a plain star placeholder — swap in your own per-skill icons; keep them white/flat so they tint cleanly against the card.
- Card size (240×320) assumes an icon-on-top, name, badge, bar vertical layout like the earlier mockup. Resize freely — the 9-slice borders keep the frame intact.
