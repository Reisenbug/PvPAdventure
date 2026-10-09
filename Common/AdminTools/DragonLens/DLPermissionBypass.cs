using System.Reflection;
using System.IO;
using DragonLens.Core.Systems;
using DragonLens.Core.Systems.ToolSystem;
using PvPAdventure.Common.Security;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.AdminTools.DragonLens;

[JITWhenModsEnabled("DragonLens")]
[ExtendsFromMod("DragonLens")]
public sealed class DLPermissionBypass : ModSystem
{
    private delegate bool orig_CanUseTools(Player player);
    private delegate void orig_HandlePacket(BinaryReader reader, int sender);
    private delegate void orig_ProcessTriggers(ToolPlayer self, TriggersSet triggers);

    public override void PostSetupContent()
    {
        MethodInfo canUseTools = typeof(PermissionHandler)
            .GetMethod("CanUseTools", BindingFlags.Public | BindingFlags.Static);

        if (canUseTools != null)
            MonoModHooks.Add(canUseTools, (orig_CanUseTools orig, Player player) => AdminPermissions.HasAccess(player.whoAmI));

        MonoModHooks.Add(typeof(ToolHandler).GetMethod("HandlePacket"),
            (orig_HandlePacket orig, BinaryReader reader, int sender) =>
            {
                if (Main.netMode == NetmodeID.Server && !AdminPermissions.HasAccess(sender))
                    reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                else
                    orig(reader, sender);
            });

        MonoModHooks.Add(typeof(PermissionHandler).GetMethod("HandlePacket"),
            (orig_HandlePacket orig, BinaryReader reader, int sender) =>
            {
                long position = reader.BaseStream.Position;
                int operation = reader.ReadInt32();
                reader.BaseStream.Position = position;
                if (Main.netMode == NetmodeID.Server && (operation == 0 || operation == 1))
                    reader.ReadBytes((int)(reader.BaseStream.Length - position));
                else
                    orig(reader, sender);
            });

        MonoModHooks.Add(typeof(ToolPlayer).GetMethod("ProcessTriggers"),
            (orig_ProcessTriggers orig, ToolPlayer self, TriggersSet triggers) =>
            {
                if (AdminPermissions.HasAccess(self.Player.whoAmI))
                    orig(self, triggers);
            });
    }
}

[JITWhenModsEnabled("DragonLens")]
[ExtendsFromMod("DragonLens")]
public sealed class DLPermissionPlayer : ModPlayer
{
    private bool hadAccess;

    public override void PreUpdate()
    {
        if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
            return;
        bool access = AdminPermissions.LocalAdmin;
        if (hadAccess && !access)
            PermissionHandler.ResetToolsForNonAdmin(Player);
        hadAccess = access;
    }
}
