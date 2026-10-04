using System.Collections.Generic;
using System.ComponentModel;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

public sealed class IndependentConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(10)]
    [Range(0, 60)]
    public int PortalCreationSeconds = 10;

    [DefaultValue(500)]
    [Range(1, 100000)]
    public int PortalLife = 500;

    [DefaultValue(10)]
    [Range(0, 1000)]
    public int StartingGold = 10;

    public List<KitItem> StartingShop = [
        new(ItemID.Wood, 10, 20),
        new(ItemID.Torch, 10, 100),
        new(ItemID.Rope, 10, 500),
        new(ItemID.WoodenBoomerang, 1, 35000),
        new(ItemID.Umbrella, 1, 15900),
        new(ItemID.Blowpipe, 1, 20000),
        new(ItemID.Seed, 20, 40000),
        new(ItemID.BlandWhip, 1, 50000),
        new(ItemID.BabyBirdStaff, 1, 17000),
        new(ItemID.Shackle, 1, 2000),
        new(ItemID.ClimbingClaws, 1, 2000),
        new(ItemID.Flipper, 1, 6000),
        new(ItemID.BandofStarpower, 1, 2000),
        new(ItemID.NaturesGift, 1, 2000),
        new(ItemID.IronBar, 1, 7500)
    ];

    public sealed class KitItem
    {
        public ItemDefinition Item = new();
        [Range(1, 9999)]
        public int Stack = 1;
        [Range(0, 10000000)]
        public int Price;

        public KitItem() { }
        public KitItem(int type, int stack, int price)
        {
            Item = new(type);
            Stack = stack;
            Price = price;
        }
    }
}
