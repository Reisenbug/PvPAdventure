using System;
using PvPAdventure.Common.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

internal sealed class MatchRespawnSystem : ModSystem
{
    private const int TicksPerSecond = 60;

    public override void Load() => On_Player.GetRespawnTime += GetRespawnTime;

    public override void Unload() => On_Player.GetRespawnTime -= GetRespawnTime;

    private static int GetRespawnTime(On_Player.orig_GetRespawnTime original, Player player, bool pvp)
    {
        if (!ModContent.GetInstance<GameManager>().IsSelected)
            return original(player, pvp);

        if (Main.netMode != NetmodeID.SinglePlayer)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if ((npc.boss || BossPartUtilities.IsPartOfEaterOfWorlds((short)npc.type)) &&
                    Math.Abs(player.Center.X - npc.Center.X) + Math.Abs(player.Center.Y - npc.Center.Y) < 4000f)
                    return 20 * TicksPerSecond;
            }
        }

        return 10 * TicksPerSecond;
    }

    internal static int GetDisplayedSeconds(int ticks) => ticks <= 2 ? 0 : (ticks - 1) / TicksPerSecond + 1;
}
