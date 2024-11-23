using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        })
        .Concat(_extendedInfo.Select(x => x.Key))
        .Distinct();
    }


    private static readonly ConditionalWeakTable<MethodBase, ExtendedDetourInfo> _extendedInfo = new();

    internal static ExtendedDetourInfo GetExtendedDetourInfo(MethodBase method)
    {
        return _extendedInfo.GetValue(method, m => new ExtendedDetourInfo(m));
    }
}

internal sealed record ExtendedDetourInfo(MethodBase Method)
{
    public MethodDetourInfo DetourInfo => DetourManager.GetDetourInfo(Method);

    private List<DetourBase>? _undoneHooks;

    public IReadOnlyList<DetourBase> AllDetours
    {
        get
        {
            var info = DetourInfo;
            var detours = info.ILHooks.Concat<DetourBase>(info.Detours);
            if (_undoneHooks is not null)
                detours = detours.Concat(_undoneHooks).DistinctBy(x => x.Method.Method);
            return detours.ToList();
        }
    }
    
    public void ReapplyDetour(DetourBase detour)
    {
        if (detour.IsApplied)
            return;
        
        detour.Apply();
        
        _undoneHooks?.Remove(detour);
    }
    
    public void UndoDetour(DetourBase detour)
    {
        if (!detour.IsApplied)
            return;
        
        detour.Undo();
        _undoneHooks ??= new();
        _undoneHooks.Add(detour);
    }
}