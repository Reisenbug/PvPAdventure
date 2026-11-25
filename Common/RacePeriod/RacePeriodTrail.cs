using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodTrail : ModPlayer
{
    private const int Lifetime = 12;
    private readonly Vector2[] samples = new Vector2[Lifetime + 1];
    private int next;
    private int count;

    public override void PostUpdate()
    {
        if (!Player.active || !Player.GetModPlayer<RacePeriodPlayer>().IsRacing)
        {
            count = 0;
            return;
        }

        Vector2 position = Player.Bottom - new Vector2(0f, 9f);
        if (count > 0 && Vector2.DistanceSquared(position, GetSample(0)) > 48f * 48f)
            count = 0;

        samples[next] = position;
        next = (next + 1) % samples.Length;
        count = System.Math.Min(count + 1, samples.Length);
    }

    private Vector2 GetSample(int age) => samples[(next + samples.Length - 1 - age) % samples.Length];

    internal void Draw(ref PlayerDrawSet drawInfo)
    {
        int team = Player.team;
        Color color = team > 0 && team < Main.teamColor.Length ? Main.teamColor[team] : new Color(180, 140, 255);
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        float remainingLength = 64f;
        for (int age = 1; age < count && remainingLength > 0f; age++)
        {
            Vector2 from = GetSample(age - 1);
            Vector2 delta = GetSample(age) - from;
            float length = delta.Length();
            if (length < 0.3f)
                continue;

            float fade = 1f - (age - 1f) / Lifetime;
            float intensity = fade * MathHelper.Clamp(length / 8f, 0.25f, 1f);
            float visibleLength = System.Math.Min(length, remainingLength);
            remainingLength -= visibleLength;
            Vector2 position = from - Main.screenPosition;
            float rotation = delta.ToRotation();
            Rectangle texel = new(0, 0, 1, 1);
            Vector2 origin = new(0f, 0.5f);
            drawInfo.DrawDataCache.Add(new DrawData(pixel, position, texel, color * (0.18f * intensity),
                rotation, origin, new Vector2(visibleLength + 1f, 12f * fade), SpriteEffects.None));
            drawInfo.DrawDataCache.Add(new DrawData(pixel, position, texel, color * (0.65f * intensity),
                rotation, origin, new Vector2(visibleLength + 1f, 8.5f * fade), SpriteEffects.None));
        }
    }
}

[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodTrailLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.MountBack);

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
        !drawInfo.headOnlyRender && drawInfo.shadow == 0f &&
        drawInfo.drawPlayer.GetModPlayer<RacePeriodPlayer>().IsRacing;

    protected override void Draw(ref PlayerDrawSet drawInfo) =>
        drawInfo.drawPlayer.GetModPlayer<RacePeriodTrail>().Draw(ref drawInfo);
}
