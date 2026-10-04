using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PvPAdventure.Common.GameTimer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Independent;

public sealed class LocalMatchSystem : ModSystem
{
    public sealed class PlayerResult
    {
        public string Identity { get; set; }
        public string Name { get; set; }
        public int Team { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int PortalUses { get; set; }
    }

    public sealed class MatchResult
    {
        public string Id { get; set; }
        public string World { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime EndedUtc { get; set; }
        public string EndReason { get; set; }
        public List<PlayerResult> Players { get; set; }
        public int[] TeamPoints { get; set; }
    }

    private readonly Dictionary<string, PlayerResult> players = [];
    private string matchId;
    private DateTime started;

    private PlayerResult GetPlayer(Player player)
    {
        string identity = Main.netMode == NetmodeID.Server &&
            Netplay.Clients[player.whoAmI].Socket?.GetRemoteAddress() is Terraria.Net.SteamAddress steam
                ? steam.SteamId.m_SteamID + ":" + player.name : player.name;
        if (!players.TryGetValue(identity, out PlayerResult result))
            players[identity] = result = new() { Identity = identity, Name = player.name };
        result.Team = player.team;
        return result;
    }

    internal void Begin()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;
        players.Clear();
        matchId = Guid.NewGuid().ToString("N");
        started = DateTime.UtcNow;
        foreach (Player player in Main.ActivePlayers)
            GetPlayer(player);
    }

    internal void RecordKill(Player killer)
    {
        if (matchId != null)
            GetPlayer(killer).Kills++;
    }

    internal void RecordDeath(Player player)
    {
        if (matchId != null)
            GetPlayer(player).Deaths++;
    }

    internal void RecordPortalUse(Player player)
    {
        if (matchId != null)
            GetPlayer(player).PortalUses++;
    }

    internal void End(string reason)
    {
        if (matchId == null || Main.netMode == NetmodeID.MultiplayerClient)
            return;
        foreach (Player player in Main.ActivePlayers)
            GetPlayer(player);
        MatchResult result = new()
        {
            Id = matchId, World = Main.worldName, StartedUtc = started,
            EndedUtc = DateTime.UtcNow, EndReason = reason,
            Players = players.Values.ToList(),
            TeamPoints = ModContent.GetInstance<global::PvPAdventure.Common.Statistics.PointsManager>()
                .Points.OrderBy(point => (int)point.Key).Select(point => point.Value).ToArray()
        };
        string folder = Path.Combine(Main.SavePath, "PvPAdventure", "Matches");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, matchId + ".json"),
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Log.Info($"Local match saved: {matchId}");
            matchId = null;
        }
        catch (IOException exception)
        {
            Log.Error($"Could not save local match: {exception}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Log.Error($"Could not save local match: {exception}");
        }
    }

    public override void OnWorldUnload() => End("WorldUnload");
}

public sealed class LocalMatchPlayer : ModPlayer
{
    public override void Kill(double damage, int hitDirection, bool pvp,
        Terraria.DataStructures.PlayerDeathReason damageSource)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            ModContent.GetInstance<LocalMatchSystem>().RecordDeath(Player);
    }
}
