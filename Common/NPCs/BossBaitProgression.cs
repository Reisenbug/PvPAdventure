using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

public sealed class BossBaitProgression : GlobalNPC
{
    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient && !NPC.downedGolemBoss &&
            npc.type is NPCID.TruffleWorm or NPCID.EmpressButterfly)
        {
            npc.active = false;
            npc.life = 0;
            npc.netUpdate = true;
        }
    }

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (!NPC.downedGolemBoss)
        {
            pool.Remove(NPCID.TruffleWorm);
            pool.Remove(NPCID.EmpressButterfly);
            return;
        }

        if (!Main.dayTime && spawnInfo.Player.ZoneHallow && spawnInfo.SpawnTileY <= Main.worldSurface &&
            spawnInfo.SpawnTileType is TileID.HallowedGrass or TileID.Pearlstone or TileID.Pearlsand or TileID.HallowedIce)
            pool[NPCID.EmpressButterfly] = 0.05f;

        if (spawnInfo.Player.ZoneGlowshroom && spawnInfo.SpawnTileY > Main.worldSurface &&
            spawnInfo.SpawnTileType is TileID.MushroomGrass or TileID.Mud)
            pool[NPCID.TruffleWorm] = 0.2f;
    }
}
