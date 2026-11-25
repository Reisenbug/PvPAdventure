using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

#nullable enable

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Supplies race animal portraits to the world map's existing player markers.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodMapHeads : ModSystem
{
    private Asset<Texture2D>? bunnyHead;

    public override void Load()
    {
        bunnyHead = Mod.Assets.Request<Texture2D>("Assets/SpiritAnimalMapHeads/bunny", AssetRequestMode.ImmediateLoad);
        On_MapHeadRenderer.DrawPlayerHead += DrawHead;
    }

    public override void Unload()
    {
        On_MapHeadRenderer.DrawPlayerHead -= DrawHead;
        bunnyHead = null;
    }

    private void DrawHead(On_MapHeadRenderer.orig_DrawPlayerHead original, MapHeadRenderer self, Camera camera,
        Player player, Vector2 position, float alpha, float scale, Color borderColor)
    {
        if (player.ghost || player.dead || player.mount.Type != RacePeriodRules.FallbackMountType ||
            bunnyHead is null || !player.TryGetModPlayer(out RacePeriodPlayer race) || !race.IsRacing)
        {
            original(self, camera, player, position, alpha, scale, borderColor);
            return;
        }

        Texture2D texture = bunnyHead.Value;
        Vector2 origin = new(texture.Width * 0.5f, texture.Height * 0.5f);
        Main.spriteBatch.Draw(texture, position, null, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
