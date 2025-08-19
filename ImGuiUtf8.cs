using Celeste.Mod.MappingUtils.Helpers;

namespace Celeste.Mod.MappingUtils;

internal static class ImGuiUtf8
{
    public static void TextUnformatted(ReadOnlySpan<byte> text)
    {
        unsafe
        {
            fixed (byte* textPtr = text)
                ImGuiNative.igTextUnformatted(textPtr, textPtr + text.Length);
        }
    }

    public static void TextUnformatted(Interpolator.HandlerU8 text)
    {
        TextUnformatted(text.Result);
    }

    public static bool Selectable(ReadOnlySpan<byte> text, bool selected = false, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, NumVector2 size = default)
    {
        unsafe
        {
            fixed (byte* textPtr = text)
                return ImGuiNative.igSelectable_Bool(textPtr, selected ? (byte)1 : (byte)0, flags, size) > 0U;
        }
    }
    
    public static bool Selectable(Interpolator.HandlerU8 text, bool selected = false, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, NumVector2 size = default)
        => Selectable(text.ResultNullTerminated(), selected, flags, size);
}