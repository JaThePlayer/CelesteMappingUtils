using Celeste.Mod.Helpers;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.MappingUtils.ModIntegration;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Celeste.Mod.MappingUtils.ImGuiHandlers;

namespace Celeste.Mod.MappingUtils.Commands;

using OPC = System.Reflection.Emit.OpCodes;
public static class ILDiff
{
    [Command("ildiff", "[Mapping Utils] Creates a diff of the IL of a method and its IL hooks, and logs it to the console.")]
    public static void Diff(string typeFullName, string methodName)
    {
        MappingUtilsModule.WriteToIngameLog = true;
        
        var m = MappingUtilsModule.FindMethod(typeFullName, methodName, out _);
        if (m is { })
        {
            var diff = new MethodDiff(m);

            diff.PrintToConsole();
            ImGuiManager.Handlers.Add(new IlDiffView(diff));
        }

        MappingUtilsModule.WriteToIngameLog = false;
    }

    private const string DiffAllTag = "MappingUtils.ILDiffAll";
    
    [Command("ildiff_all", "[Mapping Utils] IL diffs each method in the game, writing the result to the given directory")]
    public static void DiffAll(string directory)
    {
        MappingUtilsModule.WriteToIngameLog = true;
        var time = DateTime.Now;
        Directory.CreateDirectory(directory);

        var db = new DBFile
        {
            Time = time,
            EverestVersion = Everest.VersionString
        };

        foreach (var module in Everest.Modules)
        {
            if (module.Metadata.DLL is { })
                db.Mods.Add(new(module.Metadata.Name, module.Metadata.VersionString));
        }

        foreach (MethodBase key in HookDiscovery.GetHookedMethods())
        {
            var methodName = GetMethodNameForDB(key);
            var methodNameAsDirName = NameAsValidFilename(methodName);
            var dir = Path.Combine(directory, methodNameAsDirName);
            var detourInfo = DetourManager.GetDetourInfo(key);
            HashSet<string> fileList = [];
            
            if (detourInfo.ILHooks.Any())
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    MappingUtilsModule.Log(LogLevel.Info, DiffAllTag, $"Dumping {key.GetID()} to {dir}");

                    using var ilFileStream = File.Create(Path.Combine(dir, "ildiff.txt"));
                    var diff = new MethodDiff(key);
                    diff.PrintToStream(ilFileStream);
                    
                    fileList.Add("ildiff.txt");
                    
                } catch (Exception ex)
                {
                    MappingUtilsModule.Log(LogLevel.Warn, DiffAllTag, $"Failed to dump: {key.GetID()}: {ex}");
                }
            }

            if (detourInfo.Detours.Any() || detourInfo.ILHooks.Any())
            {
                Directory.CreateDirectory(dir);
                fileList.Add("allhooks.txt");

                var allHooks = detourInfo.Detours.Select(d => $"On: {d.Entry.GetID()}")
                    .Concat(detourInfo.ILHooks.Select(d => $"IL: {d.ManipulatorMethod.GetID()}")).ToList();
                
                db.Methods.Add(new(methodName, methodNameAsDirName, allHooks));
                File.WriteAllLines(Path.Combine(dir, "allhooks.txt"), allHooks);
            }

            if (fileList.Count > 0)
            {
                Directory.CreateDirectory(dir);
                var fileListFile = new FileInfo(Path.Combine(dir, "files.json"));
                using var fileListWriteStream = fileListFile.Open(FileMode.Create, FileAccess.Write);
                JsonSerializer.Serialize(fileListWriteStream, fileList);
            }
        }
        
        using var dbStream = File.Open(Path.Combine(directory, "info.json"), FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(dbStream, db);
        
        MappingUtilsModule.WriteToIngameLog = false;
    }

    public static string GetMethodNameForDB(this MethodBase method)
    {
        while (method is MethodInfo mi && method.IsGenericMethod && !method.IsGenericMethodDefinition)
            method = mi.GetGenericMethodDefinition();

        var builder = new StringBuilder();

        if (method.DeclaringType != null)
            builder.Append(method.DeclaringType!.FullName?.Replace("+", "/", StringComparison.Ordinal)).Append(".");

        builder.Append(method.Name);

        if (method.ContainsGenericParameters)
        {
            builder.Append('<');
            Type[] arguments = method.GetGenericArguments();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(arguments[i].Name);
            }
            builder.Append('>');
        }

        builder.Append('(');

        ParameterInfo[] parameters = method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            ParameterInfo parameter = parameters[i];
            if (i > 0)
                builder.Append(',');

            bool defined;
            try
            {
                defined = parameter.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length != 0;
            }
            catch (NotSupportedException)
            {
                // Newer versions of Mono are stupidly strict and like to throw a NotSupportedException on DynamicMethod args.
                defined = false;
            }
            if (defined)
                builder.Append("...,");

            builder.Append(parameter.ParameterType.Name);
        }

        builder.Append(')');

        return builder.ToString();
    }

    static void ThrowIf([NotNull] object? src, [CallerArgumentExpression(nameof(src))] string caller = default!)
    {
        if (src is null)
        {
            throw new NullReferenceException(caller);
        }
    }

    static Func<object, T> CreateGetter<T>(this FieldInfo? field, [CallerArgumentExpression(nameof(field))] string caller = default!)
    {
        ThrowIf(field, caller);
        var method = new DynamicMethod($"get_{field.Name}", typeof(T), [typeof(object), typeof(object)]);
        var il = method.GetILGenerator();

        il.Emit(OPC.Ldarg_1);
        il.Emit(OPC.Castclass, field.DeclaringType!);
        il.Emit(OPC.Ldfld, field);
        if (field.FieldType.IsValueType && !typeof(T).IsValueType)
        {
            il.Emit(OPC.Box);
        }
        il.Emit(OPC.Ret);

        return method.CreateDelegate<Func<object, T>>(field);
    }
    static FieldInfo? m_scope;
    static Type? m_dynamicscope;
    static FieldInfo? m_ILStream;
    static FieldInfo? m_tokens;

    static Func<object, object>? get_scope;
    static Func<object, byte[]>? get_ILStream;
    static Func<object, List<object>>? get_tokens;

    static Func<object, RuntimeMethodHandle>? get_methodHandle;
    static Func<object, RuntimeTypeHandle>? get_context;

    internal static MethodBase TryGetActualEntry(this MethodBase method)
    {
        const BindingFlags bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        try
        {
            if (method is DynamicMethod dm)
            {
                var dil = dm.GetILGenerator();
                m_scope ??= dil.GetType().GetField("m_scope", bf);
                ThrowIf(m_scope);
                get_scope ??= m_scope.CreateGetter<object>();
                get_ILStream ??= dil.GetType().BaseType!.GetField("m_ILStream", bf).CreateGetter<byte[]>();
                get_tokens ??= m_scope.FieldType.GetField("m_tokens", bf).CreateGetter<List<object>>();

                var ilstream = get_ILStream(dil);
                ThrowIf(ilstream);
                var scope = get_scope(dil);
                ThrowIf(scope);
                var tokens = get_tokens(scope);
                ThrowIf(tokens);

                var ils = ilstream.AsSpan();
                static bool TrimLdcI4(ref Span<byte> self)
                {
                    if (self is [0x20, _, _, _, _, ..])
                    {
                        self = self[5..];
                    }
                    else if (self is [>= 0x15 and <= 0x1e, ..])
                    {
                        self = self[1..];
                    }
                    else if (self is [0x1f, _, ..])
                    {
                        self = self[2..];
                    }
                    else
                    {
                        return false;
                    }
                    return true;
                }
                static void TrimLdRef(ref Span<byte> self)
                {
                    var a = TrimLdcI4(ref self);
                    var b = TrimLdcI4(ref self);
                    if (a != b || (a && self is not [0x28, _, _, _, _, ..]))
                    {
                        throw new InvalidOperationException("not ldref");
                    }
                    if (a)
                    {
                        self = self[5..];
                    }
                }
                TrimLdRef(ref ils);
                TrimLdRef(ref ils);
                var c = dm.GetParameters().Length;
                static void throwparam() => throw new InvalidOperationException("param count does not match");
                static void TrimLdarg(ref Span<byte> self, int hint)
                {
                    var x = (byte)(hint & 0xff);
                    var y = (byte)((hint >> 8) & 0xff);
                    if (self is [0xFE, 0x09, { } a, { } b, ..] _ && a == x && b == y) // little endian
                    {
                        self = self[4..];
                    }
                    else if (self is [0x0e, { } c, ..] && c == hint)
                    {
                        self = self[2..];
                    }
                    else if (self is [{ } d and >= 0x02 and <= 0x05, ..] && hint + 0x02 == d)
                    {
                        self = self[1..];
                    }
                    else
                    {
                        throwparam();
                    }
                }
                for (int i = 0; i < c; i++)
                {
                    TrimLdarg(ref ils, i);
                }
                if (ils[0] != 0x28)
                {
                    throwparam();
                }
                ils = ils[1..];
                var token = BinaryPrimitives.ReadInt32LittleEndian(ils) & 0xffffff;
                if (ils[4..] is not [0x2A, ..])
                {
                    throw new InvalidOperationException("not ret");
                }
                static MethodBase? Generic(object o)
                {
                    var a = o.GetType();
                    if (a.GetType().Name == "GenericMethodInfo")
                    {
                        get_methodHandle ??= a.GetField("m_methodHandle", bf).CreateGetter<RuntimeMethodHandle>();
                        get_context ??= a.GetField("m_context", bf).CreateGetter<RuntimeTypeHandle>();
                        return MethodBase.GetMethodFromHandle(get_methodHandle(o), get_context(o));
                    }
                    return null;
                }
                return tokens[token] switch
                {
                    RuntimeMethodHandle r => MethodBase.GetMethodFromHandle(r) ?? method,
                    DynamicMethod d => d,
                    { } what => Generic(what) ?? method,
                    _ => method,
                };
            }
            else
            {
                return method;
            }
        }
        catch
        {
            return method;
        }
    }
    
    private static string NameAsValidFilename(string name)
    {
        return new string(name.Select(c => FilenameInvalidChars.Contains(c) ? '_' : c).ToArray());
    }

    private static HashSet<char> FilenameInvalidChars = new()
    {
        '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?', '\\', '/',
            '+' // breaks URL's
    };

    public class DBFile
    {
        [JsonPropertyName("methods")]
        public List<Method> Methods { get; set; } = new();

        [JsonPropertyName("mods")]
        public List<Mod> Mods { get; set; } = new();

        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("everestVersion")]
        public string EverestVersion { get; set; } = "";

        public record Method(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("directoryName")] string DirectoryName,
            [property: JsonPropertyName("hooks")] List<string> Hooks // list of all hooks applied to this method
        );

        public record Mod(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("version")] string Version
        );
    }
}
