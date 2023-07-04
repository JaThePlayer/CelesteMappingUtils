using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.MappingUtils.ModIntegration;

[ModImportName("FrostHelper")]
public class FrostHelperAPI
{
    public static void LoadIfNeeded()
    {
        if (Loaded)
            return;

        typeof(FrostHelperAPI).ModInterop();

        Loaded = true;
    }

    public static bool Loaded { get; private set; }

    public static Func<Color> GetBloomColor = null!;

    public static Action<Color> SetBloomColor = null!;

    public static Action<Entity, Color> SetCustomSpinnerColor = null!;

    public static Action<Entity, Color> SetCustomSpinnerBorderColor = null!;

    public static Func<string, Type> EntityNameToType = null!;
}
