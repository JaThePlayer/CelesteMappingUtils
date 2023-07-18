using Celeste.Mod.MappingUtils.Cheats;
using Celeste.Mod.MappingUtils.ModIntegration;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public class MainMappingUtils : ImGuiHandler
{
    public static bool Enabled;

    public const int ItemWidth = 150;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (MappingUtilsModule.Settings?.OpenMenu?.Pressed ?? false)
        {
            Enabled = !Enabled;
            MappingUtilsModule.Settings?.OpenMenu.ConsumePress();
            MappingUtilsModule.Settings?.OpenMenu.ConsumeBuffer();
        }
    }

    public override void Render()
    {
        base.Render();

        FrostHelperAPI.LoadIfNeeded();

        if (Engine.Scene is not Level level || !Enabled)
        {
            return;
        }

        var open = true;
        ImGui.SetNextWindowSize(new(ItemWidth * 2.5f, -1f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Mapping Utils", ref open);
        if (ImGui.BeginTabBar("a", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            Metadata(level);
            Flags(level);
            Cheats(level);
            StylegroundViewTab.Stylegrounds(level);
            Entities(level);

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    static Color Entities_CustomSpinnerTint = Color.White;
    static Color Entities_CustomSpinnerBorderColor = Color.Black;
    static void Entities(Level level)
    {
        if (!ImGui.BeginTabItem("Entities"))
            return;

        if (FrostHelperAPI.EntityNameToType is { } entityNameToType && FrostHelperAPI.SetCustomSpinnerColor is { } setTint && FrostHelperAPI.SetCustomSpinnerBorderColor is { } setBorderColor)
        {
            if (entityNameToType("FrostHelper/IceSpinner") is { } customSpinnerType && level.Tracker.Entities.TryGetValue(customSpinnerType, out var spinners) && spinners.Count > 0)
            {
                ImGui.Text("Frost Helper Custom Spinners");

                if (ImGuiExt.ColorEdit("Tint", ref Entities_CustomSpinnerTint, Helpers.ColorFormat.RGBA, "Change the color of all Frost Helper custom spinners"))
                {
                    foreach (var item in spinners)
                    {
                        setTint(item, Entities_CustomSpinnerTint);
                    }
                }

                if (ImGuiExt.ColorEdit("Border Color", ref Entities_CustomSpinnerBorderColor, Helpers.ColorFormat.RGBA, "Change the border color of all Frost Helper custom spinners"))
                {
                    foreach (var item in spinners)
                    {
                        setBorderColor(item, Entities_CustomSpinnerBorderColor);
                    }
                }

                ImGui.Separator();
            }
        }

        ImGui.EndTabItem();
    }

    static void Cheats(Level level)
    {
        if (!ImGui.BeginTabItem("Cheats"))
            return;

        var fly = Fly.Enabled;
        if (ImGui.Checkbox("Fly+NoClip", ref fly).WithTooltip("Enables you to freely fly anywhere and noclip through solids"))
        {
            Fly.Enabled = fly;
        }

        ImGui.DragFloat("Fly Speed", ref Fly.SpeedMult, 0.1f, 0.1f);

        if (SpawnEntity.Available && ImGuiExt.Combo("Spawn Entity", ref SpawnEntity.LastSummoned, SpawnEntity.SummonableSIDs.Value, (s) => s, SpawnEntity.ComboCache,
            "Spawns an entity of the selected type next to the player"))
        {
            SpawnEntity.Spawn(level, SpawnEntity.LastSummoned);
        }

        ImGui.EndTabItem();
    }

    static string FlagsNewFlag = "";
    static void Flags(Level level)
    {
        if (!ImGui.BeginTabItem("Flags"))
            return;

        var flags = level.Session.Flags;

        foreach (var f in flags)
        {
            ImGui.SetNextItemWidth(ItemWidth);
            if (ImGui.Selectable(f))
            {
                level.OnEndOfFrame += () => level.Session.SetFlag(f, false);
            }
        }

        ImGui.SetNextItemWidth(ItemWidth);
        ImGui.InputText("New Flag", ref FlagsNewFlag, 512);
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            level.Session.SetFlag(FlagsNewFlag, true);
        }

        ImGui.EndTabItem();
    }

    static List<string>? ColorGrades;
    static ComboCache<string> ColorGradeCache = new();

    static void Metadata(Level level)
    {
        if (!ImGui.BeginTabItem("Metadata"))
            return;

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


        ImGui.EndTabItem();
    }
}
