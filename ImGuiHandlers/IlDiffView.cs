using Celeste.Mod.MappingUtils.Helpers;
using MonoMod.Utils;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

internal sealed class IlDiffView(MethodDiff diff) : ImGuiHandler
{
    public override void Render()
    {
        base.Render();

        bool open = true;
        if (ImGui.Begin($"IL Diff - {diff.Method.GetID()}", ref open))
        {
            const int columnCount = 3;
            var textBaseWidth = ImGui.CalcTextSize("m").X;
            
            var flags = ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings;
            if (!ImGui.BeginTable("Instructions", columnCount, flags))
            {
                ImGui.End();
                return;
            }

            ImGui.TableSetupColumn("*", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 2f);
            ImGui.TableSetupColumn("Instruction");
            ImGui.TableSetupColumn("Source");
            ImGui.TableHeadersRow();

            foreach (var instr in diff.Instructions)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                var color = (instr.Type switch
                {
                    MethodDiff.ElementType.Unchanged => Color.White,
                    MethodDiff.ElementType.Added => Color.Green,
                    MethodDiff.ElementType.Removed => Color.Red,
                    _ => throw new ArgumentOutOfRangeException()
                }).ToNumVec4();
                
                ImGui.TextColored(color, instr.Type switch
                {
                    MethodDiff.ElementType.Unchanged => "",
                    MethodDiff.ElementType.Added => "+",
                    MethodDiff.ElementType.Removed => "-",
                    _ => throw new ArgumentOutOfRangeException()
                });
                
                ImGui.TableNextColumn();
                ImGui.TextColored(color, instr.Instruction.FixedToString());
                
                ImGui.TableNextColumn();

                if (instr.Source is { })
                {
                    ImGuiExt.DecompilableMethod(instr.Source);
                }
            }
            
            ImGui.EndTable();
            
            ImGui.End();
        }

        if (!open)
        {
            Engine.Scene.OnEndOfFrame += () => ImGuiManager.Handlers.Remove(this);
        }
    }
}