using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Core.Input;
using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Statistics.UI;

[Autoload(Side = ModSide.Client)]
public sealed class AdventureScoreboard : ModSystem
{
    internal const string LayerName = "PvPAdventure: Scoreboard";
    internal static Rectangle Bounds { get; private set; }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0)
        {
            layers.Insert(index, new LegacyGameInterfaceLayer(LayerName, Draw, InterfaceScaleType.None));
            layers.Insert(index + 1, ModContent.GetInstance<PointsManager>().BossCompletion);
        }
    }

    private static bool Draw()
    {
        Bounds = Rectangle.Empty;
        if (Main.gameMenu || Main.mapFullscreen || ModContent.GetInstance<Keybinds>().Scoreboard?.Current != true ||
            !ModContent.GetInstance<GameManager>().IsSelected)
            return true;

        const int width = 530;
        const int rowHeight = 24;
        int x = (Main.screenWidth - width) / 2;
        int y = 60;
        int top = y;
        Point mouse = new(Main.mouseX, Main.mouseY);
        foreach (var group in Main.player.Where(player => player?.active == true && player.team > 0)
            .GroupBy(player => (Team)player.team).OrderBy(group => group.Key))
        {
            Rectangle heading = new(x, y, width, 36);
            Utils.DrawInvBG(Main.spriteBatch, heading, Main.teamColor[(int)group.Key] * 0.65f);
            Utils.DrawBorderString(Main.spriteBatch, group.Key.ToString(), new Vector2(x + 12, y + 8), Color.White);
            Utils.DrawBorderString(Main.spriteBatch, "K / D       Damage", new Vector2(x + 215, y + 8), Color.White, 0.85f);
            AdventureScoreboardHeader.DrawTeamExtra(new Rectangle(heading.Right - 100, y, 100, heading.Height), group.Key);
            y += heading.Height;

            foreach (Player player in group.OrderByDescending(player => player.GetModPlayer<MatchStatsPlayer>().GetStat(MatchStatKey.Kills))
                         .ThenBy(player => player.name, StringComparer.OrdinalIgnoreCase))
            {
                if (y + rowHeight > Main.screenHeight - 32)
                    break;

                MatchStatsPlayer stats = player.GetModPlayer<MatchStatsPlayer>();
                Rectangle row = new(x, y, width, rowHeight);
                Utils.DrawInvBG(Main.spriteBatch, row);
                Utils.DrawBorderString(Main.spriteBatch, player.name, new Vector2(x + 12, y + 3), Color.White, 0.85f);
                Utils.DrawBorderString(Main.spriteBatch,
                    $"{stats.GetStat(MatchStatKey.Kills)} / {stats.GetStat(MatchStatKey.Deaths)}       {stats.GetStat(MatchStatKey.DamageDealt):N0}",
                    new Vector2(x + 215, y + 3), Color.White, 0.85f);
                if (row.Contains(mouse))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    Main.instance.MouseText($"Damage taken: {stats.GetStat(MatchStatKey.DamageTaken):N0}\n" +
                        $"Streak: {stats.GetStat(MatchStatKey.CurrentStreak)} · Best: {stats.GetStat(MatchStatKey.BestStreak)}\n" +
                        $"Boss damage: {stats.GetStat(MatchStatKey.BossDamageDealt):N0} · Portals: {stats.GetStat(MatchStatKey.PortalKills)}\n" +
                        $"Tiles mined: {stats.GetStat(MatchStatKey.TilesMined):N0} · Placed: {stats.GetStat(MatchStatKey.TilesPlaced):N0}");
                }
                y += rowHeight;
            }
        }
        Bounds = new Rectangle(x, top, width, y - top);
        return true;
    }
}
