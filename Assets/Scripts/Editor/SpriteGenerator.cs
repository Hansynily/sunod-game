using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// SUNOD placeholder sprite generator.
// Draws 32x32 pixel-art sprites from shape code (no image files, no external tools)
// and writes them into Assets/ with correct pixel-art import settings applied.
//
// To add a sprite: write a new draw method (see the examples below), then add one
// line to the Sprites table in Generate(). Run via the SUNOD menu.
//
// Drawing API note: the Canvas uses TOP-DOWN coordinates (y=0 is the top row), like
// the design mockups. The y-flip to Unity's bottom-up texture space happens on export.
public static class SpriteGenerator
{
    const string OUT_DIR = "Assets/Art/Generated/Quests";
    const int SIZE = 32;

    // 32x32 sprite at 32 PPU = 1 world unit. The existing project art imports at 16 PPU;
    // if generated props look half the size of the rest of the art, change this to 16.
    const int PIXELS_PER_UNIT = 32;

    // ── Palette (named so sprites read clearly) ──────────────────────────────
    static readonly Color32 Clear      = new Color32(0, 0, 0, 0);
    static readonly Color32 WoodDark   = new Color32(0x5c, 0x34, 0x16, 255);
    static readonly Color32 WoodMid    = new Color32(0x8a, 0x56, 0x1f, 255);
    static readonly Color32 WoodLight  = new Color32(0x9c, 0x64, 0x30, 255);
    static readonly Color32 WoodEdge   = new Color32(0x3a, 0x24, 0x10, 255);
    static readonly Color32 StoneMid   = new Color32(0x8b, 0x8b, 0x93, 255);
    static readonly Color32 StoneLight = new Color32(0xa9, 0xa9, 0xb0, 255);
    static readonly Color32 StoneHi    = new Color32(0xc2, 0xc2, 0xc8, 255);
    static readonly Color32 StoneDark  = new Color32(0x6d, 0x6d, 0x75, 255);
    static readonly Color32 CapMid     = new Color32(0x4a, 0x8f, 0x5c, 255);
    static readonly Color32 CapDark    = new Color32(0x2f, 0x6b, 0x40, 255);
    static readonly Color32 Glow       = new Color32(0x7f, 0xce, 0x8f, 255);
    static readonly Color32 Stem       = new Color32(0xe8, 0xe0, 0xcf, 255);
    static readonly Color32 StemShade  = new Color32(0xc9, 0xc0, 0xaa, 255);
    static readonly Color32 Paper      = new Color32(0xc9, 0xb5, 0x8f, 255);
    static readonly Color32 PaperLine  = new Color32(0xa8, 0x8f, 0x5f, 255);
    static readonly Color32 Spine      = new Color32(0x8a, 0x5a, 0x2b, 255);
    static readonly Color32 SpineDark  = new Color32(0x6d, 0x4a, 0x20, 255);
    static readonly Color32 CrackGrey  = new Color32(0x55, 0x55, 0x5c, 255);
    static readonly Color32 Fur        = new Color32(0xd8, 0xc8, 0xb0, 255);
    static readonly Color32 FurShade   = new Color32(0xb8, 0xa4, 0x88, 255);
    static readonly Color32 Ink        = new Color32(0x2a, 0x2a, 0x2a, 255);
    static readonly Color32 CanvasCol  = new Color32(0xe6, 0xe0, 0xd0, 255);
    static readonly Color32 FrameCol   = new Color32(0x6b, 0x4a, 0x2a, 255);
    static readonly Color32 PaintA     = new Color32(0xd6, 0x5a, 0x4a, 255);
    static readonly Color32 PaintB     = new Color32(0x4a, 0x7a, 0xd6, 255);
    static readonly Color32 PaintC     = new Color32(0xe0, 0xc0, 0x40, 255);
    static readonly Color32 MudDark    = new Color32(0x3d, 0x2a, 0x18, 255);
    static readonly Color32 MudMid     = new Color32(0x5a, 0x40, 0x28, 255);
    static readonly Color32 MudHi      = new Color32(0x6e, 0x50, 0x34, 255);
    static readonly Color32 LeafDark   = new Color32(0x2c, 0x5a, 0x2e, 255);
    static readonly Color32 LeafMid    = new Color32(0x3e, 0x7d, 0x3f, 255);
    static readonly Color32 LeafHi     = new Color32(0x5a, 0x9c, 0x52, 255);
    static readonly Color32 ClothMid   = new Color32(0xcf, 0xc7, 0xb4, 255);
    static readonly Color32 ClothShade = new Color32(0xac, 0xa3, 0x8d, 255);
    static readonly Color32 SkyPaint   = new Color32(0x7c, 0xb2, 0xe0, 255);
    static readonly Color32 HillPaint  = new Color32(0x6f, 0xa8, 0x4f, 255);
    static readonly Color32 SunPaint   = new Color32(0xf0, 0xd0, 0x55, 255);
    static readonly Color32 AwningRed  = new Color32(0xc4, 0x4a, 0x40, 255);
    static readonly Color32 AwningPale = new Color32(0xe8, 0xe0, 0xd4, 255);
    static readonly Color32 RadioBody  = new Color32(0x3f, 0x6e, 0x8c, 255);
    static readonly Color32 RadioDark  = new Color32(0x26, 0x44, 0x58, 255);
    static readonly Color32 Speaker    = new Color32(0x1e, 0x1e, 0x24, 255);
    static readonly Color32 DialGlow   = new Color32(0xf2, 0xc9, 0x4c, 255);
    static readonly Color32 WireRed    = new Color32(0xd0, 0x40, 0x3a, 255);
    static readonly Color32 WaterDeep  = new Color32(0x2e, 0x5f, 0x8a, 255);
    static readonly Color32 WaterMid   = new Color32(0x3f, 0x7f, 0xb0, 255);
    static readonly Color32 WaterHi    = new Color32(0x9c, 0xd0, 0xee, 255);
    static readonly Color32 FishOrange = new Color32(0xf0, 0x8a, 0x30, 255);
    static readonly Color32 Blossom    = new Color32(0xf2, 0xa6, 0xc8, 255);
    static readonly Color32 DrumRed    = new Color32(0xb8, 0x34, 0x30, 255);
    static readonly Color32 Beanbag    = new Color32(0x5a, 0xa8, 0x9a, 255);
    static readonly Color32 BeanbagDark= new Color32(0x3a, 0x7a, 0x70, 255);
    static readonly Color32 BeanbagHi  = new Color32(0x8a, 0xd0, 0xc0, 255);
    static readonly Color32 PotClay    = new Color32(0xc0, 0x68, 0x3c, 255);
    static readonly Color32 PotRim     = new Color32(0x9a, 0x4e, 0x2c, 255);
    static readonly Color32 ToyShelf   = new Color32(0xf0, 0xd8, 0xe8, 255);
    static readonly Color32 ToyShelfDark = new Color32(0xc0, 0x80, 0xa8, 255);
    static readonly Color32 TeddyBrown = new Color32(0xa8, 0x70, 0x40, 255);
    static readonly Color32 DrumRim    = new Color32(0xe8, 0xe0, 0xd4, 255);

    // ── Entry point ──────────────────────────────────────────────────────────
    [MenuItem("SUNOD/Generate Quest Sprites")]
    static void Generate()
    {
        Directory.CreateDirectory(OUT_DIR);

        var sprites = new Dictionary<string, Action<Canvas>>
        {
            { "crate",        DrawCrate },
            { "stone",        DrawStone },
            { "mushroom",     DrawMushroom },
            { "ledger",       DrawLedger },
            { "floor_broken", DrawFloorBroken },
            { "floor_new",    DrawFloorNew },
            { "poster_blank", DrawPosterBlank },
            { "poster_done",  DrawPosterDone },
            { "rabbit",       DrawRabbit },
            { "mud_patch",    DrawMudPatch },
            { "bush",         DrawBush },
            { "backdrop",     DrawBackdrop },
            { "painted_set",  DrawPaintedSet },
            { "stall",        DrawStall },
            { "notes",        DrawNotes },
            { "mural_blank",  DrawMuralBlank },
            { "mural_done",   DrawMuralDone },
            { "radio_broken", DrawRadioBroken },
            { "radio_fixed",  DrawRadioFixed },
            { "pond",         DrawPond },
            { "fish_school",  DrawFishSchool },
            { "lily_pad",     DrawLilyPad },
            { "dock",         DrawDock },
            { "drum",         DrawDrum },
            { "drum_parade",  DrawDrumParade },
            { "pennants",     DrawPennants },
            { "storybook",    DrawStorybook },
            { "bookshelf",    DrawBookshelf },
            { "beanbag",      DrawBeanbag },
            { "rug_round",    DrawRugRound },
            { "snack_jars",   DrawSnackJars },
            { "price_sign",   DrawPriceSign },
            { "cash_box",     DrawCashBox },
            { "seedling_small", DrawSeedlingSmall },
            { "seedling_tall",  DrawSeedlingTall },
            { "watering_can", DrawWateringCan },
            { "seed_tray",    DrawSeedTray },
            { "toy_shelf",    DrawToyShelf },
            { "teddy",        DrawTeddy },
            { "toy_train",    DrawToyTrain },
            { "toy_blocks",   DrawToyBlocks },
        };

        int count = 0;
        foreach (var kv in sprites)
        {
            var canvas = new Canvas();
            kv.Value(canvas);
            if (WriteSprite(kv.Key, canvas)) count++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[SpriteGen] Wrote {count}/{sprites.Count} sprites to {OUT_DIR} ({SIZE}x{SIZE}, {PIXELS_PER_UNIT} PPU, Point filter).");
    }

    // ── Sprite definitions (each is just shape code) ─────────────────────────
    static void DrawCrate(Canvas c)
    {
        c.Rect(4, 4, 24, 24, WoodDark);          // outer frame
        c.Rect(6, 6, 20, 20, WoodMid);           // inner face
        c.Rect(6, 6, 20, 2, WoodLight);          // top highlight
        c.Rect(5, 15, 22, 2, WoodDark);          // horizontal plank seam
        c.Rect(15, 6, 2, 20, WoodDark);          // vertical plank seam
        c.Line(6, 6, 25, 25, WoodEdge);          // diagonal braces
        c.Line(25, 6, 6, 25, WoodEdge);
    }

    static void DrawStone(Canvas c)
    {
        c.Ellipse(16, 20, 12, 8, StoneMid);
        c.Ellipse(14, 17, 9, 5, StoneLight);
        c.Rect(11, 14, 4, 2, StoneHi);           // top glint
        c.Rect(9, 24, 3, 2, StoneDark);          // ground contact shading
        c.Rect(20, 23, 4, 2, StoneDark);
    }

    static void DrawMushroom(Canvas c)
    {
        c.Rect(13, 17, 6, 10, Stem);
        c.Rect(13, 17, 2, 10, StemShade);
        c.EllipseTop(16, 15, 11, 7, CapMid);     // cap (upper half)
        c.Rect(5, 15, 22, 1, CapDark);           // cap underline
        c.Rect(9, 9, 3, 3, Glow);                // glow spots
        c.Rect(18, 8, 4, 3, Glow);
        c.Rect(14, 11, 3, 2, Glow);
    }

    static void DrawLedger(Canvas c)
    {
        c.Rect(6, 4, 20, 24, Paper);
        c.Rect(6, 4, 3, 24, Spine);              // spine
        for (int i = 0; i < 7; i++) c.Rect(11, 8 + i * 3, 12, 1, PaperLine);
        c.Rect(6, 4, 20, 1, SpineDark);          // top/bottom edge
        c.Rect(6, 27, 20, 1, SpineDark);
    }

    static void DrawFloorBroken(Canvas c)
    {
        c.Rect(3, 8, 26, 18, WoodMid);
        c.Rect(3, 8, 26, 1, WoodLight);
        for (int p = 0; p < 4; p++) c.Rect(3 + p * 7, 8, 1, 18, WoodDark);  // plank gaps
        c.Line(8, 10, 12, 24, CrackGrey);        // cracks
        c.Line(20, 9, 17, 23, CrackGrey);
        c.Line(14, 14, 22, 18, CrackGrey);
    }

    static void DrawFloorNew(Canvas c)
    {
        c.Rect(3, 8, 26, 18, WoodLight);
        c.Rect(3, 8, 26, 1, Paper);              // fresh sheen
        for (int p = 0; p < 4; p++) c.Rect(3 + p * 7, 8, 1, 18, WoodMid);
    }

    static void DrawPosterBlank(Canvas c)
    {
        c.Rect(6, 4, 20, 22, FrameCol);          // easel frame
        c.Rect(8, 6, 16, 18, CanvasCol);         // blank canvas
        c.Rect(14, 26, 4, 4, FrameCol);          // easel leg
    }

    static void DrawPosterDone(Canvas c)
    {
        c.Rect(6, 4, 20, 22, FrameCol);
        c.Rect(8, 6, 16, 18, CanvasCol);
        c.Rect(10, 9, 5, 5, PaintA);             // splashes of art
        c.Rect(17, 8, 5, 4, PaintB);
        c.Rect(11, 16, 10, 3, PaintC);
        c.Rect(15, 12, 3, 6, PaintB);
        c.Rect(14, 26, 4, 4, FrameCol);
    }

    static void DrawRabbit(Canvas c)
    {
        c.Ellipse(15, 20, 8, 6, Fur);            // body
        c.Ellipse(21, 15, 4, 4, Fur);            // head
        c.Rect(20, 8, 2, 6, Fur);                // ears
        c.Rect(23, 8, 2, 6, Fur);
        c.Rect(20, 8, 2, 6, FurShade);
        c.Rect(8, 19, 3, 3, Fur);                // tail
        c.Set(23, 15, Ink);                      // eye
    }

    static void DrawMudPatch(Canvas c)
    {
        c.Ellipse(16, 19, 13, 8, MudMid);        // muddy gap
        c.Ellipse(15, 18, 10, 6, MudDark);       // deep wet center
        c.Rect(9, 13, 4, 2, MudHi);              // damp glints
        c.Rect(20, 15, 3, 2, MudHi);
        c.Rect(13, 22, 5, 2, MudDark);
        c.Ellipse(24, 21, 3, 2, MudMid);         // splatter to the side
        c.Ellipse(7, 22, 2, 2, MudMid);
    }

    static void DrawBush(Canvas c)
    {
        c.Ellipse(11, 20, 7, 6, LeafDark);       // clustered foliage lobes
        c.Ellipse(21, 20, 7, 6, LeafDark);
        c.Ellipse(16, 16, 9, 7, LeafMid);
        c.Ellipse(16, 14, 6, 5, LeafHi);         // top highlight
        c.Rect(9, 12, 2, 2, LeafHi);             // scattered leaf glints
        c.Rect(22, 13, 2, 2, LeafHi);
        c.Rect(15, 25, 2, 3, LeafDark);          // little trunk shadow
    }

    static void DrawBackdrop(Canvas c)
    {
        c.Rect(4, 3, 24, 2, FrameCol);           // top rail the cloth hangs from
        c.Rect(5, 5, 22, 21, ClothMid);          // plain hanging cloth
        c.Rect(5, 5, 2, 21, ClothShade);         // fold shading
        c.Rect(15, 5, 2, 21, ClothShade);
        c.Rect(5, 24, 22, 2, ClothShade);        // bottom hem
    }

    static void DrawPaintedSet(Canvas c)
    {
        c.Rect(4, 3, 24, 2, FrameCol);
        c.Rect(5, 5, 22, 21, SkyPaint);          // painted sky
        c.EllipseTop(9, 15, 4, 4, SunPaint);     // sun
        c.Rect(5, 18, 22, 8, HillPaint);         // rolling hills
        c.Ellipse(13, 18, 6, 3, HillPaint);      // hill mound
        c.Ellipse(22, 19, 5, 3, HillPaint);
        c.Rect(5, 24, 22, 2, ClothShade);        // hem
    }

    static void DrawStall(Canvas c)
    {
        // Striped awning
        for (int i = 0; i < 6; i++)
            c.Rect(3 + i * 4, 4, 4, 5, (i % 2 == 0) ? AwningRed : AwningPale);
        c.Rect(3, 9, 26, 1, WoodEdge);           // awning trim
        c.Rect(5, 9, 2, 15, WoodDark);           // support posts
        c.Rect(25, 9, 2, 15, WoodDark);
        c.Rect(4, 20, 24, 6, WoodMid);           // counter
        c.Rect(4, 20, 24, 1, WoodLight);         // counter edge highlight
        c.Rect(9, 15, 4, 4, PaintA);             // goods on display
        c.Rect(14, 14, 4, 5, PaintC);
        c.Rect(19, 15, 4, 4, PaintB);
    }

    static void DrawNotes(Canvas c)
    {
        c.Rect(7, 5, 18, 22, Paper);             // loose sheet of notes
        c.Rect(7, 5, 18, 1, PaperLine);          // top edge
        c.Rect(7, 26, 18, 1, PaperLine);         // bottom edge
        for (int i = 0; i < 6; i++)              // scribbled lines
            c.Rect(10, 9 + i * 3, 12, 1, Ink);
        c.Rect(10, 9, 6, 1, Ink);                // a shorter "heading" line
        c.Rect(19, 23, 4, 2, PaintB);            // small diagram/sketch mark
    }

    static void DrawMuralBlank(Canvas c)
    {
        c.Rect(2, 2, 28, 28, StoneMid);          // plain cracked wall face
        c.Rect(2, 2, 28, 2, StoneLight);         // top edge highlight
        c.Rect(2, 28, 28, 2, StoneDark);         // bottom edge shading
        c.Line(6, 5, 11, 20, CrackGrey);         // cracks
        c.Line(18, 4, 14, 16, CrackGrey);
        c.Line(20, 18, 26, 27, CrackGrey);
        c.Rect(9, 9, 3, 2, StoneDark);           // worn patches
        c.Rect(21, 12, 3, 2, StoneDark);
    }

    static void DrawMuralDone(Canvas c)
    {
        c.Rect(2, 2, 28, 28, StoneMid);          // same wall, now painted over
        c.Rect(4, 4, 24, 24, SkyPaint);          // painted sky
        c.EllipseTop(10, 10, 4, 4, SunPaint);    // sun
        c.Rect(4, 20, 24, 8, HillPaint);         // rolling hills
        c.Ellipse(12, 20, 6, 3, HillPaint);      // hill mound
        c.Ellipse(22, 21, 5, 3, HillPaint);
        c.Rect(2, 2, 28, 2, StoneLight);         // frame edges kept from the wall
        c.Rect(2, 28, 28, 2, StoneDark);
    }

    // ── 32x32 drawing surface + primitives (top-down y) ──────────────────────
    static void DrawRadioBroken(Canvas c)
    {
        c.Rect(5, 12, 22, 14, RadioDark);        // open case (no front)
        c.Rect(7, 14, 18, 10, Speaker);          // exposed insides
        c.Rect(9, 16, 5, 3, CapMid);             // circuit board
        c.Line(14, 17, 22, 22, WireRed);         // loose wires
        c.Line(12, 22, 20, 15, PaintC);
        c.Rect(18, 6, 2, 6, StoneMid);           // bent antenna
        c.Line(20, 6, 25, 3, StoneMid);
        c.Rect(2, 27, 4, 3, RadioBody);          // loose parts on the table
        c.Rect(26, 27, 3, 3, StoneLight);
        c.Rect(12, 28, 3, 2, CapMid);
    }

    static void DrawRadioFixed(Canvas c)
    {
        c.Rect(5, 12, 22, 14, RadioDark);        // case outline
        c.Rect(6, 13, 20, 12, RadioBody);        // front
        c.Ellipse(12, 19, 4, 4, Speaker);        // speaker
        c.Rect(18, 15, 6, 3, DialGlow);          // lit dial
        c.Rect(18, 20, 2, 2, StoneHi);           // knobs
        c.Rect(22, 20, 2, 2, StoneHi);
        c.Line(21, 12, 27, 3, StoneLight);       // straight antenna
    }

    static void DrawPond(Canvas c)
    {
        c.Ellipse(16, 16, 15, 13, StoneDark);    // muddy rim
        c.Ellipse(16, 16, 14, 12, WaterDeep);
        c.Ellipse(15, 14, 11, 8, WaterMid);
        c.Rect(8, 10, 5, 1, WaterHi);            // ripples
        c.Rect(18, 19, 6, 1, WaterHi);
        c.Rect(12, 23, 4, 1, WaterHi);
    }

    static void DrawFishSchool(Canvas c)
    {
        c.Ellipse(16, 16, 11, 9, WaterMid);      // ripple patch so it reads on any water
        c.Ellipse(16, 16, 10, 8, WaterDeep);
        DrawFish(c, 9, 12);
        DrawFish(c, 17, 17);
        DrawFish(c, 11, 21);
        c.Rect(20, 9, 3, 1, WaterHi);
    }

    static void DrawFish(Canvas c, int x, int y)
    {
        c.Ellipse(x + 3, y, 3, 2, FishOrange);
        c.Rect(x + 5, y - 1, 1, 1, Ink);         // eye
        c.Line(x - 1, y - 2, x - 1, y + 2, FishOrange); // tail
        c.Set(x, y, FishOrange);
    }

    static void DrawLilyPad(Canvas c)
    {
        c.Ellipse(16, 18, 10, 7, LeafMid);
        c.Ellipse(14, 16, 6, 4, LeafHi);
        c.Line(16, 18, 24, 13, LeafDark);        // notch
        c.Ellipse(20, 14, 2, 2, Blossom);
    }

    static void DrawDock(Canvas c)
    {
        c.Rect(2, 8, 28, 16, WoodDark);
        for (int i = 0; i < 4; i++) c.Rect(3, 9 + i * 4, 26, 3, i % 2 == 0 ? WoodMid : WoodLight);
        c.Rect(4, 24, 3, 6, WoodEdge);           // posts
        c.Rect(25, 24, 3, 6, WoodEdge);
    }

    static void DrawDrum(Canvas c)
    {
        c.Line(10, 30, 13, 22, WoodEdge);        // stand legs
        c.Line(22, 30, 19, 22, WoodEdge);
        c.Rect(6, 12, 20, 11, DrumRed);          // shell
        c.Rect(6, 12, 20, 2, DrumRim);
        c.Rect(6, 21, 20, 2, DrumRim);
        c.Ellipse(16, 11, 10, 3, CanvasCol);     // drum head
        for (int x = 8; x < 26; x += 4) c.Line(x, 14, x + 2, 20, StoneLight); // tension cords
        c.Line(23, 4, 28, 9, WoodLight);         // sticks resting
        c.Line(25, 3, 29, 7, WoodLight);
    }

    static void DrawDrumParade(Canvas c)
    {
        c.Line(10, 30, 13, 22, WoodEdge);
        c.Line(22, 30, 19, 22, WoodEdge);
        c.Rect(6, 14, 20, 11, DrumRed);
        c.Rect(6, 14, 20, 2, PaintC);            // gold rims, dressed up for the parade
        c.Rect(6, 23, 20, 2, PaintC);
        c.Ellipse(16, 13, 10, 3, CanvasCol);
        c.Rect(12, 17, 8, 4, PaintB);            // star badge
        c.Line(6, 16, 2, 26, PaintB);            // ribbons
        c.Line(26, 16, 30, 26, PaintC);
        c.Line(9, 8, 13, 12, WoodLight);         // sticks mid-hit
        c.Line(23, 8, 19, 12, WoodLight);
        c.Rect(3, 2, 2, 4, Ink); c.Rect(5, 2, 2, 1, Ink); c.Rect(2, 5, 2, 2, Ink); // music notes
        c.Rect(27, 1, 1, 4, Ink); c.Rect(26, 4, 2, 2, Ink);
    }

    static void DrawPennants(Canvas c)
    {
        c.Line(0, 4, 31, 8, Ink);                // string
        Color32[] cols = { DrumRed, PaintC, PaintB, HillPaint };
        for (int i = 0; i < 5; i++)
        {
            int x = 2 + i * 6, y = 5 + i;
            for (int r = 0; r < 6; r++) c.Rect(x + r / 2, y + r, 5 - r, 1, cols[i % cols.Length]);
        }
    }

    static void DrawStorybook(Canvas c)
    {
        c.Rect(4, 10, 24, 16, SpineDark);        // open cover
        c.Rect(5, 11, 10, 13, CanvasCol);        // left page
        c.Rect(17, 11, 10, 13, CanvasCol);       // right page
        c.Rect(15, 10, 2, 16, Spine);            // spine
        c.Ellipse(10, 15, 3, 2, SunPaint);       // picture: sun
        c.Rect(6, 19, 8, 3, HillPaint);          // picture: hill
        for (int y = 13; y < 23; y += 3) c.Rect(18, y, 8, 1, PaperLine); // text lines
    }

    static void DrawBookshelf(Canvas c)
    {
        c.Rect(3, 1, 26, 30, WoodDark);
        c.Rect(5, 3, 22, 26, WoodMid);
        Color32[] books = { PaintA, PaintB, PaintC, HillPaint, Blossom, RadioBody };
        for (int shelf = 0; shelf < 3; shelf++)
        {
            int y = 3 + shelf * 9;
            c.Rect(5, y + 7, 22, 2, WoodDark);   // shelf plank
            for (int b = 0; b < 7; b++) c.Rect(6 + b * 3, y + 1 + (b % 2), 2, 6 - (b % 2), books[(b + shelf) % books.Length]);
        }
    }

    static void DrawBeanbag(Canvas c)
    {
        c.Ellipse(16, 20, 12, 9, BeanbagDark);
        c.Ellipse(15, 18, 10, 7, Beanbag);
        c.Ellipse(12, 15, 4, 2, BeanbagHi);
    }

    static void DrawRugRound(Canvas c)
    {
        c.Ellipse(16, 16, 15, 11, PaintB);
        c.Ellipse(16, 16, 12, 8, PaintC);
        c.Ellipse(16, 16, 8, 5, PaintA);
        c.Ellipse(16, 16, 4, 2, CanvasCol);
    }

    static void DrawSnackJars(Canvas c)
    {
        c.Rect(1, 24, 30, 4, WoodMid);           // tray
        DrawJar(c, 4, PaintC);                   // banana chips
        DrawJar(c, 13, PaintA);                  // candies
        DrawJar(c, 22, MudHi);                   // cookies
    }

    static void DrawJar(Canvas c, int x, Color32 fill)
    {
        c.Rect(x, 12, 7, 12, StoneHi);
        c.Rect(x + 1, 15, 5, 8, fill);
        c.Rect(x + 1, 10, 5, 2, AwningRed);      // lid
        c.Rect(x + 1, 13, 1, 9, CanvasCol);      // glass shine
    }

    static void DrawPriceSign(Canvas c)
    {
        c.Rect(15, 18, 2, 13, WoodDark);         // post
        c.Rect(3, 4, 26, 15, WoodEdge);
        c.Rect(4, 5, 24, 13, CanvasCol);
        c.Rect(6, 7, 3, 9, PaintA);              // "P5" price, chunky letters
        c.Rect(9, 7, 3, 3, PaintA);
        c.Rect(9, 10, 1, 3, CanvasCol);
        c.Rect(14, 7, 6, 2, PaintB); c.Rect(14, 9, 2, 3, PaintB); c.Rect(14, 11, 6, 2, PaintB);
        c.Rect(18, 13, 2, 2, PaintB); c.Rect(14, 15, 6, 1, PaintB);
        c.Ellipse(24, 11, 2, 2, SunPaint);       // coin
    }

    static void DrawCashBox(Canvas c)
    {
        c.Rect(5, 14, 22, 12, RadioDark);
        c.Rect(6, 15, 20, 10, RadioBody);
        c.Rect(13, 18, 6, 2, Speaker);           // coin slot
        c.Ellipse(9, 11, 3, 2, SunPaint);        // coins on top
        c.Ellipse(15, 10, 3, 2, DialGlow);
        c.Rect(20, 9, 6, 4, HillPaint);          // paper bill
    }

    static void DrawPot(Canvas c)
    {
        c.Rect(9, 22, 14, 3, PotRim);
        c.Rect(10, 25, 12, 6, PotClay);
        c.Rect(11, 21, 10, 2, MudMid);           // soil
    }

    static void DrawSeedlingSmall(Canvas c)
    {
        DrawPot(c);
        c.Rect(15, 16, 2, 5, LeafMid);           // short stem
        c.Ellipse(13, 16, 2, 1, LeafHi);
        c.Ellipse(18, 15, 2, 1, LeafHi);
    }

    static void DrawSeedlingTall(Canvas c)
    {
        DrawPot(c);
        c.Rect(15, 4, 2, 17, LeafMid);           // tall stem, grew in the sun
        c.Ellipse(12, 14, 3, 2, LeafHi);
        c.Ellipse(20, 10, 3, 2, LeafHi);
        c.Ellipse(12, 6, 3, 2, LeafMid);
        c.Ellipse(16, 3, 2, 2, SunPaint);        // bud
    }

    static void DrawWateringCan(Canvas c)
    {
        c.Rect(8, 14, 14, 12, RadioBody);
        c.Rect(8, 14, 14, 2, WaterHi);
        c.Line(22, 18, 29, 11, RadioBody);       // spout
        c.Line(22, 19, 29, 12, RadioDark);
        c.Rect(27, 9, 4, 3, RadioDark);
        c.Line(9, 14, 13, 8, RadioDark);         // handle
        c.Line(13, 8, 19, 8, RadioDark);
        c.Line(19, 8, 21, 14, RadioDark);
    }

    static void DrawSeedTray(Canvas c)
    {
        c.Rect(2, 16, 28, 10, WoodDark);
        c.Rect(3, 17, 26, 8, MudMid);
        for (int i = 0; i < 6; i++) { c.Rect(5 + i * 4, 14, 1, 4, LeafMid); c.Set(4 + i * 4, 14, LeafHi); c.Set(6 + i * 4, 14, LeafHi); }
    }

    static void DrawToyShelf(Canvas c)
    {
        c.Rect(2, 2, 28, 28, ToyShelfDark);
        c.Rect(4, 4, 24, 24, ToyShelf);
        c.Rect(4, 14, 24, 2, ToyShelfDark);
        c.Ellipse(9, 10, 3, 3, PaintA);          // ball
        c.Rect(15, 8, 4, 5, PaintC);             // box
        c.Rect(21, 7, 5, 6, PaintB);             // robot
        c.Rect(22, 8, 1, 1, CanvasCol); c.Rect(24, 8, 1, 1, CanvasCol);
        c.Rect(6, 20, 6, 6, HillPaint);          // blocks
        c.Ellipse(19, 23, 4, 3, Fur);            // plush
        c.Ellipse(17, 19, 1, 1, Fur); c.Ellipse(21, 19, 1, 1, Fur);
    }

    static void DrawTeddy(Canvas c)
    {
        c.Ellipse(16, 21, 7, 7, TeddyBrown);     // body
        c.Ellipse(16, 11, 6, 5, TeddyBrown);     // head
        c.Ellipse(11, 7, 2, 2, TeddyBrown);      // ears
        c.Ellipse(21, 7, 2, 2, TeddyBrown);
        c.Ellipse(16, 22, 4, 4, Fur);            // tummy
        c.Ellipse(16, 13, 2, 1, Fur);            // snout
        c.Set(14, 10, Ink); c.Set(18, 10, Ink); c.Set(16, 12, Ink);
        c.Rect(13, 16, 6, 2, PaintA);            // bow
    }

    static void DrawToyTrain(Canvas c)
    {
        c.Rect(3, 14, 12, 9, PaintA);            // engine
        c.Rect(9, 9, 6, 6, PaintA);              // cab
        c.Rect(10, 10, 4, 3, WaterHi);
        c.Rect(4, 10, 3, 4, Ink);                // chimney
        c.Rect(17, 15, 11, 8, PaintB);           // wagon
        c.Rect(15, 19, 2, 1, Ink);               // coupler
        foreach (int x in new[] { 6, 12, 20, 25 }) c.Ellipse(x, 24, 2, 2, Ink);
    }

    static void DrawToyBlocks(Canvas c)
    {
        c.Rect(4, 18, 10, 10, PaintA);  c.Rect(6, 20, 6, 6, CanvasCol); c.Rect(8, 21, 2, 4, PaintA);
        c.Rect(16, 18, 10, 10, PaintB); c.Rect(18, 20, 6, 6, CanvasCol); c.Rect(19, 21, 4, 4, PaintB);
        c.Rect(10, 7, 10, 10, PaintC);  c.Rect(12, 9, 6, 6, CanvasCol); c.Rect(14, 10, 2, 4, PaintC);
    }

    class Canvas
    {
        public readonly Color32[] Px = new Color32[SIZE * SIZE];

        public Canvas()
        {
            for (int i = 0; i < Px.Length; i++) Px[i] = Clear;
        }

        public void Set(int x, int y, Color32 col)
        {
            if (x < 0 || x >= SIZE || y < 0 || y >= SIZE) return;
            Px[y * SIZE + x] = col;
        }

        public void Rect(int x, int y, int w, int h, Color32 col)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Set(x + i, y + j, col);
        }

        public void Ellipse(int cx, int cy, int rx, int ry, Color32 col)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float nx = rx == 0 ? 0 : (float)x / rx;
                    float ny = ry == 0 ? 0 : (float)y / ry;
                    if (nx * nx + ny * ny <= 1f) Set(cx + x, cy + y, col);
                }
        }

        // Upper half of an ellipse - for domed caps.
        public void EllipseTop(int cx, int cy, int rx, int ry, Color32 col)
        {
            for (int y = -ry; y <= 0; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float nx = rx == 0 ? 0 : (float)x / rx;
                    float ny = ry == 0 ? 0 : (float)y / ry;
                    if (nx * nx + ny * ny <= 1f) Set(cx + x, cy + y, col);
                }
        }

        // Bresenham line.
        public void Line(int x0, int y0, int x1, int y1, Color32 col)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }

    // ── Export: build texture (with y-flip), write PNG, apply import settings ─
    static bool WriteSprite(string name, Canvas canvas)
    {
        try
        {
            var flipped = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                    flipped[(SIZE - 1 - y) * SIZE + x] = canvas.Px[y * SIZE + x];

            var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.SetPixels32(flipped);
            tex.Apply();

            string path = $"{OUT_DIR}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ApplyImportSettings(path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SpriteGen] Failed to write '{name}': {e.Message}");
            return false;
        }
    }

    static void ApplyImportSettings(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        importer.SaveAndReimport();
    }
}
