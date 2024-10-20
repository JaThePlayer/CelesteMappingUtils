using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Celeste.Mod.Helpers;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace Celeste.Mod.MappingUtils.ModIntegration;

public static class IlSpyCmd
{
    public static async Task<(bool success, string message)> DecompileAsync(Type type, MethodBase? method)
    {
        try
        {
            var asm = type.Assembly;
            string? path = null;

            if (!string.IsNullOrWhiteSpace(asm.Location))
            {
                path = asm.Location;
            }
            else
            {
                var containingMod = Everest.Modules.FirstOrDefault(m => m.GetType().Assembly == asm);
                if (containingMod is {})
                {
                    path = Everest.Relinker.GetCachedPath(containingMod.Metadata, Path.GetFileNameWithoutExtension(containingMod.Metadata.DLL));
                }
            }

            if (path is null)
            {
                return (false, $"Couldn't locate the path to the assembly containing {type}");
            }
            
            /*
            var args = $"-t \"{type.FullName}\" \"{path}\" -r \"{Engine.AssemblyDirectory}\" -usepdb";

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ilspycmd",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };

            proc.Start();
            
            var ret = await proc.StandardOutput.ReadToEndAsync();

            return (true, ret);
            */

            var task = Task.Run(() =>
            {
                var d = GetDecompiler(path, [Engine.AssemblyDirectory]);
                
                

                if (method is { })
                {
                    var methodDef = d.TypeSystem.FindType(new FullTypeName(type.FullName)).GetMethods(m => m.Name == method.Name).FirstOrDefault();
                    if (methodDef is {})
                        return d.DecompileAsString(methodDef.MetadataToken);
                }

                return d.DecompileTypeAsString(new FullTypeName(type.FullName));
            });

            var ret = await task;

            return (true, ret);
        }
        catch (Exception ex)
        {
            ex.LogDetailed();
            return (false, ex.ToString());
        }
    }
    
    static DecompilerSettings GetSettings(PEFile module)
    {
        return new DecompilerSettings(LanguageVersion.Latest) {
            ThrowOnAssemblyResolveErrors = false,
            RemoveDeadCode = false,
            RemoveDeadStores = false,
            UseSdkStyleProjectFormat = WholeProjectDecompiler.CanUseSdkStyleProjectFormat(module),
            UseNestedDirectoriesForNamespaces = true,
        };
    }

    static CSharpDecompiler GetDecompiler(string assemblyFileName, string[] referencePaths)
    {
        var module = new PEFile(assemblyFileName);
        var resolver = new UniversalAssemblyResolver(assemblyFileName, false, module.Metadata.DetectTargetFrameworkId());
        foreach (var path in (referencePaths ?? Array.Empty<string>()))
        {
            resolver.AddSearchDirectory(path);
        }
        return new CSharpDecompiler(assemblyFileName, resolver, GetSettings(module)) {
            DebugInfoProvider = TryLoadPDB(module)
        };
    }
    
    static IDebugInfoProvider TryLoadPDB(PEFile module)
    {
        return null!;
    }
}