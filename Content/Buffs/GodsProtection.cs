using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Buffs;

public sealed class GodsProtection : ModBuff
{
    public override string Texture => "Terraria/Images/Buff_" + BuffID.ShadowDodge;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
    }
}
