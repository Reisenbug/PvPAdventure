using PvPAdventure.Core.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Movement;

[Autoload(Side = ModSide.Client)]
internal sealed class DashKeySystem : ModSystem
{
    public override void Load() => On_Player.DoCommonDashHandle += HandleDash;

    public override void Unload() => On_Player.DoCommonDashHandle -= HandleDash;

    private static void HandleDash(On_Player.orig_DoCommonDashHandle original, Player player,
        out int direction, out bool dashing, Player.DashStartAction start)
    {
        var bind = ModContent.GetInstance<Keybinds>().Dash;
        InputMode mode = PlayerInput.UsingGamepad ? InputMode.XBoxGamepad : InputMode.Keyboard;
        if (player.whoAmI != Main.myPlayer || bind == null || bind.GetAssignedKeys(mode).Count == 0)
        {
            original(player, out direction, out dashing, start);
            return;
        }

        player.dashTime = 0;
        direction = 0;
        dashing = bind.JustPressed;
        if (!dashing)
            return;

        if (player.controlLeft != player.controlRight)
            direction = player.controlLeft ? -1 : 1;
        else if (player.velocity.X != 0f)
            direction = player.velocity.X < 0f ? -1 : 1;
        else
            direction = player.direction < 0 ? -1 : 1;

        player.timeSinceLastDashStarted = 0;
        start?.Invoke(direction);
    }
}
