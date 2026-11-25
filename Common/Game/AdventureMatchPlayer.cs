using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

// Awards team points for PvP kills that happen during a match.
public class AdventureMatchPlayer : ModPlayer
{
    private const int AttributionTicks = 15 * 60;
    private Player recentAttacker;
    private ulong lastHit;
    private string matchToken;

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !info.PvP)
            return;

        GameManager game = ModContent.GetInstance<GameManager>();
        Player attacker = GetOpponent(info.DamageSource.SourcePlayerIndex);
        if (game.CurrentPhase != GameManager.Phase.Playing || attacker == null)
            return;

        recentAttacker = attacker;
        lastHit = Main.GameUpdateCount;
        matchToken = game.CurrentMatchToken;
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        GameManager game = ModContent.GetInstance<GameManager>();
        if (game.CurrentPhase == GameManager.Phase.Playing)
        {
            Player killer = pvp ? GetOpponent(damageSource.SourcePlayerIndex) : null;
            if (killer == null && recentAttacker is { dead: false } recent &&
                matchToken == game.CurrentMatchToken && Main.GameUpdateCount - lastHit < AttributionTicks &&
                ReferenceEquals(GetOpponent(recent.whoAmI), recent))
                killer = recent;

            if (killer != null)
            {
                MatchStatsPlayer.RecordKill(killer, Player);
                ModContent.GetInstance<PointsManager>().AwardPlayerKillToTeam(killer, Player);
            }
            else
                Player.GetModPlayer<MatchStatsPlayer>().ResetStreak();
        }

        ClearRecentHits();
    }

    public override void PlayerDisconnect() => ClearRecentHits();

    private void ClearRecentHits()
    {
        recentAttacker = null;
        foreach (Player player in Main.ActivePlayers)
        {
            AdventureMatchPlayer attribution = player.GetModPlayer<AdventureMatchPlayer>();
            if (ReferenceEquals(attribution.recentAttacker, Player))
                attribution.recentAttacker = null;
        }
    }

    private Player GetOpponent(int slot) =>
        Player.team != (int)Team.None && slot >= 0 && slot < Main.maxPlayers && slot != Player.whoAmI &&
        Main.player[slot] is { active: true } player && player.team != (int)Team.None && player.team != Player.team
            ? player : null;
}
