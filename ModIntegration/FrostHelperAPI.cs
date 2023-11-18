using Microsoft.Xna.Framework.Graphics;
using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.MappingUtils.ModIntegration;

// ReSharper disable InconsistentNaming 
// ReSharper disable UnassignedField.Global
#pragma warning disable CS0649 // Field is never assigned to
#pragma warning disable CA2211

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

    public static Func<Color>? GetBloomColor;

    public static Action<Color>? SetBloomColor;

    public static Action<Entity, Color>? SetCustomSpinnerColor;

    public static Action<Entity, Color>? SetCustomSpinnerBorderColor;

    public static Func<string, Type>? EntityNameToType;
    public static Func<string, Type?>? EntityNameToTypeOrNull;
    public static Func<Type, string?>? EntityNameFromType;

    public static Action<Backdrop, BlendState>? SetBackdropBlendState;
    public static Func<Backdrop, BlendState?>? GetBackdropBlendState;
}
