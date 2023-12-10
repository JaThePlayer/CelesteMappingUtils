using Celeste.Mod.Helpers;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.MappingUtils.ModIntegration;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Celeste.Mod.MappingUtils.ImGuiHandlers;

namespace Celeste.Mod.MappingUtils.Commands;

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
        var timestampFilename = $"{time.Ticks}.txt";

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

        //ConcurrentDictionary<MethodBase, ManagedDetourState> detourStates
        var detourStates = (IDictionary)DetourManager_detourStates.Value.GetValue(null)!;
        foreach (MethodBase key in detourStates.Keys)
        {
            if (!DetourManager.GetDetourInfo(key).ILHooks.Any())
                continue;

            try
            {
                //var methodName = $"{key.DeclaringType?.FullName ?? $"unk-{Guid.NewGuid()}"}.{key.Name}";
                var methodName = GetMethodNameForDB(key);
                var methodNameAsDirName = NameAsValidFilename(methodName);
                var dir = Path.Combine(directory, methodNameAsDirName);
                Directory.CreateDirectory(dir);

                MappingUtilsModule.Log(LogLevel.Info, DiffAllTag, $"Dumping {key.GetID()} to {dir}");

                using var ilFileStream = File.Create(Path.Combine(dir, timestampFilename));
                var diff = new MethodDiff(key);
                diff.PrintToStream(ilFileStream);

                var fileListFile = new FileInfo(Path.Combine(dir, "files.json"));
                HashSet<string> fileList = null!;
                if (fileListFile.Exists)
                {
                    using var fileListReadStream = fileListFile.Open(FileMode.OpenOrCreate, FileAccess.Read);
                    fileList = JsonSerializer.Deserialize<HashSet<string>>(fileListReadStream) ?? new();
                } else
                {
                    fileList = new();
                }

                fileList.Add(timestampFilename);
                using var fileListWriteStream = fileListFile.Open(FileMode.Create, FileAccess.Write);
                JsonSerializer.Serialize(fileListWriteStream, fileList);

                db.Methods.Add(new(methodName, methodNameAsDirName, diff.AppliedHookNames));

            } catch (Exception ex)
            {
                MappingUtilsModule.Log(LogLevel.Warn, DiffAllTag, $"Failed to dump: {key.GetID()}: {ex}");
            }

            using var dbStream = File.Open(Path.Combine(directory, "info.json"), FileMode.Create, FileAccess.Write);
            JsonSerializer.Serialize(dbStream, db);
        }
        
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
    private static Lazy<FieldInfo> DetourManager_detourStates = new(() => typeof(DetourManager).GetField("detourStates", BindingFlags.Static | BindingFlags.NonPublic)!);

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
