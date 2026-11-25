using ErkySSC.Common.Visualization;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

#nullable enable

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Supplies race animal portraits to the world map's existing player markers.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodMapHeads : ModSystem
{
    private Asset<Texture2D>? bunnyHead;

    public override void PostSetupContent()
    {
        bunnyHead = Mod.Assets.Request<Texture2D>("Assets/SpiritAnimalMapHeads/bunny", AssetRequestMode.ImmediateLoad);

        ModContent.GetInstance<MapPlayerHeadControlSystem>().MapHeadAppearanceProvider = GetHead;
    }

    public override void Unload()
    {
        MapPlayerHeadControlSystem map = ModContent.GetInstance<MapPlayerHeadControlSystem>();
        if (map?.MapHeadAppearanceProvider == GetHead)
            map.MapHeadAppearanceProvider = null;
        bunnyHead = null;
    }

    private MapHeadAppearance? GetHead(Player player)
    {
        if (player.ghost || player.dead || !player.TryGetModPlayer(out RacePeriodPlayer race) || !race.IsRacing)
            return null;

        if (player.mount.Type != RacePeriodRules.FallbackMountType || bunnyHead is null)
            return null;

        return new MapHeadAppearance(bunnyHead.Value);
    }
}
