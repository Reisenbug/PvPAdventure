using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Common.Travel.Beds;
using PylonMatch.Common.Scoreboard;
using AdventureAssets = PvPAdventure.Core.Utilities.Ass;
using GameAssets = PylonMatch.Core.Assets.GameAssets;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Compat;

// Wires Adventure into Framework's combat events and Pylon Match's scoreboard.
public class PvPFrameworkIntegration : ModSystem
{
    public override void Load()
    {
        ScoreboardPlayerInfoService.RowsProvider = BuildScoreboardInfoRows;
    }

    public override void Unload()
    {
        ScoreboardPlayerInfoService.RowsProvider = null;
    }

    private static IReadOnlyList<ScoreboardInfoRow> BuildScoreboardInfoRows(
        Player player,
        ScoreboardEntry scoreboard)
    {
        int[] itemIcons =
        [
            ItemID.LavaBucket,
            ItemID.GoldCrown,
            ItemID.PlatinumCrown,
            ItemID.Skull,
            ItemID.SuspiciousLookingEye,
            ItemID.PickaxeAxe,
            ItemID.DirtBlock,
            ItemID.HoneyBucket
        ];

        foreach (int itemId in itemIcons)
            Main.instance.LoadItem(itemId);

        MatchStatsPlayer stats = player.GetModPlayer<MatchStatsPlayer>();
        long pointBalance =
            (long)stats.GetStat(MatchStatKey.PointKills) -
            stats.GetStat(MatchStatKey.PointDeaths);

        return
        [
            Row(Stat(GameAssets.Attack.Value, "Damage dealt", scoreboard.Damage, 1f, 0), Stat(GameAssets.Defense.Value, "Damage taken", scoreboard.DamageTaken, 1f, 0)),
            Row(ItemStat(ItemID.GoldCrown, "Current streak", scoreboard.CurrentStreak, 1f, -2), ItemStat(ItemID.PlatinumCrown, "Best streak", scoreboard.BestStreak, 1f, -2)),
            Row(ItemStat(ItemID.PickaxeAxe, "Tiles mined", stats.GetStat(MatchStatKey.TilesMined), 1f, 0), ItemStat(ItemID.DirtBlock, "Tiles placed", stats.GetStat(MatchStatKey.TilesPlaced), 1f, 0)),
            Row(ItemStat(ItemID.LavaBucket, "Lava touched", stats.GetStat(MatchStatKey.LavaTouched), 1f, -2), ItemStat(ItemID.HoneyBucket, "Honey lost", stats.GetStat(MatchStatKey.LostHoney), 1f, -2)),
            Row(Stat(PortalMinimapIcon(player), "Portal kills", stats.GetStat(MatchStatKey.PortalKills), 1f, -2), ItemStat(ItemID.SuspiciousLookingEye, "Boss damage", stats.GetStat(MatchStatKey.BossDamageDealt), 1f, -2)),
            Row(Stat(TextureAssets.Item[ItemID.Skull].Value, "K/D", Kd(scoreboard), 1f, 0), Stat(AdventureAssets.IconPointsSetter.Value, "Points positive", Signed(pointBalance), 1f, 0))
        ];
    }

    private static ScoreboardInfoRow Row(ScoreboardInfoStat left, ScoreboardInfoStat right) => new(left, right);

    private static Texture2D PortalMinimapIcon(Player player)
    {
        string team = (Team)player.team switch
        {
            Team.Red => "Red",
            Team.Green => "Green",
            Team.Blue => "Blue",
            Team.Yellow => "Yellow",
            Team.Pink => "Pink",
            _ => "NoTeam"
        };

        return ModContent.Request<Texture2D>(
            $"PvPAdventure/Assets/Portals/PortalMinimap_{team}").Value;
    }

    private static ScoreboardInfoStat ItemStat(int itemId, string label, long value, float iconScale = 1f, int iconY = 0)
    {
        Texture2D texture = TextureAssets.Item[itemId].Value;
        Rectangle? frame = Main.itemAnimations[itemId]?.GetFrame(texture);
        return new(texture, label, value.ToString("N0"), frame, iconScale, iconY);
    }

    private static ScoreboardInfoStat Stat(Texture2D icon, string label, long value, float iconScale = 1f, int iconY = 0) => Stat(icon, label, value.ToString("N0"), iconScale, iconY);

    private static ScoreboardInfoStat Stat(Texture2D icon, string label, string value, float iconScale = 1f, int iconY = 0) => new(icon, label, value, IconScale: iconScale, IconYOffset: iconY);

    private static string Signed(long value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");

    private static string Kd(ScoreboardEntry stats)
    {
        double ratio = stats.Deaths == 0 ? stats.Kills : stats.Kills / (double)stats.Deaths;
        return $"{stats.Kills:N0}/{stats.Deaths:N0} ({ratio.ToString("0.00", CultureInfo.InvariantCulture)} KD)";
    }

}

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
                ModContent.GetInstance<PointsManager>().AwardPlayerKillToTeam(killer, Player);
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
