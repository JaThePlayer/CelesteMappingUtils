﻿using System.Linq;
using System.Reflection;
using Celeste.Mod.Helpers;
using Celeste.Mod.MappingUtils.ImGuiHandlers;
using Celeste.Mod.MappingUtils.ModIntegration;

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

    public static MethodBase? FindMethod(string typeName, string methodName, out bool ambiguousMatch)
    {
        ambiguousMatch = false;
        
        FrostHelperAPI.LoadIfNeeded();

        Type? type = null;

        if (FrostHelperAPI.EntityNameToTypeOrNull is { } nameToType)
        {
            type = nameToType(typeName);
        }

        type ??= FakeAssembly.GetFakeEntryAssembly().GetType(typeName);
        
        if (type is null)
        {
            Log(LogLevel.Warn, "MappingUtils", $"Couldn't find type {typeName}!");
            return null;
        }

        MethodBase? method = null;

        try
        {
            if (methodName is "ctor" or ".ctor")
            {
                var ctors = type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

                method = ctors.FirstOrDefault();
            }

            method ??= type.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
        }
        catch (AmbiguousMatchException ex)
        {
            ambiguousMatch = true;
            Log(LogLevel.Warn, "MappingUtils", $"Couldn't find method {typeName}.{methodName}, because there are multiple methods of the same name.");
            return null;
        }

        
        if (method is null)
        {
            Log(LogLevel.Warn, "MappingUtils", $"Couldn't find method {typeName}.{methodName}!");
            return null;
        }

        return method;
    }

    public static void Log(LogLevel level, string tag, string text)
    {
        Logger.Log(level, tag, text);

        if (WriteToIngameLog)
        {
            Engine.Commands.Log($"[{tag}] {text}", level switch
            {
                LogLevel.Error => Color.Red,
                LogLevel.Warn => Color.Yellow,
                LogLevel.Debug => Color.LightGray,
                _ => Color.White
            });
        }
    }

    public static bool WriteToIngameLog { get; set; } = false;
}