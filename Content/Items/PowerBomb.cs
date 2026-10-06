using PvPAdventure.Content.Projectiles;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Spawnbox;
using PvPAdventure.Core.Utilities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Items;

internal class PowerBomb : ModItem
{
    public override string Texture => "PvPAdventure/Assets/Items/PowerBomb";

    public override void SetDefaults()
    {
        Item.width = Item.height = 22;
        Item.useTime = Item.useAnimation = 40;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.shoot = ModContent.ProjectileType<PowerBombProjectile>();
        Item.shootSpeed = 6f;
        Item.consumable = true;
        Item.maxStack = 9999;
        Item.noMelee = Item.noUseGraphic = true;
        Item.DamageType = DamageClass.Summon;
        Item.value = Item.sellPrice(silver: 2);
        Item.rare = ItemRarityID.LightRed;
    }

    public override void AddRecipes()
    {
        foreach (int bar in new[] { ItemID.CobaltBar, ItemID.PalladiumBar })
            CreateRecipe().AddIngredient(ItemID.Bomb, 5).AddIngredient(bar)
                .AddTile(TileID.Anvils).Register();
    }

    public override bool CanUseItem(Player player)
    {
        var region = ModContent.GetInstance<RegionManager>().GetRegionIntersecting(player.Hitbox.ToTileRectangle());
        return !player.dead && !player.ghost && ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing
            && (region == null || region.AllowCombat);
    }
}

internal class StickyPowerBomb : PowerBomb
{
    public override string Texture => "PvPAdventure/Assets/Items/StickyPowerBomb";

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.shoot = ModContent.ProjectileType<StickyPowerBombProjectile>();
    }

    public override void AddRecipes() => CreateRecipe().AddIngredient(ModContent.ItemType<PowerBomb>())
        .AddIngredient(ItemID.Gel).Register();
}
