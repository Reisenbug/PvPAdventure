using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Combat.PvP;

internal class PvPBeetleArmorPlayer : ModPlayer
{
    public override void Load()
    {
        // Remove logic for handling Beetle Might buffs.
        IL_Player.UpdateBuffs += EditPlayerUpdateBuffs;
        // Simplify logic for handling Beetle Scale Mail set bonus to do the bare minimum required.
        IL_Player.UpdateArmorSets += EditPlayerUpdateArmorSets;
    }

    public override void SetStaticDefaults()
    {
        BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight1] = false;
        BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight2] = false;
        BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight3] = false;
    }

    private void EditPlayerUpdateBuffs(ILContext il)
    {
        var cursor = new ILCursor(il);

        ILLabel label = null;
        // First, find a load of Player.buffType that is somewhere followed by a load of BuffID.BeetleMight1 and a blt
        // instruction...
        cursor.GotoNext(i =>
            i.MatchLdfld<Player>("buffType") && i.Next.Next.Next.MatchLdcI4(98) &&
            i.Next.Next.Next.Next.MatchBlt(out label));

        // ...and go back to the "this" load...
        cursor.Index -= 1;
        // ...while ensuring that instructions removed and emitted are labeled correctly...
        cursor.MoveAfterLabels();
        // ...to emit a branch to the fail case.
        cursor.EmitBr(label);
    }

    private void EditPlayerUpdateArmorSets(ILContext il)
    {
        var cursor = new ILCursor(il);

        // Find a load to a string...
        cursor.GotoNext(i => i.MatchLdstr("ArmorSetBonus.BeetleDamage"));
        // ...and go back to the branch instruction...
        cursor.Index -= 4;

        // ...to grab it's label...
        var label = (ILLabel)cursor.Next!.Operand;
        cursor.Index += 1;

        // ...and prepare a delegate call, doing the bare minimum for set bonus functionality...
        cursor.EmitLdarg0();
        cursor.EmitDelegate((Player self) =>
        {
            self.setBonus = Language.GetTextValue("Mods.PvPAdventure.ArmorEnergy.BeetleSetBonus");
            self.beetleOffense = true;
        });
        // ...then branch away so we skip the original code.
        cursor.EmitBr(label);
    }
}
