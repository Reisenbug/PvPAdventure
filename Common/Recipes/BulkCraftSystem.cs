using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Recipes;

// VANILLA145_BACKPORT: Shift crafts up to ten recipe batches per click.
public sealed class BulkCraftSystem : ModSystem
{
    public override void Load() => On_Main.CraftItem += CraftItem;

    public override void Unload() => On_Main.CraftItem -= CraftItem;

    private static void CraftItem(On_Main.orig_CraftItem orig, Recipe recipe)
    {
        if (Main.netMode == NetmodeID.Server || !ItemSlot.ShiftInUse)
        {
            orig(recipe);
            return;
        }
        for (int i = 0; i < 10; i++)
        {
            bool available = false;
            for (int j = 0; j < Main.numAvailableRecipes; j++)
                if (Main.recipe[Main.availableRecipe[j]] == recipe)
                {
                    available = true;
                    break;
                }
            if (!available || (!Main.mouseItem.IsAir &&
                (!ItemLoader.CanStack(Main.mouseItem, recipe.createItem) ||
                 Main.mouseItem.stack + recipe.createItem.stack > Main.mouseItem.maxStack)))
                break;
            orig(recipe);
        }
    }
}
