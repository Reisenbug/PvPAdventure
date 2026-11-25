using PvPAdventure.Common.Security;
using PvPAdventure.Core.Net;
using PvPAdventure.Common.Game;
using System;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

/// <summary>Validates and applies administrator point edits.</summary>
internal static class TeamPointsNetHandler
{
    public static void Set(Team team, int value)
    {
        if (Main.gameMenu || Main.LocalPlayer?.active != true || !AdminPermissions.RequireLocal() ||
            !ModContent.GetInstance<GameManager>().IsSelected || !ValidTeam(team)) return;
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            ModContent.GetInstance<PointsManager>().SetTeamPoints(team, value);
            return;
        }
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamPoints);
        packet.Write((byte)team);
        packet.Write(value);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int sender)
    {
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers ||
            !AdminPermissions.HasAccess(sender) || !ModContent.GetInstance<GameManager>().IsSelected) return;
        Team team = (Team)reader.ReadByte();
        int value = reader.ReadInt32();
        if (ValidTeam(team)) ModContent.GetInstance<PointsManager>().SetTeamPoints(team, value);
    }

    private static bool ValidTeam(Team team) => team != Team.None && Enum.IsDefined(team);
}
