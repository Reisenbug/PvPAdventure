using PvPAdventure.Common.Statistics;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.GameTimer;

public static class GameTimerNetHandler
{
    public enum GameTimerPacketType : byte
    {
        PauseGame,
        StartGame,
        AdjustGameTime,
        EndGame,
        SetPoints
    }

    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        var subPacket = (GameTimerPacketType)reader.ReadByte();

        if (Main.netMode == NetmodeID.Server && !Common.Security.AdminPermissions.HasAccess(whoAmI))
        {
            switch (subPacket)
            {
                case GameTimerPacketType.StartGame:
                    reader.ReadInt32();
                    reader.ReadInt32();
                    break;
                case GameTimerPacketType.AdjustGameTime:
                    reader.ReadInt32();
                    break;
                case GameTimerPacketType.SetPoints:
                    reader.ReadByte();
                    reader.ReadInt32();
                    break;
            }
            return;
        }

        switch (subPacket)
        {
            case GameTimerPacketType.PauseGame:
                {
                    if (Main.netMode != NetmodeID.Server)
                    {
                        return;
                    }

                    var pm = ModContent.GetInstance<PauseManager>();
                    pm.TogglePause();

                    return;
                }

            case GameTimerPacketType.StartGame:
                {
                    int time = reader.ReadInt32();
                    int countdown = reader.ReadInt32();

                    if (Main.netMode != NetmodeID.Server)
                    {
                        return;
                    }

                    var gm = ModContent.GetInstance<GameManager>();

                    if (gm.CurrentPhase == GameManager.Phase.Playing)
                    {
                        return;
                    }

                    if (gm._startGameCountdown.HasValue)
                    {
                        return;
                    }

                    gm.StartGame(time, countdown);
                    return;
                }

            case GameTimerPacketType.AdjustGameTime:
                {
                    int deltaFrames = reader.ReadInt32();

                    if (Main.netMode != NetmodeID.Server)
                    {
                        return;
                    }

                    var gm = ModContent.GetInstance<GameManager>();
                    gm.AdjustTimeRemaining(deltaFrames);
                    return;
                }

            case GameTimerPacketType.EndGame:
                {
                    if (Main.netMode != NetmodeID.Server)
                    {
                        return;
                    }

                    var gm = ModContent.GetInstance<GameManager>();
                    gm.EndGame();
                    return;
                }

            case GameTimerPacketType.SetPoints:
                {
                    var team = (Team)reader.ReadByte();
                    int value = reader.ReadInt32();

                    var pointsManager = ModContent.GetInstance<PointsManager>();
                    pointsManager._points[team] = value;

                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendData(MessageID.WorldData);
                        return;
                    }

                    if (!Main.dedServ)
                    {
                        pointsManager.UiScoreboard?.Invalidate();
                    }

                    return;
                }
        }
    }
}
