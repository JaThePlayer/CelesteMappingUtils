using Celeste.Mod.MappingUtils.ModIntegration;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

public class StylegroundViewTab : Tab
{
    public override string Name => "Stylegrounds";

    private static Backdrop? Selection;

    public static List<string> BlendModes = null!;
    private static ComboCache<string> BlendModeCombo = new();

    public override void Render(Level level)
    {
        if (BlendModes is null)
        {
            BlendModes = new() { "additive", "alphablend" };
            if (IntegrationUtils.EeveeHelperLoaded.Value)
            {
                BlendModes.Add("multiply");
                BlendModes.Add("subtract");
                BlendModes.Add("reversesubtract");
            }
        }

        if (Selection is { } s)
        {
            ImGui.Text($"Edit style - {GetName(s)}");

            ImGui.SetNextItemWidth(ItemWidth);
            ImGuiExt.ColorEdit("Color", ref s.Color, Helpers.ColorFormat.RGBA, null);

            ImGui.SetNextItemWidth(ItemWidth);
            ImGuiExt.DragFloat2("Position", ref s.Position);

            ImGui.SetNextItemWidth(ItemWidth);
            ImGuiExt.DragFloat2("Scroll", ref s.Scroll);

            ImGui.SetNextItemWidth(ItemWidth);
            ImGuiExt.DragFloat2("Speed", ref s.Speed);

            ImGui.Checkbox("FlipX", ref s.FlipX);
            ImGui.SameLine();
            ImGui.Checkbox("FlipY", ref s.FlipY);

            ImGui.Checkbox("LoopX", ref s.LoopX);
            ImGui.SameLine();
            ImGui.Checkbox("LoopY", ref s.LoopY);

            if (FrostHelperAPI.GetBackdropBlendState is { } getState)
            {
                var blendState = getState(s);
                string? blendStateName = GetBlendStateName(blendState);

                if (blendStateName is { })
                {
                    ImGui.SetNextItemWidth(ItemWidth);
                    if (ImGuiExt.Combo("Blend Mode", ref blendStateName, BlendModes, x => x, BlendModeCombo))
                    {
                        FrostHelperAPI.SetBackdropBlendState(s, blendStateName switch
                        {
                            "alphablend" => BlendState.AlphaBlend,
                            "additive" => BlendState.Additive,
                            "subtract" => GFX.Subtract,
                            "reversesubtract" => EeveeHelper_ReverseSubtract,
                            "multiply" => EeveeHelper_Multiply,
                            _ => BlendState.Opaque
                        });
                    }
                }
                else
                {
                    // account for the blend mode option for parallaxes to make the ui not jump
                    ImGui.NewLine();
                }
            }
        }

        RenderStyleList(level);
    }

    private static string? GetBlendStateName(BlendState? blendState)
    {
        if (blendState == null)
            return null;

        var blendStateName = blendState.Name switch
        {
            "BlendState.AlphaBlend" => "alphablend",
            "BlendState.Additive" => "additive",
            _ => null
        };

        if (blendStateName is null && IntegrationUtils.EeveeHelperLoaded.Value)
        {
            if (blendState is
                {
                    ColorBlendFunction: BlendFunction.Add,
                    ColorSourceBlend: Blend.DestinationColor,
                    ColorDestinationBlend: Blend.Zero
                })
            {
                blendStateName = "multiply";
            }
            else if (blendState is
            {
                ColorSourceBlend: Blend.One,
                ColorDestinationBlend: Blend.One,
                ColorBlendFunction: BlendFunction.ReverseSubtract,
                AlphaSourceBlend: Blend.One,
                AlphaDestinationBlend: Blend.One,
                AlphaBlendFunction: BlendFunction.Add
            })
            {
                blendStateName = "subtract";
            }
            else if (blendState is
            {
                ColorSourceBlend: Blend.One,
                ColorDestinationBlend: Blend.One,
                ColorBlendFunction: BlendFunction.Subtract,
                AlphaSourceBlend: Blend.One,
                AlphaDestinationBlend: Blend.One,
                AlphaBlendFunction: BlendFunction.Add
            })
            {
                blendStateName = "reversesubtract";
            }
        }

        return blendStateName;
    }

    private static BlendState EeveeHelper_ReverseSubtract = new BlendState
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Subtract,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add
    };

    private static BlendState EeveeHelper_Multiply = new BlendState
    {
        ColorBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero
    };

    private static void RenderStyleList(Level level)
    {
        if (!ImGui.BeginChild("list", new(ImGui.GetColumnWidth() - ImGui.GetStyle().FramePadding.X * 2, ImGui.GetWindowHeight() - 500f)))
            return;

        var flags = ImGuiExt.TableFlags;
        var textBaseWidth = ImGui.CalcTextSize("A").X;

        if (!ImGui.BeginTable("Styles", 2, flags))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Rooms", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 12f);
        ImGui.TableHeadersRow();

        foreach (var style in level.Foreground.Backdrops)
        {
            RenderStyleImgui(style);
        }
        foreach (var style in level.Background.Backdrops)
        {
            RenderStyleImgui(style);
        }

        ImGui.EndTable();
        ImGui.EndChild();
    }

    private static void SetOrAddSelection(Backdrop style)
    {
        Selection = style;
    }

    private static void RenderStyleImgui(Backdrop style)
    {
        var id = style.GetHashCode();

        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        var flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanFullWidth;
        if (Selection == style)
        {
            flags |= ImGuiTreeNodeFlags.Selected;
        }

        var open = ImGui.TreeNodeEx($"##{id}", flags);
        var clicked = ImGui.IsItemClicked();
        if (clicked)
        {
            SetOrAddSelection(style);
        }

        ImGui.SameLine();
        if (!style.Visible)
        {
            ImGui.BeginDisabled();
        }

        ImGui.Text(GetName(style));

        RenderOtherTabs(style);

        if (!style.Visible)
        {
            ImGui.EndDisabled();
        }
    }

    private static void RenderOtherTabs(Backdrop style)
    {
        ImGui.TableNextColumn();

        var only = style.OnlyIn;

        ImGui.Text(only is { } ? string.Join(',', only) : "");
    }

    private static string GetName(Backdrop style)
        => style.Name ?? style.GetType().Name ?? "";
}
