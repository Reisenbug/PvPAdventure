using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Combat.EJ;

public sealed class GlovesDontStack : ModPlayer
{
    public override void PostUpdateEquips()
    {
        int gloves = 0;
        for (int i = 3; i <= 9; i++)
            if (Player.IsItemSlotUnlockedAndUsable(i) && Player.armor[i].type is
                ItemID.FeralClaws or ItemID.PowerGlove or ItemID.BerserkerGlove or ItemID.MechanicalGlove or ItemID.FireGauntlet)
                gloves++;

        if (gloves > 1)
            Player.GetAttackSpeed(DamageClass.Melee) -= 0.12f * (gloves - 1);
    }
}
