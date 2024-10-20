using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.MappingUtils.Helpers;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

public class ParticleTab : Tab
{
    public override string Name => "Particles";

    public override bool CanBeVisible() => true;

    private bool _particleSet;
    
    public ParticleType Particle;

    public ParticleSystem System = new(int.MaxValue, 500);

    private Camera PreviewCamera = new()
    {
        Zoom = 6f,
    };
    
    /*
/// <summary>If set, the particle will use this texture.</summary>
    public MTexture Source;
    /// <summary>
    /// If set, the particle will use a texture from its choices.
    /// </summary>
    public Chooser<MTexture> SourceChooser;

     */
    
    public override void Render(Level? level)
    {
        if (!_particleSet)
        {
            _particleSet = true;
            Particle = ParticleTypes.Dust;
        }
        
        var w = (int)ImGui.GetWindowWidth();
        var h = 300;
        var scale = 6f;
        
        ImGuiExt.XnaWidget("particlePreview", w, h, () =>
        {
            System.Emit(Particle, new Vector2(w / scale / 2f, h / scale / 2f));
            
            System.Update();
            System.Render();
        }, Matrix.CreateScale(scale));
        
        ImGui.Separator();
        
        //SourceChooser
        if (Particle.Source is { })
        {
            var a = (Particle.Source, Particle.Source.AtlasPath);
            if (ImGuiExt.Combo("Source", ref a, Textures, p => p.Item2, TextureCombo))
            {
                Particle.Source = a.Source;
            }
        }

        
        ImGuiExt.ColorEdit("Color", ref Particle.Color, ColorFormat.RGBA);
        ImGuiExt.ColorEdit("Color2", ref Particle.Color2, ColorFormat.RGBA);
        
        ImGuiExt.EnumCombo("ColorMode", ref Particle.ColorMode, ColorModeCombo);
        ImGuiExt.EnumCombo("FadeMode", ref Particle.FadeMode, FadeModeCombo);
        ImGuiExt.EnumCombo("RotationMode", ref Particle.RotationMode, RotationModeCombo);
        
        ImGui.InputFloat("SpeedMin", ref Particle.SpeedMin);
        ImGui.InputFloat("SpeedMax", ref Particle.SpeedMax);
        ImGui.InputFloat("SpeedMultiplier", ref Particle.SpeedMultiplier);

        var acc = new NumVector2(Particle.Acceleration.X, Particle.Acceleration.Y);
        if (ImGui.InputFloat2("Acceleration", ref acc))
            Particle.Acceleration = new(acc.X, acc.Y);
        
        ImGui.InputFloat("Friction", ref Particle.Friction);
        ImGui.InputFloat("Direction", ref Particle.Direction);
        ImGui.InputFloat("DirectionRange", ref Particle.DirectionRange);
        
        ImGui.InputFloat("LifeMin", ref Particle.LifeMin);
        ImGui.InputFloat("LifeMax", ref Particle.LifeMax);
        
        ImGui.InputFloat("Size", ref Particle.Size);
        ImGui.InputFloat("SizeRange", ref Particle.SizeRange);
        
        ImGui.InputFloat("SpinMin", ref Particle.SpinMin);
        ImGui.InputFloat("SpinMax", ref Particle.SpinMax);

        ImGui.Checkbox("SpinFlippedChance", ref Particle.SpinFlippedChance).WithTooltip("If true, the spin direction has a 50% chance of being flipped.");
        ImGui.Checkbox("ScaleOut", ref Particle.ScaleOut).WithTooltip("Whether the particle size will cube out over its lifetime.");
    }


    private static readonly ComboCache<ParticleType.ColorModes> ColorModeCombo = new();
    private static readonly ComboCache<ParticleType.FadeModes> FadeModeCombo = new();
    private static readonly ComboCache<ParticleType.RotationModes> RotationModeCombo = new();

    private static List<(MTexture, string)> Textures = GFX.Game.Textures.Select(p => (p.Value, p.Key)).ToList();
    
    private static readonly ComboCache<(MTexture, string)> TextureCombo = new();
}