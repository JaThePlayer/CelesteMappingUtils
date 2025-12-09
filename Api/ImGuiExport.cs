using MonoMod.ModInterop;

namespace Celeste.Mod.MappingUtils.Api;

/// <summary>
/// Provides basic ImGui functions as mod exports, for being able to easily create basic mapping utils integration,
/// without an asm reference to ImGui.
/// </summary>
[ModExportName("MappingUtils.ImGui")]
[MinMappingUtilsVersion("1.10.1")]
public static class ImGuiExport
{
    [MinMappingUtilsVersion("1.10.1")]
    public static void Text(string txt) => ImGui.Text(txt);
    
    [MinMappingUtilsVersion("1.10.1")]
    public static bool Button(string label) => ImGui.Button(label);
    
    [MinMappingUtilsVersion("1.10.1")]
    public static bool InputText(string label, ref string text, uint maxLength) => ImGui.InputText(label, ref text, maxLength);
}
