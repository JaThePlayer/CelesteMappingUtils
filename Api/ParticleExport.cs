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
            Name = name,
            ModName = modName,
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
            Name = name,
            ModName = modName,
            ImGuiRenderFunc = imguiRender
        });
    }
    
    /// <summary>
    /// Registers a Particle Tab importer, which adds a button which imports the particle from the clipboard.
    /// </summary>
    /// <param name="modName">The name of the mod needed to make use of this importer.</param>
    /// <param name="name">The name of this importer, displayed on the button.</param>
    /// <param name="tooltip">Tooltip displayed when hovering over the button.</param>
    /// <param name="importFunc">Function called to import the given particle type and emitter from the string.</param>
    [MinMappingUtilsVersion("1.11.0")]
    public static void RegisterCopyFromClipboardParticleImporter(string modName, string name, string tooltip, 
        Func<string, (ParticleType, ParticleEmitter?)> importFunc)
    {
        ParticleImporterRegistry.Register(new ParticleImporter
        {
            Name = name,
            ModName = modName,
            ImGuiRenderFunc = ParticleImporterRegistry.CreateClipboardRenderFunc(modName, name, tooltip, importFunc)
        });
    }
    
    /// <summary>
    /// Registers a Particle Tab importer, which allows custom imgui rendering.
    /// </summary>
    /// <param name="modName">The name of the mod needed to make use of this importer.</param>
    /// <param name="name">The name of this importer, used for deduplication on hot reload.</param>
    /// <param name="imguiRender">Callback which renders the importer via imgui.</param>
    [MinMappingUtilsVersion("1.11.0")]
    public static void RegisterParticleImporter(string modName, string name, Func<(ParticleType, ParticleEmitter?)?> imguiRender)
    {
        ParticleImporterRegistry.Register(new ParticleImporter
        {
            Name = name,
            ModName = modName,
            ImGuiRenderFunc = imguiRender
        });
    }
}
