using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Independent;

internal static class PortalVisuals
{
    internal const int FrameCount = 8;
    private static readonly string[] Teams = ["NoTeam", "Red", "Green", "Blue", "Yellow", "Pink"];

    internal static string Path(string kind, int team) =>
        $"PvPAdventure/Assets/Portals/{kind}_{Teams[Math.Clamp(team, 0, 5)]}";

    internal static Texture2D Texture(string kind, int team) => ModContent.Request<Texture2D>(Path(kind, team)).Value;

    internal static void Draw(SpriteBatch batch, Vector2 center, int team, float progress = 1f, float flash = 0f)
    {
        Texture2D texture = Texture("Portal", team);
        Rectangle frame = texture.Frame(1, FrameCount, 0, (int)(Main.GameUpdateCount / 5 % FrameCount));
        Vector2 origin = frame.Size() / 2f;
        Vector2 position = center - Main.screenPosition;
        float scale = MathHelper.Lerp(0.35f, 1f, progress);
        Texture2D outline = ModContent.Request<Texture2D>(Path("Portal", team) + "_Outlines").Value;
        batch.Draw(outline, position, frame, Color.White * (0.4f + progress * 0.6f), 0, origin, scale, SpriteEffects.None, 0);
        batch.Draw(texture, position, frame, Color.White * progress, 0, origin, scale, SpriteEffects.None, 0);
        if (flash > 0f)
            batch.Draw(texture, position, frame, new Color(255, 255, 255, 0) * flash, 0, origin, scale, SpriteEffects.None, 0);
    }

    internal static void DrawBar(SpriteBatch batch, Vector2 position, float fraction, Color color)
    {
        fraction = MathHelper.Clamp(fraction, 0, 1);
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)position.X - 23, (int)position.Y, 46, 7), Color.Black * 0.8f);
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)position.X - 21, (int)position.Y + 2, (int)(42 * fraction), 3), color);
    }

    internal static void Ambient(Vector2 center, int team, float progress = 1f)
    {
        if (Main.dedServ)
            return;
        Color color = Main.teamColor[Math.Clamp(team, 0, 5)];
        Lighting.AddLight(center, color.ToVector3() * 0.25f);
        if (Main.GameUpdateCount % 2 == 0)
            new PotionOfReturnGateHelper(PotionOfReturnGateHelper.GateType.EntryPoint,
                center + new Vector2(0, 23), progress).SpawnReturnPortalDust();
    }

    internal static void Burst(Vector2 center, int team, bool inward = false)
    {
        if (Main.dedServ)
            return;
        Color color = Main.teamColor[Math.Clamp(team, 0, 5)];
        for (int i = 0; i < (inward ? 32 : 56); i++)
        {
            Vector2 direction = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            Vector2 position = center + direction * Main.rand.NextFloat(inward ? 36 : 8, inward ? 68 : 22);
            Vector2 velocity = direction * Main.rand.NextFloat(inward ? -5.5f : 4.2f, inward ? -2f : 8.2f);
            Dust dust = Dust.NewDustPerfect(position, DustID.FireworksRGB, velocity, 80, color, Main.rand.NextFloat(1.15f, 1.9f));
            dust.noGravity = true;
            dust.fadeIn = Main.rand.NextFloat(0.5f, 1.1f);
        }
    }
}
