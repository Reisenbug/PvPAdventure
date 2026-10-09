using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

internal static class WeaponBalanceDefaults
{
    internal static Dictionary<ItemDefinition, float> ItemDamage() => new()
    {
        [new(ItemID.FlyingKnife)] = 0.55f,
        [new(ItemID.DaedalusStormbow)] = 0.35f,
        [new(ItemID.TacticalShotgun)] = 0.8f,
        [new(ItemID.ChlorophyteShotbow)] = 0.63f,
        [new(ItemID.PulseBow)] = 0.67f,
        [new(ItemID.Tsunami)] = 0.4f,
        [new(ItemID.LightDisc)] = 0.8f,
        [new(ItemID.TungstenPickaxe)] = 0.2f,
        [new(ItemID.BubbleGun)] = 0.53f,
        [new(ItemID.PiercingStarlight)] = 0.3f,
        [new(ItemID.Flamethrower)] = 1f,
        [new(ItemID.ScourgeoftheCorruptor)] = 0.9f,
        [new(ItemID.VenusMagnum)] = 0.5f,
        [new(ItemID.StaffofEarth)] = 0.75f,
        [new(ItemID.PiranhaGun)] = 0.38f,
        [new(ItemID.GrenadeLauncher)] = 0.75f,
        [new(ItemID.NailGun)] = 0.85f,
        [new(ItemID.TheAxe)] = 0.65f,
        [new(ItemID.RazorbladeTyphoon)] = 0.38f,
        [new(ItemID.Stynger)] = 0.82f,
        [new(ItemID.TrueNightsEdge)] = 0.9f,
        [new(ItemID.PewMaticHorn)] = 0.75f,
        [new(ItemID.Uzi)] = 0.7f,
        [new(ItemID.SpiritFlame)] = 0.6f,
        [new(ItemID.PossessedHatchet)] = 0.6f,
        [new(ItemID.HeatRay)] = 0.28f,
        [new(ItemID.VenomStaff)] = 0.8f,
        [new(ItemID.Xenopopper)] = 0.32f,
        [new(ItemID.FairyQueenMagicItem)] = 0.17f,
        [new(ItemID.PearlwoodBow)] = 0.8f,
        [new(ItemID.TrueExcalibur)] = 0.87f,
        [new(ItemID.PaladinsHammer)] = 0.9f,
        [new(ItemID.FairyQueenRangedItem)] = 0.3f,
        [new(ItemID.SniperRifle)] = 0.33f,
        [new(ItemID.ShadowbeamStaff)] = 0.48f,
        [new(ItemID.ToxicFlask)] = 0.4f,
        [new(ItemID.LaserMachinegun)] = 0.24f,
        [new(ItemID.RocketLauncher)] = 0.52f,
        [new(ItemID.InfernoFork)] = 0.72f,
        [new(ItemID.Keybrand)] = 0.75f,
        [new(ItemID.Kraken)] = 0.5f,
        [new(ItemID.Boomstick)] = 0.6f,
    };

    internal static Dictionary<ProjectileDefinition, float> ProjectileDamage() => new()
    {
        [new(ProjectileID.ChlorophyteArrow)] = 0.5f,
        [new(ProjectileID.ElectrosphereMissile)] = 0.53f,
        [new(ProjectileID.Electrosphere)] = 0.2f,
    };

    internal static Dictionary<ItemDefinition, float> ItemAP() => new()
    {
        [new(ItemID.MeteorStaff)] = 0.45f,
        [new(ItemID.ClockworkAssaultRifle)] = 0.4f,
        [new(ItemID.CrystalVileShard)] = 0.33f,
        [new(ItemID.CrystalStorm)] = 0.75f,
        [new(ItemID.SkyFracture)] = 0.5f,
        [new(ItemID.ChainKnife)] = 0.33f,
        [new(ItemID.OnyxBlaster)] = 0.33f,
        [new(ItemID.BubbleGun)] = 0.33f,
        [new(ItemID.PiranhaGun)] = 0.5f,
        [new(ItemID.Flamethrower)] = 0.2f,
        [new(ItemID.TacticalShotgun)] = 0.35f,
        [new(ItemID.FairyQueenMagicItem)] = 0.5f,
        [new(ItemID.PiercingStarlight)] = 0.25f,
        [new(ItemID.FairyQueenRangedItem)] = 0.25f,
        [new(ItemID.ToxicFlask)] = 0.5f,
        [new(ItemID.LaserMachinegun)] = 0.39f,
        [new(ItemID.Xenopopper)] = 0.39f,
        [new(ItemID.HeatRay)] = 0.29f,
        [new(ItemID.VenusMagnum)] = 0.33f,
    };

    internal static Dictionary<ProjectileDefinition, float> ProjectileAP() => new()
    {
        [new(ProjectileID.CoolWhipProj)] = 1f,
        [new(ProjectileID.SandnadoFriendly)] = 0.33f,
        [new(ProjectileID.InsanityShadowFriendly)] = 0.5f,
    };
}
