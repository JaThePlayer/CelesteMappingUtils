using Microsoft.Xna.Framework.Graphics;
using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.MappingUtils.ModIntegration;

[ModImportName("FrostHelper")]
public class FrostHelperAPI
{
    public static bool LoadIfNeeded()
    {
        if (Loaded)
            return true;

        typeof(FrostHelperAPI).ModInterop();

        Loaded = true;

        return true;
    }

    public static bool Loaded { get; private set; }

    public static Func<Color> GetBloomColor = null!;

    public static Action<Color> SetBloomColor = null!;

    public static Action<Entity, Color> SetCustomSpinnerColor = null!;

    public static Action<Entity, Color> SetCustomSpinnerBorderColor = null!;

    public static Func<string, Type> EntityNameToType = null!;
    public static Func<string, Type?> EntityNameToTypeOrNull = null!;
    public static Func<Type, string?> EntityNameFromType = null!;

    public static Action<Backdrop, BlendState> SetBackdropBlendState = null!;
    public static Func<Backdrop, BlendState?> GetBackdropBlendState = null!;
}
