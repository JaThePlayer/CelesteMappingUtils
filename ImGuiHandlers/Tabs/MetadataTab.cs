using Celeste.Mod.MappingUtils.ModIntegration;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class MetadataTab : Tab
{
    public override string Name => "Metadata";

    public override void Render(Level level)
    {
        ImGui.SetNextItemWidth(ItemWidth);
        ImGui.DragFloat("Bloom Base", ref level.Bloom.Base, 0.01f, 0f);
        ImGui.SetNextItemWidth(ItemWidth);
        ImGui.DragFloat("Bloom Strength", ref level.Bloom.Strength, 0.01f, 0f);

        if (FrostHelperAPI.GetBloomColor is { } getBloomColor && FrostHelperAPI.SetBloomColor is { } setBloomColor)
        {
            var c = getBloomColor();

            ImGui.SetNextItemWidth(ItemWidth);
            if (ImGuiExt.ColorEdit("Bloom Color [Frost Helper]", ref c, Helpers.ColorFormat.RGBA, tooltip: null))
                setBloomColor(c);
        }

        ImGui.SetNextItemWidth(ItemWidth);
        //if (ImGui.CollapsingHeader("Darkness"))
        {
            ImGui.SetNextItemWidth(ItemWidth);
            ImGui.DragFloat("Darkness Alpha", ref level.Lighting.Alpha, 0.01f, 0f);

            ImGui.SetNextItemWidth(ItemWidth);
            ImGuiExt.ColorEdit("Darkness Color", ref level.Lighting.BaseColor, Helpers.ColorFormat.RGBA, tooltip: null);
        }

        var colorgrade = level.Session.ColorGrade;
        ImGui.SetNextItemWidth(ItemWidth);
        if (ImGuiExt.Combo("Colorgrade", ref colorgrade, ColorGrades ??= GFX.ColorGrades.Textures.Keys.ToList(), x => x, ColorGradeCache))
        {
            level.SnapColorGrade(colorgrade);
        }

        #region Camera
        ImGui.Separator();
        ImGui.Text("Camera");

        var cam = level.Camera;
        // match camera offset triggers
        var offset = new NumVector2(level.CameraOffset.X / 48f, level.CameraOffset.Y / 32f);

        ImGui.SetNextItemWidth(ItemWidth);
        if (ImGui.DragFloat2("Offset", ref offset, 0.1f))
        {
            level.CameraOffset.X = offset.X * 48f;
            level.CameraOffset.Y = offset.Y * 32f;
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset"))
        {
            level.CameraOffset = Vector2.Zero;
        }

        #endregion
    }

    static List<string>? ColorGrades;
    static ComboCache<string> ColorGradeCache = new();
}
