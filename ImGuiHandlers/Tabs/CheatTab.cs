using Celeste.Mod.MappingUtils.Cheats;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class CheatTab : Tab
{
    public override string Name => "Cheats";

    public override void Render(Level level)
    {
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
    }
}
