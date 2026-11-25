using System;
using System.IO;
using PvPAdventure.Common.Fishing;
using PvPAdventure.Common.Game.GameReporters;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

public sealed class GameManager : ModSystem
{
    internal const int DefaultDurationMinutes = 180;
    internal const int MaxDurationMinutes = 24 * 60;

    private static AdventureMatchReportingSystem Reporting => ModContent.GetInstance<AdventureMatchReportingSystem>();
    private int durationTicks;
    public enum Phase : byte { Waiting, Playing, Inactive }
    public Phase CurrentPhase { get; private set; } = Phase.Inactive;
    public bool IsSelected => CurrentPhase != Phase.Inactive;
    public bool IsRunning => CurrentPhase == Phase.Playing || CountdownTicks > 0;
    public bool IsPaused { get; private set; }
    public int CountdownTicks { get; private set; }
    public int RemainingTicks { get; private set; }
    internal string CurrentMatchToken => Reporting.CurrentMatchToken;

    public override void OnWorldLoad()
    {
        CurrentPhase = Phase.Waiting;
        AdventureWorldRules.EnterLobby();
    }

    public override void OnWorldUnload()
    {
        CurrentPhase = Phase.Inactive;
        CountdownTicks = RemainingTicks = 0;
        IsPaused = false;
    }

    internal bool Start(int minutes, int countdownSeconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsSelected || IsRunning ||
            minutes is < 1 or > MaxDurationMinutes || countdownSeconds is < 0 or > 60)
            return false;

        durationTicks = minutes * 3600;
        CountdownTicks = countdownSeconds * 60;
        if (CountdownTicks == 0)
            Begin();
        else
            Sync();
        return true;
    }

    private void Begin()
    {
        CurrentPhase = Phase.Playing;
        RemainingTicks = durationTicks;
        AdventureWorldRules.SetTimeFrozen(false);
        AnglerQuestSystem.BeginMatch();
        Reporting.Begin();
        Sync();
    }

    internal bool TogglePause()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsRunning)
            return false;

        IsPaused = !IsPaused;
        if (CurrentPhase == Phase.Playing)
            AdventureWorldRules.SetTimeFrozen(IsPaused);
        Sync();
        return true;
    }

    internal bool End()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsRunning)
            return false;

        bool wasPlaying = CurrentPhase == Phase.Playing;
        CurrentPhase = Phase.Waiting;
        CountdownTicks = RemainingTicks = 0;
        IsPaused = false;
        if (wasPlaying)
        {
            AnglerQuestSystem.EndMatch();
            Reporting.Complete();
            AdventureWorldRules.EnterLobby();
            AdventureWorldRules.ReturnPlayersToSpawn();
        }
        Sync();
        return true;
    }

    public override void PostUpdateEverything()
    {
        bool authority = Main.netMode != NetmodeID.MultiplayerClient;
        if (!IsPaused)
        {
            if (CountdownTicks > 0 && --CountdownTicks == 0 && authority)
                Begin();
            else if (CurrentPhase == Phase.Playing && RemainingTicks > 0 && --RemainingTicks == 0 && authority)
                End();
        }

        if (authority)
            Reporting.Tick();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write((byte)CurrentPhase);
        writer.Write(IsPaused);
        writer.Write(CountdownTicks);
        writer.Write(RemainingTicks);
    }

    public override void NetReceive(BinaryReader reader)
    {
        CurrentPhase = (Phase)reader.ReadByte();
        IsPaused = reader.ReadBoolean();
        CountdownTicks = reader.ReadInt32();
        RemainingTicks = reader.ReadInt32();
    }

    private static void Sync()
    {
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.WorldData);
    }
}
