using System.Reflection;
using DragonLens.Core.Systems;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.AdminTools.DragonLens;

[JITWhenModsEnabled("DragonLens")]
[ExtendsFromMod("DragonLens")]
public sealed class DLPermissionBypass : ModSystem
{
    private delegate bool orig_CanUseTools(Player player);
    private delegate void orig_OnEnterWorld(PermissionPlayer self);

    public override void PostSetupContent()
    {
        MethodInfo canUseTools = typeof(PermissionHandler)
            .GetMethod("CanUseTools", BindingFlags.Public | BindingFlags.Static);

        if (canUseTools != null)
            MonoModHooks.Add(canUseTools, (orig_CanUseTools orig, Player player) => true);

        MethodInfo onEnterWorld = typeof(PermissionPlayer)
            .GetMethod("OnEnterWorld", BindingFlags.Public | BindingFlags.Instance);

        if (onEnterWorld != null)
            MonoModHooks.Add(onEnterWorld, (orig_OnEnterWorld orig, PermissionPlayer self) => { });
    }
}
