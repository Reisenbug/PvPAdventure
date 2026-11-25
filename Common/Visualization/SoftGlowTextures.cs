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
    private Texture2D bloom;

    internal Texture2D Band => band ??= Create(radial: false);
    internal Texture2D Bloom => bloom ??= Create(radial: true);

    private static Texture2D Create(bool radial)
    {
        var texture = new Texture2D(Main.graphics.GraphicsDevice, radial ? Size : 1, Size);
        var pixels = new Color[texture.Width * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < texture.Width; x++)
        {
            float dy = (y + 0.5f) * 2f / Size - 1f;
            float dx = radial ? (x + 0.5f) * 2f / Size - 1f : 0f;
            float fade = Math.Max(0f, 1f - MathF.Sqrt(dx * dx + dy * dy));
            pixels[y * texture.Width + x] = Color.White * (fade * fade);
        }
        texture.SetData(pixels);
        return texture;
    }

    public override void Unload()
    {
        Texture2D oldBand = band;
        Texture2D oldBloom = bloom;
        band = bloom = null;
        if (oldBand != null || oldBloom != null)
            Main.QueueMainThreadAction(() =>
            {
                oldBand?.Dispose();
                oldBloom?.Dispose();
            });
    }
}
