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

        public MethodBase? FindMethod()
        {
            var method = MappingUtilsModule.FindMethod(TypeName, MethodName, out var ambiguousMatch);

            return method;
        }
    }
}
