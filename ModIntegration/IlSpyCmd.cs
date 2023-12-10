using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Celeste.Mod.Helpers;

namespace Celeste.Mod.MappingUtils.ModIntegration;

public static class IlSpyCmd
{
    public static async Task<(bool success, string message)> DecompileAsync(Type type)
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
        }
        catch (Exception ex)
        {
            ex.LogDetailed();
            return (false, ex.ToString());
        }
    }
}