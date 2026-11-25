using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

internal sealed class MatchPauseSystem : ModSystem
{
    public override void Load()
    {
        On_Main.CanPauseGame += CanPauseGame;
        On_Player.DropSelectedItem_int_refItem += DropSelectedItem;
    }

    public override void Unload()
    {
        On_Main.CanPauseGame -= CanPauseGame;
        On_Player.DropSelectedItem_int_refItem -= DropSelectedItem;
    }

    private static bool MatchPaused => ModContent.GetInstance<GameManager>().IsPaused;

    private static bool CanPauseGame(On_Main.orig_CanPauseGame original) => MatchPaused || original();

    private static void DropSelectedItem(On_Player.orig_DropSelectedItem_int_refItem original, Player player, int slot,
        ref Item item)
    {
        if (!MatchPaused)
            original(player, slot, ref item);
    }
}
