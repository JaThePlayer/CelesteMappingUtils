using Celeste.Mod.MappingUtils.ModIntegration;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal sealed class EntityTab : Tab
{
    public override string Name => "Entities";

    public override void Render(Level level)
    {
        if (FrostHelperAPI.EntityNameToType is { } entityNameToType && FrostHelperAPI.SetCustomSpinnerColor is { } setTint && FrostHelperAPI.SetCustomSpinnerBorderColor is { } setBorderColor)
        {
            if (entityNameToType("FrostHelper/IceSpinner") is { } customSpinnerType && level.Tracker.Entities.TryGetValue(customSpinnerType, out var spinners) && spinners.Count > 0)
            {
                ImGui.Text("Frost Helper Custom Spinners");

                if (ImGuiExt.ColorEdit("Tint", ref CustomSpinnerTint, Helpers.ColorFormat.RGBA, "Change the color of all Frost Helper custom spinners"))
                {
                    foreach (var item in spinners)
                    {
                        setTint(item, CustomSpinnerTint);
                    }
                }

                if (ImGuiExt.ColorEdit("Border Color", ref CustomSpinnerBorderColor, Helpers.ColorFormat.RGBA, "Change the border color of all Frost Helper custom spinners"))
                {
                    foreach (var item in spinners)
                    {
                        setBorderColor(item, CustomSpinnerBorderColor);
                    }
                }

                ImGui.Separator();
            }
        }
    }

    Color CustomSpinnerTint = Color.White;
    Color CustomSpinnerBorderColor = Color.Black;
}
