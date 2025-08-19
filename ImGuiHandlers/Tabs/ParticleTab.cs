using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.MappingUtils.Api;
using Celeste.Mod.MappingUtils.Helpers;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal sealed class ParticleTab : Tab
{
    public override string Name => "Particles";

    public override bool CanBeVisible() => true;
    
    private ParticleType? _particle;

    private readonly ParticleSystem _system = new(int.MaxValue, 500);
    
    private ParticleEmitter? _emitter;

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
        var w = (int)ImGui.GetWindowWidth();
        var h = 300;
        var scale = 6f;

        _particle ??= new ParticleType(ParticleTypes.Dust)
        {
            Source = GFX.Game["particles/smoke0"] // TODO: support SourceChooser.
        };
        
        if (_emitter is null)
        {
            var dummy = new Entity();
            _emitter = new ParticleEmitter(_system, _particle, default, default, 1, 0f);
            dummy.Add(_emitter);
        }

        _emitter.Position = new Vector2(w / scale / 2f, h / scale / 2f);
        
        ImGuiExt.XnaWidget("particlePreview", w / 6, h / 6, () =>
        {
            _emitter.Update();
            
            _system.Update();
            _system.Render();
        }, /*Matrix.CreateScale(scale)*/Matrix.Identity, imguiScale: 6f);

        if (ImGui.BeginChild("fields", ImGui.GetContentRegionAvail()))
        {
            ImGui.SeparatorText("Texture");
            
            var a = (_particle.Source, _particle.Source?.AtlasPath ?? "");
            if (ImGuiExt.Combo("Source", ref a, Textures, p => p.Item2 ?? "", TextureCombo))
            {
                _particle.Source = a.Source;
                _particle.SourceChooser = null;
            }
            ImGui.InputFloat("Size", ref _particle.Size);
            ImGui.InputFloat("Size Range", ref _particle.SizeRange);
            
            ImGui.SeparatorText("Colors");
            ImGuiExt.ColorEdit("Color", ref _particle.Color, ColorFormat.RGBA);
            ImGuiExt.ColorEdit("Color2", ref _particle.Color2, ColorFormat.RGBA);
            ImGuiExt.EnumCombo("ColorMode", ref _particle.ColorMode, ColorModeCombo);
            ImGuiExt.EnumCombo("FadeMode", ref _particle.FadeMode, FadeModeCombo);
            
            ImGui.SeparatorText("Speed");
            ImGui.InputFloat("Min", ref _particle.SpeedMin);
            ImGui.InputFloat("Max", ref _particle.SpeedMax);
            ImGui.InputFloat("Multiplier", ref _particle.SpeedMultiplier);
            var acc = new NumVector2(_particle.Acceleration.X, _particle.Acceleration.Y);
            if (ImGui.InputFloat2("Acceleration", ref acc))
                _particle.Acceleration = new(acc.X, acc.Y);
            ImGui.InputFloat("Friction", ref _particle.Friction);

            ImGui.SeparatorText("Rotation");
            var dir = _particle.Direction.ToDeg();
            ImGui.InputFloat("Direction", ref dir);
            _particle.Direction = dir.ToRad();
            var dirRange = _particle.DirectionRange.ToDeg();
            ImGui.InputFloat("DirectionRange", ref dirRange);
            _particle.DirectionRange = dirRange.ToRad();
            ImGui.InputFloat("SpinMin", ref _particle.SpinMin);
            ImGui.InputFloat("SpinMax", ref _particle.SpinMax);
            ImGui.Checkbox("SpinFlippedChance", ref _particle.SpinFlippedChance).WithTooltip("If true, the spin direction has a 50% chance of being flipped.");
            ImGui.Checkbox("ScaleOut", ref _particle.ScaleOut).WithTooltip("Whether the particle size will cube out over its lifetime.");
            ImGuiExt.EnumCombo("RotationMode", ref _particle.RotationMode, RotationModeCombo);
            
            ImGui.SeparatorText("Lifetime");
            ImGui.InputFloat("Min##Life", ref _particle.LifeMin);
            ImGui.InputFloat("Max##Life", ref _particle.LifeMax);
            
            ImGui.SeparatorText("Emitter");
            ImGui.InputFloat("Interval", ref _emitter.Interval);
            ImGui.InputFloat("Range Min", ref _emitter.Range.X);
            ImGui.InputFloat("Range Max", ref _emitter.Range.Y);
            ImGui.InputInt("Amount", ref _emitter.Amount);

            var hasDir = _emitter.Direction is { };
            var emitterDir = _emitter.Direction ?? 0f;
            if (ImGui.Checkbox("", ref hasDir))
            {
                _emitter.Direction = hasDir ? emitterDir : null;
            }
            ImGui.SameLine();
            ImGui.BeginDisabled(!hasDir);
            if (ImGui.InputFloat("Direction", ref emitterDir))
            {
                _emitter.Direction = emitterDir;
            }
            ImGui.EndDisabled();
            
            ImGui.SeparatorText("Export");
            foreach (var exporter in ParticleExporterRegistry.Exporters)
            {
                exporter.ImGuiRenderFunc(_particle, _emitter);
            }
        
            ImGui.EndChild();
        }
    }

    private static readonly ComboCache<ParticleType> PresetCombo = new();
    private static readonly ComboCache<ParticleType.ColorModes> ColorModeCombo = new();
    private static readonly ComboCache<ParticleType.FadeModes> FadeModeCombo = new();
    private static readonly ComboCache<ParticleType.RotationModes> RotationModeCombo = new();

    private static List<(MTexture, string)> Textures = GFX.Game.Textures.Select(p => (p.Value, p.Key)).ToList();
    
    private static readonly ComboCache<(MTexture, string)> TextureCombo = new();
}

internal static class ParticleExporterRegistry
{
    private static string ToCSharpConstructorCode(Color color) => $"new Color({color.R}, {color.G}, {color.B}, {color.A})";
    private static string ToCSharpConstructorCode(Vector2 v) => $"new Vector2({v.X}f, {v.Y}f)";
    private static string ToCSharpConstructorCode(float f) => $"{f}f";
    private static string ToCSharpConstructorCode(bool b) => b ? "true" : "false";

    private static string ToCSharpConstructorCode<T>(T b) where T : struct, Enum
    {
        return $"{b.GetType().FullName}.{b.ToString()}";
    }
    
    private static readonly List<ParticleExporter> _exporters = [
        new ParticleExporter()
        {
            Name = "C# Declaration",
            ImGuiRenderFunc = CreateClipboardRenderFunc(
                "", "C# Declaration", "Exports this particle as C# code that creates a ParticleType.",
                (particle, emitter) => $$"""
                    new ParticleType()
                    {
                        Source = {{(particle.Source is { AtlasPath: {} path } ? $"GFX.Game[\"{path}\"]" : "null")}},
                        SourceChooser = null,
                        Color = {{ToCSharpConstructorCode(particle.Color)}},
                        Color2 = {{ToCSharpConstructorCode(particle.Color2)}},
                        ColorMode = ParticleType.ColorModes.{{particle.ColorMode}},
                        FadeMode = ParticleType.FadeModes.{{particle.FadeMode}},
                        SpeedMin = {{ToCSharpConstructorCode(particle.SpeedMin)}},
                        SpeedMax = {{ToCSharpConstructorCode(particle.SpeedMax)}},
                        SpeedMultiplier = {{ToCSharpConstructorCode(particle.SpeedMultiplier)}},
                        Acceleration = {{ToCSharpConstructorCode(particle.Acceleration)}},
                        Friction = {{ToCSharpConstructorCode(particle.Friction)}},
                        Direction = {{ToCSharpConstructorCode(particle.Direction)}},
                        DirectionRange = {{ToCSharpConstructorCode(particle.DirectionRange)}},
                        LifeMin = {{ToCSharpConstructorCode(particle.LifeMin)}},
                        LifeMax = {{ToCSharpConstructorCode(particle.LifeMax)}},
                        Size = {{ToCSharpConstructorCode(particle.Size)}},
                        SizeRange = {{ToCSharpConstructorCode(particle.SizeRange)}},
                        RotationMode = ParticleType.RotationModes.{{particle.RotationMode}},
                        SpinMin = {{ToCSharpConstructorCode(particle.SpinMin)}},
                        SpinMax = {{ToCSharpConstructorCode(particle.SpinMax)}},
                        SpinFlippedChance = {{ToCSharpConstructorCode(particle.SpinFlippedChance)}},
                        ScaleOut = {{ToCSharpConstructorCode(particle.ScaleOut)}},
                        UseActualDeltaTime = {{ToCSharpConstructorCode(particle.UseActualDeltaTime)}},
                    };
                    """)
        },
        new ParticleExporter()
        {
            Name = "FemtoHelper/ParticleEmitter",
            ImGuiRenderFunc = CreateClipboardRenderFunc(
                "Femto Helper", "Particle Emitter", "Exports this particle as a Femto Helper Particle Emitter to the clipboard in Loenn format.",
                (particle, emitter) =>
                {
                    var data = new EntityData
                    {
                        Name = "FemtoHelper/ParticleEmitter",
                        Values = new()
                        {
                            ["particleTexture"] = particle.Source?.AtlasPath ?? "",
                            ["noTexture"] = (particle.Source?.AtlasPath ?? "") is "",
                            ["particleSpinSpeedMin"] = particle.SpinMin,
                            ["particleScaleOut"] = particle.ScaleOut,
                            ["particleAlpha"] = 1,
                            ["flag"] = "",
                            ["particleCount"] = emitter.Amount,
                            ["particleAccelY"] = particle.Acceleration.Y,
                            ["particleLifespanMin"] = particle.LifeMin,
                            ["particleColor"] = particle.Color.ToRGBAString(),
                            ["tag"] = "",
                            ["particleRotationMode"] = (int)particle.RotationMode,
                            ["particleFriction"] = particle.Friction,
                            ["bloomRadius"] = 6,
                            ["particleSpeedMax"] = particle.SpeedMax,
                            ["foreground"] = false,
                            ["particleSpeedMin"] = particle.SpinMin,
                            ["particleAngleRange"] = particle.DirectionRange,
                            ["particleSizeRange"] = particle.SizeRange,
                            ["spawnChance"] = 100,
                            ["particleAngle"] = particle.Direction,
                            ["particleColorMode"] = particle.ColorMode.ToString(),
                            ["particleAccelX"] = particle.Acceleration.X,
                            ["particleSpawnSpread"] = particle.DirectionRange,// 4, // TODO: is this correct?
                            ["particleColor2"] = particle.Color2.ToRGBAString(),
                            ["particleSpinSpeedMax"] = particle.SpinMax,
                            ["particleFlipChance"] = particle.SpinFlippedChance,
                            ["bloomAlpha"] = 0,
                            ["particleFadeMode"] = particle.FadeMode.ToString(),
                            ["particleSize"] = particle.Size,
                            ["particleLifespanMax"] = particle.LifeMax,
                            ["spawnInterval"] = float.Max(emitter.Interval, 0.00001f),

                        }
                    };

                    return LoennFormatHelper.ToLoennEntity(data); 
                })
        }
    ];
    
    public static IEnumerable<ParticleExporter> Exporters => _exporters;

    public static void Register(ParticleExporter exporter)
    {
        _exporters.RemoveAll(x => x.Name == exporter.Name);
        _exporters.Add(exporter);
    }

    public static Action<ParticleType, ParticleEmitter> CreateClipboardRenderFunc
        (string modName, string name, string tooltip, Func<ParticleType, string?> exportFunc) => (particle, emitter) =>
    {
        if (ImGui.Button($"{name} [{modName}]").WithTooltip(tooltip).WithTooltip(() =>
            {
                ImGui.TextColored(Color.LightGray.ToNumVec4(), $"Requires mod '{modName}'");
            }))
        {
            var text = exportFunc(particle);
            TextInput.SetClipboardText(text);
        }
    };
    
    public static Action<ParticleType, ParticleEmitter> CreateClipboardRenderFunc
        (string modName, string name, string tooltip, Func<ParticleType, ParticleEmitter, string?> exportFunc) => (particle, emitter) =>
    {
        if (string.IsNullOrWhiteSpace(modName))
        {
            if (ImGui.Button(name).WithTooltip(tooltip))
            {
                var text = exportFunc(particle, emitter);
                if (text is not null)
                    TextInput.SetClipboardText(text);
            }
            
            return;
        }
        
        if (ImGui.Button($"{name} [{modName}]").WithTooltip(tooltip).WithTooltip(() =>
            {
                ImGui.TextColored(Color.LightGray.ToNumVec4(), $"Requires mod '{modName}'");
            }))
        {
            var text = exportFunc(particle, emitter);
            if (text is not null)
                TextInput.SetClipboardText(text);
        }
    };
}

internal sealed class ParticleExporter
{
    public required string Name { get; init; }
    
    public required Action<ParticleType, ParticleEmitter> ImGuiRenderFunc { get; init; }
}