using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Combat.TeamBoss;
using PvPAdventure.Common.Independent;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace IndependentChecks;

public sealed class IndependentChecks : Mod { }

public sealed class ClientSlopeBootstrap : ModSystem
{
    public override void PostAddRecipes()
    {
        if (Main.dedServ || !Terraria.Program.LaunchParameters.ContainsKey("-slopeclient")) return;
        Main.QueueMainThreadAction(() =>
        {
            Main.ActiveWorldFileData = Terraria.IO.WorldFile.GetAllMetadata("/tmp/pvpa-grapple-145/World.wld", false);
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData("/tmp/pvpa-slope-client.plr", false)
                { Player = new Player { name = "SlopeCheck" } };
            Main.player[Main.myPlayer] = Main.ActivePlayerFileData.Player;
            Main.menuMode = 10;
            WorldGen.playWorld();
        });
    }
}

public sealed class RuntimeChecks : ModSystem
{
    private delegate int TextOriginal(Rectangle location, Color color, int amount, bool dramatic, bool dot);
    private delegate int TextHook(TextOriginal original, Rectangle location, Color color, int amount, bool dramatic, bool dot);
    private delegate void PacketOriginal(ModPacket packet, int toClient, int ignoreClient);
    private delegate void PacketHook(PacketOriginal original, ModPacket packet, int toClient, int ignoreClient);
    private delegate void CommandOriginal(PvPAdventure.Common.Security.AdminPermissions.AdminCommand command, CommandCaller caller, string input, string[] args);
    private delegate void CommandHook(CommandOriginal original, PvPAdventure.Common.Security.AdminPermissions.AdminCommand command, CommandCaller caller, string input, string[] args);
    private delegate int PopupOriginal(PopupTextContext context, Item item, int stack, bool noStack, bool longText);
    private delegate int PopupHook(PopupOriginal original, PopupTextContext context, Item item, int stack, bool noStack, bool longText);
    private sealed class TestChatCaller : CommandCaller
    {
        public CommandType CommandType => CommandType.Chat;
        public Player Player => Main.LocalPlayer;
        public void Reply(string text, Color color = default) => throw new Exception(text);
    }
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
            if (Terraria.Program.LaunchParameters.ContainsKey("-randomteamchecks"))
            {
                CheckRandomTeams();
                Console.WriteLine($"RANDOM_TEAM_CHECKS_PASSED {assertions}");
                Environment.Exit(0);
            }
            if (!Main.dedServ && Terraria.Program.LaunchParameters.ContainsKey("-slopeclient"))
            {
                MeasureSlopes();
                Mod.Logger.Info($"CLIENT_SLOPE_CHECKS_PASSED {assertions}");
                Environment.Exit(0);
            }
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
            int miningX = Main.spawnTileX + 40;
            int miningY = Main.spawnTileY;
            Tile miningTile = Main.tile[miningX, miningY];
            ushort originalType = miningTile.TileType;
            miningTile.TileType = TileID.Stone;
            int miningDamage = (int)Call(typeof(Player), "GetPickaxeDamage", a,
                miningX, miningY, 100, 0, miningTile);
            Mod.Logger.Info($"Measured stone mining damage: {miningDamage}");
            Check(miningDamage == 150, "Stone mining preserves 1.5x damage without crashing");
            miningTile.TileType = TileID.Dirt;
            Check((int)Call(typeof(Player), "GetPickaxeDamage", a,
                miningX, miningY, 100, 0, miningTile) == 300, "Dirt mining preserves 1.5x damage");
            miningTile.TileType = TileID.Hellstone;
            Check((int)Call(typeof(Player), "GetPickaxeDamage", a,
                miningX, miningY, 50, 0, miningTile) == 0, "Insufficient pick power still cannot mine hellstone");
            miningTile.TileType = originalType;
            NPC damageNpc = new NPC();
            damageNpc.SetDefaults(NPCID.BlueSlime);
            var damageGlobal = damageNpc.GetGlobalNPC<TeamBossNPC>();
            bool hiddenDamage = false;
            On_NPC.orig_StrikeNPC_HitInfo_bool_bool strike = (npc, hit, fromNet, noInteraction) =>
            {
                hiddenDamage = hit.HideCombatText;
                return hit.Damage;
            };
            Call(typeof(TeamBossNPC), "OnNPCStrikeNPC", damageGlobal, strike, damageNpc,
                new NPC.HitInfo { Damage = 17 }, false, false);
            Check(!hiddenDamage, "Unassigned team hits retain vanilla damage numbers");
            bool wasDedicated = Main.dedServ;
            int damageTexts = 0;
            using var textHook = new MonoMod.RuntimeDetour.Hook(
                typeof(CombatText).GetMethod("NewText", new[] { typeof(Rectangle), typeof(Color), typeof(int), typeof(bool), typeof(bool) }),
                (TextHook)((original, location, color, amount, dramatic, dot) =>
                {
                    if (amount == 17 && color == Main.teamColor[(int)Terraria.Enums.Team.Red]) damageTexts++;
                    return 0;
                }));
            try
            {
                Main.dedServ = false;
                Call(typeof(TeamBossNPC), "OnNPCStrikeNPC", damageGlobal, strike, damageNpc,
                    new NPC.HitInfo { Damage = 17 }, false, false);
                Check(!hiddenDamage && damageTexts == 0, "Client without a team retains vanilla damage numbers");
                damageGlobal.MarkNextStrikeForTeam(damageNpc, Terraria.Enums.Team.Red);
                Call(typeof(TeamBossNPC), "OnNPCStrikeNPC", damageGlobal, strike, damageNpc,
                    new NPC.HitInfo { Damage = 17 }, false, false);
                Check(hiddenDamage && damageTexts == 1,
                    "Team hit draws exactly one damage number");
                Call(typeof(TeamBossNPC), "OnNPCStrikeNPC", damageGlobal, strike, damageNpc,
                    new NPC.HitInfo { Damage = 17, HideCombatText = true }, false, false);
                Check(hiddenDamage && damageTexts == 1,
                    "Hidden hits remain hidden without extra team damage numbers");
            }
            finally
            {
                Main.dedServ = wasDedicated;
            }
            GameManager game = ModContent.GetInstance<GameManager>();
            game.CurrentPhase = GameManager.Phase.Waiting;
            void Shop(Player player, byte action, int type = 0, System.Collections.Generic.Dictionary<int, int> preset = null) =>
                Call(typeof(KitShopSystem), "Execute", null, player, action, type, preset, 1);
            Shop(a, 3);
            var freshKit = a.GetModPlayer<KitShopPlayer>();
            Mod.Logger.Info($"Measured fresh shop: balance={typeof(KitShopPlayer).GetField("Balance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshKit)}, initialized={typeof(KitShopPlayer).GetField("Initialized", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshKit)}");
            Check(((string)Call(typeof(KitShopSystem), "BudgetText", null)).Contains("Reset kit to activate"),
                "Fresh shop explains inactive budget instead of showing zero gold");
            Shop(a, 1);
            Check((int)typeof(KitShopPlayer).GetField("Balance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshKit) == 100000 &&
                (bool)typeof(KitShopPlayer).GetField("Initialized", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshKit),
                "Reset activates configured ten-gold budget on authority");
            Check(a.inventory.Any(item => item.type == ModContent.ItemType<PortalCreatorItem>()), "Reset kit grants portal creator");
            Shop(a, 0, ItemID.BlandWhip);
            Shop(a, 0, ItemID.BlandWhip);
            Shop(a, 0, ItemID.BlandWhip);
            Check(a.inventory.Count(item => item.type == ItemID.BlandWhip) == 2, "Server rejects purchase beyond budget");
            Check(((string)Call(typeof(KitShopSystem), "BudgetText", null)) == "Starting kit budget: 0 gold",
                "Spent budget remains a genuine initialized zero balance");
            Shop(a, 1);
            Call(typeof(KitShopSystem), "Execute", null, a, (byte)0, ItemID.BlandWhip, null, 10);
            Check(a.inventory.Count(item => item.type == ItemID.BlandWhip) == 2 &&
                (int)typeof(KitShopPlayer).GetField("Balance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(freshKit) == 0,
                "Bulk starting-kit purchase is limited by authoritative budget");
            Main.netMode = NetmodeID.MultiplayerClient;
            using (MemoryStream budgetStream = new())
            {
                using BinaryWriter budgetWriter = new(budgetStream, System.Text.Encoding.UTF8, true);
                budgetWriter.Write((byte)4);
                budgetWriter.Write(true);
                budgetWriter.Write(4321);
                budgetWriter.Write(0);
                budgetStream.Position = 0;
                Call(typeof(KitShopSystem), "Handle", null, new BinaryReader(budgetStream), 0);
            }
            Check(((string)Call(typeof(KitShopSystem), "BudgetText", null)).Contains("0.4321 gold"),
                "Client receives initialized flag and authoritative budget amount");
            Main.netMode = NetmodeID.SinglePlayer;
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
                bool rejectionSent = false;
                using var replyTransport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
                    (PacketHook)((original, packet, toClient, ignoreClient) => rejectionSent = toClient == a.whoAmI));
                Call(typeof(KitShopSystem), "Handle", null, new BinaryReader(stream), a.whoAmI);
                Check(rejectionSent, "Server sends shop rejection feedback to requesting player");
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
            a.controlUseItem = a.channel = true;
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
            CheckGrappleJump(a);
            CheckPowerBomb(a, b, c);
            CheckMatchControls(a);
            CheckAdminPermissions(a, c);
            CheckPortalInterruption(a);
            CheckBulkCraft(a);
            CheckBulkBuy(a);
            MeasureSlopes();
            CheckDragonLensPermissions(a, c);
            CheckCombatBalance(a, c);
            CheckNamedAdministrator(c);
            CheckArmorEnergy(a, c);
            CheckRecipesAndResources();
            CheckBossBalance();
            CheckBossLoot();
            CheckSkeletronAI(a);
            CheckGoblinDelay(a);
            CheckBossSpawnPause(a);
            CheckBossBaitProgression(a);
            CheckRandomTeams();
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

    private void CheckBossBaitProgression(Player player)
    {
        bool savedGolem = NPC.downedGolemBoss;
        bool savedDay = Main.dayTime;
        bool savedHallow = player.ZoneHallow;
        bool savedMushroom = player.ZoneGlowshroom;
        var rules = new PvPAdventure.Common.NPCs.BossBaitProgression();
        try
        {
            NPC.downedGolemBoss = false;
            foreach (int type in new[] { NPCID.TruffleWorm, NPCID.EmpressButterfly })
            {
                var npc = new NPC();
                npc.SetDefaults(type);
                npc.active = true;
                rules.OnSpawn(npc, npc.GetSource_FromAI());
                Check(!npc.active && npc.life == 0, $"Boss bait {type} cannot spawn before Golem");
            }
            var pool = new System.Collections.Generic.Dictionary<int, float>
                { [0] = 1f, [NPCID.TruffleWorm] = 1f, [NPCID.EmpressButterfly] = 1f };
            rules.EditSpawnPool(pool, new NPCSpawnInfo { Player = player });
            Check(pool.Count == 1 && pool.ContainsKey(0), "Early boss bait entries removed without changing vanilla pool");
            NPC.downedGolemBoss = true;
            Main.dayTime = false;
            player.ZoneHallow = true;
            player.ZoneGlowshroom = false;
            rules.EditSpawnPool(pool, new NPCSpawnInfo { Player = player, SpawnTileY = (int)Main.worldSurface,
                SpawnTileType = TileID.HallowedGrass });
            Check(pool[NPCID.EmpressButterfly] == 0.05f && !pool.ContainsKey(NPCID.TruffleWorm), "Post Golem surface Hallow lacewing weight");
            pool.Remove(NPCID.EmpressButterfly);
            Main.dayTime = true;
            rules.EditSpawnPool(pool, new NPCSpawnInfo { Player = player, SpawnTileY = (int)Main.worldSurface,
                SpawnTileType = TileID.HallowedGrass });
            Check(!pool.ContainsKey(NPCID.EmpressButterfly), "No added daytime lacewing spawn");
            player.ZoneHallow = false;
            player.ZoneGlowshroom = true;
            rules.EditSpawnPool(pool, new NPCSpawnInfo { Player = player, SpawnTileY = (int)Main.worldSurface + 1,
                SpawnTileType = TileID.MushroomGrass });
            Check(pool[NPCID.TruffleWorm] == 0.2f, "Post Golem underground mushroom worm weight");
        }
        finally
        {
            NPC.downedGolemBoss = savedGolem;
            Main.dayTime = savedDay;
            player.ZoneHallow = savedHallow;
            player.ZoneGlowshroom = savedMushroom;
        }
    }

    private void CheckBossSpawnPause(Player player)
    {
        int savedNetmode = Main.netMode;
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            foreach (int type in new[] { NPCID.SkeletronHead, NPCID.CultistBoss, NPCID.Plantera })
            {
                var npc = new NPC();
                npc.SetDefaults(type);
                var pause = npc.GetGlobalNPC<PvPAdventure.Common.NPCs.BossSpawnPause>();
                int style = npc.aiStyle;
                npc.velocity = Vector2.One;
                pause.OnSpawn(npc, npc.GetSource_FromAI());
                int cooldownSlot = 0;
                Check(pause.IsPaused && npc.dontTakeDamage && npc.velocity == Vector2.Zero &&
                    !pause.CanHitPlayer(npc, player, ref cooldownSlot) && !pause.CheckActive(npc),
                    $"Boss {type} waits without movement, damage or despawn");
                Main.netMode = NetmodeID.MultiplayerClient;
                for (int i = 0; i < 10; i++) Check(!pause.PreAI(npc), $"Client does not finish boss {type} spawn wait");
                Main.netMode = NetmodeID.SinglePlayer;
                int duration = type == NPCID.Plantera ? 1800 : 3600;
                for (int i = 0; i < duration - 1; i++) pause.PreAI(npc);
                Check(pause.IsPaused && npc.dontTakeDamage, $"Boss {type} remains paused until duration ends");
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
                pause.SendExtraAI(npc, null, writer);
                stream.Position = 0;
                var client = new NPC();
                client.SetDefaults(type);
                var clientPause = client.GetGlobalNPC<PvPAdventure.Common.NPCs.BossSpawnPause>();
                using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
                clientPause.ReceiveExtraAI(client, null, reader);
                Check(clientPause.IsPaused && client.dontTakeDamage, $"Boss {type} pause state syncs to client");
                Check(pause.PreAI(npc) && !pause.IsPaused && !npc.dontTakeDamage && npc.aiStyle == style &&
                    pause.CanHitPlayer(npc, player, ref cooldownSlot) && pause.CheckActive(npc),
                    $"Boss {type} restores original AI and vulnerability at duration boundary");
            }
        }
        finally
        {
            Main.netMode = savedNetmode;
        }
    }

    private void CheckGoblinDelay(Player player)
    {
        var system = ModContent.GetInstance<PvPAdventure.Common.World.HardmodeGoblinInvasionSystem>();
        var saved = new Terraria.ModLoader.IO.TagCompound();
        system.SaveWorldData(saved);
        bool savedHardmode = Main.hardMode;
        bool savedDay = Main.dayTime;
        bool savedDowned = NPC.downedGoblins;
        int savedNetmode = Main.netMode;
        int savedCrystals = player.ConsumedLifeCrystals;
        var invasionFields = typeof(Main).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.IsLiteral && !f.IsInitOnly && f.Name.StartsWith("invasion", StringComparison.Ordinal))
            .ToDictionary(f => f, f => f.GetValue(null));
        int Dawns()
        {
            var state = new Terraria.ModLoader.IO.TagCompound();
            system.SaveWorldData(state);
            return state.GetInt("remainingDawns");
        }
        try
        {
            Main.netMode = NetmodeID.SinglePlayer;
            Main.hardMode = false;
            Main.dayTime = false;
            NPC.downedGoblins = false;
            Main.invasionType = 0;
            player.ConsumedLifeCrystals = 5;
            system.ClearWorld();
            Main.hardMode = true;
            system.PostUpdateWorld();
            Check(Dawns() == 2 && Main.invasionType == 0, "Hardmode does not immediately start goblins");
            Main.dayTime = true;
            system.PostUpdateWorld();
            Check(Dawns() == 1 && Main.invasionType == 0, "First hardmode dawn does not start goblins");
            var pending = new Terraria.ModLoader.IO.TagCompound();
            system.SaveWorldData(pending);
            system.ClearWorld();
            system.LoadWorldData(pending);
            system.PostUpdateWorld();
            Check(Dawns() == 1, "Reload preserves remaining dawn and does not count current dawn twice");
            Main.dayTime = false;
            system.PostUpdateWorld();
            Main.netMode = NetmodeID.MultiplayerClient;
            Main.dayTime = true;
            system.PostUpdateWorld();
            Check(Dawns() == 1 && Main.invasionType == 0, "Client cannot advance or start hardmode invasion");
            Main.netMode = NetmodeID.SinglePlayer;
            Main.invasionType = InvasionID.PirateInvasion;
            system.PostUpdateWorld();
            Check(Dawns() == 0 && Main.invasionType == InvasionID.PirateInvasion, "Second dawn waits for existing invasion");
            Main.invasionType = 0;
            system.PostUpdateWorld();
            Check(Dawns() == -1 && Main.invasionType == InvasionID.GoblinArmy && Main.invasionSize > 0,
                "Second dawn starts real goblin invasion once");
            Main.invasionType = 0;
            system.PostUpdateWorld();
            Check(Main.invasionType == 0, "Finished hardmode invasion does not restart");
            system.ClearWorld();
            system.LoadWorldData(new Terraria.ModLoader.IO.TagCompound { ["wasHardmode"] = true });
            system.PostUpdateWorld();
            Check(Dawns() == -1 && Main.invasionType == 0, "Legacy hardmode world does not repeat old immediate invasion");
        }
        finally
        {
            Main.hardMode = savedHardmode;
            Main.dayTime = savedDay;
            NPC.downedGoblins = savedDowned;
            Main.netMode = savedNetmode;
            player.ConsumedLifeCrystals = savedCrystals;
            foreach (var field in invasionFields) field.Key.SetValue(null, field.Value);
            system.LoadWorldData(saved);
        }
    }

    private void CheckSkeletronAI(Player player)
    {
        int savedMode = Main.GameMode;
        bool savedDay = Main.dayTime;
        var savedSlots = new[] { Main.npc[195], Main.npc[196], Main.npc[197] };
        Vector2 savedPosition = player.position;
        bool savedActive = player.active;
        bool savedDead = player.dead;
        try
        {
            Main.dayTime = false;
            player.active = true;
            player.dead = false;
            player.position = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
            foreach (int mode in new[] { 0, 1 })
            {
                Main.GameMode = 0;
                var head = Main.npc[195] = new NPC();
                head.SetDefaults(NPCID.SkeletronHead);
                head.whoAmI = 195;
                head.active = true;
                head.target = player.whoAmI;
                head.position = player.position - new Vector2(0, 200);
                head.ai[0] = 1;
                head.ai[2] = 1;
                for (int i = 196; i <= 197; i++)
                {
                    var hand = Main.npc[i] = new NPC();
                    hand.SetDefaults(NPCID.SkeletronHand);
                    hand.whoAmI = i;
                    hand.active = true;
                    hand.ai[1] = 195;
                }
                Main.GameMode = mode;
                head.VanillaAI();
                Mod.Logger.Info($"SKELETRON_AI mode={mode} defense={head.defense} lifeMax={head.lifeMax} worldExpert={Main.expertMode}");
                Check(head.defense == 60, $"Skeletron expert AI defense in world mode {mode}");
                Check(Main.GameMode == mode && head.lifeMax == 6600, "Skeletron AI preserves world difficulty and classic health");
            }
        }
        finally
        {
            Main.GameMode = savedMode;
            Main.dayTime = savedDay;
            player.position = savedPosition;
            player.active = savedActive;
            player.dead = savedDead;
            for (int i = 0; i < savedSlots.Length; i++) Main.npc[195 + i] = savedSlots[i];
        }
    }

    private void CheckBossLoot()
    {
        foreach (int npcType in new[] { NPCID.Golem, NPCID.Plantera, NPCID.DukeFishron, NPCID.HallowBoss })
        {
            var rules = Main.ItemDropsDB.GetRulesForNPCID(npcType, false).OfType<FewFromRulesRule>().ToArray();
            Check(rules.Length == (npcType == NPCID.Plantera ? 2 : 1), $"Boss {npcType} registered weapon pools");
            foreach (var rule in rules)
            {
                Check(rule.amount == 2 && rule.chanceDenominator == 1, $"Boss {npcType} guarantees two weapons per pool");
                for (int seed = 0; seed < 5; seed++)
                {
                    var selected = new System.Collections.Generic.List<int>();
                    var result = rule.TryDroppingItem(new DropAttemptInfo { rng = new Terraria.Utilities.UnifiedRandom(seed) },
                        (option, info) =>
                        {
                            selected.Add(((CommonDrop)option).itemId);
                            return new ItemDropAttemptResult { State = ItemDropAttemptResultState.Success };
                        });
                    Check(result.State == ItemDropAttemptResultState.Success && selected.Count == 2 && selected.Distinct().Count() == 2,
                        $"Boss {npcType} actually selects two distinct weapons, seed {seed}");
                }
            }
        }
        foreach (var spec in new[] { (NPCID.QueenSlimeBoss, 2), (NPCID.Golem, 1) })
        {
            var pools = Main.ItemDropsDB.GetRulesForNPCID(spec.Item1, false).OfType<FewFromOptionsDropRule>().ToArray();
            Check(pools.Length == spec.Item2 && pools.All(p => p.amount == 2 && p.chanceNumerator == 1 && p.chanceDenominator == 1),
                $"Boss {spec.Item1} guarantees two items from each equipment pool");
        }
        foreach (var spec in new[]
        {
            (NPCID.Golem, ItemID.BeetleHusk, 12, 16),
            (NPCID.DukeFishron, ItemID.SharkFin, 14, 14),
            (NPCID.Plantera, ItemID.TempleKey, 1, 1),
            (NPCID.Plantera, ItemID.TheAxe, 1, 1),
            (NPCID.Plantera, ItemID.ThornHook, 1, 1)
        })
        {
            var drops = Main.ItemDropsDB.GetRulesForNPCID(spec.Item1, false).OfType<CommonDrop>().Where(d => d.itemId == spec.Item2).ToArray();
            Check(drops.Length == 1 && drops[0].chanceDenominator == 1 && drops[0].amountDroppedMinimum == spec.Item3 && drops[0].amountDroppedMaximum == spec.Item4,
                $"Boss {spec.Item1} guaranteed material {spec.Item2} occurs once with correct quantity");
        }
        var fishron = Main.ItemDropsDB.GetRulesForNPCID(NPCID.DukeFishron, false).OfType<FewFromRulesRule>().Single();
        Check(fishron.options.OfType<CommonDrop>().Any(d => d.itemId == ItemID.TempestStaff), "Fishron weapon pool includes Tempest Staff");
    }

    private void CheckBossBalance()
    {
        var config = ModContent.GetInstance<ServerConfig>();
        var savedBalance = config.BossBalance;
        int savedMode = Main.GameMode;
        config.BossBalance = [];
        Main.GameMode = 0;
        try
        {
            foreach (var spec in new[]
            {
                (NPCID.SkeletronHead, 6600),
                (NPCID.SkeletronHand, 600),
                (NPCID.QueenBee, 6256),
                (NPCID.WallofFlesh, 24000),
                (NPCID.WallofFleshEye, 24000),
                (NPCID.QueenSlimeBoss, 36000),
                (NPCID.Plantera, 150000)
            })
            {
                var npc = new NPC();
                npc.SetDefaults(spec.Item1);
                Check(npc.lifeMax == spec.Item2 && npc.life == spec.Item2,
                    $"Classic boss {spec.Item1} starts with {spec.Item2} HP");
            }
            Check(config.BossBalance.Count == 0, "Default boss HP does not enable team health pools");
        }
        finally
        {
            Main.GameMode = savedMode;
            config.BossBalance = savedBalance;
        }
    }

    private void CheckCombatBalance(Player attacker, Player target)
    {
        var config = ModContent.GetInstance<ServerConfig>();
        attacker.active = target.active = true;
        attacker.hostile = target.hostile = true;
        attacker.team = 1;
        target.team = 2;
        attacker.ghostHeal = false;
        attacker.inventory[0] = new Item(ItemID.DaedalusStormbow);
        attacker.selectedItem = 0;
        int index = Projectile.NewProjectile(attacker.GetSource_ItemUse(attacker.HeldItem), attacker.Center,
            Vector2.Zero, ProjectileID.ChlorophyteArrow, 100, 0, attacker.whoAmI);
        Projectile shot = Main.projectile[index];
        var source = shot.GetGlobalProjectile<PvPAdventure.Common.Statistics.StatisticsProjectile>();
        Check(Terraria.DataStructures.PlayerDeathReason.ByProjectile(attacker.whoAmI, index).SourceItem.type == ItemID.DaedalusStormbow,
            "Projectile records firing weapon rather than ammunition");
        attacker.inventory[0].SetDefaults(ItemID.CopperShortsword);
        var reason = Terraria.DataStructures.PlayerDeathReason.ByProjectile(attacker.whoAmI, index);
        Check(reason.SourceItem.type == ItemID.DaedalusStormbow,
            "Changing weapons after firing preserves original projectile source");
        Player.HurtModifiers modifiers = new() { DamageSource = reason, PvP = true };
        PlayerLoader.ModifyHurt(target, ref modifiers);
        Mod.Logger.Info($"BALANCE_MEASURE stormbow/chlorophyte multiplier={modifiers.IncomingDamageMultiplier.Value}");
        Check(Math.Abs(modifiers.IncomingDamageMultiplier.Value - 0.175f) < 0.00001f,
            "Stormbow 35 percent and chlorophyte arrow 50 percent apply exactly once");
        int childIndex = Projectile.NewProjectile(shot.GetSource_FromThis(), shot.Center, Vector2.Zero,
            ProjectileID.HolyArrow, 100, 0, attacker.whoAmI);
        Projectile child = Main.projectile[childIndex];
        Check(Terraria.DataStructures.PlayerDeathReason.ByProjectile(attacker.whoAmI, childIndex).SourceItem.type == ItemID.DaedalusStormbow,
            "Child projectile inherits firing weapon");
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
        source.SendExtraAI(shot, null, writer);
        stream.Position = 0;
        var remote = child.GetGlobalProjectile<PvPAdventure.Common.Statistics.StatisticsProjectile>();
        remote.ReceiveExtraAI(shot, null, new BinaryReader(stream));
        Check(Terraria.DataStructures.PlayerDeathReason.ByProjectile(attacker.whoAmI, childIndex).SourceItem.type == ItemID.DaedalusStormbow,
            "Projectile weapon survives network serialization");
        reason = Terraria.DataStructures.PlayerDeathReason.ByProjectile(target.whoAmI, index);
        Check(reason.SourceItem == null,
            "Mismatched projectile owner cannot supply weapon source");
        reason = Terraria.DataStructures.PlayerDeathReason.ByPlayerItem(attacker.whoAmI, new Item(ItemID.CrystalVileShard));
        modifiers = new() { DamageSource = reason, PvP = true };
        PlayerLoader.ModifyHurt(target, ref modifiers);
        Mod.Logger.Info($"BALANCE_MEASURE vile shard scalingAP={modifiers.ScalingArmorPenetration.Value}, flatAP={modifiers.ArmorPenetration.Value}");
        Check(Math.Abs(modifiers.ScalingArmorPenetration.Value - 0.33f) < 0.00001f && modifiers.ArmorPenetration.Value == 0,
            "Configured percent penetration replaces legacy flat penetration");
        modifiers = new() { DamageSource = reason, PvP = false };
        PlayerLoader.ModifyHurt(target, ref modifiers);
        Check(modifiers.ScalingArmorPenetration.Value == 0 && modifiers.IncomingDamageMultiplier.Value == 1,
            "PvP balance leaves PvE damage and penetration unchanged");
        reason = Terraria.DataStructures.PlayerDeathReason.ByProjectile(attacker.whoAmI, childIndex);
        config.WeaponBalance.Damage.ItemDamage[new(ItemID.DaedalusStormbow)] = 0.7f;
        modifiers = new() { DamageSource = reason, PvP = true };
        PlayerLoader.ModifyHurt(target, ref modifiers);
        Check(Math.Abs(modifiers.IncomingDamageMultiplier.Value - 0.7f) < 0.00001f,
            "Server configuration overrides shipped weapon multiplier");
        config.WeaponBalance.Damage.ItemDamage[new(ItemID.DaedalusStormbow)] = 0.35f;
        shot.active = child.active = false;
    }

    private void CheckRandomTeams()
    {
        int savedMode = Main.netMode;
        var player = Main.player[0];
        bool savedActive = player.active;
        int savedTeam = player.team;
        var file = Main.ActivePlayerFileData;
        typeof(PlayerLoader).GetMethod("SetupPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { player });
        var state = player.GetModPlayer<PvPAdventure.Common.Teams.TeamResyncPlayer>();
        int packets = 0;
        byte[] lastPacket = [];
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) =>
            {
                packets++;
                lastPacket = ((MemoryStream)packet.BaseStream).ToArray();
            }));
        try
        {
            Main.netMode = NetmodeID.Server;
            player.active = true;
            state.PlayerDisconnect();
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
            int assigned = player.team;
            Check(assigned >= 1 && assigned <= 5, "Join assignment selects a colored team, never neutral");
            Check(lastPacket[^5] == 0 && BitConverter.ToInt32(lastPacket, lastPacket.Length - 4) == assigned,
                "Server broadcasts the assigned player and team");
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
            Check(player.team == assigned, "Repeated join request does not reroll the team");
            player.team = 5;
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
            Check(player.team == 5, "Repeated request preserves a subsequent manual team change");
            var colors = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                state.PlayerDisconnect();
                PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
                colors.Add(player.team);
            }
            Check(colors.SetEquals(new[] { 1, 2, 3, 4, 5 }), "New connections can receive each of the five colors");
            int before = packets;
            player.active = false;
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(-1);
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(Main.maxPlayers);
            Check(packets == before, "Inactive and invalid senders cannot receive team assignments");
            Main.netMode = NetmodeID.MultiplayerClient;
            PvPAdventure.Common.Teams.PlayerTeamNetHandler.AssignRandomTeam(0);
            Check(packets == before, "Client cannot execute server team assignment");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData("/tmp/ghost.plr", false);
            state.OnEnterWorld();
            for (int i = 0; i < 30; i++) state.PostUpdate();
            Check(packets == before, "SSC temporary join does not request a team before character restoration");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData("/tmp/character.SSC", false);
            state.OnEnterWorld();
            for (int i = 0; i < 30; i++) state.PostUpdate();
            Check(packets == before + 1 && lastPacket[^1] == (byte)PvPAdventure.Core.Net.AdventurePacketIdentifier.RandomTeamRequest,
                "Restored SSC character requests a server-assigned team without submitting an old team");
        }
        finally
        {
            state.PlayerDisconnect();
            player.active = savedActive;
            player.team = savedTeam;
            Main.netMode = savedMode;
            Main.ActivePlayerFileData = file;
        }
    }

    private void CheckNamedAdministrator(Player player)
    {
        string originalName = player.name;
        Main.netMode = NetmodeID.Server;
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => { }));
        var permissions = player.GetModPlayer<PvPAdventure.Common.Security.AdminPermissions.AdminPlayer>();
        permissions.PlayerDisconnect();
        player.name = "reisenberg";
        permissions.PreUpdate();
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(player.whoAmI),
            "Other character names are not automatically administrators");
        player.name = "Reisenberg";
        Main.netMode = NetmodeID.MultiplayerClient;
        permissions.PreUpdate();
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(player.whoAmI),
            "Client cannot grant itself named administrator permission");
        Main.netMode = NetmodeID.Server;
        permissions.PreUpdate();
        Check(PvPAdventure.Common.Security.AdminPermissions.HasAccess(player.whoAmI),
            "Server automatically grants Reisenberg administrator permission");
        permissions.PlayerDisconnect();
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(player.whoAmI),
            "Named administrator disconnect revokes slot permission");
        player.name = originalName;
        permissions.PreUpdate();
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(player.whoAmI),
            "Reused player slot does not inherit named administrator permission");
        Main.netMode = NetmodeID.SinglePlayer;
    }

    private void CheckRecipesAndResources()
    {
        foreach (var spec in new[]
        {
            (ItemID.ManaCrystal, new[] { (ItemID.FallenStar, 3) }, -1),
            (ItemID.DeerThing, new[] { (ItemID.DemoniteOre, 5), (ItemID.Lens, 1), (ItemID.FlinxFur, 3) }, -1),
            (ItemID.ShroomiteBar, new[] { (ItemID.GlowingMushroom, 1), (ItemID.ChlorophyteBar, 1) }, TileID.Autohammer),
            (ItemID.DemonWings, new[] { (ItemID.SoulofNight, 5), (ItemID.Feather, 3), (ItemID.SoulofFlight, 20) }, TileID.MythrilAnvil),
            (ItemID.AngelWings, new[] { (ItemID.SoulofLight, 5), (ItemID.Feather, 3), (ItemID.SoulofFlight, 20) }, TileID.MythrilAnvil),
            (ItemID.FairyWings, new[] { (ItemID.PixieDust, 45), (ItemID.SoulofFlight, 20) }, TileID.MythrilAnvil),
            (ItemID.Hellforge, new[] { (ItemID.Furnace, 1), (ItemID.Hellstone, 30) }, TileID.Anvils)
        })
        {
            var recipes = Main.recipe.Where(r => r != null && !r.Disabled && r.createItem.type == spec.Item1).ToArray();
            Check(recipes.Length == 1, $"Exactly one active recipe for {spec.Item1}");
            var recipe = recipes.Single();
            var ingredients = recipe.requiredItem.Where(i => !i.IsAir).ToArray();
            Check(recipe.createItem.stack == 1 && ingredients.Length == spec.Item2.Length &&
                spec.Item2.All(i => ingredients.Any(actual => actual.type == i.Item1 && actual.stack == i.Item2)),
                $"Registered ingredients for {spec.Item1}");
            Check(spec.Item3 < 0 ? recipe.requiredTile.Count == 0 : recipe.requiredTile.SequenceEqual(new[] { spec.Item3 }),
                $"Registered station for {spec.Item1}");
        }

        System.Collections.Generic.List<Terraria.GameContent.ItemDropRules.DropRateInfo> Drops(int npcType)
        {
            var rates = new System.Collections.Generic.List<Terraria.GameContent.ItemDropRules.DropRateInfo>();
            foreach (var rule in Main.ItemDropsDB.GetRulesForNPCID(npcType, false))
                rule.ReportDroprates(rates, new Terraria.GameContent.ItemDropRules.DropRateInfoChainFeed(1f));
            return rates;
        }

        Check(Drops(NPCID.GiantTortoise).Where(d => d.itemId == ItemID.TurtleShell).All(d => d.dropRate == 0.5f) &&
            Drops(NPCID.GiantTortoise).Any(d => d.itemId == ItemID.TurtleShell), "Registered turtle shell chance is one half");
        Check(Drops(NPCID.SnowFlinx).Any(d => d.itemId == ItemID.FlinxFur && d.stackMin == 1 && d.stackMax == 1 && d.dropRate == 1f),
            "Snow Flinx has an additional guaranteed fur drop");
        Check(Main.recipe.Single(r => r != null && !r.Disabled && r.createItem.type == ItemID.Hellforge).DecraftDisabled,
            "Crafted Hellforge cannot be decrafted");
        foreach (var spec in new[]
        {
            (NPCID.Lihzahrd, ItemID.LihzahrdPowerCell, 30),
            (NPCID.LihzahrdCrawler, ItemID.LihzahrdPowerCell, 30),
            (NPCID.FlyingSnake, ItemID.LihzahrdPowerCell, 30),
            (NPCID.AngryTrapper, ItemID.Uzi, 15),
            (NPCID.RustyArmoredBonesAxe, ItemID.MaceWhip, 20),
            (NPCID.RustyArmoredBonesFlail, ItemID.MaceWhip, 20),
            (NPCID.RustyArmoredBonesSword, ItemID.MaceWhip, 20),
            (NPCID.RustyArmoredBonesSwordNoArmor, ItemID.MaceWhip, 20),
            (NPCID.BlueArmoredBones, ItemID.MaceWhip, 20),
            (NPCID.BlueArmoredBonesMace, ItemID.MaceWhip, 20),
            (NPCID.BlueArmoredBonesNoPants, ItemID.MaceWhip, 20),
            (NPCID.BlueArmoredBonesSword, ItemID.MaceWhip, 20),
            (NPCID.HellArmoredBones, ItemID.MaceWhip, 20),
            (NPCID.HellArmoredBonesSpikeShield, ItemID.MaceWhip, 20),
            (NPCID.HellArmoredBonesMace, ItemID.MaceWhip, 20),
            (NPCID.HellArmoredBonesSword, ItemID.MaceWhip, 20)
        })
        {
            var rates = Drops(spec.Item1).Where(d => d.itemId == spec.Item2).ToArray();
            Check(rates.Length > 0 && rates.All(d =>
            {
                float expected = 1f / spec.Item3;
                if (spec.Item2 == ItemID.MaceWhip && d.conditions?.Any(c => c is Terraria.GameContent.ItemDropRules.Conditions.IsExpert) == true)
                    expected = 1f - (1f - expected) * (1f - expected);
                return Math.Abs(d.dropRate - expected) < 0.000001f;
            }),
                $"Registered drop chance for NPC {spec.Item1}, item {spec.Item2}");
        }
        foreach (int npcType in new[] { NPCID.TheDestroyer, NPCID.SkeletronPrime, NPCID.Retinazer, NPCID.Spazmatism })
        {
            var bars = Drops(npcType).Where(d => d.itemId == ItemID.HallowedBar).ToArray();
            Check(bars.Length > 0 && bars.All(d => d.stackMin == 20 && d.stackMax == 30),
                $"Registered mechanical boss bars range for {npcType}");
        }
    }

    private void CheckArmorEnergy(Player attacker, Player target)
    {
        bool wasMenu = Main.gameMenu;
        Main.gameMenu = false;
        Main.netMode = NetmodeID.Server;
        int sent = 0;
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => sent++));
        foreach (Player player in new[] { attacker, target })
        {
            player.active = true;
            player.dead = player.ghost = false;
            for (int i = 0; i < player.armor.Length; i++)
                player.armor[i] = new Item();
            player.statLife = 1000;
            player.statLifeMax2 = 400;
            player.GetModPlayer<PvPAdventure.Common.Combat.ArmorEnergyPlayer>().PlayerDisconnect();
        }
        attacker.team = 1;
        target.team = 2;
        attacker.armor[0] = new Item(ItemID.BeetleHelmet);
        attacker.armor[1] = new Item(ItemID.BeetleScaleMail);
        attacker.armor[2] = new Item(ItemID.BeetleLeggings);
        target.armor[3] = new Item(ItemID.CrossNecklace);
        var offense = attacker.GetModPlayer<PvPAdventure.Common.Combat.ArmorEnergyPlayer>();
        Check(!BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight1] &&
            !BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight2] &&
            !BuffID.Sets.TimeLeftDoesNotDecrease[BuffID.BeetleMight3],
            "Beetle buffs no longer retain old permanent duration flags");
        Check(Terraria.Localization.Language.GetTextValue("Mods.PvPAdventure.ArmorEnergy.BeetleSetBonus").Contains("9 per damage") &&
            Terraria.Localization.Language.GetTextValue("Mods.PvPAdventure.Buffs.GodsProtection.Description").Contains("50%"),
            "Equipment localization keys resolve to rule descriptions");
        var protection = target.GetModPlayer<PvPAdventure.Common.Combat.ArmorEnergyPlayer>();
        var reason = Terraria.DataStructures.PlayerDeathReason.ByPlayerItem(attacker.whoAmI, new Item(ItemID.PaladinsHammer));
        Player.HurtInfo hit = new()
        {
            DamageSource = reason, Damage = 100, SourceDamage = 100, PvP = true,
            DustDisabled = true, SoundDisabled = true
        };
        target.Hurt(hit, true);
        Mod.Logger.Info($"ARMOR_ENERGY_MEASURE beetle={offense.BeetleEnergy}, cross={protection.CrossEnergy}");
        Check(offense.BeetleEnergy == 900 && protection.CrossEnergy == 200,
            "Actual server hurt awards 9x melee damage and 2x received PvP damage once");
        Check(offense.BeetleTier == 1 && !protection.Protected, "First armor energy thresholds match");
        int authoritativePackets = sent;
        Main.netMode = NetmodeID.MultiplayerClient;
        protection.OnHurt(hit);
        Check(offense.BeetleEnergy == 900 && protection.CrossEnergy == 200 && sent == authoritativePackets,
            "Client hurt cannot duplicate armor energy or send grants");
        void Snapshot(Player player, int cross, int beetle)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)player.whoAmI);
            writer.Write(cross);
            writer.Write(beetle);
            stream.Position = 0;
            Call(typeof(PvPAdventure.Common.Combat.ArmorEnergyPlayer), "Handle", null, new BinaryReader(stream));
            Check(stream.Position == stream.Length, "Armor energy packet is fully consumed");
        }
        Main.netMode = NetmodeID.Server;
        Snapshot(attacker, 12345, 3600);
        Check(offense.BeetleEnergy == 900 && offense.CrossEnergy == 0, "Server rejects client-forged armor energy");
        foreach (var value in new[] { (599, 0), (600, 1), (1439, 1), (1440, 2), (3000, 2), (3001, 3), (3600, 3) })
        {
            Main.netMode = NetmodeID.MultiplayerClient;
            Snapshot(attacker, 0, value.Item1);
            Check(offense.BeetleTier == value.Item2, "Beetle tier at energy " + value.Item1);
        }
        Main.netMode = NetmodeID.Server;
        protection.OnHurt(hit);
        Check(offense.BeetleEnergy == 3600 && protection.CrossEnergy == 400 && protection.Protected,
            "Beetle energy caps at 3600 and protection activates at maximum life");
        Player.HurtModifiers modifiers = new() { DamageSource = reason, PvP = true };
        protection.ModifyHurt(ref modifiers);
        Check(modifiers.FinalDamage.ApplyTo(100) == 50, "Active protection halves final damage");
        protection.PostUpdate();
        Check(protection.CrossEnergy == 399 && !protection.Protected, "Protection decays by one and ends below threshold");
        offense.PostUpdate();
        Check(offense.BeetleEnergy == 3599 && attacker.HasBuff(BuffID.BeetleMight3) &&
            !attacker.HasBuff(BuffID.BeetleMight1) && !attacker.HasBuff(BuffID.BeetleMight2),
            "Beetle decay refreshes exactly one tier buff");
        attacker.ResetEffects();
        attacker.UpdateBuffs(attacker.whoAmI);
        Mod.Logger.Info($"ARMOR_SLOT_MEASURE beetle head={attacker.head}, body={attacker.body}, legs={attacker.legs}; equipped={attacker.armor[0].headSlot}/{attacker.armor[1].bodySlot}/{attacker.armor[2].legSlot}");
        attacker.head = attacker.armor[0].headSlot;
        attacker.body = attacker.armor[1].bodySlot;
        attacker.legs = attacker.armor[2].legSlot;
        attacker.beetleCounter = 5000;
        attacker.UpdateArmorSets(attacker.whoAmI);
        Check(attacker.beetleOffense && attacker.beetleCounter == 5000,
            "Actual beetle armor set hook bypasses vanilla charge decay");
        Mod.Logger.Info($"BEETLE_STATS_MEASURE damage={attacker.GetDamage(DamageClass.Melee).Additive}, speed={attacker.GetAttackSpeed(DamageClass.Melee)}");
        Check(Math.Abs(attacker.GetDamage(DamageClass.Melee).Additive - 1.3f) < 0.00001f &&
            Math.Abs(attacker.GetAttackSpeed(DamageClass.Melee) - 1.3f) < 0.00001f,
            "Tier three gives 30 percent melee damage and attack speed without vanilla double application");
        attacker.armor[1] = new Item();
        offense.PostUpdate();
        Check(offense.BeetleEnergy == 0 && !attacker.HasBuff(BuffID.BeetleMight3),
            "Removing beetle scale mail clears energy and buffs");
        target.armor[3] = new Item();
        protection.PostUpdate();
        Check(protection.CrossEnergy == 0, "Removing protection equipment clears stored energy");
        target.armor[0] = new Item(ItemID.HallowedHood);
        target.armor[1] = new Item(ItemID.HallowedPlateMail);
        target.armor[2] = new Item(ItemID.HallowedGreaves);
        Mod.Logger.Info($"ARMOR_SLOT_MEASURE hallowed head={target.head}, body={target.body}, legs={target.legs}; equipped={target.armor[0].headSlot}/{target.armor[1].bodySlot}/{target.armor[2].legSlot}");
        target.head = target.armor[0].headSlot;
        target.body = target.armor[1].bodySlot;
        target.legs = target.armor[2].legSlot;
        target.UpdateArmorSets(target.whoAmI);
        Check(target.onHitDodge, "Vanilla hallowed armor enables dodge before mod replacement");
        target.shadowDodge = true;
        protection.PostUpdateEquips();
        Check(protection.HasCrossEquipment && !target.onHitDodge && !target.shadowDodge,
            "Full hallowed summoner set uses energy protection instead of vanilla dodge");
        Main.netMode = NetmodeID.MultiplayerClient;
        Snapshot(target, 500, 3600);
        Main.netMode = NetmodeID.Server;
        protection.Kill(100, 0, true, reason);
        Check(protection.CrossEnergy == 0 && protection.BeetleEnergy == 0 && !protection.Protected,
            "Death clears both armor energy pools");
        int beforeNonPvp = protection.CrossEnergy;
        hit.PvP = false;
        protection.OnHurt(hit);
        Check(protection.CrossEnergy == beforeNonPvp, "PvE damage does not charge protection");
        target.armor[0] = target.armor[1] = target.armor[2] = new Item();
        target.armor[3] = new Item(ItemID.FeralClaws);
        target.armor[4] = new Item(ItemID.PowerGlove);
        target.armor[5] = new Item(ItemID.FireGauntlet);
        target.GetAttackSpeed(DamageClass.Melee) = 1.36f;
        target.GetModPlayer<PvPAdventure.Common.Combat.EJ.GlovesDontStack>().PostUpdateEquips();
        Check(Math.Abs(target.GetAttackSpeed(DamageClass.Melee) - 1.12f) < 0.00001f,
            "Three equipped gloves retain only one 12 percent speed bonus");
        target.armor[4] = target.armor[5] = new Item();
        target.GetAttackSpeed(DamageClass.Melee) = 1.12f;
        target.GetModPlayer<PvPAdventure.Common.Combat.EJ.GlovesDontStack>().PostUpdateEquips();
        Check(Math.Abs(target.GetAttackSpeed(DamageClass.Melee) - 1.12f) < 0.00001f,
            "One glove keeps its speed bonus");
        target.extraAccessory = false;
        target.armor[8] = new Item(ItemID.PowerGlove);
        target.armor[9] = new Item(ItemID.FireGauntlet);
        target.GetModPlayer<PvPAdventure.Common.Combat.EJ.GlovesDontStack>().PostUpdateEquips();
        Check(Math.Abs(target.GetAttackSpeed(DamageClass.Melee) - 1.12f) < 0.00001f,
            "Locked accessory slots do not count as equipped gloves");
        Main.gameMenu = wasMenu;
        Main.netMode = NetmodeID.SinglePlayer;
    }

    private void CheckMatchControls(Player player)
    {
        GameManager game = ModContent.GetInstance<GameManager>();
        PauseManager pause = ModContent.GetInstance<PauseManager>();
        Main.netMode = NetmodeID.Server;
        using var adminTransport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => { }));
        Call(typeof(PvPAdventure.Common.Security.AdminPermissions), "Grant", null, player.whoAmI, true);
        game.CurrentPhase = GameManager.Phase.Playing;
        void Command(GameTimerNetHandler.GameTimerPacketType action)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)action);
            stream.Position = 0;
            GameTimerNetHandler.HandlePacket(new BinaryReader(stream), player.whoAmI);
        }
        Check(((string)Call(typeof(KitShopSystem), "BlockReason", null, player)).Contains("before a match"),
            "Shop explains why a running match blocks access");
        Command(GameTimerNetHandler.GameTimerPacketType.PauseGame);
        Check(pause.IsPaused, "Pause command pauses a running match");
        Command(GameTimerNetHandler.GameTimerPacketType.PauseGame);
        Check(!pause.IsPaused, "Pause command resumes a paused match");
        Command(GameTimerNetHandler.GameTimerPacketType.PauseGame);
        Command(GameTimerNetHandler.GameTimerPacketType.EndGame);
        Check(game.CurrentPhase == GameManager.Phase.Waiting && game.TimeRemaining == 0 &&
            !game._startGameCountdown.HasValue && !pause.IsPaused, "End command clears match and pause state");
        game.StartGame(3600, 10);
        Command(GameTimerNetHandler.GameTimerPacketType.EndGame);
        Check(!game._startGameCountdown.HasValue && game.TimeRemaining == 0,
            "End command cancels countdown without starting a match");
        player.Center = new Vector2((Main.spawnTileX + 80) * 16, (Main.spawnTileY - 40) * 16);
        Check(((string)Call(typeof(KitShopSystem), "BlockReason", null, player)).Contains("spawn area"),
            "Shop explains why wilderness blocks access");
        Check(((string)Call(typeof(PortalSystem), "CreationBlockReason", null, player)).Contains("during a match"),
            "Portal explains why waiting blocks creation");
        game.CurrentPhase = GameManager.Phase.Playing;
        int slot = Array.FindIndex(player.inventory, item => item.type == ModContent.ItemType<PortalCreatorItem>());
        Item creator = player.inventory[slot];
        player.inventory[slot] = new Item();
        Check(((string)Call(typeof(PortalSystem), "CreationBlockReason", null, player)).Contains("No Portal Creator"),
            "Portal explains missing creator and how to obtain it");
        player.inventory[slot] = creator;
        player.velocity = new Vector2(1, 0);
        Check(((string)Call(typeof(PortalSystem), "CreationBlockReason", null, player)).Contains("Stand still"),
            "Portal explains movement restriction");
        player.velocity = Vector2.Zero;
        Check(Call(typeof(PortalSystem), "CreationBlockReason", null, player) == null,
            "Living stationary player with creator in wilderness may create portal");
        Call(typeof(PvPAdventure.Common.Security.AdminPermissions), "Grant", null, player.whoAmI, false);
    }

    private void CheckDragonLensPermissions(Player owner, Player guest)
    {
        if (!ModLoader.TryGetMod("DragonLens", out Mod dragonLens))
            return;
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => { }));
        Type handler = dragonLens.Code.GetType("DragonLens.Core.Systems.PermissionHandler");
        MethodInfo canUse = handler.GetMethod("CanUseTools", BindingFlags.Static | BindingFlags.Public);
        Type permissions = typeof(PvPAdventure.Common.Security.AdminPermissions);
        Main.netMode = NetmodeID.Server;
        Check(!(bool)canUse.Invoke(null, new object[] { guest }), "DragonLens denies tool permission to guest");
        void Packet(string type, Action<BinaryWriter> write, int sender)
        {
            using MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(type);
                write(writer);
            }
            stream.Position = 0;
            dragonLens.HandlePacket(new BinaryReader(stream), sender);
            Check(stream.Position == stream.Length, "DragonLens packet fully consumed: " + type);
        }
        void TimePacket(int sender) => Packet("ToolPacket", writer =>
        {
            writer.Write("DragonLens/Time");
            writer.Write(12345d);
            writer.Write(true);
            writer.Write(-1);
            writer.Write(false);
            writer.Write(0);
        }, sender);
        Main.time = 54321;
        TimePacket(guest.whoAmI);
        Check(Main.time == 54321, "Guest DragonLens tool packet cannot change world time");
        Packet("AdminUpdate", writer => { writer.Write(0); writer.Write(guest.whoAmI); }, guest.whoAmI);
        Check(!(bool)canUse.Invoke(null, new object[] { guest }), "Forged DragonLens admin packet cannot grant tool permission");
        Call(permissions, "Grant", null, owner.whoAmI, true);
        Check((bool)canUse.Invoke(null, new object[] { owner }), "Independent administrator may use DragonLens tools");
        TimePacket(owner.whoAmI);
        Check(Main.time == 12345, "Authenticated DragonLens tool packet changes world time");
        Packet("AdminUpdate", writer => { writer.Write(1); writer.Write(owner.whoAmI); }, guest.whoAmI);
        Check((bool)canUse.Invoke(null, new object[] { owner }), "Guest cannot revoke administrator through DragonLens packets");
        owner.GetModPlayer<PvPAdventure.Common.Security.AdminPermissions.AdminPlayer>().PlayerDisconnect();
        Check(!(bool)canUse.Invoke(null, new object[] { owner }), "Disconnect revokes DragonLens tool permission");
        Main.netMode = NetmodeID.SinglePlayer;
        Check((bool)canUse.Invoke(null, new object[] { guest }), "Single-player DragonLens tools remain available");
    }

    private void CheckPortalInterruption(Player player)
    {
        Main.netMode = NetmodeID.SinglePlayer;
        Main.myPlayer = player.whoAmI;
        player.dead = player.ghost = false;
        ModContent.GetInstance<GameManager>().CurrentPhase = GameManager.Phase.Playing;
        player.selectedItem = 0;
        player.inventory[0] = new Item(ModContent.ItemType<PortalCreatorItem>());
        var state = player.GetModPlayer<PortalPlayer>();
        var remaining = typeof(PortalPlayer).GetField("Remaining", BindingFlags.Instance | BindingFlags.NonPublic);
        var origin = typeof(PortalPlayer).GetField("Origin", BindingFlags.Instance | BindingFlags.NonPublic);
        void Arm(int ticks = 300)
        {
            remaining.SetValue(state, ticks);
            origin.SetValue(state, player.Center);
            player.itemAnimation = player.itemAnimationMax = 600;
            player.itemTime = player.itemTimeMax = 600;
            player.channel = player.controlUseItem = true;
        }
        bool Released() => player.itemAnimation == 0 && player.itemTime == 0 && !player.channel && !player.controlUseItem;
        Arm();
        Call(typeof(PortalPlayer), "Cancel", state);
        Mod.Logger.Info($"PORTAL_CANCEL_MEASURE animation={player.itemAnimation} time={player.itemTime} channel={player.channel} use={player.controlUseItem}");
        Check(Released(),
            "Interrupted portal creation releases item animation and channel immediately");
        Arm(0);
        player.controlUseItem = false;
        state.PostUpdate();
        Check(Released(), "Releasing portal tool before countdown acknowledgement clears stale animation");
        Arm();
        player.controlUseItem = false;
        state.PostUpdate();
        Check(Released() && (int)remaining.GetValue(state) == 0, "Releasing portal use cancels countdown and unlocks tool");
        Arm();
        player.position.X += 3;
        state.PostUpdate();
        Check(Released() && (int)remaining.GetValue(state) == 0, "Movement interruption unlocks portal tool");
        Arm();
        state.OnHurt(default);
        Check(Released(), "Damage interruption unlocks portal tool");
        Arm(1);
        state.PostUpdate();
        Check(Released(), "Successful creation releases portal tool animation");
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => { }));
        Main.netMode = NetmodeID.MultiplayerClient;
        Arm();
        player.channel = false;
        state.PostUpdate();
        Check(Released() && (int)remaining.GetValue(state) == 0, "Client cancellation unlocks immediately before server reply");
        Arm();
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)3);
            writer.Write(0);
            stream.Position = 0;
            Call(typeof(PortalSystem), "Handle", null, new BinaryReader(stream), player.whoAmI);
        }
        Check(Released(), "Server cancellation reply clears client item animation");
        Arm();
        state.OnHurt(default);
        Check(Released(), "Client damage unlocks immediately before server reply");
        Arm(1);
        state.PostUpdate();
        Check(Released(), "Client completed countdown unlocks portal tool");
        player.selectedItem = 1;
        player.inventory[1] = new Item(ItemID.CopperShortsword);
        Arm();
        Call(typeof(PortalPlayer), "Cancel", state);
        Check(player.itemAnimation == 600 && player.itemTime == 600 && player.channel && player.controlUseItem,
            "Portal cancellation does not interrupt a different selected item");
        player.itemAnimation = player.itemTime = 0;
        player.channel = player.controlUseItem = false;
        Main.netMode = NetmodeID.SinglePlayer;
    }

    private void CheckBulkCraft(Player player)
    {
        using var popup = new MonoMod.RuntimeDetour.Hook(typeof(PopupText).GetMethod(nameof(PopupText.NewText),
            new[] { typeof(PopupTextContext), typeof(Item), typeof(int), typeof(bool), typeof(bool) }),
            (PopupHook)((original, context, item, stack, noStack, longText) => -1));
        Main.netMode = NetmodeID.SinglePlayer;
        Main.myPlayer = player.whoAmI;
        player.chest = -1;
        foreach (Item item in player.inventory) item.TurnToAir();
        foreach (Item item in player.bank4.item) item.TurnToAir();
        Recipe recipe = Main.recipe.First(r => r != null && r.createItem.type == ItemID.Torch &&
            r.requiredItem.Any(i => i.type == ItemID.Wood) && r.requiredItem.Any(i => i.type == ItemID.Gel));
        void Prepare(int amount, bool shift, int cursorStack = 0)
        {
            player.inventory[0] = new Item(ItemID.Wood, amount);
            player.inventory[1] = new Item(ItemID.Gel, amount);
            Main.mouseItem = cursorStack == 0 ? new Item() : new Item(ItemID.Torch, cursorStack);
            Main.keyState = shift ? new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.LeftShift) : default;
            Recipe.FindRecipes();
        }
        Prepare(20, false);
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == 3 && player.inventory[0].stack == 19, "Normal click crafts one recipe batch");
        Prepare(20, true);
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == 30 && player.inventory[0].stack == 10, "Shift crafts ten recipe batches with real ingredient consumption");
        Prepare(4, true);
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == 12 && player.inventory[0].IsAir && player.inventory[1].IsAir,
            "Shift stops at available ingredients without free crafts");
        Prepare(20, true, new Item(ItemID.Torch).maxStack - 6);
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == Main.mouseItem.maxStack && player.inventory[0].stack == 18,
            "Shift stops when cursor stack fills without losing ingredients");
        int bombType = ModContent.Find<ModItem>("PvPAdventure/PowerBomb").Type;
        var bombs = Main.recipe.Where(r => r != null && !r.Disabled && r.createItem.type == bombType).ToArray();
        Check(bombs.Length == 2 && bombs.All(r => r.createItem.stack == 5), "Both Power Bomb recipes produce five bombs");
        recipe = bombs.Single(r => r.requiredItem.Any(i => i.type == ItemID.CobaltBar));
        player.adjTile[TileID.Anvils] = true;
        foreach (var item in player.inventory) item.TurnToAir();
        player.inventory[0] = new Item(ItemID.Bomb, 50);
        player.inventory[1] = new Item(ItemID.CobaltBar, 10);
        Main.mouseItem = new Item();
        Main.keyState = default;
        Recipe.FindRecipes();
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == 5 && player.inventory[0].stack == 45 && player.inventory[1].stack == 9,
            "Power Bomb craft consumes five bombs and one bar for five outputs");
        Main.mouseItem = new Item();
        Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.LeftShift);
        Recipe.FindRecipes();
        Main.CraftItem(recipe);
        Check(Main.mouseItem.stack == 45 && player.inventory[0].IsAir && player.inventory[1].IsAir,
            "Shift crafts remaining nine Power Bomb batches for forty-five outputs");
        player.adjTile[TileID.Anvils] = false;
        Main.keyState = default;
    }

    private void CheckBulkBuy(Player player)
    {
        Main.netMode = NetmodeID.SinglePlayer;
        Main.myPlayer = player.whoAmI;
        Main.instance.shop[Main.npcShop] = new Chest();
        Main.mouseLeft = true;
        Main.cursorOverride = 0;
        Main.superFastStack = 0;
        var stock = Main.instance.shop[Main.npcShop].item;
        void Buy(int coins, bool shift, int supply = 9999, int cursorStack = 0)
        {
            foreach (var item in player.inventory) item.TurnToAir();
            foreach (var bank in new[] { player.bank, player.bank2, player.bank3, player.bank4 })
                foreach (var item in bank.item) item.TurnToAir();
            player.inventory[0] = new Item(ItemID.CopperCoin, coins);
            stock[0] = new Item(ItemID.Torch, supply) { shopCustomPrice = 1, buyOnce = supply != 9999 };
            Main.mouseItem = cursorStack == 0 ? new Item() : new Item(ItemID.Torch, cursorStack);
            Main.stackSplit = 0;
            Main.keyState = shift ? new Microsoft.Xna.Framework.Input.KeyboardState(Microsoft.Xna.Framework.Input.Keys.LeftShift) : default;
            Call(typeof(Terraria.UI.ItemSlot), "HandleShopSlot", null, stock, 0, false, true);
        }
        Buy(20, false);
        Check(Main.mouseItem.stack == 1 && player.inventory[0].stack == 19, "Normal shop click buys one and pays once");
        Buy(20, true);
        Check(Main.mouseItem.stack == 10 && player.inventory[0].stack == 10, "Shift shop click buys ten and pays ten times");
        Buy(4, true);
        Check(Main.mouseItem.stack == 4 && player.inventory[0].IsAir, "Shift shop purchase stops at available money");
        Buy(20, true, 3);
        Check(Main.mouseItem.stack == 3 && player.inventory[0].stack == 17 && stock[0].IsAir, "Shift shop purchase respects limited stock");
        Buy(20, true, cursorStack: new Item(ItemID.Torch).maxStack - 2);
        Check(Main.mouseItem.stack == Main.mouseItem.maxStack && player.inventory[0].stack == 18, "Shift shop purchase stops at cursor stack limit");
        Main.keyState = default;
        Main.mouseLeft = false;
    }

    private void MeasureSlopes()
    {
        int x = 500, y = 200;
        for (int i = x - 12; i <= x + 12; i++)
            for (int j = y - 6; j <= y + 2; j++)
            {
                Tile tile = Main.tile[i, j];
                tile.ClearEverything();
                if (j >= y + 1) { tile.HasTile = true; tile.TileType = TileID.Stone; }
            }
        Tile slope = Main.tile[x, y];
        slope.HasTile = true;
        slope.TileType = TileID.Stone;
        var region = ModContent.GetInstance<PvPAdventure.Common.Spawnbox.RegionManager>();
        float MeasureFullUpdate(int direction, bool local = false, bool trace = true, bool replay = false)
        {
            var savedPlayer = Main.player[4];
            int savedNetmode = Main.netMode;
            int savedMyPlayer = Main.myPlayer;
            var player = replay ? Player.LoadPlayer("/tmp/pvpa-slope-Reisenberg.plr", false).Player : new Player();
            player.whoAmI = 4;
            player.active = true;
            if (replay)
                Mod.Logger.Info($"SLOPE_REPLAY_PLAYER name={player.name} lifeMax={player.statLifeMax} armor={string.Join(',', player.armor.Select(item => item.type))}");
            Main.player[4] = player;
            Main.netMode = NetmodeID.SinglePlayer;
            if (local) Main.myPlayer = 4;
            try
            {
                slope.Slope = direction == 1 ? SlopeType.SlopeDownRight : SlopeType.SlopeDownLeft;
                player.position = new Vector2(x * 16 + (direction == 1 ? -20 : 16), (y + 1) * 16 - 42);
                float start = player.position.X;
                for (int frame = 0; frame < 40; frame++)
                {
                    player.controlRight = direction == 1;
                    player.controlLeft = direction == -1;
                    player.Update(4);
                    if (trace && (frame < 3 || frame == 39))
                        Mod.Logger.Info($"SLOPE_FULL_FRAME dir={direction} frame={frame} position={player.position} velocity={player.velocity} ghost={player.ghost}");
                }
                return (player.position.X - start) * direction;
            }
            finally
            {
                Main.player[4] = savedPlayer;
                Main.netMode = savedNetmode;
                Main.myPlayer = savedMyPlayer;
            }
        }
        float fullRight = MeasureFullUpdate(1), fullLeft = MeasureFullUpdate(-1);
        Mod.Logger.Info($"SLOPE_FULL_UPDATE runtime={Environment.Version} right={fullRight} left={fullLeft}");
        Check(fullRight > 40f && fullLeft > 40f && fullRight == fullLeft,
            "Full Player.Update crosses exact-corner slopes in both directions");
        if (!Main.dedServ)
        {
            float localRight = MeasureFullUpdate(1, true), localLeft = MeasureFullUpdate(-1, true);
            Mod.Logger.Info($"SLOPE_LOCAL_CLIENT runtime={Environment.Version} right={localRight} left={localLeft}");
            Check(localRight > 40f && localLeft > 40f && localRight == localLeft,
                "Local client Player.Update crosses exact-corner slopes in both directions");
        }
        region.Unload();
        region.Load();
        float reloadedRight = MeasureFullUpdate(1), reloadedLeft = MeasureFullUpdate(-1);
        Mod.Logger.Info($"SLOPE_FULL_REHOOK runtime={Environment.Version} right={reloadedRight} left={reloadedLeft}");
        Check(reloadedRight == fullRight && reloadedLeft == fullLeft,
            "Player collision hook reload preserves full exact-corner movement");
        for (int warmup = 0; warmup < 100; warmup++)
        {
            MeasureFullUpdate(1, trace: false);
            MeasureFullUpdate(-1, trace: false);
        }
        System.Threading.Thread.Sleep(2000);
        float warmRight = MeasureFullUpdate(1), warmLeft = MeasureFullUpdate(-1);
        Mod.Logger.Info($"SLOPE_FULL_WARM runtime={Environment.Version} right={warmRight} left={warmLeft}");
        Check(warmRight == fullRight && warmLeft == fullLeft,
            "Warm runtime preserves full exact-corner movement after 8000 player frames");
        if (File.Exists("/tmp/pvpa-slope-Reisenberg.plr"))
        {
            float replayRight = MeasureFullUpdate(1, replay: true), replayLeft = MeasureFullUpdate(-1, replay: true);
            region.Unload();
            float replayVanillaRight, replayVanillaLeft;
            try
            {
                replayVanillaRight = MeasureFullUpdate(1, replay: true);
                replayVanillaLeft = MeasureFullUpdate(-1, replay: true);
            }
            finally { region.Load(); }
            Mod.Logger.Info($"SLOPE_REPLAY runtime={Environment.Version} mod right={replayRight} left={replayLeft}; no region right={replayVanillaRight} left={replayVanillaLeft}");
            Check(replayRight > 40f && replayLeft > 40f && replayRight == replayVanillaRight && replayLeft == replayVanillaLeft,
                "Saved player equipment crosses exact-corner slopes with and without region hooks");
        }
        float Measure(int direction, bool touching = false, float offsetX = 0)
        {
            slope.Slope = direction == 1 ? SlopeType.SlopeDownRight : SlopeType.SlopeDownLeft;
            Vector2 position = new(x * 16 + (direction == 1 ? (touching ? -20 : -60) : (touching ? 16 : 56)) + offsetX,
                (y + 1) * 16 - 42);
            float start = position.X, step = 1, offset = 0;
            for (int frame = 0; frame < 40; frame++)
            {
                Vector2 velocity = new(direction * (touching ? Math.Min(3f, (frame + 1) * 0.08f) : 3f), 0.4f);
                Collision.StepUp(ref position, ref velocity, 20, 42, ref step, ref offset);
                var player = new Player { position = position, velocity = velocity, gravity = 0.4f };
                player.DryCollision(false, false);
                position = player.position;
                velocity = player.velocity;
                Vector4 result = Collision.SlopeCollision(position, velocity, 20, 42, 0.4f);
                position = new Vector2(result.X, result.Y);
            }
            return (position.X - start) * direction;
        }
        float modRight = Measure(1), modLeft = Measure(-1);
        float cornerModRight = Measure(1, true), cornerModLeft = Measure(-1, true);
        region.Unload();
        float vanillaRight, vanillaLeft;
        float cornerVanillaRight, cornerVanillaLeft;
        try
        {
            vanillaRight = Measure(1); vanillaLeft = Measure(-1);
            cornerVanillaRight = Measure(1, true); cornerVanillaLeft = Measure(-1, true);
        }
        finally { region.Load(); }
        Mod.Logger.Info($"SLOPE_MEASURE mod right={modRight} left={modLeft}; vanilla right={vanillaRight} left={vanillaLeft}");
        Mod.Logger.Info($"SLOPE_CONTACT runtime={Environment.Version} mod right={cornerModRight} left={cornerModLeft}; vanilla right={cornerVanillaRight} left={cornerVanillaLeft}");
        Check(modRight == vanillaRight && modLeft == vanillaLeft, "Mod region collision matches vanilla on mirrored wilderness slopes");
        foreach (float offset in new[] { 0f, 0.01f, -0.01f, 0.5f, -0.5f })
        {
            modRight = Measure(1, true, offset);
            modLeft = Measure(-1, true, -offset);
            region.Unload();
            try { vanillaRight = Measure(1, true, offset); vanillaLeft = Measure(-1, true, -offset); }
            finally { region.Load(); }
            Mod.Logger.Info($"SLOPE_CORNER runtime={Environment.Version} offset={offset} mod right={modRight} left={modLeft}; vanilla right={vanillaRight} left={vanillaLeft}");
            Check(modRight == vanillaRight && modLeft == vanillaLeft, $"Exact-corner slope collision preserves vanilla movement at offset {offset}");
        }
        var regions = (System.Collections.Generic.List<PvPAdventure.Common.Spawnbox.RegionManager.Region>)region.Regions;
        var boundary = new PvPAdventure.Common.Spawnbox.RegionManager.Region
        {
            Area = new Rectangle(700, 100, 10, 10), CanEnter = false, CanExit = true
        };
        regions.Add(boundary);
        Vector2 Move(Vector2 position, Vector2 velocity, int fluid = 0)
        {
            var player = new Player { position = position, velocity = velocity };
            if (fluid == 1) player.WaterCollision(false, false);
            else if (fluid == 2) player.HoneyCollision(false, false);
            else if (fluid == 3) player.ShimmerCollision(false, false, false);
            else player.DryCollision(false, false);
            return player.velocity;
        }
        try
        {
            Vector2 leftOutside = new(699 * 16 - 20, 102 * 16);
            Check(Move(leftOutside, new Vector2(2, 0)).X == 0,
                "Region collision still blocks forbidden entry");
            Check(Move(leftOutside, new Vector2(32, 0)).X == 0,
                "High-speed player collision blocks forbidden entry");
            Check(Move(new Vector2(704 * 16, 99 * 16 - 42), new Vector2(0, 2)).Y == 0,
                "Player collision blocks vertical entry");
            for (int fluid = 1; fluid <= 3; fluid++)
                Check(Move(leftOutside, new Vector2(2, 0), fluid).X == 0,
                    $"Fluid collision path {fluid} blocks forbidden entry");
            boundary.CanEnter = true;
            Check(Move(leftOutside, new Vector2(2, 0)).X == 2,
                "Region collision permits allowed entry");
            for (int fluid = 1; fluid <= 3; fluid++)
                Check(Move(leftOutside, new Vector2(2, 0), fluid).X == 2,
                    $"Fluid collision path {fluid} permits allowed entry");
            boundary.CanExit = false;
            Check(Move(new Vector2(700 * 16, 102 * 16), new Vector2(-2, 0)).X == 0 &&
                Move(new Vector2(710 * 16 - 20, 102 * 16), new Vector2(2, 0)).X == 0,
                "Region collision preserves both inner exit boundaries");
            Check(Move(new Vector2(704 * 16, 100 * 16), new Vector2(0, -2)).Y == 0 &&
                Move(new Vector2(704 * 16, 110 * 16 - 42), new Vector2(0, 2)).Y == 0,
                "Player collision preserves upper and lower inner exit boundaries");
            boundary.CanExit = true;
            Check(Move(new Vector2(710 * 16 - 20, 102 * 16), new Vector2(2, 0)).X == 2,
                "Region collision permits allowed exit");
        }
        finally { regions.Remove(boundary); }
    }

    private void CheckAdminPermissions(Player owner, Player guest)
    {
        Main.netMode = NetmodeID.Server;
        using var transport = new MonoMod.RuntimeDetour.Hook(typeof(ModPacket).GetMethod(nameof(ModPacket.Send)),
            (PacketHook)((original, packet, toClient, ignoreClient) => { }));
        Type permissions = typeof(PvPAdventure.Common.Security.AdminPermissions);
        string parsedPassword = null;
        using (var commandHook = new MonoMod.RuntimeDetour.Hook(
            typeof(PvPAdventure.Common.Security.AdminPermissions.AdminCommand).GetMethod("Action"),
            (CommandHook)((original, command, caller, input, args) => parsedPassword = args.Single())))
        {
            Call(typeof(CommandLoader), "HandleCommand", null, "/pvpadmin AbC012dEf", new TestChatCaller());
        }
        Check(parsedPassword == "AbC012dEf", "Real chat command parser preserves administrator password case");
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(guest.whoAmI), "Guest starts without administrator permission");
        string password = File.ReadAllText(Path.Combine(Main.SavePath, "PvPAdventure", "AdminPassword.txt")).Trim();
        Check(!(bool)Call(permissions, "Authenticate", null, "wrong") && (bool)Call(permissions, "Authenticate", null, password),
            "Server-only administrator password rejects invalid credentials and accepts stored credentials");
        var game = ModContent.GetInstance<GameManager>();
        game.CurrentPhase = GameManager.Phase.Playing;
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)GameTimerNetHandler.GameTimerPacketType.EndGame);
            stream.Position = 0;
            GameTimerNetHandler.HandlePacket(new BinaryReader(stream), guest.whoAmI);
        }
        Check(game.CurrentPhase == GameManager.Phase.Playing, "Guest cannot end a running match by sending a packet");
        game.EndGame();
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)GameTimerNetHandler.GameTimerPacketType.StartGame);
            writer.Write(3600);
            writer.Write(10);
            stream.Position = 0;
            GameTimerNetHandler.HandlePacket(new BinaryReader(stream), guest.whoAmI);
            Check(!game._startGameCountdown.HasValue && stream.Position == stream.Length,
                "Guest cannot start a match and rejected packet is fully consumed");
        }
        game.CurrentPhase = GameManager.Phase.Playing;
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)GameTimerNetHandler.GameTimerPacketType.PauseGame);
            stream.Position = 0;
            GameTimerNetHandler.HandlePacket(new BinaryReader(stream), guest.whoAmI);
        }
        Check(!ModContent.GetInstance<PauseManager>().IsPaused, "Guest cannot pause a match");
        int timeBefore = game.TimeRemaining;
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)GameTimerNetHandler.GameTimerPacketType.AdjustGameTime);
            writer.Write(1234);
            stream.Position = 0;
            GameTimerNetHandler.HandlePacket(new BinaryReader(stream), guest.whoAmI);
        }
        Check(game.TimeRemaining == timeBefore, "Guest cannot change match duration");
        using (MemoryStream stream = new())
        {
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)2);
            writer.Write((byte)1);
            writer.Write(guest.whoAmI);
            stream.Position = 0;
            Call(permissions, "Handle", null, new BinaryReader(stream), guest.whoAmI);
            Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(guest.whoAmI) && stream.Position == stream.Length,
                "Forged client permission sync cannot grant administrator access");
        }
        string[] configNames = { "ServerConfig", "SSCConfig", "IndependentConfig", "SpectatorConfig", "ArenasConfig" };
        foreach (string name in configNames)
        {
            Type configType = typeof(IndependentConfig).Assembly.GetType("PvPAdventure.Core.Config." + name);
            var config = (Terraria.ModLoader.Config.ModConfig)typeof(ModContent).GetMethod("GetInstance")
                .MakeGenericMethod(configType).Invoke(null, null);
            Terraria.Localization.NetworkText message = null;
            Check(!config.AcceptClientChanges(config, guest.whoAmI, ref message), "Guest cannot edit " + name);
        }
        ModContent.GetInstance<IndependentConfig>().StartingShopEnabled = false;
        Check(((string)Call(typeof(KitShopSystem), "BlockReason", null, owner)).Contains("disabled"), "Disabled starting shop rejects access");
        PvPAdventure.Common.SSC.StartingItems.ApplyStartItems(guest);
        PvPAdventure.Common.SSC.StartingItems.ApplyStartItems(guest);
        Check(guest.inventory.Count(item => item.type == ModContent.ItemType<PortalCreatorItem>()) == 1,
            "Starting items grant one portal creator even when starting shop is disabled");
        ModContent.GetInstance<IndependentConfig>().StartingShopEnabled = true;
        Type spectator = typeof(IndependentConfig).Assembly.GetType("PvPAdventure.Common.Spectator.SpectatorSystem");
        Type handler = typeof(IndependentConfig).Assembly.GetType("PvPAdventure.Common.Spectator.SpectatorNetHandler");
        void Mode(int sender)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)2);
            writer.Write(guest.whoAmI);
            writer.Write((byte)1);
            stream.Position = 0;
            handler.GetMethod("Receive").Invoke(null, new object[] { new BinaryReader(stream), sender });
        }
        Mode(guest.whoAmI);
        Check((byte)spectator.GetMethod("GetMode").Invoke(null, new object[] { guest.whoAmI }) == 0,
            "Guest cannot switch themselves into spectator mode");
        guest.ghost = true;
        guest.GetModPlayer<PvPAdventure.Common.Security.AdminPermissions.AdminPlayer>().PreUpdate();
        Check(!guest.ghost, "Normal guest cannot keep ghost movement enabled");
        Call(permissions, "Grant", null, owner.whoAmI, true);
        Check(PvPAdventure.Common.Security.AdminPermissions.HasAccess(owner.whoAmI), "Server grants administrator permission");
        Mode(owner.whoAmI);
        Check((byte)spectator.GetMethod("GetMode").Invoke(null, new object[] { guest.whoAmI }) == 1,
            "Administrator may assign spectator mode");
        Terraria.Localization.NetworkText adminMessage = null;
        Check(ModContent.GetInstance<IndependentConfig>().AcceptClientChanges(ModContent.GetInstance<IndependentConfig>(), owner.whoAmI, ref adminMessage),
            "Administrator may change server configuration");
        owner.GetModPlayer<PvPAdventure.Common.Security.AdminPermissions.AdminPlayer>().PlayerDisconnect();
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(owner.whoAmI), "Disconnect revokes session administrator permission");
        void Login(int sender, string credential)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, true);
            writer.Write((byte)1);
            writer.Write(credential);
            stream.Position = 0;
            Call(permissions, "Handle", null, new BinaryReader(stream), sender);
        }
        Login(owner.whoAmI, "wrong");
        Check(!PvPAdventure.Common.Security.AdminPermissions.HasAccess(owner.whoAmI), "Invalid login packet cannot grant administrator access");
        Login(guest.whoAmI, password);
        Check(PvPAdventure.Common.Security.AdminPermissions.HasAccess(guest.whoAmI), "Valid login packet grants administrator access");
        Call(permissions, "Grant", null, guest.whoAmI, false);
    }

    private void CheckGrappleJump(Player player)
    {
        int originalHeight = Player.jumpHeight;
        float originalSpeed = Player.jumpSpeed;
        Player.jumpHeight = 15;
        Player.jumpSpeed = 5.01f;
        Projectile hook = Main.projectile[990];
        hook.SetDefaults(ProjectileID.Hook);
        hook.owner = player.whoAmI;
        hook.ai[0] = 2;
        hook.Center = player.Center;
        player.grappling[0] = hook.whoAmI;
        player.grapCount = 1;
        player.controlJump = player.releaseJump = true;
        player.controlDown = false;
        player.velocity = Vector2.Zero;
        player.GrappleMovement();
        Mod.Logger.Info($"Measured grapple jump: jump={player.jump}, jumpHeight={Player.jumpHeight}, velocityY={player.velocity.Y}");
        Check(player.jump == Player.jumpHeight && player.velocity.Y == -Player.jumpSpeed,
            "Attached hook starts full jump duration with unchanged launch speed");
        Check(player.grappling[0] == -1 && !player.releaseJump, "Jump releases hooks and consumes jump press");
        player.gravDir = 1;
        player.JumpMovement();
        Check(player.jump == Player.jumpHeight - 1 && player.velocity.Y == -Player.jumpSpeed,
            "Held jump consumes full duration through vanilla jump movement");
        player.controlJump = false;
        player.JumpMovement();
        Check(player.jump == 0, "Releasing jump still cancels sustained ascent");
        hook.active = true;
        hook.Center = player.Center;
        player.grappling[0] = hook.whoAmI;
        player.grapCount = 1;
        player.controlJump = player.releaseJump = player.controlDown = true;
        player.velocity = Vector2.Zero;
        player.GrappleMovement();
        Check(player.jump == 0 && player.grappling[0] == -1,
            "Down plus jump still detaches without starting a full jump");
        player.controlDown = false;
        hook.active = true;
        hook.Center = player.Center + new Vector2(32, -64);
        player.grappling[0] = hook.whoAmI;
        player.grapCount = 1;
        player.controlJump = player.releaseJump = true;
        player.velocity = Vector2.Zero;
        player.jump = 0;
        player.GrappleMovement();
        Mod.Logger.Info($"Measured blocked grapple jump: jump={player.jump}, velocityY={player.velocity.Y}");
        Check(player.jump == Player.jumpHeight && player.velocity.Y == -Player.jumpSpeed,
            "Blocked stationary player jumps despite upward hook pull");
        CheckHookCase(player, hook, new Vector2(1.99f, 0), new Vector2(32, -64), false, false, false, true,
            "Slow movement below two permits grapple jump");
        CheckHookCase(player, hook, new Vector2(2, 0), new Vector2(32, -64), false, false, false, false,
            "Speed of two does not start grapple jump even with zero vertical speed");
        CheckHookCase(player, hook, new Vector2(5, -5), new Vector2(32, -64), false, false, true, false,
            "Up does not force a jump while moving quickly");
        CheckHookCase(player, hook, new Vector2(5, 0), new Vector2(32, -64), true, false, false, true,
            "Floating in water permits jump despite fast horizontal movement");
        CheckHookCase(player, hook, new Vector2(5, 0.02f), new Vector2(32, -64), true, false, false, true,
            "Water tolerance follows double conversion of incoming float velocity");
        CheckHookCase(player, hook, new Vector2(5, 0.021f), new Vector2(32, -64), true, false, false, false,
            "Vertical water movement outside tolerance only detaches");
        CheckHookCase(player, hook, Vector2.Zero, new Vector2(32, 64), false, false, false, false,
            "Downward hook pull from standing only detaches without up");
        CheckHookCase(player, hook, Vector2.Zero, new Vector2(32, 64), false, false, true, true,
            "Up permits jumping off downward hook pull from standing");
        CheckHookCase(player, hook, new Vector2(0, 1), new Vector2(32, 64), false, false, false, true,
            "Slow falling may jump despite downward hook pull");
        CheckHookCase(player, hook, Vector2.Zero, new Vector2(32, -64), false, true, true, false,
            "Down still prevents jump even when up is also pressed");
        player.controlUp = player.controlDown = player.wet = false;
        Player.jumpHeight = originalHeight;
        Player.jumpSpeed = originalSpeed;
    }

    private void CheckHookCase(Player player, Projectile hook, Vector2 incoming, Vector2 anchor,
        bool wet, bool down, bool up, bool expectedJump, string name)
    {
        hook.active = true;
        hook.ai[0] = 2;
        hook.Center = player.Center + anchor;
        player.grappling[0] = hook.whoAmI;
        player.grapCount = 1;
        player.controlJump = player.releaseJump = true;
        player.controlDown = down;
        player.controlUp = up;
        player.wet = wet;
        player.velocity = incoming;
        player.jump = 0;
        player.GrappleMovement();
        Mod.Logger.Info($"Measured {name}: incoming={incoming}, jump={player.jump}, velocityY={player.velocity.Y}");
        Check(player.jump == (expectedJump ? Player.jumpHeight : 0) && player.grappling[0] == -1 &&
            !player.releaseJump && (!expectedJump || player.velocity.Y == -Player.jumpSpeed), name);
    }

    private void CheckPowerBomb(Player owner, Player teammate, Player enemy)
    {
        Type bombType = typeof(TeamBossNPC).Assembly.GetType("PvPAdventure.Content.Projectiles.PowerBombProjectile");
        int itemType = ModContent.Find<ModItem>("PvPAdventure/PowerBomb").Type;
        Check(ModContent.GetInstance<IndependentConfig>().StartingShop.Any(entry => entry.Item.Type == itemType && entry.Price == 100000),
            "Power bomb is available for the upstream 10-gold starting price");
        int x = Main.spawnTileX + 80, y = Main.spawnTileY + 50;
        Vector2 center = new(x * 16 + 8, y * 16 + 8);
        foreach (Player player in new[] { owner, teammate, enemy })
        {
            player.active = true;
            player.dead = player.ghost = player.immune = false;
            player.hostile = true;
            player.Center = center;
            player.statLife = player.statLifeMax = player.statLifeMax2 = 1000;
            player.buffType = new int[Player.MaxBuffs];
            player.buffTime = new int[Player.MaxBuffs];
            player.buffImmune = new bool[BuffLoader.BuffCount];
        }
        int bombId = Projectile.NewProjectile(owner.GetSource_Misc("PowerBombChecks"), center, Vector2.Zero,
            ModContent.Find<ModProjectile>("PvPAdventure/PowerBombProjectile").Type, 0, 0, owner.whoAmI);
        Projectile bomb = Main.projectile[bombId];
        Check(bomb.timeLeft == 300 && bomb.ModProjectile.CanDamage() == false, "Five-second fuse does not damage on contact");
        Check((bool)Call(bombType, "CanDamagePlayer", bomb.ModProjectile, owner, owner), "Power bomb can hurt its owner");
        Check(!(bool)Call(bombType, "CanDamagePlayer", bomb.ModProjectile, owner, teammate), "Power bomb protects teammates");
        Check((bool)Call(bombType, "CanDamagePlayer", bomb.ModProjectile, owner, enemy), "Power bomb can hurt nearby enemies");
        enemy.ghost = true;
        Check(!(bool)Call(bombType, "CanDamagePlayer", bomb.ModProjectile, owner, enemy), "Power bomb does not hurt spectators");
        enemy.ghost = false;
        enemy.Center = new Vector2(Main.spawnTileX * 16, Main.spawnTileY * 16);
        bomb.Center = enemy.Center;
        Check(!(bool)Call(bombType, "CanDamagePlayer", bomb.ModProjectile, owner, enemy), "Power bomb respects spawn combat protection");
        bomb.Center = enemy.Center = center;
        Item bombItem = new(itemType);
        Check(!bombItem.ModItem.CanUseItem(owner), "Power bomb cannot be thrown before a match");
        Tile stone = Main.tile[x, y];
        stone.HasTile = true;
        stone.TileType = TileID.Stone;
        Tile brick = Main.tile[x + 1, y];
        brick.HasTile = true;
        brick.TileType = TileID.BlueDungeonBrick;
        Check((bool)Call(bombType, "CanDestroyTile", null, x, y), "Power bomb may destroy ordinary stone");
        Check(!(bool)Call(bombType, "CanDestroyTile", null, x + 1, y), "Power bomb protects dungeon bricks");
        bool[] mechs = { NPC.downedMechBoss1, NPC.downedMechBoss2, NPC.downedMechBoss3 };
        Tile chlorophyte = Main.tile[x + 2, y];
        chlorophyte.HasTile = true;
        chlorophyte.TileType = TileID.Chlorophyte;
        NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = false;
        Check(!(bool)Call(bombType, "CanDestroyTile", null, x + 2, y), "Power bomb protects chlorophyte before all mechs");
        NPC.downedMechBoss1 = NPC.downedMechBoss2 = NPC.downedMechBoss3 = true;
        Check((bool)Call(bombType, "CanDestroyTile", null, x + 2, y), "Power bomb unlocks chlorophyte after all mechs");
        NPC.downedMechBoss1 = mechs[0]; NPC.downedMechBoss2 = mechs[1]; NPC.downedMechBoss3 = mechs[2];
        Main.netMode = NetmodeID.MultiplayerClient;
        bomb.ModProjectile.OnKill(0);
        Check(Main.tile[x, y].HasTile && owner.statLife == 1000, "Client explosion does not destroy tiles or apply player damage");
        Main.netMode = NetmodeID.Server;
        bomb.ModProjectile.OnKill(0);
        Check(!Main.tile[x, y].HasTile && Main.tile[x + 1, y].HasTile, "Authoritative explosion destroys stone but keeps protected bricks");
        Check(owner.statLife < 1000 && enemy.statLife < 1000 && teammate.statLife == 1000,
            "Authoritative explosion damages owner and enemy once, not teammates");
        var explosion = Main.projectile.Single(projectile => projectile.active && projectile.type == ModContent.Find<ModProjectile>("PvPAdventure/PowerBombExplosion").Type);
        Check(explosion.Center == center && !explosion.ModProjectile.CanHitPvp(enemy), "Explosion is centered and cannot double-hit players");
        int stickyId = Projectile.NewProjectile(owner.GetSource_Misc("PowerBombChecks"), center, Vector2.One,
            ModContent.Find<ModProjectile>("PvPAdventure/StickyPowerBombProjectile").Type, 0, 0, owner.whoAmI);
        var sticky = Main.projectile[stickyId];
        sticky.ModProjectile.OnTileCollide(Vector2.One);
        Check(sticky.ai[0] == 1 && sticky.velocity == Vector2.Zero && !sticky.tileCollide,
            "Sticky bomb uses synchronized attachment state");
    }
}
