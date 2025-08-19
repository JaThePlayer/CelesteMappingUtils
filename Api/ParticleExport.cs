using System.Collections.Generic;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;
using MonoMod.ModInterop;

namespace Celeste.Mod.MappingUtils.Api;

[ModExportName("MappingUtils.ParticleExport")]
public static class ParticleExport
{
    /// <summary>
    /// Registers a Particle Tab exporter, which adds a button which copies the particle to the clipboard.
    /// </summary>
    /// <param name="modName">The name of the mod needed to make use of this exporter.</param>
    /// <param name="name">The name of this exporter, displayed on the button.</param>
    /// <param name="tooltip">Tooltip displayed when hovering over the button.</param>
    /// <param name="exportFunc">Function called to export the given particle type and emitter. The returned string will be copied to the clipboard if non-null.</param>
    public static void RegisterCopyToClipboardParticleExporter(string modName, string name, string tooltip, Func<ParticleType, ParticleEmitter, string?> exportFunc)
    {
        ParticleExporterRegistry.Register(new ParticleExporter
        {
            Name = $"{modName}/{name}",
            ImGuiRenderFunc = ParticleExporterRegistry.CreateClipboardRenderFunc(modName, name, tooltip, exportFunc)
        });
    }
    
    /// <summary>
    /// Registers a Particle Tab exporter, which allows custom imgui rendering.
    /// </summary>
    /// <param name="modName">The name of the mod needed to make use of this exporter.</param>
    /// <param name="name">The name of this exporter, used for deduplication on hot reload.</param>
    /// <param name="imguiRender">Callback which renders the exporter via imgui.</param>
    public static void RegisterParticleExporter(string modName, string name, Action<ParticleType, ParticleEmitter> imguiRender)
    {
        ParticleExporterRegistry.Register(new ParticleExporter
        {
            Name = $"{modName}/{name}",
            ImGuiRenderFunc = imguiRender
        });
    }
}
