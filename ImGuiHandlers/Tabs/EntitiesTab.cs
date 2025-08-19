using System.Linq;
using System.Text.Json;
using System.Text.Unicode;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.Registry;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

public class EntitiesTab : Tab
{
    private Type? _selectedType;
    private Entity? _selectedEntity;
    
    public override string Name => "Entities";
    
    public override void Render(Level? _)
    {
        var scene = Engine.Scene;
        var textBaseWidth = ImGui.CalcTextSize("A").X;
        const int columnCount = 2;
        
        if (!ImGui.BeginTable("Entities", columnCount, ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 2f);
        ImGui.TableHeadersRow();
        ImGui.TableSetupScrollFreeze(columnCount, 1);

        foreach (var group in scene.Entities.GroupBy(x => x.GetType()))
        {
            var t = group.Key;
            var displayName = t.Name;
            
            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(ItemWidth);
            
            var flags = ImGuiTreeNodeFlags.SpanFullWidth;
            if (_selectedType == t) {
                flags |= ImGuiTreeNodeFlags.Selected;
            }

            var open = ImGui.TreeNodeEx($"##{displayName}", flags);
            if (ImGui.IsItemClicked())
            {
                _selectedEntity = null;
            }
            ImGui.SameLine();
            ImGui.Text(displayName);
            
            if (open)
            {
                _selectedType = t;
                foreach (var entity in group)
                {
                    var data = entity.SourceData;
                    
                    flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth;
                    if (_selectedEntity == entity) {
                        flags |= ImGuiTreeNodeFlags.Selected;
                    }

                    
                    //open = ImGui.TreeNodeEx($"##{display}", flags);
                    
                    var clicked = ImGui.IsItemClicked();
                    if (clicked) {
                        _selectedEntity = entity;
                    }

                    ImGui.PushStyleColor(ImGuiCol.Button, entity.Active ? Color.LightSkyBlue.ToNumVec4() : Color.Gray.ToNumVec4());
                    if (ImGui.Button("A").WithTooltip("Active"))
                    {
                        entity.Active = !entity.Active;
                    }
                    ImGui.PopStyleColor(1);
                    
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, entity.Visible ? Color.LightSkyBlue.ToNumVec4() : Color.Gray.ToNumVec4());
                    if (ImGui.Button("V").WithTooltip("Visible"))
                    {
                        entity.Visible = !entity.Visible;
                    }
                    ImGui.PopStyleColor(1);
                    
                    ImGui.SameLine();
                    if (data is not null)
                        ImGuiUtf8.Selectable($"{data.Name} [{entity.SourceId.ID}]");
                    else
                        ImGuiUtf8.Selectable($"[{entity.SourceId.ID}]");
                    
                    if (ImGui.IsItemHovered() && ImGui.BeginTooltip())
                    {
                        ImGuiUtf8.TextUnformatted($"Depth: {entity.Depth}");
                        ImGuiUtf8.TextUnformatted($"Position: {entity.Position}");
                        if (data is not null)
                        {
                            ImGuiUtf8.TextUnformatted($"EntityData: {JsonSerializer.SerializeToUtf8Bytes(data.Values, _options).AsSpan()}");
                        }
                        
                        ImGui.EndTooltip();
                    }
                }
                
                ImGui.TreePop();
            }
            
            ImGui.TableNextColumn();
            ImGui.Text(group.Count().ToString());
        }
        
        ImGui.EndTable();
    }

    public override bool CanBeVisible() => Engine.Scene is not null;

    private readonly JsonSerializerOptions _options = new JsonSerializerOptions()
    {
        WriteIndented = true,
        IncludeFields = true,
    };
}