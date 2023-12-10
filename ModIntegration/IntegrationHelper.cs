﻿using System;

namespace Celeste.Mod.MappingUtils.ModIntegration;

internal static class IntegrationUtils
{
    // From Communal Helper
    // Modified version of Everest.Loader.DependencyLoaded
    public static bool TryGetModule(EverestModuleMetadata meta, out EverestModule module)
    {
        foreach (EverestModule other in Everest.Modules)
        {
            EverestModuleMetadata otherData = other.Metadata;
            if (otherData.Name != meta.Name)
                continue;

            Version version = otherData.Version;
            if (Everest.Loader.VersionSatisfiesDependency(meta.Version, version))
            {
                module = other;
                return true;
            }
        }

        module = null!;
        return false;
    }

    public static readonly Lazy<bool> EeveeHelperLoaded = new(() => TryGetModule(new()
    {
        Name = "EeveeHelper",
        Version = new(1, 5, 3),
    }, out _));
}

