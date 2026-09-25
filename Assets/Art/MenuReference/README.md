# SUNOD Menu UI Kit — Unity Import Guide

## Contents
- `buttons/wood_button_[normal|hover|pressed|disabled].png` — secondary buttons (CONTINUE, TUTORIAL, MY PROGRESS, SETTINGS, LOGOUT)
- `buttons/primary_button_[normal|hover|pressed|disabled].png` — the highlighted action (NEW GAME)
- `buttons/avatar_frame.png` — portrait/avatar slot frame
- `icons/icon_*.png` — white silhouette icons (continue, tutorial, new game, progress, settings, logout, avatar placeholder, star). White so you can tint them per-button via the Image component's Color field.

All PNGs are 384×176 (buttons) or 320×320 (frame) or 128×128 (icons), transparent background, exported at 4x so they stay crisp when scaled down in Unity.

## Import settings (per sprite)
1. Select the PNG in Project window.
2. Inspector → Texture Type: **Sprite (2D and UI)**.
3. Sprite Mode: **Single**.
4. Filter Mode: **Point (no filter)** — keeps pixel-art edges crisp.
5. Compression: **None** (or set per your build size budget).
6. Click **Sprite Editor** to set the 9-slice border (see below), then **Apply**.

## 9-slice border values (buttons, 384×176 canvas)
Use these numbers in the Sprite Editor's L/R/T/B border fields:
- Left: 40
- Right: 40
- Top: 40
- Bottom: 40

This keeps the rounded corners and bevel fixed while the flat center stretches, so one sprite works for a 150px-wide button or a 400px-wide one without distortion.

For `avatar_frame.png` (320×320), use border 40 on all sides too.

## Wiring up a Button component
1. Add a `Button` (UI → Button - TextMeshPro, or Legacy).
2. On the Image component: drag in `wood_button_normal` (or `primary_button_normal` for NEW GAME), set **Image Type: Sliced**, tick **Fill Center**.
3. On the Button component's **Transition**, choose **Sprite Swap**, then assign:
   - Highlighted → `*_hover`
   - Pressed → `*_pressed`
   - Disabled → `*_disabled`
4. Add a child `Image` for the icon (`icon_*`), and a `TextMeshPro - Text` for the label, laid out same as the mockup (icon above label, centered).

## Colors used (if you want to make more variants)
- Wood base: #C9631F, highlight #F3A75C, shadow #A94A1F, outline #6E2A1C
- Primary (green) base: #2F6B22, highlight #7FD858, shadow #173F10

## Notes
- Icons are intentionally plain silhouettes, not final art — swap in your own if you want more personality (e.g. a proper gear icon, a bird/sparrow motif matching your quest system).
- The avatar frame's inner well is dark navy (#102033) — drop the player's profile picture Image as a child, masked to the inner rounded rect if you want it clipped.
