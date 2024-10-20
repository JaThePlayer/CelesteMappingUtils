using System.Linq;
using System.Reflection;
using Celeste.Mod.MappingUtils.Commands;
using Celeste.Mod.MappingUtils.Helpers;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal sealed class HooksTab : Tab
{
    private MethodBase? _selectedMethod;
    private ComboCache<MethodBase> _comboCache = new();

    private MethodDetourInfo? _detourInfo;
    private MethodDiff? _diff;
    
    public override string Name => "Hooks";

    public override bool CanBeVisible() => true;

    public override void Render(Level? level)
    {
        var hooks = HookDiscovery.GetHookedMethods().ToList();
        if (ImGuiExt.Combo("Method", ref _selectedMethod!, hooks, m => m?.GetMethodNameForDB() ?? "", _comboCache, tooltip: null,
                ImGuiComboFlags.None))
        {
            _detourInfo = DetourManager.GetDetourInfo(_selectedMethod);
            _diff = new MethodDiff(_selectedMethod);
        }

        if (_detourInfo is { })
        {
            ImGui.Text(_selectedMethod.GetMethodNameForDB());

            ImGui.Button("Decompile");
            ImGuiExt.AddDecompilationTooltip(_selectedMethod);
            var textBaseWidth = ImGui.CalcTextSize("m").X;

            ImGui.SeparatorText("Hooks");
            if (ImGui.BeginTable("Hooks", 3, ImGuiExt.TableFlags))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 5f);
                ImGui.TableSetupColumn("Source");
                ImGui.TableHeadersRow();
                
                foreach (var ilHook in _detourInfo.ILHooks)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                
                    ImGui.Text(ilHook.ManipulatorMethod.Name);
                    ImGui.TableNextColumn();
                
                    ImGui.Text("IL");
                    ImGui.TableNextColumn();
                
                    ImGui.Text(ilHook.ManipulatorMethod.GetMethodNameForDB());
                    ImGuiExt.AddDecompilationTooltip(ilHook.ManipulatorMethod);
                }
                
                foreach (var hook in _detourInfo.Detours)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                
                    ImGui.Text(hook.Entry.Name);
                    ImGui.TableNextColumn();
                
                    ImGui.Text("On");
                    ImGui.TableNextColumn();
                
                    ImGui.Text(hook.Entry.GetMethodNameForDB());
                    ImGuiExt.AddDecompilationTooltip(hook.Entry);
                }
                
                ImGui.EndTable();
            }

            if (_diff is { } && _diff.Instructions.Any(i => i.Type != MethodDiff.ElementType.Unchanged))
            {
                ImGui.SeparatorText("IL Diff");
                IlDiffView.RenderDiff(_diff);
            }
        }
    }
}