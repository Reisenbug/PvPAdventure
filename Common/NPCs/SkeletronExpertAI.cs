using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

public sealed class SkeletronExpertAI : ModSystem
{
    public override void Load() => IL_NPC.VanillaAI_Inner += UseExpertAI;

    public override void Unload() => IL_NPC.VanillaAI_Inner -= UseExpertAI;

    private static void UseExpertAI(ILContext il)
    {
        var cursor = new ILCursor(il);
        int matches = 0;
        while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchCall<Main>("get_expertMode")))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, NPC, bool>>((expert, npc) =>
                expert || npc.type is NPCID.SkeletronHead or NPCID.SkeletronHand);
            matches++;
        }
        if (matches == 0)
            throw new InvalidOperationException("Skeletron expert AI difficulty checks not found");
    }
}
