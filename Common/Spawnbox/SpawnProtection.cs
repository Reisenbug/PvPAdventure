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
