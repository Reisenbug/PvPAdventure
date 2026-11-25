using Microsoft.Xna.Framework;
using PvPAdventure.Common.Travel;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Net;

namespace PvPAdventure.Common.Game;

/// <summary>Adventure's lobby cleanup, player staging, and world-time policy.</summary>
internal static class AdventureWorldRules
{
    public static void EnterLobby()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.townNPC || npc.isLikeATownNPC || npc.type == NPCID.TargetDummy) continue;
            npc.life = 0;
            npc.netSkip = -1;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
        }
        SetTimeFrozen(true);
    }

    public static void ReturnPlayersToSpawn()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            if (player.dead)
                continue;
            Vector2 position = new(Main.spawnTileX * 16f + 8f - player.width * 0.5f,
                Main.spawnTileY * 16f - player.height);
            if (Main.netMode == NetmodeID.Server)
                TravelSectionSyncSystem.PrepareTeleport(player.whoAmI, position);
            player.velocity = Vector2.Zero;
            player.Teleport(position, TeleportationStyleID.RecallPotion);
            player.fallStart = (int)(position.Y / 16f);
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI,
                    number3: position.X, number4: position.Y, number5: TeleportationStyleID.RecallPotion);
        }
    }

    public static void SetTimeFrozen(bool frozen)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        var power = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
        power.SetPowerInfo(frozen);
        if (Main.netMode != NetmodeID.Server) return;
        var packet = NetCreativePowersModule.PreparePacket(power.PowerId, 1);
        packet.Writer.Write(power.Enabled);
        NetManager.Instance.Broadcast(packet);
    }
}
