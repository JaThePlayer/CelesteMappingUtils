namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public abstract class Tab
{
    public const int ItemWidth = MainMappingUtils.ItemWidth;

    internal bool OpenLastFrame;

    public abstract string Name { get; }

    public abstract void Render(Level level);

    public virtual void OnOpen() { }

    public virtual void OnClose() { }
}
