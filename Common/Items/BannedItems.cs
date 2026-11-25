using System.Linq;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Items;

public sealed class BannedItems : GlobalItem
{
    private static bool IsBanned(Item item) =>
        ModContent.GetInstance<ServerConfig>().PreventUse.Any(definition => definition.Type == item.type);

    public override bool CanUseItem(Item item, Player player) => !IsBanned(item);

    public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) => !IsBanned(item);

    public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) => IsBanned(ammo) ? false : null;
}
