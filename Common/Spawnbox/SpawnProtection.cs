using System;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.RacePeriod;
using PvPAdventure.Core.Compat;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Spawnbox;

internal sealed class SpawnProtectionTile : GlobalTile
{
    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) =>
        !AdventureRegionSystem.TileArea.Contains(i, j);

    public override bool CanExplode(int i, int j, int type) =>
        !AdventureRegionSystem.TileArea.Contains(i, j);

    public override bool CanPlace(int i, int j, int type) =>
        !AdventureRegionSystem.TileArea.Contains(i, j);

    public override bool CanReplace(int i, int j, int type, int tileTypeBeingPlaced) =>
        !AdventureRegionSystem.TileArea.Contains(i, j);
}

internal sealed class SpawnProtectionPlayer : ModPlayer
{
    private bool confined;
    [ThreadStatic]
    private static Player collisionPlayer;

    public override void Load()
    {
        On_Player.Update += UpdateWithSpawnCollision;
        On_Collision.TileCollision += CollideWithSpawn;
    }

    public override void Unload()
    {
        On_Player.Update -= UpdateWithSpawnCollision;
        On_Collision.TileCollision -= CollideWithSpawn;
    }

    private static void UpdateWithSpawnCollision(On_Player.orig_Update orig, Player player, int index)
    {
        Player previous = collisionPlayer;
        collisionPlayer = player;
        try
        {
            orig(player, index);
        }
        finally
        {
            collisionPlayer = previous;
        }
    }

    private static Vector2 CollideWithSpawn(On_Collision.orig_TileCollision orig, Vector2 position,
        Vector2 velocity, int width, int height, bool fallThrough, bool fall2, int gravity)
    {
        Vector2 movement = orig(position, velocity, width, height, fallThrough, fall2, gravity);
        Player player = collisionPlayer;
        if (player == null || player.dead || player.ghost || player.position != position ||
            player.width != width || (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI != Main.myPlayer) ||
            ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
            return movement;

        Rectangle bounds = AdventureRegionSystem.WorldArea;
        float left = position.X;
        float right = left + width;
        float top = position.Y;
        float bottom = top + height;
        if (bounds.IsEmpty || (left < bounds.Right && right > bounds.Left && top < bounds.Bottom && bottom > bounds.Top))
            return movement;

        if (bottom > bounds.Top && top < bounds.Bottom)
        {
            if (right <= bounds.Left && right + movement.X > bounds.Left)
                movement.X = bounds.Left - right;
            else if (left >= bounds.Right && left + movement.X < bounds.Right)
                movement.X = bounds.Right - left;
        }

        if (right + movement.X > bounds.Left && left + movement.X < bounds.Right)
        {
            if (bottom <= bounds.Top && bottom + movement.Y > bounds.Top)
            {
                movement.Y = bounds.Top - bottom;
                Collision.down = true;
            }
            else if (top >= bounds.Bottom && top + movement.Y < bounds.Bottom)
            {
                movement.Y = bounds.Bottom - top;
                Collision.up = true;
            }
        }

        return movement;
    }

    public override void PostUpdate()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
            return;

        Rectangle bounds = AdventureRegionSystem.WorldArea;
        if (Player.dead || bounds.IsEmpty ||
            ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Waiting)
        {
            confined = false;
            return;
        }

        if (!confined && !bounds.Intersects(Player.Hitbox))
            return;
        confined = true;

        if (Player.GetModPlayer<RacePeriodPlayer>().IsRacing)
            bounds.Inflate(22, 22);

        Vector2 position = Player.position;
        float x = MathHelper.Clamp(position.X, bounds.Left, bounds.Right - Player.width);
        float y = MathHelper.Clamp(position.Y, bounds.Top, bounds.Bottom - Player.height);
        if (x != position.X)
            Player.velocity.X = 0;
        if (y != position.Y)
            Player.velocity.Y = 0;
        Player.position = new Vector2(x, y);
    }

    public override bool CanHitPvp(Item item, Player target) =>
        !AdventureRegionSystem.Contains(Player) && !AdventureRegionSystem.Contains(target);

    public override bool CanHitPvpWithProj(Projectile proj, Player target) =>
        !AdventureRegionSystem.Contains(Player) && !AdventureRegionSystem.Contains(target);
}

internal sealed class SpawnProtectionWall : GlobalWall
{
    public override void KillWall(int i, int j, int type, ref bool fail)
    {
        if (AdventureRegionSystem.TileArea.Contains(i, j))
            fail = true;
    }
}

internal sealed class SpawnProtectionProjectile : GlobalProjectile
{
    public override bool? CanCutTiles(Projectile projectile) =>
        AdventureRegionSystem.WorldArea.Intersects(projectile.Hitbox) ? false : null;
}
