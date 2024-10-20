namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class FlagTab : Tab
{
    public override string Name => "Flags";

    public override void Render(Level? level)
    {
        if (level is null)
            return;
        
        var flags = level.Session.Flags;

        if (!ImGui.BeginTable("Flags", 1, ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        ImGui.TableSetupColumn("Flag", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();
        
        foreach (var f in flags)
        {
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ItemWidth);
            if (ImGui.Selectable(f))
            {
                level.OnEndOfFrame += () => level.Session.SetFlag(f, false);
            }
        }
        
        ImGui.EndTable();

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
