using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Travel;
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

    private sealed record CharacterSession(ServerCharacterStore Store, PlayerFileData File, Vector2? SavedPosition)
    {
        public bool Joined { get; set; }
    }
    private static ServerCharacters Instance => ModContent.GetInstance<ServerCharacters>();

    public override void Load()
    {
        On_Player.InternalSavePlayerFile += SaveLocalFile;
        On_RemoteClient.Reset += SaveBeforeClientReset;
    }

    public override void Unload()
    {
        On_Player.InternalSavePlayerFile -= SaveLocalFile;
        On_RemoteClient.Reset -= SaveBeforeClientReset;
    }

    private void SaveBeforeClientReset(On_RemoteClient.orig_Reset original, RemoteClient client)
    {
        if (Main.netMode == NetmodeID.Server && sessions.Remove(client.Id, out CharacterSession session))
            Save(session);
        original(client);
    }

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
            Save(previous);
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

        TagCompound character = modded.GetCompound("PvPAdventureSSC");
        int team = character.GetInt("team");
        if (team is < 1 or > 5)
            team = Random.Shared.Next(1, 6);
        Player player = file.Player;
        player.name = joining.name;
        player.whoAmI = slot;
        player.active = joining.active;
        player.team = team;
        player.respawnTimer = 0;
        player.FindSpawn();
        bool hasBed = WorldGen.InWorld(player.SpawnX, player.SpawnY, 10) &&
            Player.CheckSpawn(player.SpawnX, player.SpawnY);
        if (!hasBed)
            player.RemoveSpawn();
        byte[] payload = Encode(new TagCompound
        {
            ["vanilla"] = vanilla, ["modded"] = modded, ["team"] = team,
            ["bedX"] = player.SpawnX, ["bedY"] = player.SpawnY
        });
        if (payload.Length > MaxTransferBytes)
            throw new InvalidDataException("The server character is too large to send.");

        Main.player[slot] = player;
        sessions.Add(slot, new CharacterSession(store, file, ReadPosition(character)));
        PvPAdventure.ImportSscStats(player, player.name, modded);
        Send(slot, payload);
        if (hasBed)
            RemoteClient.CheckSection(slot, new Vector2(player.SpawnX * 16f + 8f, player.SpawnY * 16f));
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
            int bedX = payload.GetInt("bedX");
            int bedY = payload.GetInt("bedY");
            if (WorldGen.InWorld(bedX, bedY, 10))
                loaded.Player.ChangeSpawn(bedX, bedY);
            else
                loaded.Player.RemoveSpawn();
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
        if (Main.netMode != NetmodeID.Server)
            return;

        foreach ((int slot, CharacterSession session) in sessions)
        {
            Player player = session.File.Player;
            if (Netplay.Clients[slot].State != 10 || !player.active)
                continue;

            if (!session.Joined)
            {
                session.Joined = true;
                RestorePosition(player, session.SavedPosition);
            }

            if (Main.GameUpdateCount % (60 * 60) == 0)
                Save(session);
        }
    }

    private static Vector2? ReadPosition(TagCompound character)
    {
        if (!character.ContainsKey("posX") || !character.ContainsKey("posY") ||
            character.GetInt("worldId") != Main.worldID || character.GetString("worldName") != Main.worldName)
            return null;

        Vector2 position = new(character.GetFloat("posX"), character.GetFloat("posY"));
        return float.IsFinite(position.X) && float.IsFinite(position.Y) ? position : null;
    }

    private static void RestorePosition(Player player, Vector2? savedPosition)
    {
        if (player.dead)
            return;

        Vector2 position;
        if (ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting)
            position = new(Main.spawnTileX * 16f + 8f - player.width * 0.5f, Main.spawnTileY * 16f - player.height);
        else if (savedPosition is { } saved && IsUsablePosition(player, saved))
            position = saved;
        else
            return;

        TravelSectionSyncSystem.PrepareTeleport(player.whoAmI, position);
        player.velocity = Vector2.Zero;
        player.Teleport(position, TeleportationStyleID.RecallPotion);
        player.fallStart = (int)(position.Y / 16f);
        NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI,
            number3: position.X, number4: position.Y, number5: TeleportationStyleID.RecallPotion);
    }

    private static bool IsUsablePosition(Player player, Vector2 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) &&
        position.X >= 16 && position.Y >= 16 &&
        position.X + player.width < (Main.maxTilesX - 1) * 16f &&
        position.Y + player.height < (Main.maxTilesY - 1) * 16f &&
        !Collision.SolidCollision(position, player.width, player.height);

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

    public override void LoadWorldData(TagCompound tag) { }

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
        if (!session.Joined)
            return;

        try
        {
            Player player = session.File.Player;
            TagCompound modded = PlayerIO.SaveData(player);
            TagCompound character = new() { ["team"] = player.team };
            if (!player.dead && IsUsablePosition(player, player.position))
            {
                character["posX"] = player.position.X;
                character["posY"] = player.position.Y;
                character["worldId"] = Main.worldID;
                character["worldName"] = Main.worldName;
            }
            modded["PvPAdventureSSC"] = character;
            PvPAdventure.ExportSscStats(player, player.name, modded);
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
