using Celeste.Mod.MappingUtils.ImGuiHandlers;
using System;

namespace Celeste.Mod.MappingUtils;

public class MappingUtilsModule : EverestModule
{
    public static MappingUtilsModule Instance { get; private set; } = null!;

    public override Type SettingsType => typeof(MappingUtilsModuleSettings);
    public static MappingUtilsModuleSettings Settings => (MappingUtilsModuleSettings)Instance._Settings;

    public override Type SessionType => typeof(MappingUtilsModuleSession);
    public static MappingUtilsModuleSession Session => (MappingUtilsModuleSession)Instance._Session;

    public MappingUtilsModule()
    {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(MappingUtilsModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(MappingUtilsModule), LogLevel.Info);
#endif
    }

    private static MainMappingUtils? Handler;

    public override void Load()
    {
        ImGuiManager.Handlers.Add(Handler = new MainMappingUtils());
    }

    public override void OnInputInitialize()
    {
        base.OnInputInitialize();
    }

    public override void Unload()
    {
        OnUnload?.Invoke();

        if (Handler is { })
        {
            ImGuiManager.Handlers.Remove(Handler);
        }
    }

    public static Action? OnUnload;
}