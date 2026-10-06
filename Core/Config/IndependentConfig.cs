using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
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

    new(ItemID.Wood, 1, Item.buyPrice(silver: 2)),
    new(ItemID.MiningPotion, 1, Item.buyPrice(silver: 750)),
    new(ItemID.Torch, 1, Item.buyPrice(silver: 1)),
    new(ItemID.WoodenBoomerang, 1, Item.buyPrice(silver: 350)),
    new(ItemID.Umbrella, 1, Item.buyPrice(silver: 159)),
    new(ItemID.StoneBlock, 1, Item.buyPrice(copper: 125)),
    new(ItemID.Blowpipe, 1, Item.buyPrice(silver: 200)),
    new(ItemID.Seed, 1, Item.buyPrice(silver: 20)),
    new(ItemID.BlandWhip, 1, Item.buyPrice(silver: 500)),
    new(ItemID.BabyBirdStaff, 1, Item.buyPrice(silver: 170)),
    new(ItemID.Shackle, 1, Item.buyPrice(silver: 20)),
    new(ItemID.ClimbingClaws, 1, Item.buyPrice(silver: 20)),
    new(ItemID.Flipper, 1, Item.buyPrice(silver: 60)),
    new(ItemID.BandofStarpower, 1, Item.buyPrice(silver: 20)),
    new(ItemID.NaturesGift, 1, Item.buyPrice(silver: 20)),
    new(ItemID.IronBar, 1, Item.buyPrice(silver: 75)),
    new(ItemID.IronskinPotion, 1, Item.buyPrice(silver: 500)),
    new(ItemID.ThrowingKnife, 1, Item.buyPrice(silver: 35)),
    new(ItemID.Shuriken, 1, Item.buyPrice(silver: 16)),
    new(ItemID.LesserHealingPotion, 1, Item.buyPrice(silver: 300)),
    new(ItemID.ShinePotion, 1, Item.buyPrice(silver: 125)),
    new(ItemID.NightOwlPotion, 1, Item.buyPrice(silver: 160)),
    new(ItemID.SwiftnessPotion, 1, Item.buyPrice(silver: 120)),
    new(ItemID.HeartreachPotion, 1, Item.buyPrice(silver: 185)),
    new(ItemID.CalmingPotion, 1, Item.buyPrice(silver: 52)),
    new(ItemID.RegenerationPotion, 1, Item.buyPrice(silver: 500)),
    new(ItemID.SpelunkerPotion, 1, Item.buyPrice(copper: 80000)),
    new(ItemID.BuilderPotion, 1, Item.buyPrice(silver: 250)),
    new(ItemID.Cobweb, 1, Item.buyPrice(silver: 3)),
    new(ItemID.Grenade, 1, Item.buyPrice(silver: 250)),
    new(ItemID.Aglet, 1, Item.buyPrice(silver: 70)),
    new(ItemID.Trident, 1, Item.buyPrice(silver: 350)),
    new(ItemID.Toolbox, 1, Item.buyPrice(silver: 300)),
    new(ItemID.PortableStool, 1, Item.buyPrice(silver: 80)),
    new(ItemID.Bottle, 1, Item.buyPrice(silver: 10)),
    new(ItemID.Mushroom, 1, Item.buyPrice(silver: 17)),
    new(ItemID.WandofSparking, 1, Item.buyPrice(silver: 875)),
    new(ItemID.HunterPotion, 1, Item.buyPrice(silver: 175)),
    new(ItemID.Rope, 1, Item.buyPrice(copper: 50)),
    new(ItemID.WaterWalkingBoots, 1, Item.buyPrice(silver: 125)),
    new(ItemID.MoonLordLegs, 1, Item.buyPrice(silver: 999)),
    new(ItemID.Bomb, 1, Item.buyPrice(silver: 70)),
    new(ItemID.Worm, 1, Item.buyPrice(silver: 15)),
    new(ItemID.EnchantedNightcrawler, 1, Item.buyPrice(silver: 50)),
    new(ItemID.GoldWorm, 1, Item.buyPrice(silver: 1000)),
    new(ItemID.Glowstick, 1, Item.buyPrice(silver: 10)),
    new(ItemID.GillsPotion, 1, Item.buyPrice(silver: 100)),
    new(ItemID.TrapsightPotion, 1, Item.buyPrice(silver: 100)),
    new(ItemID.FlipperPotion, 1, Item.buyPrice(silver: 30)),
    new(ItemID.Dynamite, 1, Item.buyPrice(silver: 300)),
    new(ItemID.WhoopieCushion, 1, Item.buyPrice(silver: 200)),
    new(ItemID.Radar, 1, Item.buyPrice(silver: 20)),
    new(ItemID.Rambutan, 1, Item.buyPrice(silver: 278)),
    new(ItemID.Spear, 1, Item.buyPrice(silver: 20)),
    new(ItemID.CopperBar, 1, Item.buyPrice(copper: 1300)),
    new(ItemID.TinBar, 1, Item.buyPrice(copper: 1600)),
    new(ItemID.BlinkrootSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.DaybloomSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.DeathweedSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.MoonglowSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.ShiverthornSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.WaterleafSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.FireblossomSeeds, 1, Item.buyPrice(silver: 15)),
    new(ItemID.Blinkroot, 1, Item.buyPrice(silver: 200)),
    new(ItemID.Daybloom, 1, Item.buyPrice(silver: 100)),
    new(ItemID.Waterleaf, 1, Item.buyPrice(silver: 20)),
    new(ItemID.Shiverthorn, 1, Item.buyPrice(silver: 20)),
    new(ItemID.Fireblossom, 1, Item.buyPrice(silver: 500)),
    new(ItemID.Deathweed, 1, Item.buyPrice(silver: 500)),
    new(ItemID.Moonglow, 1, Item.buyPrice(silver: 300)),
    new(ItemID.Cactus, 1, Item.buyPrice(silver: 20)),
    new(ItemID.FloatingTube, 1, Item.buyPrice(silver: 50)),
    new() { Item = new ItemDefinition("PvPAdventure/PowerBomb"), Stack = 1, Price = Item.buyPrice(gold: 10) }
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
