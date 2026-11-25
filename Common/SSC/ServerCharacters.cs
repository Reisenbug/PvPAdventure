using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.SSC;

internal sealed class ServerCharacters : ModSystem
{
    private const int ChunkSize = 48 * 1024;
    private const int MaxTransferBytes = 4 * 1024 * 1024;
    private readonly Dictionary<int, CharacterSession> sessions = [];
    private PlayerFileData originalFile;
    private PlayerFileData serverFile;
    private MemoryStream transfer;

    private sealed record CharacterSession(ServerCharacterStore Store, PlayerFileData File);
    private static ServerCharacters Instance => ModContent.GetInstance<ServerCharacters>();

    public override void Load() => On_Player.InternalSavePlayerFile += SaveLocalFile;

    public override void Unload() => On_Player.InternalSavePlayerFile -= SaveLocalFile;

    private void SaveLocalFile(On_Player.orig_InternalSavePlayerFile original, PlayerFileData file)
    {
        if (!ReferenceEquals(file, serverFile))
            original(file);
    }

    public override bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)
    {
        if (Main.netMode != NetmodeID.Server || messageType != MessageID.SpawnTileData ||
            playerNumber < 0 || playerNumber >= Main.maxPlayers || Netplay.Clients[playerNumber].State != 2)
            return false;

        if (sessions.TryGetValue(playerNumber, out CharacterSession previous))
        {
            if (ReferenceEquals(previous.File.Player, Main.player[playerNumber]))
                return false;
            sessions.Remove(playerNumber);
        }

        SSCConfig config = ModContent.GetInstance<SSCConfig>();
        if (!config.IsSSCEnabled)
            return false;

        try
        {
            LoadForJoin(playerNumber, config);
            return false;
        }
        catch (Exception ex)
        {
            Mod.Logger.Error($"Could not load server character in slot {playerNumber}.", ex);
            NetMessage.SendData(MessageID.Kick, playerNumber, text: NetworkText.FromLiteral("Could not load server character."));
            return true;
        }
    }

    private void LoadForJoin(int slot, SSCConfig config)
    {
        Player joining = Main.player[slot];
        ServerCharacterStore store = ServerCharacterStore.Open(joining) ??
            throw new InvalidOperationException("The player connection did not provide a character identity.");
        PlayerFileData file;
        TagCompound modded;
        byte[] vanilla;
        if (store.TryLoad(out vanilla, out modded))
            file = Decode(vanilla, Encode(modded));
        else
        {
            Player fresh = new()
            {
                name = joining.name,
                hair = joining.hair,
                skinVariant = joining.skinVariant,
                hairColor = joining.hairColor,
                eyeColor = joining.eyeColor,
                skinColor = joining.skinColor,
                shirtColor = joining.shirtColor,
                underShirtColor = joining.underShirtColor,
                pantsColor = joining.pantsColor,
                shoeColor = joining.shoeColor
            };
            StartingItems.Apply(fresh);
            int life = Math.Clamp(config.StartLife, 100, 500) - 100;
            fresh.ConsumedLifeCrystals = Math.Min(life / 20, Player.LifeCrystalMax);
            fresh.ConsumedLifeFruit = (life - fresh.ConsumedLifeCrystals * 20) / 5;
            fresh.ConsumedManaCrystals = (Math.Clamp(config.StartMana, 20, 200) - 20) / 20;
            fresh.statLife = fresh.statLifeMax;
            fresh.statMana = fresh.statManaMax;
            file = NewFile();
            file.Player = fresh;
            vanilla = Player.SavePlayerFile_Vanilla(file);
            modded = PlayerIO.SaveData(fresh);
        }

        int team = modded.GetCompound("PvPAdventureSSC").GetInt("team");
        if (team is < 1 or > 5)
            team = Random.Shared.Next(1, 6);
        byte[] payload = Encode(new TagCompound { ["vanilla"] = vanilla, ["modded"] = modded, ["team"] = team });
        if (payload.Length > MaxTransferBytes)
            throw new InvalidDataException("The server character is too large to send.");

        Player player = file.Player;
        player.name = joining.name;
        player.whoAmI = slot;
        player.active = joining.active;
        player.team = team;
        player.respawnTimer = 0;
        Main.player[slot] = player;
        sessions.Add(slot, new CharacterSession(store, file));
        Send(slot, payload);
    }

    private void Send(int slot, byte[] bytes)
    {
        for (int offset = 0; offset < bytes.Length; offset += ChunkSize)
        {
            int length = Math.Min(ChunkSize, bytes.Length - offset);
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.ServerCharacter);
            packet.Write(bytes.Length);
            packet.Write(offset);
            packet.Write((ushort)length);
            packet.Write(bytes, offset, length);
            packet.Send(slot);
        }
    }

    internal static void Receive(BinaryReader reader)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            Instance.ReceiveChunk(reader);
    }

    private void ReceiveChunk(BinaryReader reader)
    {
        int total = reader.ReadInt32();
        int offset = reader.ReadInt32();
        int length = reader.ReadUInt16();
        if (total is <= 0 or > MaxTransferBytes || length is <= 0 or > ChunkSize ||
            offset < 0 || offset > total - length)
            return;

        if (offset == 0)
        {
            transfer?.Dispose();
            transfer = new MemoryStream(total);
        }
        if (transfer == null || transfer.Capacity != total || transfer.Length != offset)
            return;

        byte[] chunk = reader.ReadBytes(length);
        if (chunk.Length != length)
            return;
        transfer.Write(chunk);
        if (transfer.Length != total)
            return;

        try
        {
            transfer.Position = 0;
            TagCompound payload = TagIO.FromStream(transfer);
            PlayerFileData loaded = Decode(payload.GetByteArray("vanilla"), Encode(payload.GetCompound("modded")));
            Player joining = Main.LocalPlayer;
            loaded.Player.name = joining.name;
            loaded.Player.whoAmI = Main.myPlayer;
            loaded.Player.active = joining.active;
            loaded.Player.team = payload.GetInt("team");
            loaded.MarkAsServerSide();
            originalFile ??= Main.ActivePlayerFileData;
            serverFile = loaded;
            loaded.SetAsActive();
        }
        catch (Exception ex)
        {
            Mod.Logger.Error("Could not apply the server character.", ex);
            Main.menuMode = 15;
            Main.statusText = "Could not apply the server character.";
            Netplay.Disconnect = true;
        }
        finally
        {
            transfer.Dispose();
            transfer = null;
        }
    }

    public override void PostUpdateEverything()
    {
        if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % (60 * 60) == 0)
            foreach ((int slot, CharacterSession session) in sessions)
                if (Netplay.Clients[slot].State == 10 && session.File.Player.active)
                    Save(session);
    }

    public override void PreSaveAndQuit()
    {
        if (Main.netMode == NetmodeID.Server)
            foreach (CharacterSession session in sessions.Values)
                Save(session);
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (Main.netMode == NetmodeID.Server)
            foreach ((int slot, CharacterSession session) in sessions)
                if (Netplay.Clients[slot].State == 10 && session.File.Player.active)
                    Save(session);
    }

    internal void Disconnect(Player player)
    {
        if (Main.netMode == NetmodeID.Server && sessions.TryGetValue(player.whoAmI, out CharacterSession session) &&
            ReferenceEquals(session.File.Player, player))
        {
            sessions.Remove(player.whoAmI);
            Save(session);
        }
    }

    private void Save(CharacterSession session)
    {
        try
        {
            Player player = session.File.Player;
            TagCompound modded = PlayerIO.SaveData(player);
            modded["PvPAdventureSSC"] = new TagCompound { ["team"] = player.team };
            session.Store.Save(session.File, modded);
        }
        catch (Exception ex)
        {
            Mod.Logger.Error($"Could not save server character in slot {session.File.Player.whoAmI}.", ex);
        }
    }

    public override void OnWorldUnload()
    {
        sessions.Clear();
        transfer?.Dispose();
        transfer = null;
        if (serverFile != null && ReferenceEquals(Main.ActivePlayerFileData, serverFile))
            Main.ActivePlayerFileData = originalFile;
        originalFile = serverFile = null;
    }

    private static PlayerFileData NewFile() => new(Path.Combine(Main.PlayerPath, "PvPAdventure.SSC"), false)
    {
        Metadata = FileMetadata.FromCurrentSettings(FileType.Player)
    };

    private static PlayerFileData Decode(byte[] vanilla, byte[] modded)
    {
        PlayerFileData file = Player.LoadPlayerFromStream(NewFile(), vanilla, modded);
        if (file.Player == null || file.Player.loadStatus != 0)
            throw new InvalidDataException("The server character could not be decoded.");
        return file;
    }

    private static byte[] Encode(TagCompound tag)
    {
        using MemoryStream stream = new();
        TagIO.ToStream(tag, stream);
        return stream.ToArray();
    }
}

internal sealed class ServerCharacterPlayer : ModPlayer
{
    public override void PlayerDisconnect() => ModContent.GetInstance<ServerCharacters>().Disconnect(Player);
}
