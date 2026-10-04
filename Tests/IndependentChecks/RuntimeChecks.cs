using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Independent;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace IndependentChecks;

public sealed class IndependentChecks : Mod { }

public sealed class RuntimeChecks : ModSystem
{
    private bool ran;
    private int assertions;
    private static object Call(Type type, string method, object instance, params object[] arguments) =>
        type.GetMethod(method, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(instance, arguments);
    private void Check(bool result, string name)
    {
        if (!result) throw new Exception(name);
        assertions++;
        Mod.Logger.Info("PASS: " + name);
    }

    public override void OnWorldLoad()
    {
        if (ran || Terraria.Program.LaunchParameters.ContainsKey("-subworld")) return;
        ran = true;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            Main.myPlayer = 0;
            Player a = Main.player[0];
            Player b = Main.player[1];
            Player c = Main.player[2];
            foreach (Player player in new[] { a, b, c })
            {
                player.whoAmI = Array.IndexOf(Main.player, player);
                typeof(PlayerLoader).GetMethod("SetupPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { player });
                player.active = true;
                player.dead = false;
                player.ghost = false;
                player.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
                player.name = "Test" + player.whoAmI;
            }
            a.team = b.team = 1;
            c.team = 2;
            GameManager game = ModContent.GetInstance<GameManager>();
            game.CurrentPhase = GameManager.Phase.Waiting;
            void Shop(Player player, byte action, int type = 0, System.Collections.Generic.Dictionary<int, int> preset = null) =>
                Call(typeof(KitShopSystem), "Execute", null, player, action, type, preset);
            Shop(a, 1);
            Check(a.inventory.Any(item => item.type == ModContent.ItemType<PortalCreatorItem>()), "Reset kit grants portal creator");
            Shop(a, 0, ItemID.BlandWhip);
            Shop(a, 0, ItemID.BlandWhip);
            Shop(a, 0, ItemID.BlandWhip);
            Check(a.inventory.Count(item => item.type == ItemID.BlandWhip) == 2, "Server rejects purchase beyond budget");
            Shop(a, 2, preset: new() { [ItemID.BlandWhip] = -1 });
            Check(a.inventory.Count(item => item.type == ItemID.BlandWhip) == 2, "Invalid preset leaves inventory unchanged");
            Shop(a, 2, preset: new() { [ItemID.BabyBirdStaff] = 1 });
            Check(a.inventory.Count(item => item.type == ItemID.BabyBirdStaff) == 1 &&
                !a.inventory.Any(item => item.type == ItemID.BlandWhip), "Preset replaces kit and buys permitted items");
            game.CurrentPhase = GameManager.Phase.Playing;
            Shop(a, 1);
            Check(a.inventory.Any(item => item.type == ItemID.BabyBirdStaff), "Shop reset is denied after match starts");
            Main.netMode = NetmodeID.Server;
            using (MemoryStream stream = new())
            {
                using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
                writer.Write((byte)1);
                stream.Position = 0;
                Call(typeof(KitShopSystem), "Handle", null, new BinaryReader(stream), a.whoAmI);
            }
            Check(a.inventory.Any(item => item.type == ItemID.BabyBirdStaff), "Server packet cannot reset a kit during a match");
            Main.netMode = NetmodeID.SinglePlayer;
            a.selectedItem = Array.FindIndex(a.inventory, item => item.type == ModContent.ItemType<PortalCreatorItem>());
            a.Center = new Vector2((Main.spawnTileX + 80) * 16, (Main.spawnTileY - 40) * 16);
            a.velocity = Vector2.Zero;
            for (int x = (int)a.Center.X / 16 - 4; x <= (int)a.Center.X / 16 + 4; x++)
                for (int y = (int)a.Center.Y / 16 - 6; y <= (int)a.Center.Y / 16 + 6; y++)
                {
                    Tile tile = Main.tile[x, y];
                    tile.HasTile = false;
                    tile.LiquidAmount = 0;
                }
            ModContent.GetInstance<IndependentConfig>().PortalCreationSeconds = 0;
            Call(typeof(PortalSystem), "Execute", null, a, (byte)0, -1);
            a.GetModPlayer<PortalPlayer>().PostUpdate();
            NPC portal = Main.npc.First(npc => npc.active && npc.ModNPC is TeamPortal);
            Check((bool)Call(typeof(TeamPortal), "CanTravel", portal.ModNPC, b), "Teammate may use portal");
            Check(!(bool)Call(typeof(TeamPortal), "CanTravel", portal.ModNPC, c), "Enemy cannot use portal");
            Vector2 before = c.Center;
            Main.netMode = NetmodeID.Server;
            using (MemoryStream stream = new())
            {
                using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
                writer.Write((byte)2);
                writer.Write(portal.whoAmI);
                stream.Position = 0;
                Call(typeof(PortalSystem), "Handle", null, new BinaryReader(stream), c.whoAmI);
            }
            Main.netMode = NetmodeID.SinglePlayer;
            Check(c.Center == before, "Forged enemy travel request rejected");
            Call(typeof(PortalSystem), "Execute", null, b, (byte)2, portal.whoAmI);
            Check(Vector2.Distance(b.Center, portal.Center) < 100, "Teammate teleports to measured safe destination");
            Call(typeof(LocalMatchSystem), "RecordKill", ModContent.GetInstance<LocalMatchSystem>(), a);
            b.GetModPlayer<LocalMatchPlayer>().Kill(100, 0, true,
                Terraria.DataStructures.PlayerDeathReason.ByPlayerItem(a.whoAmI, a.HeldItem));
            b.active = false;
            game.CurrentPhase = GameManager.Phase.Waiting;
            string folder = Path.Combine(Main.SavePath, "PvPAdventure", "Matches");
            var match = JsonSerializer.Deserialize<LocalMatchSystem.MatchResult>(
                File.ReadAllText(Directory.GetFiles(folder, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).First()));
            Check(match.Players.Single(player => player.Name == a.name).Kills == 1, "Local match records kill");
            var offline = match.Players.Single(player => player.Name == b.name);
            Check(offline.Deaths == 1 && offline.PortalUses == 1, "Disconnected participant remains in local result");
            Mod.Logger.Info($"INDEPENDENT_CHECKS_PASSED {assertions}");
            Console.WriteLine($"INDEPENDENT_CHECKS_PASSED {assertions}");
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            Mod.Logger.Error("INDEPENDENT_CHECKS_FAILED", exception);
            Console.WriteLine(exception);
            Environment.Exit(1);
        }
    }
}
