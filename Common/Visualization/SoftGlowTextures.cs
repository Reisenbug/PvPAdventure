using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Visualization;

[Autoload(Side = ModSide.Client)]
internal sealed class SoftGlowTextures : ModSystem
{
    internal const int Size = 32;
    private Texture2D band;
    private Texture2D corner;

    internal Texture2D Band => band ??= Create(1, (_, y) => BorderOpacity(y));
    internal Texture2D Corner => corner ??= Create(Size, (x, y) => BorderOpacity(Math.Min(x, y)));

    private static float BorderOpacity(int distance)
    {
        float fade = 1f - distance / (Size - 1f);
        return fade * fade;
    }

    private static Texture2D Create(int width, Func<int, int, float> opacity)
    {
        var texture = new Texture2D(Main.graphics.GraphicsDevice, width, Size);
        var pixels = new Color[width * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < width; x++)
            pixels[y * width + x] = Color.White * opacity(x, y);
        texture.SetData(pixels);
        return texture;
    }

    public override void Unload()
    {
        Texture2D oldBand = band;
        Texture2D oldCorner = corner;
        band = corner = null;
        if (oldBand != null || oldCorner != null)
            Main.QueueMainThreadAction(() =>
            {
                oldBand?.Dispose();
                oldCorner?.Dispose();
            });
    }
}
