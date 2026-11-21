using System;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.NPCs;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace IndependentChecks;

public sealed partial class RuntimeChecks
{
    private void CheckNovemberChanges(Player player)
    {
        CheckMatchPoints();
        CheckScheduledWallOfFlesh();
        CheckBossDisengagement(player);
        var globals = Main.ItemDropsDB.GetRulesForNPCID(NPCID.BlueSlime, true).OfType<CommonDrop>().ToArray();
        foreach (int soul in new[] { ItemID.SoulofLight, ItemID.SoulofNight })
        {
            var rule = globals.Single(drop => drop.itemId == soul);
            Check(rule.chanceNumerator == 1 && rule.chanceDenominator == 3, $"Soul {soul} registered at 1/3");
        }
        Check(globals.Single(drop => drop.itemId == ItemID.PirateMap).chanceDenominator == 100,
            "Soul patch leaves unrelated global drop unchanged");

        Main.hardMode = false;
        CheckBossSpawnPause(player);
        var wall = new NPC();
        wall.SetDefaults(NPCID.WallofFlesh);
        var pause = wall.GetGlobalNPC<BossSpawnPause>();
        pause.OnSpawn(wall, wall.GetSource_FromAI());
        int slot = 0;
        Check(pause.IsPaused && !pause.PreAI(wall) && wall.velocity == Vector2.Zero &&
            !pause.CanHitPlayer(wall, player, ref slot), "First Wall of Flesh stays still and harmless");
        for (int i = 0; i < 3598; i++) pause.PreAI(wall);
        Check(pause.IsPaused, "Wall of Flesh waits through frame 3599");
        Check(pause.PreAI(wall) && !pause.IsPaused && !wall.dontTakeDamage, "Wall of Flesh resumes at frame 3600");
        Main.hardMode = true;
        wall = new NPC();
        wall.SetDefaults(NPCID.WallofFlesh);
        pause = wall.GetGlobalNPC<BossSpawnPause>();
        pause.OnSpawn(wall, wall.GetSource_FromAI());
        Check(!pause.IsPaused && !wall.dontTakeDamage, "Hardmode Wall of Flesh skips wait");

        var mech = ModContent.GetInstance<MechBossSpawnSystem>();
        Main.dayTime = false;
        Main.time = 0;
        NPC.downedMechBoss1 = NPC.downedMechBoss2 = true;
        NPC.downedMechBoss3 = false;
        foreach (Player p in Main.ActivePlayers) p.ZoneOverworldHeight = false;
        mech.PostSetupContent();
        mech.PostUpdateWorld();
        var announced = typeof(MechBossSpawnSystem).GetField("hasAnnouncedTonight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Check((bool)announced.GetValue(mech), "Mech warning does not require a surface player");
        Main.time = 3600;
        mech.PostUpdateWorld();
        Check(NPC.AnyNPCs(NPCID.SkeletronPrime), "Mech spawns even when the living players are underground");
        var prime = Main.npc.Single(npc => npc.active && npc.type == NPCID.SkeletronPrime);
        Check(Math.Abs(prime.Center.X - (Main.dungeonX * 16 + 8)) < 1 &&
            Math.Abs(prime.Bottom.Y - (Main.dungeonY * 16 - 160)) < 1, "Mech spawns at dungeon entrance, not player");
        int bossCount = Main.npc.Count(npc => npc.active);
        mech.PostUpdateWorld();
        Check(Main.npc.Count(npc => npc.active) == bossCount, "Only one automatic mech spawn per night");
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
        Call(typeof(MechBossSpawnSystem), "SpawnMechanicalBoss", mech, NPCID.Retinazer);
        var eyes = Main.npc.Where(npc => npc.active && npc.type is NPCID.Retinazer or NPCID.Spazmatism).ToArray();
        Check(eyes.Length == 2 && Math.Abs(eyes[0].Center.X - eyes[1].Center.X) == 96,
            "Twins spawn separately on both sides of dungeon entrance");
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;

        var medallion = ModContent.GetInstance<PvPAdventure.Common.Items.PumpkinMoonMedallion>();
        var item = new Item(ItemID.PumpkinMoonMedallion);
        player.ZoneOverworldHeight = true;
        player.ZoneDesert = false;
        player.ZonePurity = true;
        Check(player.ZoneForest && medallion.CanUseItem(item, player), "Medallion accepts night surface forest");
        Main.pumpkinMoon = false;
        Call(typeof(Player), "ItemCheck_UseEventItems", player, item);
        Check(!Main.pumpkinMoon, "Vanilla item event path does not start Pumpkin Moon");
        medallion.UseItem(item, player);
        Check(NPC.AnyNPCs(NPCID.MourningWood) && !Main.pumpkinMoon, "Forest medallion summons Mourning Wood without event");
        Check(!medallion.CanUseItem(item, player), "Medallion rejects duplicate boss");
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
        player.ZoneDesert = true;
        player.ZonePurity = false;
        Check(medallion.CanUseItem(item, player), "Medallion accepts other surface biomes");
        medallion.UseItem(item, player);
        Check(NPC.AnyNPCs(NPCID.Pumpking) && !Main.pumpkinMoon, "Other biome medallion summons Pumpking without event");
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
        Main.dayTime = true;
        Check(!medallion.CanUseItem(item, player), "Medallion rejects daytime");
        Main.dayTime = false;
        player.ZoneOverworldHeight = false;
        Check(!medallion.CanUseItem(item, player), "Medallion rejects underground use");

        var pool = Main.ItemDropsDB.GetRulesForNPCID(NPCID.Pumpking, false).OfType<FewFromRulesRule>().Single();
        Check(pool.amount == 3 && pool.chanceDenominator == 1 && pool.options.Length == 6,
            "Pumpking guarantees three weapons from six options");
        foreach (int weapon in new[] { ItemID.JackOLanternLauncher, ItemID.CandyCornRifle })
        {
            var option = pool.options.OfType<CommonDrop>().Single(drop => drop.itemId == weapon);
            Check(option.ChainedRules.Single().RuleToChain is CommonDrop ammo &&
                ammo.amountDroppedMinimum == 60 && ammo.amountDroppedMaximum == 99,
                $"Pumpking weapon {weapon} supplies 60-99 ammo");
        }
        var woodDrops = Main.ItemDropsDB.GetRulesForNPCID(NPCID.MourningWood, false).OfType<CommonDrop>().ToArray();
        var wood = woodDrops.Single(drop => drop.itemId == ItemID.SpookyWood);
        Check(wood.amountDroppedMinimum == 400 && wood.amountDroppedMaximum == 500 && wood.chanceDenominator == 1,
            "Mourning Wood guarantees 400-500 spooky wood");
        Check(woodDrops.Single(drop => drop.itemId == ItemID.StakeLauncher).chanceDenominator == 2 &&
            woodDrops.Single(drop => drop.itemId == ItemID.NecromanticScroll).chanceDenominator == 2,
            "Mourning Wood weapon and scroll each have 1/2 chance");

        var altar = ModContent.GetInstance<PvPAdventure.Common.Misc.LihzahrdAltarChanges>();
        bool blocked = false;
        Check(!altar.CanKillTile(0, 0, TileID.LihzahrdAltar, ref blocked) &&
            !altar.CanExplode(0, 0, TileID.LihzahrdAltar), "Altar rejects mining and explosions");
        Check(TileID.Sets.PreventsTileRemovalIfOnTopOfIt[TileID.LihzahrdAltar] &&
            TileID.Sets.PreventsTileHammeringIfOnTopOfIt[TileID.LihzahrdAltar], "Altar protects supporting tiles");
        Check(altar.CanKillTile(0, 0, TileID.Stone, ref blocked) && altar.CanExplode(0, 0, TileID.Stone),
            "Altar protection leaves ordinary blocks unchanged");
    }

    private void CheckBossDisengagement(Player player)
    {
        var positions = Main.player.Select(p => p.Center).ToArray();
        bool wasDay = Main.dayTime;
        double time = Main.time;
        try
        {
            Main.dayTime = false;
            foreach (Player activePlayer in Main.ActivePlayers)
                activePlayer.Center = new Vector2(1000);

            var wall = new NPC { active = true };
            wall.SetDefaults(NPCID.WallofFlesh);
            wall.Center = new Vector2(10000);
            var wallBehavior = wall.GetGlobalNPC<BossDespawnRework>();
            Check(!wallBehavior.CheckActive(wall) && !wallBehavior.PreAI(wall) && wall.velocity == Vector2.Zero,
                "Unattended Wall of Flesh stays in place");

            var plant = new NPC { active = true };
            plant.SetDefaults(NPCID.Plantera);
            plant.Center = new Vector2(10000);
            var plantBehavior = plant.GetGlobalNPC<BossDespawnRework>();
            plantBehavior.PreAI(plant);
            plant.Center += new Vector2(200, 0);
            Check(!plantBehavior.PreAI(plant) && plant.velocity == new Vector2(-12, 0),
                "Unattended Plantera moves toward its spawn at 12 pixels per frame");
            player.Center = plant.Center;
            Check(plantBehavior.PreAI(plant) && plant.timeLeft == 3600,
                "Nearby player re-engages Plantera");

            var prime = new NPC();
            prime.SetDefaults(NPCID.SkeletronPrime);
            prime.active = true;
            var mechBehavior = prime.GetGlobalNPC<BossDespawnRework>();
            Main.dayTime = true;
            Main.time = 599;
            mechBehavior.PreAI(prime);
            Check(prime.active, "Mechanical boss remains before the dawn cutoff");
            Main.time = 600;
            mechBehavior.PreAI(prime);
            mechBehavior.PostAI(prime);
            Check(!prime.active, "Mechanical boss disappears at the dawn cutoff");
        }
        finally
        {
            for (int i = 0; i < positions.Length; i++)
                Main.player[i].Center = positions[i];
            Main.dayTime = wasDay;
            Main.time = time;
        }
    }

    private void CheckScheduledWallOfFlesh()
    {
        var progression = ModContent.Find<ModSystem>("PvPAdventure/BespokeProgression");
        bool wasDay = Main.dayTime;
        bool wasHardMode = Main.hardMode;
        try
        {
            progression.ClearWorld();
            Main.hardMode = false;
            Main.dayTime = true;
            progression.PostUpdateWorld();
            Main.dayTime = false;
            progression.PostUpdateWorld();
            Check(!NPC.AnyNPCs(NPCID.WallofFlesh), "No automatic Wall of Flesh on the first night");
            Main.dayTime = true;
            progression.PostUpdateWorld();
            Main.dayTime = false;
            progression.PostUpdateWorld();
            Check(Main.npc.Count(npc => npc.active && npc.type == NPCID.WallofFlesh) == 1,
                "Wall of Flesh spawns on the second night");
            progression.PostUpdateWorld();
            Check(Main.npc.Count(npc => npc.active && npc.type == NPCID.WallofFlesh) == 1,
                "Scheduled Wall of Flesh only spawns once");
        }
        finally
        {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.type == NPCID.WallofFlesh)
                    npc.active = false;
            Main.dayTime = wasDay;
            Main.hardMode = wasHardMode;
            progression.ClearWorld();
        }
    }

    private void CheckMatchPoints()
    {
        var config = ModContent.GetInstance<ServerConfig>();
        var original = config.Points;
        try
        {
            config.Points = new ServerConfig.PointsConfig();
            var points = ModContent.GetInstance<PointsManager>();
            points.ResetForMatch();
            Check(points.Points[Team.Red] == 4 && points.Points[Team.Blue] == 4,
                "Both teams start with four points");

            var boss = new NPC();
            foreach (int type in new[] { NPCID.SkeletronHead, NPCID.WallofFlesh, NPCID.TheDestroyer,
                         NPCID.Retinazer, NPCID.Spazmatism, NPCID.SkeletronPrime, NPCID.EyeofCthulhu })
            {
                boss.SetDefaults(type);
                points.AwardNpcKillToTeam(Team.Red, boss);
                points.AwardNpcKillToTeam(Team.Red, boss);
                points.AwardNpcKillToTeam(Team.Blue, boss);
            }
            Check(points.Points[Team.Red] == 19 && points.Points[Team.Blue] == 19,
                "Both teams get full first-kill points, the Twins count once, and other bosses give zero");

            boss.SetDefaults(NPCID.Plantera);
            points.AwardNpcKillToTeam(Team.Red, boss);
            points.AwardNpcKillToTeam(Team.Red, boss);
            boss.SetDefaults(NPCID.CultistBoss);
            points.AwardNpcKillToTeam(Team.Red, boss);
            points.AwardNpcKillToTeam(Team.Red, boss);
            points.AwardNpcKillToTeam(Team.None, boss);
            Check(points.Points[Team.Red] == 45 && points.Points[Team.None] == 0,
                "Plantera and Cultist can score again, while unassigned players never score");

            points.ResetForMatch();
            Check(points.Points[Team.Red] == 4 && points.DownedNpcs[Team.Red].Count == 0,
                "A new match resets the score and first-kill history");
        }
        finally
        {
            config.Points = original;
            ModContent.GetInstance<PointsManager>().ResetForMatch();
        }
    }
}
