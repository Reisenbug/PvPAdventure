using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Independent;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace IndependentChecks;

public sealed partial class RuntimeChecks
{
    private void CheckPortalVisuals(Player local, Player other)
    {
        Mod mod = ModContent.GetInstance<PvPAdventure.PvPAdventure>();
        Type visuals = typeof(PortalSystem).Assembly.GetType("PvPAdventure.Common.Independent.PortalVisuals");
        foreach (int team in new[] { 0, 1, 2, 3, 4, 5 })
        foreach (string kind in new[] { "Portal", "PortalCreator", "PortalMinimap", "Outline" })
        {
            string path = (string)Call(visuals, "Path", null, kind == "Outline" ? "Portal" : kind, team);
            if (kind == "Outline") path += "_Outlines";
            using var stream = mod.GetFileStream(path.Substring("PvPAdventure/".Length) + ".rawimg");
            ImageIO.ReadRaw(stream, out int width, out int height);
            Check(kind switch
            {
                "Portal" or "Outline" => width == 36 && height == 46 * 8,
                "PortalCreator" => width == 40 && height == 42,
                _ => width == 28 && height == 30
            }, $"Packaged team {team} {kind} texture has correct dimensions");
        }
        Check(((string)Call(visuals, "Path", null, "Portal", -1)).EndsWith("NoTeam") &&
            ((string)Call(visuals, "Path", null, "Portal", 99)).EndsWith("Pink"), "Invalid visual team indices are bounded");
        var portal = new NPC();
        portal.SetDefaults(ModContent.NPCType<TeamPortal>());
        Check(Main.npcFrameCount[portal.type] == 8, "Portal registers eight animation frames");

        var remote = other.GetModPlayer<PortalPlayer>();
        Type stateType = typeof(PortalPlayer);
        var remaining = stateType.GetField("Remaining", BindingFlags.Instance | BindingFlags.NonPublic);
        var duration = stateType.GetField("CreationFrames", BindingFlags.Instance | BindingFlags.NonPublic);
        var origin = stateType.GetField("Origin", BindingFlags.Instance | BindingFlags.NonPublic);
        var progress = stateType.GetProperty("Progress", BindingFlags.Instance | BindingFlags.NonPublic);
        byte[] packetBytes = null;
        int recipient = 0;
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) =>
            {
                packetBytes = ((MemoryStream)packet.BaseStream).ToArray();
                recipient = toClient;
            }));
        Main.netMode = NetmodeID.Server;
        remaining.SetValue(remote, 450);
        duration.SetValue(remote, 600);
        origin.SetValue(remote, new Vector2(120, 240));
        Call(stateType, "Notify", remote);
        Check(recipient == -1 && packetBytes != null, "Creation progress broadcasts to all clients");
        using var payload = new MemoryStream(packetBytes[^18..]);
        Main.netMode = NetmodeID.MultiplayerClient;
        Main.myPlayer = local.whoAmI;
        remaining.SetValue(remote, 0);
        duration.SetValue(remote, 0);
        origin.SetValue(remote, Vector2.Zero);
        Call(typeof(PortalSystem), "Handle", null, new BinaryReader(payload), 256);
        Check((int)remaining.GetValue(remote) == 450 && (int)duration.GetValue(remote) == 600 &&
            (Vector2)origin.GetValue(remote) == new Vector2(120, 240), "Client restores remote preview position and timing");
        Check(Math.Abs((float)progress.GetValue(remote) - 0.25f) < 0.001f, "Preview progress uses server creation duration");
        other.selectedItem = 0;
        other.inventory[0] = new Item(ModContent.ItemType<PortalCreatorItem>());
        other.itemAnimation = other.itemTime = 123;
        other.channel = true;
        using var cancel = new MemoryStream();
        using var writer = new BinaryWriter(cancel, System.Text.Encoding.UTF8, true);
        writer.Write((byte)3);
        writer.Write(0);
        writer.Write((byte)other.whoAmI);
        writer.Write(120f);
        writer.Write(240f);
        writer.Write(600);
        cancel.Position = 0;
        Call(typeof(PortalSystem), "Handle", null, new BinaryReader(cancel), 256);
        Check((int)remaining.GetValue(remote) == 0 && other.itemAnimation == 123 && other.channel,
            "Remote preview cancellation does not overwrite remote item animation");
        CheckPortalInterruption(local);
    }
}
