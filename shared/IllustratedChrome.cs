using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using static XivHud.Style.IllustratedStyle;

namespace XivHud.Style;

/// <summary>The same game fonts, binding and paper used by Journal and Character.</summary>
internal sealed class IllustratedChrome : IDisposable
{
    private readonly IFontHandle heading, body, small;
    internal IllustratedChrome(IDalamudPluginInterface pi)
    {
        var acquired = new List<IFontHandle>();
        try
        {
            heading = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 32)); acquired.Add(heading);
            body = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 18)); acquired.Add(body);
            small = pi.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 15)); acquired.Add(small);
        }
        catch { foreach (var font in acquired) font.Dispose(); throw; }
    }
    internal IDisposable Body() => body.Push();
    internal void Title(Vector2 p, string title, string subtitle, bool sample = false, float scale = 1)
    {
        var d = ImGui.GetWindowDrawList();
        Compass(d, p + new Vector2(40, 39) * scale, 19 * scale, PaleGold);
        Text(d, p + new Vector2(73, 12) * scale, sample ? "THE ADVENTURER'S · SAMPLE" : "THE ADVENTURER'S", 12 * scale, PaleGold, small);
        Text(d, p + new Vector2(71, 29) * scale, title, 32 * scale, Ivory, heading);
        if (subtitle.Length > 0) Text(d, p + new Vector2(74, 66) * scale, subtitle, 14 * scale, Muted, small);
    }
    internal static void BeginTheme()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, V(Ivory));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, V(Ink));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, V(Ink));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, V(Leather));
        ImGui.PushStyleColor(ImGuiCol.Border, V(Gold));
        ImGui.PushStyleColor(ImGuiCol.Button, V(Leather));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, V(C(55, 81, 84)));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, V(C(72, 104, 106)));
        ImGui.PushStyleColor(ImGuiCol.Header, V(C(55, 81, 84)));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, V(Aether));
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, V(Aether));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
    }
    internal static void EndTheme() { ImGui.PopStyleVar(2); ImGui.PopStyleColor(11); }
    private static void Text(ImDrawListPtr d, Vector2 p, string text, float size, uint colour, IFontHandle font)
    { using var scope = font.Push(); d.AddText(ImGui.GetFont(), size, p, colour, text); }
    public void Dispose() { heading.Dispose(); body.Dispose(); small.Dispose(); }
}
