using System;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Movement;

// VANILLA145_BACKPORT: full jump duration when jumping off an attached hook.
public sealed class GrappleJumpSystem : ModSystem
{
    public override void Load() => IL_Player.GrappleMovement += FullGrappleJump;

    public override void Unload() => IL_Player.GrappleMovement -= FullGrappleJump;

    private static void FullGrappleJump(ILContext il)
    {
        ILCursor cursor = new(il);
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchLdsfld<Player>(nameof(Player.jumpHeight)),
            i => i.MatchLdcI4(2),
            i => i.MatchDiv(),
            i => i.MatchStfld<Player>(nameof(Player.jump))))
            throw new InvalidOperationException("VANILLA145_BACKPORT: grapple jump IL pattern not found");

        cursor.Index++;
        cursor.RemoveRange(2);
    }
}
