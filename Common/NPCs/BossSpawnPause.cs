using System.IO;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.NPCs;

public sealed class BossSpawnPause : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private int remainingTicks;
    private bool originalInvulnerability;
    private bool outsideCombatRange;

    public bool IsPaused => remainingTicks > 0 || outsideCombatRange;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
        entity.type is NPCID.SkeletronHead or NPCID.CultistBoss or NPCID.Plantera or NPCID.WallofFlesh;

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        if (npc.type == NPCID.WallofFlesh && Main.hardMode)
            return;
        originalInvulnerability = npc.dontTakeDamage;
        remainingTicks = 3600;
        npc.dontTakeDamage = true;
        npc.velocity = Vector2.Zero;
        npc.netUpdate = true;
        AnnounceCountdown(npc);
    }

    public override bool PreAI(NPC npc)
    {
        if (!IsPaused)
            return true;

        npc.velocity = Vector2.Zero;
        npc.dontTakeDamage = true;
        if (remainingTicks > 0 && Main.netMode != NetmodeID.MultiplayerClient && !Main.gamePaused &&
            !ModContent.GetInstance<PauseManager>().IsPaused)
        {
            remainingTicks--;
            AnnounceCountdown(npc);
            if (remainingTicks == 0 && !outsideCombatRange)
            {
                npc.dontTakeDamage = originalInvulnerability;
                npc.netUpdate = true;
                return true;
            }
        }
        return false;
    }

    private void AnnounceCountdown(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || npc.type is not (NPCID.WallofFlesh or NPCID.SkeletronHead) ||
            remainingTicks is not (3600 or 1800 or 600 or 300 or 240 or 180 or 120 or 60))
            return;

        var message = NetworkText.FromFormattable("{0}将在 {1} 秒后可被攻击。",
            Lang.GetNPCName(npc.type).ToNetworkText(), remainingTicks / 60);
        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(message, Color.Gold);
        else
            Main.NewText(message.ToString(), Color.Gold);
    }

    internal void SetOutsideCombatRange(NPC npc, bool outside)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || outsideCombatRange == outside)
            return;

        outsideCombatRange = outside;
        npc.dontTakeDamage = IsPaused || originalInvulnerability;
        npc.netUpdate = true;
    }

    public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !IsPaused;

    public override bool CheckActive(NPC npc) => !IsPaused;

    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        binaryWriter.Write(remainingTicks);
        binaryWriter.Write(originalInvulnerability);
        binaryWriter.Write(outsideCombatRange);
    }

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
    {
        remainingTicks = binaryReader.ReadInt32();
        originalInvulnerability = binaryReader.ReadBoolean();
        outsideCombatRange = binaryReader.ReadBoolean();
        npc.dontTakeDamage = IsPaused || originalInvulnerability;
    }
}
