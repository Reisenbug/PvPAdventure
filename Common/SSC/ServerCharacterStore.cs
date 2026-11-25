using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader.IO;
using Terraria.Net;

namespace PvPAdventure.Common.SSC;

internal sealed class ServerCharacterStore
{
    private readonly string path;
    private readonly string legacyPath;

    private ServerCharacterStore(string path, string legacyPath)
    {
        this.path = path;
        this.legacyPath = legacyPath;
    }

    internal static ServerCharacterStore Open(Player player)
    {
        if (Main.netMode != NetmodeID.Server || player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers ||
            Netplay.Clients[player.whoAmI]?.Socket?.GetRemoteAddress() is not RemoteAddress address)
            return null;

        string character = player.name;
        string world = Main.ActiveWorldFileData?.Name;
        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(world))
            return null;

        string root = Path.Combine(Main.SavePath, "PvPAdventureSSC");
        string worldKey = Key($"{Main.worldID}:{world}");
        string account = address switch
        {
            SteamAddress steam => steam.SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture),
            TcpAddress tcp => $"ip-{Key(tcp.Address.MapToIPv6().ToString())}",
            _ => null
        };
        if (account == null)
            return null;

        string legacy = address is SteamAddress && SafeSegment(world) && SafeSegment(character)
            ? Path.Combine(root, world, account, character)
            : null;
        return new ServerCharacterStore(
            Path.Combine(root, "v2", worldKey, account, Key(character.ToUpperInvariant()) + ".ssc"), legacy);
    }

    internal bool TryLoad(out byte[] vanilla, out TagCompound modded)
    {
        if (File.Exists(path))
        {
            TagCompound data = TagIO.FromFile(path);
            vanilla = data.GetByteArray("vanilla");
            modded = data.GetCompound("modded");
            if (vanilla.Length == 0)
                throw new InvalidDataException("The server character file is missing its vanilla player data.");
            return true;
        }

        if (legacyPath != null && File.Exists(legacyPath + ".plr") && File.Exists(legacyPath + ".tplr"))
        {
            vanilla = File.ReadAllBytes(legacyPath + ".plr");
            modded = TagIO.FromFile(legacyPath + ".tplr");
            return true;
        }

        vanilla = null;
        modded = null;
        return false;
    }

    internal void Save(PlayerFileData playerFile, TagCompound modded)
    {
        var data = new TagCompound
        {
            ["vanilla"] = Player.SavePlayerFile_Vanilla(playerFile),
            ["modded"] = modded
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            TagIO.ToFile(data, temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static bool SafeSegment(string value) =>
        value is not ("." or "..") && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.Contains('\\') && !value.Contains(':');

    private static string Key(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
