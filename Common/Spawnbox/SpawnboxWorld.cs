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

        var textures = ModContent.GetInstance<SoftGlowTextures>();
        int perimeter = (outer.Width + outer.Height) * 2;
        for (int i = 0; i < 12; i++)
        {
            float distance = (time * 58f + i * perimeter / 12f) % perimeter;
            Vector2 spark = PointOnBorder(outer, distance);
            batch.Draw(textures.Bloom, spark, null, color * 0.72f, 0f, new Vector2(16f), 0.58f,
                SpriteEffects.None, 0f);
        }
        return true;
    }

    private static Rectangle ToScreen(Rectangle area) =>
        new((int)(area.X * 16f - Main.screenPosition.X), (int)(area.Y * 16f - Main.screenPosition.Y),
            area.Width * 16, area.Height * 16);

    private static Vector2 PointOnBorder(Rectangle area, float distance)
    {
        if (distance < area.Width) return new(area.Left + distance, area.Top);
        distance -= area.Width;
        if (distance < area.Height) return new(area.Right, area.Top + distance);
        distance -= area.Height;
        return distance < area.Width ? new(area.Right - distance, area.Bottom)
            : new(area.Left, area.Bottom - (distance - area.Width));
    }

    private static void DrawBorder(SpriteBatch batch, Rectangle area, Color color, float opacity)
    {
        var textures = ModContent.GetInstance<SoftGlowTextures>();
        Texture2D band = textures.Band;
        Color center = Color.Lerp(color, Color.White, 0.42f);
        Vector2 origin = new(0f, SoftGlowTextures.Size / 2f);

        DrawEdge(new(area.Left, area.Top), area.Width, 0f);
        DrawEdge(new(area.Left, area.Bottom), area.Width, 0f);
        DrawEdge(new(area.Left, area.Top), area.Height, MathHelper.PiOver2);
        DrawEdge(new(area.Right, area.Top), area.Height, MathHelper.PiOver2);

        void DrawEdge(Vector2 position, float length, float angle)
        {
            batch.Draw(band, position, null, color * (opacity * 0.52f), angle, origin,
                new Vector2(length, 20f / SoftGlowTextures.Size), SpriteEffects.None, 0f);
            batch.Draw(band, position, null, center * opacity, angle, origin,
                new Vector2(length, 5f / SoftGlowTextures.Size), SpriteEffects.None, 0f);
        }
    }
}
