using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Projectiles;

internal class PowerBombExplosion : ModProjectile
{
    public override string Texture => "PvPAdventure/Assets/Projectiles/PowerBombProjectile";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = PowerBombProjectile.Radius * 32;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Summon;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 3;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.alpha = 255;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool CanHitPlayer(Player target) => false;
    public override bool CanHitPvp(Player target) => false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 closest = new(Math.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
            Math.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
        return Vector2.DistanceSquared(closest, Projectile.Center) <= PowerBombProjectile.Radius * PowerBombProjectile.Radius * 256f;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) =>
        modifiers.HitDirectionOverride = target.Center.X > Projectile.Center.X ? 1 : -1;
}
