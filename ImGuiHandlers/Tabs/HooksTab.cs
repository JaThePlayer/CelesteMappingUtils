using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod.MappingUtils.Commands;
using Celeste.Mod.MappingUtils.Helpers;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal sealed class HooksTab : Tab
{
    private MethodBase? _selectedMethod;
    private ComboCache<MethodBase> _comboCache = new();

    private ExtendedDetourInfo? _extendedDetourInfo;
    private MethodDetourInfo? _detourInfo;
    private MethodDiff? _diff;

    private IReadOnlyList<DetourBase>? _detours;
    
    public override string Name => "Hooks";

    public override bool CanBeVisible() => true;

    public override void Render(Level? level)
    {
        var hooks = HookDiscovery.GetHookedMethods().ToList();
        if (ImGuiExt.Combo("Method", ref _selectedMethod!, hooks, m => m?.GetMethodNameForDB() ?? "", _comboCache, tooltip: null,
                ImGuiComboFlags.None))
        {
            _extendedDetourInfo = HookDiscovery.GetExtendedDetourInfo(_selectedMethod);
            _detourInfo = _extendedDetourInfo.DetourInfo;
            _diff = new MethodDiff(_selectedMethod);
            _detours = _extendedDetourInfo.AllDetours;
        }

        if (_detourInfo is { })
        {
            ImGui.Text(_selectedMethod.GetMethodNameForDB());

            ImGui.Button("Decompile");
            ImGuiExt.AddDecompilationTooltip(_selectedMethod);
            var textBaseWidth = ImGui.CalcTextSize("m").X;

            ImGui.SeparatorText("Hooks");
            if (ImGui.BeginTable("Hooks", 4, ImGuiExt.TableFlags))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 5f);
                ImGui.TableSetupColumn("Source");
                ImGui.TableSetupColumn("Enabled");
                ImGui.TableHeadersRow();

                foreach (var detour in _detours ?? [])
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    switch (detour)
                    {
                        case ILHookInfo ilHook:
                        {
                            ImGui.Text(ilHook.ManipulatorMethod.Name);
                            ImGui.TableNextColumn();

                            ImGui.Text("IL");
                            ImGui.TableNextColumn();

                            ImGui.Text(ilHook.ManipulatorMethod.GetMethodNameForDB());
                            ImGuiExt.AddDecompilationTooltip(ilHook.ManipulatorMethod);
                            ImGui.TableNextColumn();
                            break;
                        }
                        case DetourInfo hook:
                        {
                            ImGui.Text(hook.Entry.Name);
                            ImGui.TableNextColumn();
                            
                            ImGui.Text("On");
                            ImGui.TableNextColumn();

                            ImGui.Text(hook.Entry.GetMethodNameForDB());
                            ImGuiExt.AddDecompilationTooltip(hook.Entry);
                            ImGui.TableNextColumn();
                            break;
                        }
                    }

                    bool isEnabled = detour.IsApplied;
                    if (ImGui.Checkbox($"##{detour.GetHashCode()}", ref isEnabled))
                    {
                        if (!isEnabled)
                            _extendedDetourInfo!.UndoDetour(detour);
                        else
                            _extendedDetourInfo!.ReapplyDetour(detour);
                        
                        if (detour is ILHookInfo)
                            _diff = new MethodDiff(_selectedMethod);
                    }
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