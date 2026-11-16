using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Misc;

public sealed class LihzahrdAltarChanges : GlobalTile
{
    public override void SetStaticDefaults()
    {
        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[TileID.LihzahrdAltar] = true;
        TileID.Sets.PreventsTileHammeringIfOnTopOfIt[TileID.LihzahrdAltar] = true;
    }

    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => type != TileID.LihzahrdAltar;
    public override bool CanExplode(int i, int j, int type) => type != TileID.LihzahrdAltar;
}
