using DragonLens.Core.Systems;
using PvPAdventure.Common.Arenas;
using PvPAdventure.Core.Config.ConfigElements.LoadoutItems;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

internal class SSCConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    #region Members
    [Header("SSC")]
    [BackgroundColor(90, 70, 160)]
    [DefaultValue(true)]
    public bool IsSSCEnabled { get; set; } = true;

    [BackgroundColor(90, 70, 160)]
    [DefaultValue(SSCPlayerNameType.Default)]
    public SSCPlayerNameType SSCPlayerNames { get; set; } = SSCPlayerNameType.Default;

    [BackgroundColor(90, 70, 160)]
    [Slider]
    [Increment(20)]
    [Range(100, 500)]
    [DefaultValue(100)]
    public int StartLife { get; set; } = 200;

    [BackgroundColor(90, 70, 160)]
    [Slider]
    [Increment(20)]
    [Range(20, 200)]
    [DefaultValue(40)]
    public int StartMana { get; set; } = 40;

    [Header("StartingItems")]
    [BackgroundColor(90, 70, 160)]
    [CustomModConfigItem(typeof(LoadoutItemListElement))]
    public List<LoadoutItem> StartItems { get; set; } = [];
    #endregion

    #region Data types
    public enum SSCPlayerNameType
    {
        Default,
        Steam,
        Discord,
        Numbered
    }
    #endregion

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
