using Celeste.Mod.MappingUtils.ModIntegration;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.MappingUtils;

public class MappingUtilsModuleSettings : EverestModuleSettings
{
    [DefaultButtonBinding(0, Keys.NumPad3)]
    public ButtonBinding OpenMenu { get; set; } = null!;

    public List<HookedMethod> ProfilerHookedMethods { get; set; } = new()
    {
        new("BloomRenderer.Apply", typeof(BloomRenderer).GetMethod(nameof(BloomRenderer.Apply))!)
    };

    public class HookedMethod
    {
        public HookedMethod() { }
        public HookedMethod(string name, MethodInfo method)
        {
            TypeName = method.DeclaringType?.FullName ?? throw new Exception("method.DeclaringType is null!");
            MethodName = method.Name;

            Name = name;
        }

        public string TypeName { get; set; } = null!;
        public string MethodName { get; set; } = null!;
        public string Name { get; set; } = null!;

        public MethodInfo? FindMethod()
        {
            FrostHelperAPI.LoadIfNeeded();

            if (FrostHelperAPI.EntityNameToTypeOrNull is not { } nameToType)
            {
                return null;
            }

            if (nameToType(TypeName) is not { } type)
            {
                Logger.Log(LogLevel.Warn, "MappingUtils.Profiler", $"Couldn't find type {TypeName} to use for settings-provided arbitrary hook!");
                return null;
            }

            if (type.GetMethod(MethodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public) is not { } method)
            {
                Logger.Log(LogLevel.Warn, "MappingUtils.Profiler", $"Couldn't find method {TypeName}.{MethodName} to use for settings-provided arbitrary hook!");
                return null;
            }

            return method;
        }
    }
}
