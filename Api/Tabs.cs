using Celeste.Mod.MappingUtils.ImGuiHandlers;
using MonoMod.ModInterop;

namespace Celeste.Mod.MappingUtils.Api;

[ModExportName("MappingUtils.Tabs")]
public static class Tabs
{
    /// <summary>
    /// Registers a new tab to Mapping Utils.
    /// </summary>
    /// <param name="modName">The name of the mod adding the tab.</param>
    /// <param name="tabName">The display name of the tab. Can be a Dialog ID.</param>
    /// <param name="renderImGui">Callback used to render the tab via imgui.</param>
    /// <param name="canBeVisible">Callback used to check if the tab can currently appear in Mapping Utils.</param>
    /// <param name="onOpen">Callback called each time the tab gets opened.</param>
    /// <param name="onClose">Callback called each time the tab gets closed.</param>
    public static void RegisterTab(string modName, string tabName, Action renderImGui, Func<bool> canBeVisible,
        Action? onOpen, Action? onClose)
    {
        var newTab = new ApiTab(modName, tabName, renderImGui, canBeVisible, onOpen, onClose);
        
        MainMappingUtils.Tabs.RemoveAll(x => x is ApiTab tab && tab.InternalName == newTab.InternalName);
        MainMappingUtils.Tabs.Add(newTab);
    }
}

internal sealed class ApiTab(string modName, string tabName, Action renderImGui, Func<bool>? canBeVisible, Action? onOpen, Action? onClose) : Tab
{
    public string InternalName { get; } = $"{modName}/{tabName}";

    public override string Name => Dialog.Has(tabName) ? Dialog.Clean(tabName) : tabName;

    public override void RenderTooltip()
    {
        ImGuiExt.AddAddedByModTooltip(modName);
    }

    public override void Render(Level? level)
    {
        renderImGui();
    }

    public override bool CanBeVisible() => canBeVisible?.Invoke() ?? true;

    public override void OnOpen()
    {
        base.OnOpen();
        onOpen?.Invoke();
    }

    public override void OnClose()
    {
        base.OnClose();
        onClose?.Invoke();
    }
}