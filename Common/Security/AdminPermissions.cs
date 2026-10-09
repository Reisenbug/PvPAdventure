using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Security;

public sealed class AdminPermissions : ModSystem
{
    private static readonly HashSet<int> admins = [];
    private static readonly Dictionary<int, DateTime> loginAfter = [];
    private static byte[] passwordHash;
    public static bool LocalAdmin => HasAccess(Main.myPlayer);
    public static bool HasAccess(int slot) => Main.netMode == NetmodeID.SinglePlayer ||
        slot >= 0 && slot < Main.maxPlayers && Main.player[slot].active && admins.Contains(slot);

    public static bool RequireLocal()
    {
        if (LocalAdmin)
            return true;
        Main.NewText("Administrator permission required. Use /pvpadmin <password> to log in.", Color.OrangeRed);
        return false;
    }

    public static bool AcceptConfig(int sender, ref NetworkText message)
    {
        bool allowed = HasAccess(sender);
        message = NetworkText.FromLiteral(allowed ? "Saved!" : "Only server administrators may change server settings.");
        return allowed;
    }

    public override void OnWorldLoad()
    {
        admins.Clear();
        loginAfter.Clear();
        passwordHash = null;
        if (Main.netMode != NetmodeID.Server)
            return;
        string folder = Path.Combine(Main.SavePath, "PvPAdventure");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "AdminPassword.txt");
        if (!File.Exists(path))
            File.WriteAllText(path, Convert.ToHexString(RandomNumberGenerator.GetBytes(12)) + Environment.NewLine);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(path).Trim()));
    }

    internal static void Grant(int slot, bool allowed)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        if (allowed)
            admins.Add(slot);
        else
        {
            admins.Remove(slot);
            Common.Spectator.SpectatorSystem.SetModeServer(slot, Common.Spectator.PlayerMode.Player);
        }
        Send(-1);
    }

    private static void Send(int target)
    {
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.AdminPermissions);
        packet.Write((byte)2);
        packet.Write((byte)admins.Count);
        foreach (int slot in admins)
            packet.Write(slot);
        packet.Send(target);
    }

    internal static void Handle(BinaryReader reader, int sender)
    {
        byte action = reader.ReadByte();
        if (Main.netMode == NetmodeID.Server)
        {
            string password = action == 1 ? reader.ReadString() : null;
            if (action == 2)
            {
                int claimedCount = reader.ReadByte();
                for (int i = 0; i < claimedCount; i++)
                    reader.ReadInt32();
            }
            if (sender < 0 || sender >= Main.maxPlayers || !Main.player[sender].active)
                return;
            if (action == 0 || action == 2)
                Send(sender);
            else if (action == 1 && password.Length <= 64)
            {
                DateTime now = DateTime.UtcNow;
                if (loginAfter.TryGetValue(sender, out DateTime next) && now < next)
                    return;
                loginAfter[sender] = now.AddSeconds(3);
                bool accepted = Authenticate(password);
                if (accepted)
                    Grant(sender, true);
                ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(accepted ?
                    "Administrator login successful." : "Invalid administrator password."), Color.Yellow, sender);
            }
            return;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient || action != 2)
            return;
        admins.Clear();
        int count = reader.ReadByte();
        for (int i = 0; i < count; i++)
        {
            int slot = reader.ReadInt32();
            if (slot >= 0 && slot < Main.maxPlayers)
                admins.Add(slot);
        }
    }

    private static bool Authenticate(string password) => passwordHash != null && password.Length > 0 &&
        CryptographicOperations.FixedTimeEquals(passwordHash, SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public sealed class AdminCommand : ModCommand
    {
        public override string Command => "pvpadmin";
        public override CommandType Type => CommandType.Console | CommandType.Chat;
        public override string Usage => "/pvpadmin <password>; console: pvpadmin <player slot> [revoke]";
        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (caller.CommandType == CommandType.Chat)
            {
                if (Main.netMode == NetmodeID.SinglePlayer)
                {
                    caller.Reply("Single-player already has administrator permission.");
                    return;
                }
                if (args.Length != 1 || args[0].Length > 64)
                {
                    caller.Reply("Usage: /pvpadmin <password>");
                    return;
                }
                ModPacket login = ModContent.GetInstance<PvPAdventure>().GetPacket();
                login.Write((byte)AdventurePacketIdentifier.AdminPermissions);
                login.Write((byte)1);
                login.Write(args[0]);
                login.Send();
                return;
            }
            if (args.Length == 0)
            {
                foreach (Player player in Main.ActivePlayers)
                    caller.Reply($"{player.whoAmI}: {player.name} (admin={HasAccess(player.whoAmI)})");
                return;
            }
            if (!int.TryParse(args[0], out int slot) || slot < 0 || slot >= Main.maxPlayers || !Main.player[slot].active)
            {
                caller.Reply("Use pvpadmin to list active player slots.");
                return;
            }
            bool allowed = args.Length < 2 || args[1] != "revoke";
            Grant(slot, allowed);
            caller.Reply($"Admin permission for {Main.player[slot].name}: {allowed}");
        }
    }

    public sealed class AdminPlayer : ModPlayer
    {
        public override void OnEnterWorld()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.AdminPermissions);
            packet.Write((byte)0);
            packet.Send();
        }

        public override void PlayerDisconnect()
        {
            admins.Remove(Player.whoAmI);
            loginAfter.Remove(Player.whoAmI);
            if (Main.netMode == NetmodeID.Server)
                Send(-1);
        }

        public override void PreUpdate()
        {
            if (!Player.dead && !HasAccess(Player.whoAmI) &&
                !Common.Spectator.SpectatorBridge.IsInSpectateMode(Player))
                Player.ghost = false;
        }
    }
}
