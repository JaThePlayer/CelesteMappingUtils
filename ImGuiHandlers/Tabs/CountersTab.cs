namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class CountersTab : Tab
{
    string _newCounterName = "";
    
    public override string Name => "Counters";

    public override void Render(Level? level)
    {
        if (level is null)
            return;
        
        var textBaseWidth = ImGui.CalcTextSize("m").X;
        
        var counters = level.Session.Counters;
        
        if (!ImGui.BeginTable("Counters", 2, ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Value").X);
        ImGui.TableHeadersRow();

        foreach (var c in counters)
        {
            ImGui.TableNextColumn();
            ImGui.Text(c.Key);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            ImGui.InputInt($"##{c.Key}", ref c.Value, 1, 1);
            ImGui.TableNextRow();
        }

        ImGui.EndTable();
        ImGui.SetNextItemWidth(ItemWidth);
        ImGui.InputText("New Counter", ref _newCounterName, 512);
        ImGui.SameLine();
        if (ImGui.Button("Add"))
        {
            level.Session.SetCounter(_newCounterName, 0);
        }
        
    }
}