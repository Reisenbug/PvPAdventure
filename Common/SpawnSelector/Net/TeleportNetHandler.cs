using Microsoft.Xna.Framework;
using System.IO;
using PvPAdventure.Common.Independent;
using Terraria;
using Terraria.ID;

namespace PvPAdventure.Common.SpawnSelector.Net;

public static class TeleportNetHandler
{
    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        byte requesterId = reader.ReadByte();
        SpawnType type = (SpawnType)reader.ReadByte();

        if (requesterId != whoAmI)
            return;

        Player requester = Main.player[requesterId];
        if (requester == null || !requester.active || requester.dead || requester.ghost ||
            !PortalSystem.Playing || !PortalSystem.CanTravelFrom(requester) ||
            type is SpawnType.Random or SpawnType.Teammate)
            return;

        Vector2 teleportPos;

        switch (type)
        {
            case SpawnType.World:
                teleportPos = new Vector2(Main.spawnTileX, Main.spawnTileY - 3).ToWorldCoordinates();
                break;

            case SpawnType.TeammateBed:
                {
                    short idx = reader.ReadInt16();
                    if (idx < 0 || idx >= Main.maxPlayers)
                        return;

                    Player bedOwner = Main.player[idx];
                    if (bedOwner == null || !bedOwner.active)
                        return;

                    if (idx != requester.whoAmI)
                    {
                        if (requester.team == 0 || bedOwner.team != requester.team)
                            return;
                    }

                    if (bedOwner.SpawnX < 0 || bedOwner.SpawnY < 0 || !Player.CheckSpawn(bedOwner.SpawnX, bedOwner.SpawnY))
                        return;

                    teleportPos = new Vector2(bedOwner.SpawnX, bedOwner.SpawnY - 6).ToWorldCoordinates();
                    break;
                }
            case SpawnType.MyBed:
                {
                    if (requester.SpawnX < 0 || requester.SpawnY < 0 ||
                        !Player.CheckSpawn(requester.SpawnX, requester.SpawnY))
                        return;

                    teleportPos = new Vector2(
                        requester.SpawnX,
                        requester.SpawnY - 6
                    ).ToWorldCoordinates();
                    break;
                }
            default:
                return;
        }

        requester.Teleport(teleportPos, TeleportationStyleID.RecallPotion);

        NetMessage.SendData(
            MessageID.TeleportEntity,
            -1, -1, null,
            number: 0,
            number2: requester.whoAmI,
            number3: teleportPos.X,
            number4: teleportPos.Y,
            number5: TeleportationStyleID.RecallPotion
        );

        // Send teleport sound effect to all clients
        TeleportFxNetHandler.Send(whoAmI);
    }
}



