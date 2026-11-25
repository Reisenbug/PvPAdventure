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
    private Vector2 movementStart;
    private bool startedOutsideSpawn;

    public override void PreUpdateMovement()
    {
        Rectangle bounds = AdventureRegionSystem.WorldArea;
        startedOutsideSpawn = !bounds.IsEmpty && !bounds.Intersects(Player.Hitbox);
        movementStart = Player.position;
    }

    public override void PostUpdate()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
            return;

        Rectangle bounds = AdventureRegionSystem.WorldArea;
        GameManager.Phase phase = ModContent.GetInstance<GameManager>().CurrentPhase;
        if (Player.dead || bounds.IsEmpty || phase != GameManager.Phase.Waiting)
        {
            confined = false;
            if (!Player.dead && !Player.ghost && phase == GameManager.Phase.Playing &&
                startedOutsideSpawn && bounds.Intersects(Player.Hitbox))
                BlockEntry(bounds);
            startedOutsideSpawn = false;
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

    private void BlockEntry(Rectangle bounds)
    {
        Rectangle start = new((int)movementStart.X, (int)movementStart.Y, Player.width, Player.height);
        if (start.Right <= bounds.Left || start.Left >= bounds.Right)
        {
            Player.position.X = start.Right <= bounds.Left ? bounds.Left - Player.width : bounds.Right;
            Player.velocity.X = 0f;
        }
        else
        {
            bool fromAbove = start.Bottom <= bounds.Top;
            Player.position.Y = fromAbove ? bounds.Top - Player.height : bounds.Bottom;
            Player.velocity.Y = 0f;
            if (fromAbove ? Player.gravDir > 0f : Player.gravDir < 0f)
            {
                Player.jump = 0;
                Player.RefreshMovementAbilities(!Player.mount.Active || !Player.mount.BlockExtraJumps);
            }
        }
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
