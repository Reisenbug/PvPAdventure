using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.RacePeriod;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Core.Compat;

public sealed class AdventureRegionSystem : ModSystem
{
    public const string RegionKey = "PvPAdventure.Spawnbox";
    private int width = 50;
    private int height = 50;
    private int offsetX;
    private int offsetY;

    public static Rectangle TileArea => ModContent.GetInstance<GameManager>().IsSelected
        ? ModContent.GetInstance<AdventureRegionSystem>().GetTileArea() : Rectangle.Empty;

    public static Rectangle WorldArea
    {
        get
        {
            Rectangle area = TileArea;
            return new Rectangle(area.X * 16, area.Y * 16, area.Width * 16, area.Height * 16);
        }
    }

    public static bool Contains(Player player) => player?.active == true && WorldArea.Intersects(player.Hitbox);

    public override void PostSetupContent()
    {
        RacePeriodRules.WaitingProvider = () =>
            ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting;
        RacePeriodRules.EntryAreaProvider = Contains;
    }

    public override void Unload()
    {
        RacePeriodRules.WaitingProvider = null;
        RacePeriodRules.EntryAreaProvider = null;
    }

    public override void ClearWorld()
    {
        width = height = 50;
        offsetX = offsetY = 0;
    }

    public override void OnWorldLoad() => ReadBounds(null, ReadLegacyConfig());

    public override void LoadWorldData(TagCompound tag) => ReadBounds(
        tag.ContainsKey("SpawnBoxWidth") ? tag : FindLegacyBounds(), ReadLegacyConfig());

    public override void SaveWorldData(TagCompound tag)
    {
        tag["SpawnBoxWidth"] = width;
        tag["SpawnBoxHeight"] = height;
        tag["SpawnBoxXOffset"] = offsetX;
        tag["SpawnBoxYOffset"] = offsetY;
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)width);
        writer.Write((byte)height);
        writer.Write((sbyte)offsetX);
        writer.Write((sbyte)offsetY);
    }

    public override void NetReceive(BinaryReader reader)
    {
        width = Math.Clamp((int)reader.ReadByte(), 5, 50);
        height = Math.Clamp((int)reader.ReadByte(), 5, 50);
        offsetX = Math.Clamp((int)reader.ReadSByte(), -20, 20);
        offsetY = Math.Clamp((int)reader.ReadSByte(), -20, 20);
    }

    private Rectangle GetTileArea()
    {
        int left = Math.Clamp(Main.spawnTileX + offsetX - width / 2, 0, Math.Max(0, Main.maxTilesX - width));
        int top = Math.Clamp(Main.spawnTileY + offsetY - height / 2, 0, Math.Max(0, Main.maxTilesY - height));
        return new Rectangle(left, top, width, height);
    }

    private static TagCompound FindLegacyBounds()
    {
        // Removed ModSystems are retained by tModLoader here, including cloud-world saves.
        // Read only the old system's payload; leave it available as a rollback backup.
        TagCompound legacy = ModContent.GetInstance<UnloadedSystem>().data?
            .FirstOrDefault(entry => entry.GetString("mod") == "PvPFramework" &&
                entry.GetString("name") == "SpawnBoxSystem")?.GetCompound("data");
        return legacy;
    }

    private void ReadBounds(TagCompound legacy, JObject config)
    {
        JObject defaults = config?["SpawnBox"] as JObject;
        bool saved = legacy?.ContainsKey("SpawnBoxWidth") == true;
        width = Math.Clamp(saved ? legacy.GetInt("SpawnBoxWidth") : (int?)defaults?["DefaultWidth"] ?? 50, 5, 50);
        height = Math.Clamp(saved ? legacy.GetInt("SpawnBoxHeight") : (int?)defaults?["DefaultHeight"] ?? 50, 5, 50);
        offsetX = Math.Clamp(saved ? legacy.GetInt("SpawnBoxXOffset") : (int?)defaults?["DefaultXOffset"] ?? 0, -20, 20);
        offsetY = Math.Clamp(saved ? legacy.GetInt("SpawnBoxYOffset") : (int?)defaults?["DefaultYOffset"] ?? 0, -20, 20);
    }

    private static JObject ReadLegacyConfig()
    {
        try
        {
            string path = Path.Combine(Main.SavePath, "ModConfigs", "PvPFramework_ServerConfig.json");
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            Log.Warn($"Could not read legacy region defaults: {ex.Message}");
            return null;
        }
    }
}
