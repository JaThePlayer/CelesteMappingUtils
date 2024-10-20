using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.MappingUtils.Helpers;

public static class HookDiscovery
{
    private static readonly FieldInfo DetourManager_detourStates =
        typeof(DetourManager).GetField("detourStates", BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// Finds all hooked methods.
    /// Call <see cref="DetourManager.GetDetourInfo"/> to get a list of hooks.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<MethodBase> GetHookedMethods()
    {
        var detourStates = (IDictionary)DetourManager_detourStates.GetValue(null)!;
        return detourStates.Keys.OfType<MethodBase>().Where(x =>
        {
            var info = DetourManager.GetDetourInfo(x);

            return info.Detours.Any() || info.ILHooks.Any();
        });
    }
}