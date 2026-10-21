using MonoMod.Cil;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace PvPAdventure.Common.WorldGenChanges;

public class WorldGenerationManager : ModSystem
{
    public override void Load()
    {
        IL_WorldGen.UpdateWorld_GrassGrowth += EditWorldGenUpdateWorld_GrassGrowth;
        IL_WorldGen.AddBuriedChest_int_int_int_bool_int_bool_ushort += EditWorldGenAddBuriedChest;
    }

    private void EditWorldGenUpdateWorld_GrassGrowth(ILContext il)
    {
        var cursor = new ILCursor(il);

        cursor.GotoNext(i => i.MatchLdsfld<NPC>(nameof(NPC.downedMechBoss3)));
        cursor.GotoNext(MoveType.Before, i => i.MatchCallvirt<UnifiedRandom>("Next"));
        cursor.EmitDelegate((int original) =>
            ModContent.GetInstance<ServerConfig>().WorldGeneration.PlanteraBulbChanceDenominator);

        cursor.GotoNext(i => i.MatchLdsfld<NPC>(nameof(NPC.downedMechBossAny)));
        cursor.GotoNext(MoveType.Before, i => i.MatchCallvirt<UnifiedRandom>("Next"));
        cursor.EmitDelegate((int original) => Main.expertMode
            ? ModContent.GetInstance<ServerConfig>().WorldGeneration.LifeFruitExpertChanceDenominator
            : ModContent.GetInstance<ServerConfig>().WorldGeneration.LifeFruitChanceDenominator);

        cursor.GotoNext(MoveType.After, i => i.MatchLdcI4(60));
        cursor.GotoNext(MoveType.Before, i => i.MatchStloc(out _));
        cursor.EmitDelegate((int original) =>
            ModContent.GetInstance<ServerConfig>().WorldGeneration.LifeFruitMinimumDistanceBetween);
    }

    
    private void EditWorldGenAddBuriedChest(ILContext il)
    {
        var cursor = new ILCursor(il);

        // Find the first reference to Main.chest...
        cursor.GotoNext(i => i.MatchLdsfld<Main>("chest"));

        // ...then advance to the initial loop condition check branch...
        cursor.Index += 6;

        // ...following the branch to the loop entry point...
        cursor.GotoLabel((ILLabel)cursor.Next!.Operand);

        // ...and go past the two instructions checking the loop condition...
        cursor.Index += 2;

        // ...to load the chest index...
        cursor.EmitLdloc(17);
        // ...and emit our own delegate to invoke.
        cursor.EmitDelegate((int chestId) =>
        {
            var adventureConfig = ModContent.GetInstance<ServerConfig>();
            var chest = Main.chest[chestId];

            foreach (var item in chest.item)
            {
                if (adventureConfig.ChestItemReplacements.TryGetValue(new(item.type), out var replacement))
                {
                    var configItem = Utils.SelectRandom(WorldGen.genRand, replacement.Items.ToArray());
                    item.SetDefaults(configItem.Item.Type);
                    item.stack = configItem.Stack;
                    item.prefix = configItem.Prefix.Type;
                }
            }
        });
    }
}
