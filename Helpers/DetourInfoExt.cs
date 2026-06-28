using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mod.MappingUtils.Helpers;

public static class DetourInfoExt
{
    private const byte RetOpCodeValue = 42;
    private const byte CallOpCodeValue = 40;

    private static readonly ConditionalWeakTable<DetourInfo, MethodBase> GetActualEntryCache = [];
    
    extension(DetourInfo detourInfo)
    {
        /// <summary>
        /// Gets the <see cref="DetourInfo.Entry"/>, but resolves LegacyMonoMod hook targets to the actual detour method.
        /// </summary>
        /// <returns></returns>
        public MethodBase GetActualEntry() =>
            GetActualEntryCache.GetValue(detourInfo, static detourInfo =>
            {
                if (detourInfo.Entry is not DynamicMethod dm)
                    return detourInfo.Entry;

                try
                {
                    return GetRealTargetFromLegacyMonoModHookDmd(dm) ?? detourInfo.Entry;
                }
                catch (Exception ex)
                {
                    Logger.Error("MappingUtils.DetourInfoExt", $"Exception while trying to get actual entry for detour: {ex}");
                    return detourInfo.Entry;
                }
            });

        private static MethodBase? GetRealTargetFromLegacyMonoModHookDmd(DynamicMethod dm)
        {
            // https://github.com/EverestAPI/Everest/blob/dev/Celeste.Mod.mm/Mod/Helpers/LegacyMonoMod/Hook.cs#L102
            if (!dm.Name.StartsWith("Hook<"))
                return null;
            
            var ilGen = new DynamicData(dm.GetILGenerator());

            if (ilGen.Get("m_scope") is not { } scope)
                return null;
            
            var il = ilGen.Get<byte[]>("m_ILStream").AsSpan().TrimEnd((byte)0);
            // The ending of legacy hook methods should be `call, 4-byte token, ret`
            if (il is not [.., CallOpCodeValue, _, _, _, _, RetOpCodeValue])
                return null;
            
            var token = BinaryPrimitives.ReadInt32LittleEndian(il[^5..^1]) & 0xffffff;
            var toCall = new DynamicData(scope).Invoke<object>("get_Item", token);

            // Simplified variant of:
            // https://github.com/dotnet/runtime/blob/main/src/coreclr/System.Private.CoreLib/src/System/Reflection/Emit/DynamicILGenerator.cs#L790
            return toCall switch
            {
                RuntimeMethodHandle r => MethodBase.GetMethodFromHandle(r),
                MethodBase mb => mb,
                not null => ResolveGenericMethodInfo(toCall),
                _ => null
            };
        }
        
        static MethodBase? ResolveGenericMethodInfo(object o)
        {
            // https://github.com/dotnet/runtime/blob/main/src/coreclr/System.Private.CoreLib/src/System/Reflection/Emit/DynamicILGenerator.cs#L822
            if (o.GetType().Name == "GenericMethodInfo")
            {
                dynamic dynO = new DynamicData(o);
                return MethodBase.GetMethodFromHandle(dynO.m_methodHandle, dynO.m_context);
            }
            
            return null;
        }
    }
}