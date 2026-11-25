using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Visualization;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Spawnbox;

[Autoload(Side = ModSide.Client)]
public class SpawnBoxWorld : ModSystem
{
    public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Interface Logic 1");
        if (index >= 0)
            layers.Insert(index + 1, new LegacyGameInterfaceLayer("PvPAdventure: SpawnBox", Draw, InterfaceScaleType.Game));
    }

    private static bool Draw()
    {
        var regions = ModContent.GetInstance<RegionManager>().Regions;
        if (regions.Count == 0 || Main.mapFullscreen)
            return true;

        var region = regions[0];
        Rectangle outer = ToScreen(region.CollisionRingBounds);
        Rectangle inner = ToScreen(region.Area);
        int team = Main.LocalPlayer.team;
        Color color = team > 0 && team < Main.teamColor.Length ? Main.teamColor[team] : new Color(125, 192, 240);
        float time = Main.GlobalTimeWrappedHourly;
        float pulse = 0.88f + 0.12f * (float)System.Math.Sin(time * 2.6f);

        var batch = Main.spriteBatch;
        DrawBorder(batch, outer, color, pulse * 0.72f);
        if (ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting)
            DrawBorder(batch, inner, color, pulse * 0.33f);

        return true;
    }

    private static Rectangle ToScreen(Rectangle area) =>
        new((int)(area.X * 16f - Main.screenPosition.X), (int)(area.Y * 16f - Main.screenPosition.Y),
            area.Width * 16, area.Height * 16);

    private static void DrawBorder(SpriteBatch batch, Rectangle area, Color color, float opacity)
    {
        const int width = 16;
        var textures = ModContent.GetInstance<SoftGlowTextures>();
        Color tint = Color.Lerp(color, Color.White, 0.23f) * opacity;
        float horizontal = area.Width - width * 2;
        float vertical = area.Height - width * 2;

        DrawEdge(new(area.Left + width, area.Top), horizontal, 0f);
        DrawEdge(new(area.Right, area.Top + width), vertical, MathHelper.PiOver2);
        DrawEdge(new(area.Right - width, area.Bottom), horizontal, MathHelper.Pi);
        DrawEdge(new(area.Left, area.Bottom - width), vertical, -MathHelper.PiOver2);

        DrawCorner(area.Left, area.Top, SpriteEffects.None);
        DrawCorner(area.Right - width, area.Top, SpriteEffects.FlipHorizontally);
        DrawCorner(area.Left, area.Bottom - width, SpriteEffects.FlipVertically);
        DrawCorner(area.Right - width, area.Bottom - width, SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically);

        void DrawEdge(Vector2 position, float length, float angle)
        {
            batch.Draw(textures.Band, position, null, tint, angle, Vector2.Zero,
                new Vector2(length, width / (float)SoftGlowTextures.Size), SpriteEffects.None, 0f);
        }

        void DrawCorner(int x, int y, SpriteEffects flip) =>
            batch.Draw(textures.Corner, new Rectangle(x, y, width, width), null, tint, 0f, Vector2.Zero, flip, 0f);
    }
}
