using Celeste.Mod.MappingUtils.ModIntegration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.MappingUtils.Cheats;

internal static class SpawnEntity
{
    internal static string LastSummoned = "";
    internal static ComboCache<string> ComboCache = new();

    public static Lazy<List<string>> SummonableSIDs = new(() => Available ? new string[] {
        "theoCrystal",
        "glider",
        //"ExtendedVariantMode/TheoCrystal", - has weird construction code
        "CommunalHelper/DreamJellyfish",
        "YetAnotherHelper/StickyJellyfish",
        "NerdHelper/BouncyJellyfish",
        "MaxHelpingHand/RespawningJellyfish",
        "cavern/crystalbomb",
        "pandorasBox/propellerBox",
        "VortexHelper/BowlPuffer",
        "batteries/battery"
    }.Where(sid => FrostHelperAPI.EntityNameToTypeOrNull(sid) is { }).ToList() : new());

    public static Dictionary<string, Dictionary<string, object>> SIDToEntityDataValues = new()
    {
        ["glider"] = new()
        {
            ["bubble"] = true,
        },
        ["CommunalHelper/DreamJellyfish"] = new()
        {
            ["bubble"] = true,
        },
        ["NerdHelper/BouncyJellyfish"] = new()
        {
            ["bubble"] = true,
        },
        ["MaxHelpingHand/RespawningJellyfish"] = new()
        {
            ["bubble"] = true,
            ["spriteDirectory"] = "objects/MaxHelpingHand/glider",
            ["respawnTime"] = 2f,
        },
    };

    public static bool Available => FrostHelperAPI.LoadIfNeeded() && FrostHelperAPI.EntityNameToTypeOrNull is { };

    private static int SummonedID = 0;

    public static void Spawn(Level level, string sid)
    {
        var player = level.Tracker.GetEntity<Player>();
        if (player == null)
            return;

        var type = FrostHelperAPI.EntityNameToTypeOrNull(sid);
        if (type is null)
            return;

        var data = new EntityData()
        {
            Name = sid,
            Position = player.Position + new Vector2(0, -12f),
            Values = SIDToEntityDataValues.GetValueOrDefault(sid) ?? new(),
        };

        if ((TryCreateInstance(type, new object[] { data, new Vector2(0, 0) }) ??
            TryCreateInstance(type, new object[] { data.Position }) ??
            TryCreateInstance(type, new object[] { data, new Vector2(0, 0), new EntityID("__MappingUtils_SpawnEntity", SummonedID++) }))
            is { } entity)
        {
            level.Add(entity);
        }
    }

    private static Entity? TryCreateInstance(Type type, object[] args)
    {
        Entity? entity = null;

        try
        {
            entity = (Entity?)Activator.CreateInstance(type, args);
        }
        catch (MissingMethodException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "MappingUtils.SpawnEntity", $"Failed to spawn entity {type.FullName}: {ex}");
        }
        return entity;
    }
}
