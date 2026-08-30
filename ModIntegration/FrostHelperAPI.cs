global using SessionExpressionDisplayParts = System.Collections.Generic.IReadOnlyList<(string Contents, string ColorId, System.Collections.Generic.IReadOnlyList<(string Contents, string ColorId)>? Tooltip)>;
global using ApiRenderPart = (string Contents, string ColorId, System.Collections.Generic.IReadOnlyList<(string Contents, string ColorId)>? Tooltip);
global using ApiSimpleRenderPart = (string Contents, string ColorId);
global using ApiAutoCompletion = (
    System.Collections.Generic.IReadOnlyList<(string Contents, string ColorId)> Name,
    System.Collections.Generic.IReadOnlyList<(string Contents, string ColorId)> Description,
    string Contents
);

using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.ModInterop;
using System.Diagnostics.CodeAnalysis;

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

    public delegate bool TryCreateSessionExpressionDelegate(string str, [NotNullWhen(true)] out object? expression);

    public static TryCreateSessionExpressionDelegate? TryCreateSessionExpression;
    
    // object GetSessionExpressionValue(object expression, Session session)
    public static Func<object, Session, object>? GetSessionExpressionValue;
    
    // Type GetSessionExpressionReturnedType(object expression)
    public static Func<object, Type>? GetSessionExpressionReturnedType;
    
    public static Func<object?, object>? CreateSessionExpressionInspector;
    public static Func<object, string>? GetInspectorText;
    public static Action<object, string>? SetInspectorText;
    public static Func<object, SessionExpressionDisplayParts>? GetInspectorRenderParts;
    public static Func<object, IReadOnlyList<string>>? GetInspectorErrors;
    
    // IDisposable RegisterNotificationSink(Func<LogLevel, string, bool> onNotification)
    public static Func<Func<LogLevel, string, bool>, IDisposable>? RegisterNotificationSink;
    
    // public static IReadOnlyList<ApiAutoCompletion> GetAutoCompletions(object inspector, int cursorIndex)
    public static Func<object, int, IReadOnlyList<ApiAutoCompletion>>? GetAutoCompletions;
}
