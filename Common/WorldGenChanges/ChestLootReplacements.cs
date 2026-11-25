using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.WorldGenChanges;

public sealed class ChestLootReplacements : ModSystem
{
    public override void PostWorldGen()
    {
        var replacements = ModContent.GetInstance<ServerConfig>().ChestItemReplacements;
        foreach (Chest chest in Main.chest)
        {
            if (chest == null)
                continue;

            foreach (Item item in chest.item)
            {
                if (item == null || item.IsAir)
                    continue;

                if (item.type is ItemID.RecallPotion or ItemID.WormholePotion or ItemID.TeleportationPotion
                    or ItemID.PotionOfReturn or ItemID.MagicMirror or ItemID.IceMirror)
                {
                    item.TurnToAir();
                    continue;
                }

                if (!replacements.TryGetValue(new(item.type), out var replacement) || replacement.Items.Count == 0)
                    continue;

                var result = replacement.Items[WorldGen.genRand.Next(replacement.Items.Count)];
                item.SetDefaults(result.Item.Type);
                if (!item.IsAir)
                {
                    item.stack = result.Stack;
                    item.prefix = result.Prefix.Type;
                }
            }
        }
    }
}
