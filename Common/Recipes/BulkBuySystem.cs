using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Recipes;

// VANILLA145_BACKPORT: Shift buys up to ten items per shop interaction.
public sealed class BulkBuySystem : ModSystem
{
    public override void Load() => IL_ItemSlot.HandleShopSlot += HandleShopSlot;

    public override void Unload() => IL_ItemSlot.HandleShopSlot -= HandleShopSlot;

    private static void HandleShopSlot(ILContext il)
    {
        var cursor = new ILCursor(il);
        cursor.GotoNext(MoveType.After, i => i.MatchLdsfld<Main>(nameof(Main.superFastStack)),
            i => i.MatchLdcI4(1), i => i.MatchAdd());
        cursor.EmitDelegate<System.Func<int, int>>(count => ItemSlot.ShiftInUse ? 10 : count);
    }
}
