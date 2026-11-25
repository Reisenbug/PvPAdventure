using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.NPCs;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

internal sealed class NpcKillAttribution : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private Player lastAttacker;
    private Player strikeAttacker;
    private ulong strikeTick;

    public override void Load()
    {
        On_NPC.PlayerInteraction += RecordInteraction;
        On_NPC.StrikeNPC_HitInfo_bool_bool += RecordStrike;
    }

    public override void Unload()
    {
        On_NPC.PlayerInteraction -= RecordInteraction;
        On_NPC.StrikeNPC_HitInfo_bool_bool -= RecordStrike;
    }

    public override void SetDefaults(NPC npc) => lastAttacker = strikeAttacker = null;

    private static void RecordInteraction(On_NPC.orig_PlayerInteraction original, NPC npc, int playerIndex)
    {
        original(npc, playerIndex);

        if (Main.netMode != NetmodeID.MultiplayerClient && playerIndex >= 0 && playerIndex < Main.maxPlayers &&
            Main.player[playerIndex] is { active: true } player)
        {
            GetOwner(npc).GetGlobalNPC<NpcKillAttribution>().lastAttacker = player;
            NpcKillAttribution source = npc.GetGlobalNPC<NpcKillAttribution>();
            source.strikeAttacker = player;
            source.strikeTick = Main.GameUpdateCount;
        }
    }

    private static int RecordStrike(On_NPC.orig_StrikeNPC_HitInfo_bool_bool original, NPC npc,
        NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return original(npc, hit, fromNet, noPlayerInteraction);

        NpcKillAttribution source = npc.GetGlobalNPC<NpcKillAttribution>();
        Player attacker = fromNet && source.strikeTick == Main.GameUpdateCount ? source.strikeAttacker : null;
        source.strikeAttacker = null;
        NPC owner = GetOwner(npc);
        bool boss = npc.boss || owner.boss || BossPartUtilities.IsPartOfTheDestroyer((short)npc.type);
        int life = owner.life;
        int damage = original(npc, hit, fromNet, noPlayerInteraction);
        if (Main.netMode == NetmodeID.SinglePlayer)
            attacker = source.strikeAttacker;
        source.strikeAttacker = null;

        if (boss && attacker != null && damage > 0 && life > 0)
            MatchStatsPlayer.RecordServerStat(attacker, MatchStatKey.BossDamageDealt, (uint)System.Math.Min(damage, life));
        return damage;
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
