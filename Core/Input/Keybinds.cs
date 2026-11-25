using Microsoft.Xna.Framework.Input;
using PvPAdventure.Common.Bounties;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Content.Portals;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Input;

[Autoload(Side = ModSide.Client)]
public class Keybinds : ModSystem
{
    public ModKeybind BountyShop { get; private set; }
    public ModKeybind UsePortalCreator { get; private set; }
    public ModKeybind Scoreboard { get; private set; }
    public ModKeybind Dash { get; private set; }

    #region Portal creator label
    public static string UsePortalCreatorLabel
    {
        get
        {
            ModKeybind keybind = ModContent.GetInstance<Keybinds>().UsePortalCreator;

            if (keybind is null)
                return null;

            List<string> keys = keybind.GetAssignedKeys();
            keys.RemoveAll(static key => string.IsNullOrWhiteSpace(key));

            return keys.Count > 0 ? string.Join(" / ", keys) : null;
        }
    }
    #endregion

    public override void Load()
    {
        BountyShop = KeybindLoader.RegisterKeybind(Mod, "BountyShop", Keys.B);
        UsePortalCreator = KeybindLoader.RegisterKeybind(Mod, "UsePortalCreator", Keys.P);
        Scoreboard = KeybindLoader.RegisterKeybind(Mod, "Scoreboard", Keys.Tab);
        Dash = KeybindLoader.RegisterKeybind(Mod, "Dash", Keys.F);
    }
}

internal class KeybindsPlayer : ModPlayer
{
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        var keybinds = ModContent.GetInstance<Keybinds>();

        // Bounty Shop
        if (keybinds.BountyShop.JustPressed)
        {
            var bountyShop = ModContent.GetInstance<BountyManager>().UiBountyShop;

            if (Main.InGameUI.CurrentState == bountyShop)
                Main.InGameUI.SetState(null);
            else
                Main.InGameUI.SetState(bountyShop);
        }

        // UsePortalCreator keybind
        if (keybinds.UsePortalCreator.JustPressed)
        {
            //Log.Chat("Portal creator item keybind pressed");
            PortalCreatorItem.TryUse(Player);
        }
    }
}
