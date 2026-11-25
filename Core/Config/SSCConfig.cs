using System.Collections.Generic;
using System.ComponentModel;
using PvPAdventure.Common.Security;
using Terraria.Localization;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

public sealed class SSCConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    public List<StartingItem> StartItems { get; set; } = [];

    public sealed class StartingItem
    {
        public ItemDefinition Item { get; set; } = new();

        [DefaultValue(1)]
        [Range(1, 9999)]
        public int Stack { get; set; } = 1;
    }

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message) =>
        AdminPermissions.AcceptConfig(whoAmI, ref message);
}
