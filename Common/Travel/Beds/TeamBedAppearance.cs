using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Beds;

[Autoload(Side = ModSide.Client)]
internal sealed class TeamBedAppearance : GlobalTile
{
    public override void DrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        if (type != TileID.Beds || !TeamBedSystem.TryGetBedOriginFromTile(i, j, out Point origin) ||
            !ModContent.GetInstance<TeamBedSystem>().TryGetTeam(origin, out Team team) ||
            (int)team <= 0 || (int)team >= Main.teamColor.Length)
            return;

        Vector4 teamTint = Color.Lerp(Color.White, Main.teamColor[(int)team], 0.65f).ToVector4();
        teamTint.W = 1f;
        drawData.tileLight = new Color(drawData.tileLight.ToVector4() * teamTint);
    }
}
