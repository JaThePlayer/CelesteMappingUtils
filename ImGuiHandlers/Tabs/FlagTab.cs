namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class FlagTab : Tab
{
    public override string Name => "Flags";

    public override void Render(Level level)
    {
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
        ImGui.InputText("New Flag", ref NewFlag, 512);
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            level.Session.SetFlag(NewFlag, true);
        }
    }

    string NewFlag = "";
}
