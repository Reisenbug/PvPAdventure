using PvPAdventure.Common.AdminTools.Tools.TeamAssigner;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Core.Net;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Teams;

public static class PlayerTeamNetHandler
{
    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var team = Team.Deserialize(reader);

        if (Main.netMode == NetmodeID.Server)
        {
            if (team.Player < 0 || team.Player >= Main.maxPlayers)
            {
                return;
            }

            Player target = Main.player[team.Player];
            if (target == null || !target.active)
            {
                return;
            }

            target.team = (int)team.Value;

            SyncTeam(target);

            return;
        }

        if (team.Player >= 0 && team.Player < Main.maxPlayers)
        {
            Player target = Main.player[team.Player];
            if (target != null && target.active)
            {
                target.team = (int)team.Value;
            }
        }

        if (!Main.dedServ)
        {
            ModContent.GetInstance<PointsManager>().UiScoreboard?.Invalidate();
        }

        var ts = ModContent.GetInstance<TeamAssignerSystem>();
        if (ts?.teamAssignerState != null)
        {
            foreach (var child in ts.teamAssignerState.Children)
            {
                if (child is TeamAssignerPanel panel)
                {
                    panel.needsRebuild = true;
                    break;
                }
            }
        }
    }

    public static void AssignRandomTeam(int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
            return;
        Player player = Main.player[whoAmI];
        if (!player.active)
            return;

        var state = player.GetModPlayer<TeamResyncPlayer>();
        if (!state.RandomTeamAssigned)
        {
            player.team = Main.rand.Next(1, 6);
            state.RandomTeamAssigned = true;
        }
        SyncTeam(player);
    }

    private static void SyncTeam(Player player)
    {
        ModContent.GetInstance<TeamBedSystem>().UpdateFromPlayer(player);
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.PlayerTeam);
        new Team((byte)player.whoAmI, (Terraria.Enums.Team)player.team).Serialize(packet);
        packet.Send();
    }
}
