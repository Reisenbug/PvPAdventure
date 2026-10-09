using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Combat.PvP;

public class PvPBeetleArmorBuff : GlobalBuff
{
    public override void Update(int type, Player player, ref int buffIndex)
    {
        // This has the contract that players should only have ONE Beetle Might buff at a time.
        if (type >= BuffID.BeetleMight1 && type <= BuffID.BeetleMight3)
        {
            // Calculate how many beetles we have based on the buff we have
            player.beetleOrbs = type - BuffID.BeetleMight1 + 1;

            if (player.GetModPlayer<ArmorEnergyPlayer>().BeetleTier != player.beetleOrbs)
            {
                player.DelBuff(buffIndex--);
                player.beetleOrbs = 0;
                return;
            }

            player.GetDamage<MeleeDamageClass>() += 0.1f * player.beetleOrbs;
            player.GetAttackSpeed<MeleeDamageClass>() += 0.1f * player.beetleOrbs;
        }
    }

    public override bool RightClick(int type, int buffIndex)
    {
        return type < BuffID.BeetleMight1 || type > BuffID.BeetleMight3;
    }
}
