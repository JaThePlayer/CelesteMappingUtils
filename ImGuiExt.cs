using Celeste.Mod.MappingUtils.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Mod.MappingUtils.ImGuiHandlers;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Utils;

namespace Celeste.Mod.MappingUtils;

public static class ImGuiExt
{
    public static ImGuiTableFlags TableFlags =>
    ImGuiTableFlags.BordersV | ImGuiTableFlags.BordersOuterH |
    ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg |
    ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.Hideable;


    /// <summary>
    /// Adds a tooltip to the last added element, then fluently returns the bool that was passed to this function, for further handling.
    /// </summary>
    public static bool WithTooltip(this bool val, string? tooltip)
    {
        if (tooltip is { } && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }

        return val;
    }
    
    public static bool WithTooltip(this bool val, Action tooltip)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            tooltip();
            ImGui.EndTooltip();
        }

        return val;
    }
    
    public static bool WithTooltip<T>(this bool val, T data, Action<T> tooltip)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            tooltip(data);
            ImGui.EndTooltip();
        }

        return val;
    }

    public static void AddTooltip(string? tooltip)
    {
        if (tooltip is { } && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }
    }
    
    public static void AddTooltip<T>(T data, Action<T> tooltip)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            tooltip(data);
            ImGui.EndTooltip();
        }
    }

    public static void AddAddedByModTooltip(string modName)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(Color.LightGray.ToNumVec4(), $"Added by mod '{modName}'");
            ImGui.EndTooltip();
        }
    }

    public static bool ColorEdit(string label, ref Color color, ColorFormat format, string? tooltip = null)
    {
        var colorHex = ColorHelper.ToString(color, format);
        bool edited = false;

        var xPadding = ImGui.GetStyle().FramePadding.X;
        var buttonWidth = ImGui.GetFrameHeight();

        ImGui.SetNextItemWidth(ImGui.CalcItemWidth() - buttonWidth - xPadding);
        if (ImGui.InputText($"##text{label}", ref colorHex, 24).WithTooltip(tooltip))
        {
            if (ColorHelper.TryGet(colorHex, format, out var newColor))
            {
                color = newColor;
            }
            edited = true;
        }

        ImGui.SameLine(0f, xPadding);

        switch (format)
        {
        case ColorFormat.RGB:
            var colorN3 = color.ToNumVec3();
            if (ImGui.ColorEdit3($"##combo{label}", ref colorN3, ImGuiColorEditFlags.NoInputs).WithTooltip(tooltip))
            {
                color = colorN3.ToColor();
                edited = true;
            }
            break;
        case ColorFormat.RGBA:
        case ColorFormat.ARGB:
            var colorN4 = color.ToNumVec4();
            if (ImGui.ColorEdit4($"##combo{label}", ref colorN4, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.NoInputs).WithTooltip(tooltip))
            {
                color = colorN4.ToColor();
                edited = true;
            }
            break;
        default:
            break;
        }


        ImGui.SameLine(0f, xPadding);
        ImGui.Text(label);
        true.WithTooltip(tooltip);

        return edited;
    }

    public static bool EnumCombo<T>(string name, ref T value, ComboCache<T> cache) where T : struct, Enum
    {
        return Combo(name, ref value, Enum.GetValues<T>(), t => t.ToString(), cache);
    }
    
    public static bool Combo<T>(string name, ref T value, IList<T> values, Func<T, string> toString, ComboCache<T> cache, string? tooltip = null, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
    {
        var valueName = toString(value);
        bool changed = false;

        if (ImGui.BeginCombo(name, valueName, flags).WithTooltip(tooltip))
        {
            var search = cache.Search;
            if (ImGui.InputText("Search", ref search, 512))
                cache.Search = search;

            cache ??= new();
            var filtered = cache.GetValue(values, toString, search);

            foreach (var item in filtered)
            {
                if (ImGui.MenuItem(toString(item)))
                {
                    value = item;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    public static bool DragFloat2(string name, ref Vector2 vec)
    {
        var nv = new System.Numerics.Vector2(vec.X, vec.Y);
        var ret = ImGui.DragFloat2(name, ref nv);

        vec = new(nv.X, nv.Y);

        return ret;
    }

    public static void DecompilableMethod(MethodBase method)
    {
        ImGui.TreeNodeEx(method.GetID(simple: true) ?? "", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        AddDecompilationTooltip(method);
    }
    
    public static void DecompilableType(Type type)
    {
        ImGui.TreeNodeEx(type.FullName ?? "", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        AddDecompilationTooltip(type, null);
    }

    public static void AddDecompilationTooltip(MethodBase method)
    {
        if (method.DeclaringType is not { } decl)
            return;
        
        AddDecompilationTooltip(decl, method);
    }
    
    public static void AddDecompilationTooltip(Type type, MethodBase? method)
    {
        AddTooltip("Click to open C# decompilation");
        if (!ImGui.IsItemClicked())
            return;
        
        var existing = ImGuiManager.Handlers.FirstOrDefault(h =>
            h is DecompilationWindow dec && dec.Type == type);
        if (existing is null)
            Engine.Scene.OnEndOfFrame += () => ImGuiManager.Handlers.Add(new DecompilationWindow(type, method));
    }
    
    private static Dictionary<string, (RenderTarget2D Target, nint ID)> Targets = new(StringComparer.Ordinal);
    

    private static ImGuiRenderer Renderer => new DynamicData(new DynamicData(ImGuiHelperModule.Instance)
        .Get<ImGuiManager>("imGuiManager")!)
        .Get<ImGuiRenderer>("renderer")!;
    
    public static void XnaWidget(string id, int w, int h, Action renderFunc, Matrix matrix, bool rerender = true, float imguiScale = 1f) {
        if (w <= 0 || h <= 0)
            return;

        var renderer = Renderer;
        
        bool isNew = false;
        if (!Targets.TryGetValue(id, out var t) || t.Target.Width != w || t.Target.Height != h) {
            if (t.Target != null) {
                renderer.UnbindTexture(t.ID);
                t.Target.Dispose();
            }

            t.Target = new(Engine.Instance.GraphicsDevice, w, h, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            t.ID = renderer.BindTexture(t.Target);
            Targets[id] = t;
            isNew = true;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1f);
        ImGui.Image(t.ID, new NumVector2(w, h) * imguiScale);
        ImGui.PopStyleVar(1);
        
        if ((rerender || isNew) && ImGui.IsItemVisible())
        {
            Engine.Scene.OnEndOfFrame += () =>
            {
                var g = Engine.Instance.GraphicsDevice;
                g.SetRenderTarget(t.Target);
                g.Clear(Color.Transparent);

                Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, matrix);
                
                renderFunc();
                
                Draw.SpriteBatch.End();
                g.SetRenderTarget(null);
            };
        }
    }

    public static void DisposeXnaWidget(string id) {
        if (Targets.TryGetValue(id, out var t)) {
            Renderer.UnbindTexture(t.ID);
            t.Target?.Dispose();
            Targets.Remove(id);
        }
    }
}

public class ComboCache<T>
{
    public ComboCache()
    {
    }

    private List<KeyValuePair<T, string>>? CachedValueDict;
    private List<T>? CachedValue;
    private string? CachedSearch;
    private HashSet<string>? CachedFavorites;

    internal void Clear()
    {
        CachedValue = null;
        CachedValueDict = null;
        CachedSearch = null;
    }

    internal List<KeyValuePair<T, string>> GetValue(IDictionary<T, string> values, string search)
    {
        if (search != CachedSearch)
        {
            CachedValueDict = null;
        }

        CachedValueDict ??= values.SearchFilter(i => i.Value, search).ToList();
        CachedSearch = search;

        return CachedValueDict;
    }

    internal List<T> GetValue(IEnumerable<T> values, Func<T, string> toString, string search, HashSet<string>? favorites = null)
    {
        if (search != CachedSearch || (favorites is null != CachedFavorites is null) || (CachedFavorites?.SetEquals(favorites!) ?? false))
        {
            CachedValue = null;
        }

        CachedValue ??= values.SearchFilter(i => toString(i), search, favorites).ToList();
        CachedSearch = search;

        return CachedValue;
    }

    private string _Search = "";
    internal string Search
    {
        get => _Search ?? "";
        set
        {
            Clear();
            _Search = value;
        }
    }
}
