using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace Celeste.Mod.MappingUtils.Helpers;

internal static class ParticleTypeDiscovery
{
    public static IList<NamedParticleType> AllParticles => field ??= FindParticles();

    private static IList<NamedParticleType> FindParticles()
    {
        var types = new List<NamedParticleType>();
        ILContext.Manipulator hook = il => ParticleTypesOnLoad(il, types);
        IL.Celeste.ParticleTypes.Load += hook;
        IL.Celeste.ParticleTypes.Load -= hook;

        return types;
    }

    private static void ParticleTypesOnLoad(ILContext il, List<NamedParticleType> types)
    {
        var cursor = new ILCursor(il);
        FieldReference? field = null;
        while (cursor.TryGotoNext(MoveType.After, i => i.MatchStsfld(out field)))
        {
            var fldReflection = field!.Resolve().ResolveReflection();
            if (fldReflection.FieldType == typeof(ParticleType))
            {
                types.Add(new NamedParticleType($"{fldReflection.DeclaringType!.FullName}.{fldReflection.Name}", (ParticleType)fldReflection.GetValue(null)!, fldReflection));
            }
        }
    }
}

internal record NamedParticleType(string Name, ParticleType ParticleType, FieldInfo FieldInfo);