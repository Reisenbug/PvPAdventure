using PvPAdventure.Common.Arenas;
using PvPAdventure.Core.Config.ConfigElements.LoadoutItems;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

internal class ArenasConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("Arenas")]
    [BackgroundColor(90, 40, 110)]
    [DefaultValue(true)]
    public bool IsArenasEnabled { get; set; } = true;

    [BackgroundColor(90, 40, 110)]
    [Slider]
    [Increment(20)]
    [Range(100, 500)]
    [DefaultValue(500)]
    public int MaxHealth { get; set; } = 500;

    [BackgroundColor(90, 40, 110)]
    [Slider]
    [Increment(20)]
    [Range(20, 200)]
    [DefaultValue(200)]
    public int MaxMana { get; set; } = 200;

    [BackgroundColor(90, 40, 110)]
    [DefaultValue(false)]
    public bool RevealMap { get; set; } = false;

    [Header("Loadouts")]
    [BackgroundColor(90, 40, 110)]
    [CustomModConfigItem(typeof(LoadoutListElement))]
    public List<Loadout> ArenaLoadouts { get; set; } = [];

    #region Hooks / methods
    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        return Common.Security.AdminPermissions.AcceptConfig(whoAmI, ref message);
    }

    public override void HandleAcceptClientChangesReply(bool success, int player, NetworkText message)
    {
        Log.Chat("Server accepted changes!");
        base.HandleAcceptClientChangesReply(success, player, message);
    }
    public override void OnLoaded()
    {
        base.OnLoaded();
    }
    public override void OnChanged()
    {
        base.OnChanged();
    }
    #endregion
}
