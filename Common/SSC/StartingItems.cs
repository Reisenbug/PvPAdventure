using System;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.SSC;

internal static class StartingItems
{
    private const int MainInventorySize = 50;

    public static void Apply(Player player)
    {
        int portalType = ModContent.ItemType<PortalCreatorItem>();
        int slot = 0;
        bool hasPortal = false;
        foreach (SSCConfig.StartingItem entry in ModContent.GetInstance<SSCConfig>().StartItems ?? [])
        {
            if (entry?.Item is not { IsUnloaded: false, Type: > ItemID.None } definition || entry.Stack <= 0)
                continue;

            if (slot >= MainInventorySize)
                break;

            Item item = new(definition.Type);
            item.stack = Math.Min(entry.Stack, item.maxStack);
            player.inventory[slot++] = item;
            hasPortal |= item.type == portalType;
        }

        if (slot == 0)
        {
            player.inventory[0] = new Item(ItemID.CopperShortsword);
            player.inventory[1] = new Item(ItemID.CopperPickaxe);
            player.inventory[2] = new Item(ItemID.CopperAxe);
            player.inventory[4] = new Item(ItemID.Bed);
            slot = 5;
        }

        if (!hasPortal)
            player.inventory[Math.Min(slot, MainInventorySize - 1)] = new Item(portalType);
    }
}
