using PvPAdventure.Common.GameTimer;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.World;

public class HardmodeGoblinInvasionSystem : ModSystem
{
    private bool wasHardmode;
    private bool wasDaytime;
    private int remainingDawns = -1;

    public override void ClearWorld()
    {
        wasHardmode = false;
        wasDaytime = Main.dayTime;
        remainingDawns = -1;
    }

    public override void PostUpdateWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || Main.gamePaused ||
            ModContent.GetInstance<PauseManager>().IsPaused)
            return;

        if (!Main.hardMode)
        {
            ClearWorld();
            return;
        }

        if (Main.hardMode && !wasHardmode)
        {
            remainingDawns = NPC.downedGoblins ? -1 : 2;
            wasHardmode = true;
            wasDaytime = Main.dayTime;
        }

        if (remainingDawns > 0 && Main.dayTime && !wasDaytime)
            remainingDawns--;

        if (NPC.downedGoblins)
            remainingDawns = -1;

        if (remainingDawns == 0 && Main.invasionType == 0)
        {
            Main.StartInvasion(InvasionID.GoblinArmy);
            if (Main.invasionType == InvasionID.GoblinArmy)
            {
                remainingDawns = -1;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.WorldData);
            }
        }
        wasDaytime = Main.dayTime;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["wasHardmode"] = wasHardmode;
        tag["remainingDawns"] = remainingDawns;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        wasHardmode = tag.GetBool("wasHardmode");
        remainingDawns = tag.ContainsKey("remainingDawns") ? tag.GetInt("remainingDawns") : -1;
        wasDaytime = Main.dayTime;
    }
}
