using PvPAdventure.Core.Compat;
using Terraria;
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
    public override bool CanHitPvp(Item item, Player target) =>
        !AdventureRegionSystem.Contains(Player) && !AdventureRegionSystem.Contains(target);

    public override bool CanHitPvpWithProj(Projectile proj, Player target) =>
        !AdventureRegionSystem.Contains(Player) && !AdventureRegionSystem.Contains(target);
}
