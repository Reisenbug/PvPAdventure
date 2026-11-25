using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Visualization;
using Terraria;
using Terraria.DataStructures;
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
        Texture2D bloom = ModContent.GetInstance<SoftGlowTextures>().Bloom;
        Color core = Color.Lerp(color, Color.White, 0.45f);
        Vector2 origin = new(SoftGlowTextures.Size / 2f);
        const float maxLength = 64f;
        const float spacing = 3f;
        float traveled = 0f;
        float nextStamp = 0f;
        for (int age = 1; age < count && nextStamp <= maxLength; age++)
        {
            Vector2 from = GetSample(age - 1);
            Vector2 to = GetSample(age);
            float length = Vector2.Distance(from, to);
            if (length < 0.3f)
                continue;

            while (nextStamp <= traveled + length && nextStamp <= maxLength)
            {
                float t = (nextStamp - traveled) / length;
                float fade = (1f - (age - 1f + t) / Lifetime) * (1f - nextStamp / maxLength);
                float opacity = fade * MathHelper.Clamp(length / 8f, 0.25f, 1f);
                Vector2 position = Vector2.CatmullRom(GetSample(System.Math.Max(0, age - 2)), from, to,
                    GetSample(System.Math.Min(count - 1, age + 1)), t) - Main.screenPosition;
                drawInfo.DrawDataCache.Add(new DrawData(bloom, position, null, color * (0.5f * opacity),
                    0f, origin, MathHelper.Lerp(0.12f, 0.58f, fade), SpriteEffects.None));
                drawInfo.DrawDataCache.Add(new DrawData(bloom, position, null, core * (0.65f * opacity),
                    0f, origin, MathHelper.Lerp(0.07f, 0.28f, fade), SpriteEffects.None));
                nextStamp += spacing;
            }
            traveled += length;
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
