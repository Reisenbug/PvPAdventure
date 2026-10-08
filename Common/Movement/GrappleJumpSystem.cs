using System;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Movement;

// VANILLA145_BACKPORT: 1.4.5 grapple jump eligibility and full jump duration.
public sealed class GrappleJumpSystem : ModSystem
{
    public override void Load() => IL_Player.GrappleMovement += FullGrappleJump;

    public override void Unload() => IL_Player.GrappleMovement -= FullGrappleJump;

    private static void FullGrappleJump(ILContext il)
    {
        var incomingVelocity = new Mono.Cecil.Cil.VariableDefinition(il.Import(typeof(Vector2)));
        var canJump = new Mono.Cecil.Cil.VariableDefinition(il.Import(typeof(bool)));
        il.Body.Variables.Add(incomingVelocity);
        il.Body.Variables.Add(canJump);
        ILCursor cursor = new(il);
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldfld, typeof(Entity).GetField(nameof(Entity.velocity)));
        cursor.Emit(OpCodes.Stloc, incomingVelocity);

        if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall<Player>("GetGrapplingForces")) ||
            !cursor.Prev.MatchLdloca(out int pullVelocityY))
            throw new InvalidOperationException("VANILLA145_BACKPORT: grapple forces IL pattern not found");
        cursor.Index++;
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Ldloc, incomingVelocity);
        cursor.Emit(OpCodes.Ldloc, il.Body.Variables[pullVelocityY]);
        cursor.EmitDelegate<Func<Player, Vector2, float, bool>>(CanJumpOffHook);
        cursor.Emit(OpCodes.Stloc, canJump);

        if (!cursor.TryGotoNext(MoveType.After,
            i => i.MatchLdarg0(),
            i => i.MatchLdfld<Player>(nameof(Player.releaseJump)),
            i => i.MatchBrfalse(out _)))
            throw new InvalidOperationException("VANILLA145_BACKPORT: grapple jump gate IL pattern not found");
        int conditionStart = cursor.Index;
        ILLabel detach = null;
        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchLdarg0(),
            i => i.MatchLdfld<Player>(nameof(Player.controlDown)),
            i => i.MatchBrtrue(out detach)))
            throw new InvalidOperationException("VANILLA145_BACKPORT: grapple down gate IL pattern not found");
        int conditionEnd = cursor.Index + 3;
        cursor.Index = conditionStart;
        cursor.RemoveRange(conditionEnd - conditionStart);
        cursor.Emit(OpCodes.Ldloc, canJump);
        cursor.Emit(OpCodes.Brfalse, detach);

        if (!cursor.TryGotoNext(MoveType.Before,
            i => i.MatchLdsfld<Player>(nameof(Player.jumpHeight)),
            i => i.MatchLdcI4(2),
            i => i.MatchDiv(),
            i => i.MatchStfld<Player>(nameof(Player.jump))))
            throw new InvalidOperationException("VANILLA145_BACKPORT: grapple jump IL pattern not found");

        cursor.Index++;
        cursor.RemoveRange(2);
    }

    private static bool CanJumpOffHook(Player player, Vector2 incomingVelocity, float pullVelocityY)
    {
        bool nearlyStill = incomingVelocity.Length() < 2f;
        bool floating = player.wet && (double)incomingVelocity.Y > -0.02 && (double)incomingVelocity.Y < 0.02;
        bool pulledDownWhileStanding = pullVelocityY > 0f && incomingVelocity.Y == 0f;
        return (nearlyStill || floating) && !player.controlDown &&
            (!pulledDownWhileStanding || player.controlUp);
    }
}
