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
        var sliders = level.Session.Sliders;
        
        if (!ImGui.BeginTable("Counters##2", 3, ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Counter").X);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Value").X * 2.5f);
        ImGui.TableHeadersRow();

        foreach (var c in counters)
        {
            ImGui.TableNextColumn();
            ImGui.Text("Counter");
            ImGui.TableNextColumn();
            ImGui.Text(c.Key);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            ImGui.InputInt($"##c_{c.Key}", ref c.Value, 1, 1);
            ImGui.TableNextRow();
        }
        
        foreach (var (name, slider) in sliders)
        {
            ImGui.TableNextColumn();
            ImGui.Text("Slider");
            ImGui.TableNextColumn();
            ImGui.Text(name);
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ImGui.GetColumnWidth());
            var value = slider.Value;
            if (ImGui.InputFloat($"##s_{name}", ref value, 1, 1))
                slider.Value = value;
            ImGui.TableNextRow();
        }

        ImGui.EndTable();
        ImGui.SetNextItemWidth(ItemWidth);
        ImGui.InputText("New Counter/Slider name", ref _newCounterName, 512);
        if (ImGui.Button("Add Counter").WithTooltip("Adds a new Session Counter, storing 32-bit integers."))
        {
            level.Session.SetCounter(_newCounterName, 0);
        }
        if (ImGui.Button("Add Slider").WithTooltip("Adds a new Session Slider, storing floating-point numbers."))
        {
            level.Session.SetSlider(_newCounterName, 0f);
        }
    }
}