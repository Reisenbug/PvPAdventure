using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.GameReporters;

/// <summary>Owns local match statistics and delayed result presentation.</summary>
internal sealed class AdventureMatchReportingSystem : ModSystem
{
    private string matchToken;
    private EndScreenSummary pendingEndScreen;
    private ulong presentAfter;
    public string CurrentMatchToken => matchToken ?? "";

    public void Begin()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        ClearPending();
        foreach (Player player in Main.ActivePlayers)
            if (player.TryGetModPlayer(out MatchStatsPlayer stats)) stats.ResetMatchStats();
        ModContent.GetInstance<PointsManager>().ResetForMatch();
        matchToken = Guid.NewGuid().ToString("N");
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        Log.Info($"Started authoritative Adventure match. MatchToken={matchToken}");
    }

    public void Complete()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || matchToken == null) return;
        matchToken = null;
        try
        {
            pendingEndScreen = AdventureEndScreenExtension.CreateSummary();
            presentAfter = Main.GameUpdateCount + 20;
        }
        catch (Exception ex) { Log.Error($"Failed to build the Adventure end screen: {ex}"); }
    }

    public void Abort()
    {
        matchToken = null;
        ClearPending();
    }

    public void Tick()
    {
        GameManager game = ModContent.GetInstance<GameManager>();
        if (!game.IsSelected) return;
        if (pendingEndScreen == null || Main.netMode == NetmodeID.MultiplayerClient) return;
        if (game.IsRunning) { ClearPending(); return; }
        if (Main.GameUpdateCount < presentAfter) return;
        EndScreenSummary summary = pendingEndScreen;
        ClearPending();
        try { EndScreenService.Present(summary); }
        catch (Exception ex) { Log.Error($"Failed to present the Adventure end screen: {ex}"); }
    }

    private void ClearPending() { pendingEndScreen = null; presentAfter = 0; }
    public override void ClearWorld() => Abort();
    public override void OnWorldUnload() => Abort();
}
