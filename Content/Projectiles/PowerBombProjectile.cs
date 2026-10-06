using Microsoft.Xna.Framework;
using PvPAdventure.Common.Spawnbox;
using PvPAdventure.Core.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Projectiles;

internal class PowerBombProjectile : ModProjectile
{
    internal const int Radius = 9;
    public override string Texture => "PvPAdventure/Assets/Projectiles/PowerBombProjectile";
    protected virtual bool Sticky => false;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Summon;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 300;
        Projectile.tileCollide = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        if (Projectile.ai[0] == 1 && Sticky)
        {
            if (Collision.SolidCollision(Projectile.position - new Vector2(2), Projectile.width + 4, Projectile.height + 4))
                Projectile.velocity = Vector2.Zero;
            else
            {
                Projectile.ai[0] = 0;
                Projectile.tileCollide = true;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            Projectile.velocity.Y += 0.2f;
            Projectile.velocity.X *= 0.99f;
            Projectile.rotation += Projectile.velocity.X * 0.05f;
        }
        if (!Main.dedServ && Main.rand.NextBool(3))
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Sticky)
        {
            Projectile.ai[0] = 1;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }
        else
        {
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
            if (Projectile.velocity.Y != oldVelocity.Y && oldVelocity.Y > 1f) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
        }
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (!Main.dedServ)
        {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
            for (int i = 0; i < 60; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    i % 2 == 0 ? DustID.Smoke : DustID.Torch);
                dust.velocity *= 6f;
                dust.noGravity = true;
            }
        }
        if (Main.netMode == NetmodeID.MultiplayerClient || (uint)Projectile.owner >= Main.maxPlayers)
            return;

        DestroyTiles();
        Player owner = Main.player[Projectile.owner];
        foreach (Player target in Main.ActivePlayers)
        {
            if (!CanDamagePlayer(owner, target)) continue;
            int direction = target.Center.X > Projectile.Center.X ? 1 : -1;
            double damage = target.Hurt(PlayerDeathReason.ByProjectile(Projectile.owner, Projectile.whoAmI),
                200, direction, out Player.HurtInfo info, pvp: true, knockback: 9f);
            if (Main.netMode == NetmodeID.Server && damage > 0)
                NetMessage.SendPlayerHurt(target.whoAmI, info);
        }
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
            ModContent.ProjectileType<PowerBombExplosion>(), 200, 9f, Projectile.owner);
    }

    private bool CanDamagePlayer(Player owner, Player target)
    {
        if (!target.active || target.dead || target.ghost ||
            Vector2.DistanceSquared(target.Center, Projectile.Center) > Radius * Radius * 256f)
            return false;
        if (target.whoAmI != owner.whoAmI && (!target.hostile || owner.team != 0 && target.team == owner.team))
            return false;
        var region = ModContent.GetInstance<RegionManager>().GetRegionIntersecting(target.Hitbox.ToTileRectangle());
        return region == null || region.AllowCombat;
    }

    private static bool CanDestroyTile(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (tile.TileType is TileID.LihzahrdBrick or TileID.LihzahrdAltar or TileID.DemonAltar
            or TileID.PinkDungeonBrick or TileID.BlueDungeonBrick or TileID.GreenDungeonBrick)
            return false;
        if (tile.TileType == TileID.Chlorophyte && !(NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3))
            return false;
        return TileLoader.CanExplode(x, y);
    }

    private void DestroyTiles()
    {
        Point center = Projectile.Center.ToTileCoordinates();
        var regions = ModContent.GetInstance<RegionManager>();
        for (int x = center.X - Radius; x <= center.X + Radius; x++)
        for (int y = center.Y - Radius; y <= center.Y + Radius; y++)
        {
            if (!WorldGen.InWorld(x, y, 1) || (x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y) > Radius * Radius)
                continue;
            var region = regions.GetRegionContaining(new Point(x, y));
            if (region != null && !region.CanModifyTiles) continue;
            Tile tile = Main.tile[x, y];
            if (tile.HasTile && CanDestroyTile(x, y)) WorldGen.KillTile(x, y);
            if (tile.WallType is not (WallID.LihzahrdBrickUnsafe or WallID.PinkDungeonUnsafe or WallID.BlueDungeonUnsafe or WallID.GreenDungeonUnsafe)
                && WallLoader.CanExplode(x, y, tile.WallType)) WorldGen.KillWall(x, y);
        }
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, center.X - Radius, center.Y - Radius, Radius * 2 + 1, Radius * 2 + 1);
    }
}

internal class StickyPowerBombProjectile : PowerBombProjectile
{
    public override string Texture => "PvPAdventure/Assets/Projectiles/StickyPowerBombProjectile";
    protected override bool Sticky => true;
}
