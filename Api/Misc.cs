using Celeste.Mod.MappingUtils.Helpers;
using MonoMod.ModInterop;

namespace Celeste.Mod.MappingUtils.Api;

[ModExportName("MappingUtils.Misc")]
public static class Misc
{
    /// <summary>
    /// Creates a string representing the provided EntityData as a Loenn selection containing an entity.
    /// </summary>
    /// <param name="entityData">EntityData to convert.</param>
    public static string CreateLoennEntitySelection(EntityData entityData)
    {
        return LoennFormatHelper.ToLoennEntity(entityData);
    }
}