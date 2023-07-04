using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.MappingUtils;

public class MappingUtilsModuleSettings : EverestModuleSettings {
    [DefaultButtonBinding(0, Keys.NumPad3)]
    public ButtonBinding OpenMenu { get; set; } = null!;
}
