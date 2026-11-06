# SUNOD UI V2 — design system (2026-11-07)

The whole game UI moves to one look: **soft cozy cards**. Reference: `C:\Users\HANY-DEV\Desktop\REFERENCE.png`
(cream rounded panels, warm brown outline, thicker bottom edge for depth, round icon buttons,
pill buttons with an icon + label, a radial skill menu). Only the UI changes; world art stays.

## Files
- `Art/UI/SunodTheme.tss`: tokens (`:root` variables) + every `sn-*` component + themed Unity
  controls + legacy `sl-*` aliases. One PanelSettings (`MainMenuPanelSettings.asset`) uses it for
  every UIDocument, so these classes work everywhere, including UI built in C#.
- `Scripts/UI/vc_UIAnim.cs`: motion (Show / Hide / Stagger / Punch / Shake / Pulse / Press / CountUp / FillTo).
- `Scripts/UI/vc_UITheme.cs`: the same colours for code, plus `MakeButton` / `MakeLabel`.
- Font: **Pixelify Sans** (`Art/Fonts/PixelifySans`, OFL). Regular for body, SemiBold for
  buttons, Bold for titles (`sn-bold`, `sn-title`, `sn-display`).

## Tokens
| token | use |
|---|---|
| `--sn-cream` 248,238,216 | panel fill |
| `--sn-cream-hi` 253,248,236 | cards inside panels, inputs |
| `--sn-cream-lo` 236,220,190 | tracks, pressed wells |
| `--sn-paper-line` 222,200,162 | dividers, card outline |
| `--sn-brown` 139,98,66 | panel / button outline |
| `--sn-brown-dark` 94,64,44 | dark panels |
| `--sn-ink` 74,52,38 | text |
| `--sn-ink-muted` 140,113,92 | secondary text |
| `--sn-green` / `-dark` | primary action, success, progress |
| `--sn-gold` / `-dark` | highlight, reward, stars, "level up", attention |
| `--sn-red` / `-dark` | destructive, failed |
| `--sn-night` | Sleep |
| `--sn-R..C` | RIASEC kinds (R red, I orange, A purple, S green, E blue, C teal) |

## Components (USS classes)
- Surfaces: `sn-panel` (main container, radius 22, border 3 + bottom 7), `sn-panel--hi`,
  `sn-panel--dark`, `sn-card` (item inside a panel), `sn-card--selected`, `sn-scrim` (full-screen
  dim backdrop that centres its child), `sn-sheet`, `sn-header`, `sn-divider`.
- Type: `sn-display` 56, `sn-title` 40, `sn-subtitle` 30, `sn-body` 26, `sn-caption` 21,
  `sn-center`, `sn-on-dark`, `sn-bold`, `sn-semibold`, `sn-medium`.
- Buttons: `sn-btn` + one variant: `--primary` (green, THE main action of a screen, max one per
  view), `--gold` (reward / claim / next stage), `--danger`, `--night` (Sleep), `--ghost`
  (tertiary: Cancel, Back, links), `--small`, `--wide`, `--icon` (96 round), `--icon-sm` (72 round).
  Icon + label: a child `VisualElement.sn-btn__icon` and a `Label.sn-btn__label`.
- Small: `sn-chip` (pill info: day, energy, stars), `sn-badge` (+`--gold`, `--silver`; Lv chips),
  `sn-progress` + child `sn-progress__fill`, `sn-check` + `sn-check--on`, `sn-attn` (gold attention).

## UX rules
1. **Hierarchy**: one primary action per view (green). Secondary = plain cream `sn-btn`.
   Cancel/Back/Close = `--ghost` or a round close icon top-right. Destructive = `--danger` and
   always behind a confirm.
2. **Touch**: every tap target at least 84 px tall (64 for `--small`, ghost); 16 px between
   targets. Nothing important within 24 px of the screen edge (safe area handled by vc_SafeArea).
3. **Type**: titles 40, body 26, caption 21; never below 18. Max ~45 characters per line in
   dialogs (use max-width on text). Sentence case on buttons ("Start day", not "START DAY").
   Short game words, no filler (Han hates wordy text): "Rec. Lv2", "Accept", "Go".
4. **Spacing**: 8 px grid. Panel padding 18/22. Gaps between groups 16-24.
5. **Colour**: text is ink on cream. Never pure white or pure black. Colour is never the only
   signal (pair it with an icon or a word).
6. **Feedback**: every button squishes on press (`vc_UIAnim.Press`, `sn-btn` also has a USS
   press state). Errors shake the field (`Shake`) + a caption in `--sn-red`. Rewards punch +
   count up.
7. **Motion** (never an instant pop-in or pop-out):
   - Modal/panel enter: scrim `Show(scrim, Enter.Fade)`, sheet `Show(sheet, Enter.Pop)` (0.32 s, ease-out-back).
   - Exit: `Hide(sheet)` 0.18 s ease-in, then the scrim. Hide fully before `display:none`.
   - Lists/grids: `Stagger(children, Enter.SlideUp)`, 0.04 s apart.
   - HUD widgets slide in from their edge (`SlideDown` for top, `SlideLeft` from the right edge, etc).
   - Toasts/stamps: `Drop` or `Pop`, auto-hide with `Hide`.
   - Attention: `Pulse` (gentle, 6%); stop it when the thing is handled.
   - Number changes: `CountUp` / `Punch`.
   - Everything runs on unscaled time, so it works on the pause menu.
8. **Consistency**: close button = round `sn-btn--icon-sm` with "×" top-right of the panel;
   dialog buttons right-to-left = primary on the right; titles centred at the top of a panel.
9. **Performance**: no blur, no per-frame layout thrash. Animate only opacity/scale/translate
   (they don't relayout). Joystick is plain sprites.

## Migrating a screen
- Replace sprite panel/button art (`background-image: url(.../Panels/*.png)`, 9-slice) with the
  sn-* classes. Old `sl-*` classes already alias to V2, but prefer real `sn-*` in new markup.
- Remove hard-coded inline colours/fonts in C# that fight the theme; use classes or `vc_UITheme`.
- Font: never set `-unity-font-definition` per screen except to pick a Pixelify weight.
- Wire motion: every place that sets `display = Flex/None` for a panel goes through
  `vc_UIAnim.Show/Hide` (keep the logic identical, only the visual changes).
- Keep every element `name` that code or the tutorial queries (`root.Q("...")`). Don't rename.
