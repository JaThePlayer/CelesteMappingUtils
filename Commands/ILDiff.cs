using Celeste.Mod.Helpers;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.MappingUtils.ModIntegration;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.MappingUtils.Commands;

public static class ILDiff
{
    [Command("ildiff", "[Mapping Utils] Creates a diff of the IL of a method and its IL hooks, and logs it to the console.")]
    public static void Diff(string typeFullName, string methodName)
    {
        FrostHelperAPI.LoadIfNeeded();

        var t = FrostHelperAPI.EntityNameToTypeOrNull(typeFullName) ?? FakeAssembly.GetFakeEntryAssembly().GetType(typeFullName) ?? throw new Exception($"Couldn't find type {typeFullName}");
        var m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).FirstOrDefault(t => t.Name == methodName) ?? throw new Exception($"Couldn't find method {methodName} on type {typeFullName}");

        var diff = new MethodDiff(m);

        diff.PrintToConsole();
    }
}
