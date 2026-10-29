using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace XivHud.Style;

/// <summary>Shared visual vocabulary for the illustrated HUD family: ink, brass, vellum and aether.</summary>
internal static class IllustratedStyle
{
    internal static uint C(byte r, byte g, byte b, byte a = 255) => (uint)(r | g << 8 | b << 16 | a << 24);
    internal static readonly uint Ink = C(20, 32, 38), Leather = C(26, 41, 47), Gold = C(184, 153, 91), PaleGold = C(219, 193, 137);
    internal static readonly uint Ivory = C(235, 224, 200), Muted = C(161, 174, 174), Aether = C(118, 193, 190);
    internal static Vector4 V(uint c) => new((c & 255) / 255f, ((c >> 8) & 255) / 255f, ((c >> 16) & 255) / 255f, (c >> 24) / 255f);

    internal static void Diamond(ImDrawListPtr d, Vector2 c, float r, uint color, bool filled = false)
    {
        if (filled) d.AddQuadFilled(c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0), color);
        else d.AddQuad(c + new Vector2(0, -r), c + new Vector2(r, 0), c + new Vector2(0, r), c + new Vector2(-r, 0), color, 1);
    }

    internal static void Rule(ImDrawListPtr d, Vector2 a, float width, uint color, float s)
    {
        d.AddLine(a, a + new Vector2(width / 2 - 10 * s, 0), color);
        d.AddLine(a + new Vector2(width / 2 + 10 * s, 0), a + new Vector2(width, 0), color);
        Diamond(d, a + new Vector2(width / 2, 0), 4 * s, color);
    }

    internal static void Compass(ImDrawListPtr d, Vector2 c, float r, uint color)
    {
        d.AddCircle(c, r, color, 64, 1);
        d.AddCircle(c, r * .86f, color, 64, 1);
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.PI / 4;
            var v = new Vector2(MathF.Sin(a), MathF.Cos(a));
            var side = new Vector2(v.Y, -v.X);
            var reach = r * (i % 2 == 0 ? .96f : .66f);
            d.AddTriangle(c + v * reach, c + side * r * .11f, c - side * r * .11f, color, 1);
        }
        Diamond(d, c, r * .16f, color);
    }

    internal static void Corner(ImDrawListPtr d, Vector2 p, Vector2 axis, float s)
    {
        Vector2 At(float x, float y) => p + new Vector2(x * axis.X, y * axis.Y) * s;
        d.AddLine(At(0, 32), At(0, 0), Gold);
        d.AddLine(At(0, 0), At(32, 0), Gold);
        d.AddBezierCubic(At(4, 29), At(27, 27), At(11, 10), At(29, 4), Gold, 1.3f * s, 20);
        d.AddBezierCubic(At(3, 23), At(14, 22), At(16, 8), At(23, 3), Gold, .7f * s, 16);
        Diamond(d, At(4, 4), 3 * s, PaleGold, true);
    }

    internal static void Frame(ImDrawListPtr d, Vector2 p, Vector2 size, float s, bool ornament)
    {
        for (var i = 5; i >= 1; i--)
            d.AddRectFilled(p - new Vector2(i * 2 * s) + new Vector2(0, 4 * s), p + size + new Vector2(i * 2 * s), C(0, 0, 0, 12), 13 * s);
        d.AddRectFilled(p, p + size, Leather, 10 * s);
        d.AddRect(p, p + size, Gold, 10 * s, ImDrawFlags.None, s);
        d.AddRect(p + new Vector2(5 * s), p + size - new Vector2(5 * s), C(70, 78, 70), 7 * s);
        if (!ornament) return;
        for (var x = 18f; x < size.X / s - 18; x += 9)
        {
            d.AddLine(p + new Vector2(x, 9) * s, p + new Vector2(x + 3, 9) * s, C(113, 112, 86, 135));
            d.AddLine(p + new Vector2(x, size.Y / s - 9) * s, p + new Vector2(x + 3, size.Y / s - 9) * s, C(113, 112, 86, 135));
        }
        Corner(d, p + new Vector2(13, 13) * s, Vector2.One, s);
        Corner(d, p + new Vector2(size.X - 13 * s, 13 * s), new(-1, 1), s);
        Corner(d, p + new Vector2(13 * s, size.Y - 13 * s), new(1, -1), s);
        Corner(d, p + size - new Vector2(13 * s), new(-1, -1), s);
    }

    internal static void Paper(ImDrawListPtr d, Vector2 p, Vector2 size, float s, bool dark, bool ornament)
    {
        d.AddRectFilled(p + new Vector2(4, 5) * s, p + size + new Vector2(4, 5) * s, dark ? C(44, 54, 56) : C(164, 149, 118), 3 * s);
        d.AddRectFilled(p + new Vector2(2, 3) * s, p + size + new Vector2(2, 3) * s, dark ? C(53, 64, 65) : C(192, 178, 145), 3 * s);
        var a = dark ? C(43, 58, 60) : C(227, 215, 185);
        var b = dark ? C(32, 46, 50) : C(242, 232, 207);
        d.AddRectFilledMultiColor(p, p + size, a, b, b, a);
        d.AddRect(p + new Vector2(8 * s), p + size - new Vector2(8 * s), dark ? C(133, 132, 100, 100) : C(151, 127, 81, 95));
        d.AddRectFilledMultiColor(p, p + new Vector2(24 * s, size.Y), C(32, 24, 16, 66), C(32, 24, 16, 0), C(32, 24, 16, 0), C(32, 24, 16, 66));
        if (!ornament) return;
        // Subtle fibers: deterministic, clipped, independent of frame time. No external texture or scaling blur.
        d.PushClipRect(p, p + size, true);
        uint seed = 0x7a458bd;
        for (var i = 0; i < 240; i++)
        {
            seed = seed * 1664525 + 1013904223; var x = (seed & 65535) / 65535f * size.X;
            seed = seed * 1664525 + 1013904223; var y = (seed & 65535) / 65535f * size.Y;
            d.AddLine(p + new Vector2(x, y), p + new Vector2(x + (3 + i % 8) * s, y + s * .2f), dark ? C(212, 205, 172, 6) : C(97, 72, 35, 8));
        }
        Compass(d, p + new Vector2(size.X - 95 * s, size.Y - 96 * s), 67 * s,
            dark ? C(194, 180, 136, 22) : C(126, 101, 55, 27));
        d.PopClipRect();
    }
}
