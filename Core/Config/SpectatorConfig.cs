using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

internal class SpectatorConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref Terraria.Localization.NetworkText message) =>
        Common.Security.AdminPermissions.AcceptConfig(whoAmI, ref message);

    [Header("Spectate")]
    [BackgroundColor(40, 40, 110)]
    [DefaultValue(true)]
    public bool IsSpectateEnabled;

    [BackgroundColor(40, 40, 110)]
    [DefaultValue(false)]
    public bool ForcePlayersToSpectateWhenJoining;
}
