using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod.MappingUtils.Helpers;

internal static partial class LoennFormatHelper
{
    public static string ToLoennEntity(EntityData data)
    {
        StringBuilder sb = new();

        sb.Append(CultureInfo.InvariantCulture, $$"""
        {
            {
                _fromLayer = "entities",
                _type = "entity",
                _id = {{ToLuaLiteral(data.ID)}},
                _name = {{ToLuaLiteral(data.Name)}},
                x = {{ToLuaLiteral(data.Position.X)}},
                y = {{ToLuaLiteral(data.Position.Y)}},
        """);

        if (data.Width != 0)
            sb.Append(CultureInfo.InvariantCulture, $"        width = {ToLuaLiteral(data.Width)},");
        if (data.Height != 0)
            sb.Append(CultureInfo.InvariantCulture, $"        width = {ToLuaLiteral(data.Height)},");

        if (data.Nodes is { Length: > 0 })
        { 
            const string sep = ",\n            ";
            var nodesString = string.Join(sep, data.Nodes.Select(n => $"{{ x={ToLuaLiteral(n.X)}, y={ToLuaLiteral(n.Y)} }}"));
            sb.AppendLine(CultureInfo.InvariantCulture, $$"""
                    nodes = {
                        {{nodesString}}
                    },
            """);
        }

        foreach (var (k, v) in data.Values ?? [])
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"        {TableKeyString(k)} = {ToLuaLiteral(v)},");
        }
        
        sb.Append(CultureInfo.InvariantCulture, $$"""
            }
        }
        """);
        
        return sb.ToString();
        /*
{
    {
        _fromLayer = "entities",
        _name = "FemtoHelper/ParticleEmitter",
        _id = 0,
        _type = "entity",
        particleSpinSpeedMin = 4,
        particleScaleOut = true,
        particleAlpha = 1,
        flag = "",
        particleCount = 1,
        particleAccelY = -60,
        particleLifespanMin = 1.25,
        particleColor = "ff7777",
        tag = "",
        particleRotationMode = 1,
        particleFriction = 50,
        bloomRadius = 6,
        particleSpeedMax = 20,
        foreground = false,
        particleSpeedMin = 10,
        particleAngleRange = 360,
        particleSizeRange = 0.5,
        particleTexture = "particles/feather",
        spawnChance = 90,
        particleAngle = 180,
        particleColorMode = 3,
        particleAccelX = 30,
        particleSpawnSpread = 4,
        particleColor2 = "9c2e36",
        particleSpinSpeedMax = 8,
        particleFlipChance = true,
        bloomAlpha = 0,
        particleFadeMode = 2,
        noTexture = false,
        particleSize = 1.5,
        particleLifespanMax = 1.75,
        spawnInterval = 0.1,
        x = 152,
        y = 24,
    },
}
         */
    }
    
    private static readonly char[] EscapableChars = new char[] { '\a', '\b', '\f', '\n', '\r', '\t', '\v', '\\', '"', '\'' };
    private static readonly Dictionary<char, string> EscapeSequences = new() {
        ['\a'] = @"\a",
        ['\b'] = @"\b",
        ['\f'] = @"\f",
        ['\n'] = @"\n",
        ['\r'] = @"\r",
        ['\t'] = @"\t",
        ['\v'] = @"\v",
        ['\0'] = @"\0",
    };
    
    private static string ToLuaLiteral(object obj) => obj switch {
        string s => $@"""{SanitizeString(s)}""",
        char c => $"\"{SanitizeString(c.ToString())}\"",
        int i => i.ToString(CultureInfo.InvariantCulture),
        long i => i.ToString(CultureInfo.InvariantCulture),
        float f => f.ToString(CultureInfo.InvariantCulture),
        double f => f.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        null => "nil",
        _ => throw new InvalidOperationException($"Cannot convert object to lua literal: {obj} [{obj.GetType().FullName}]"),
    };
    
    private static string SanitizeString(string s) {
        StringBuilder builder = new(s.Length);
        var span = s.AsSpan();
        int i;
        var escapable = EscapableChars;
        var sequences = EscapeSequences;

        while ((i = span.IndexOfAny(escapable.AsSpan())) > -1) {
            builder.Append(span[..i]);
            if (sequences.TryGetValue(span[i], out var escape)) {
                builder.Append(escape);
            } else {
                builder.Append('\\');
                builder.Append(span[i]);
            }

            span = span[(i+1)..];
        }
        builder.Append(span);

        return builder.ToString();
    }
    
    private static string TableKeyString(string key) {
        if (IsValidKey(key))
            return key;

        return $"""
                ["{ToLuaLiteral(key)}"]
                """;
    }

    private static bool IsValidKey(string key) {
        if (LuaKeywords.Contains(key))
            return false;

        return VariableNameRegex().IsMatch(key);
    }

    private static readonly HashSet<string> LuaKeywords =
    [
        "and", "break", "do", "else", "elseif", "end", "false", "for",
        "function", "goto", "if", "in", "local", "nil", "not", "or",
        "repeat", "return", "then", "true", "until", "while"
    ];
    
    [GeneratedRegex("^[a-zA-Z_][\\w_]*$")]
    private static partial Regex VariableNameRegex();
}