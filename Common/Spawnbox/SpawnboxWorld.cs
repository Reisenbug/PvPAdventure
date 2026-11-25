using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.GameTimer;
using Terraria;
using Terraria.GameContent;
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
        float pulse = 0.75f + 0.25f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f);

        var batch = Main.spriteBatch;
        DrawBorder(batch, outer, 9, color * (0.16f * pulse));
        DrawBorder(batch, outer, 3, color * (0.72f * pulse));
        if (ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting)
        {
            DrawBorder(batch, inner, 7, color * (0.11f * pulse));
            DrawBorder(batch, inner, 2, color * (0.48f * pulse));
        }
        return true;
    }

    private static Rectangle ToScreen(Rectangle area) =>
        new((int)(area.X * 16f - Main.screenPosition.X), (int)(area.Y * 16f - Main.screenPosition.Y),
            area.Width * 16, area.Height * 16);

    private static void DrawBorder(SpriteBatch batch, Rectangle area, int thickness, Color color)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        batch.Draw(pixel, new Rectangle(area.Left, area.Top, area.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(area.Left, area.Bottom - thickness, area.Width, thickness), color);
        batch.Draw(pixel, new Rectangle(area.Left, area.Top + thickness, thickness, area.Height - thickness * 2), color);
        batch.Draw(pixel, new Rectangle(area.Right - thickness, area.Top + thickness, thickness, area.Height - thickness * 2), color);
    }
}
