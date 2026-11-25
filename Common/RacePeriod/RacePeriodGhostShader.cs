using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodGhostShader : ModSystem
{
    internal const int BodyShader = int.MinValue;
    private static readonly Vector2[] RimOffsets = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];
    private static readonly Dictionary<Texture2D, Texture2D> Masks = new();

    private delegate void SetShaderForData(Player player, int headShader, ref DrawData data);
    private delegate void ShaderHook(SetShaderForData original, Player player, int headShader, ref DrawData data);

    private ArmorShaderData shader;
    private EffectParameter wave;
    private Hook hook;

    public override void Load()
    {
        Asset<Effect> effect = Mod.Assets.Request<Effect>("Assets/Effects/SpiritAnimalGhost", AssetRequestMode.ImmediateLoad);
        shader = new ArmorShaderData(effect, "GhostPass").UseOpacity(RacePeriodPulse.BrightBodyOpacity);
        effect.Value.Parameters["uDimOpacity"].SetValue(RacePeriodPulse.DimBodyOpacity);
        wave = effect.Value.Parameters["uWave"];
        var method = typeof(PlayerDrawHelper).GetMethod(nameof(PlayerDrawHelper.SetShaderForData),
            [typeof(Player), typeof(int), typeof(DrawData).MakeByRefType()]);
        hook = new Hook(method, (ShaderHook)Apply);
    }

    private void Apply(SetShaderForData original, Player player, int headShader, ref DrawData data)
    {
        if (data.shader == BodyShader)
        {
            wave.SetValue(RacePeriodPulse.GetWave(player));
            Color team = player.team > 0 && player.team < Main.teamColor.Length
                ? Main.teamColor[player.team] : Color.White;
            shader.UseColor(team).Apply(player, data);
        }
        else
            original(player, headShader, ref data);
    }

    internal static void AddGlow(ref PlayerDrawSet drawInfo, int start, int end)
    {
        Player player = drawInfo.drawPlayer;
        float pulse = RacePeriodPulse.GetWave(player);
        Color team = player.team > 0 && player.team < Main.teamColor.Length ? Main.teamColor[player.team] : Color.White;
        Color tint = Color.Lerp(team, Color.White, 0.35f) * (0.18f * RacePeriodPulse.GetOutlineOpacity(pulse));
        var draws = drawInfo.DrawDataCache;

        for (int i = end - 1; i >= start; i--)
        {
            DrawData rim = draws[i];
            if (rim.texture is not { IsDisposed: false, Format: SurfaceFormat.Color } texture || rim.useDestinationRectangle)
                continue;

            rim.texture = GetMask(texture);
            rim.color = tint * (rim.color.A / 255f);
            rim.shader = 0;
            Vector2 position = rim.position;
            foreach (Vector2 offset in RimOffsets)
            {
                rim.position = position + offset;
                draws.Insert(start, rim);
            }
            i += RimOffsets.Length;
            start += RimOffsets.Length;
        }
    }

    private static Texture2D GetMask(Texture2D source)
    {
        if (Masks.TryGetValue(source, out Texture2D mask))
            return mask;

        Color[] pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.White * (pixels[i].A / 255f);
        mask = new Texture2D(source.GraphicsDevice, source.Width, source.Height);
        mask.SetData(pixels);
        Masks.Add(source, mask);
        return mask;
    }

    public override void Unload()
    {
        hook?.Dispose();
        hook = null;
        shader = null;
        wave = null;
        Main.QueueMainThreadAction(() =>
        {
            foreach (Texture2D mask in Masks.Values)
                mask.Dispose();
            Masks.Clear();
            RacePeriodAnimalTextures.Dispose();
        });
    }
}
