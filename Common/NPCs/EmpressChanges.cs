using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

/// <summary>
/// Modifies the Empress of Light's damage taken and dealt during daytime.
/// </summary>
public class EmpressDayTimeNPC : GlobalNPC
{
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
    {
        return entity.type == NPCID.HallowBoss;
    }

    public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
    {
        if (Main.dayTime)
        {
            modifiers.SourceDamage *= 2f;
        }
    }

    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        if (Main.dayTime)
        {
            // Empress takes 75% less damage during the day
            modifiers.FinalDamage *= 0.25f;
        }
    }
}
