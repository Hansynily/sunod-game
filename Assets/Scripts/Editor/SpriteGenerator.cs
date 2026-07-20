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

    // ── 32x32 drawing surface + primitives (top-down y) ──────────────────────
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
