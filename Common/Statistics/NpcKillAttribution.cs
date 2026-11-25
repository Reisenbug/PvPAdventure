using PvPAdventure.Common.Game;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

internal sealed class NpcKillAttribution : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private Player lastAttacker;

    public override void Load() => On_NPC.PlayerInteraction += RecordInteraction;

    public override void Unload() => On_NPC.PlayerInteraction -= RecordInteraction;

    public override void SetDefaults(NPC npc) => lastAttacker = null;

    private static void RecordInteraction(On_NPC.orig_PlayerInteraction original, NPC npc, int playerIndex)
    {
        original(npc, playerIndex);

        if (Main.netMode != NetmodeID.MultiplayerClient && playerIndex >= 0 && playerIndex < Main.maxPlayers &&
            Main.player[playerIndex] is { active: true } player)
            GetOwner(npc).GetGlobalNPC<NpcKillAttribution>().lastAttacker = player;
    }

    public override void OnKill(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient ||
            ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
            return;

        Player player = GetOwner(npc).GetGlobalNPC<NpcKillAttribution>().lastAttacker;
        if (player is { active: true } && ReferenceEquals(Main.player[player.whoAmI], player) &&
            (Team)player.team != Team.None)
            ModContent.GetInstance<PointsManager>().AwardNpcKillToTeam((Team)player.team, npc);
    }

    private static NPC GetOwner(NPC npc) =>
        npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife] is { active: true } owner
            ? owner
            : npc;
}
