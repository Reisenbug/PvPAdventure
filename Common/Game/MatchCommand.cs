using System.IO;
using PvPAdventure.Common.Statistics;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

public sealed class MatchCommand : ModCommand
{
    internal enum ActionKind : byte { Start, Pause, End }

    public override string Command => "pvpmatch";
    public override CommandType Type => CommandType.Chat | CommandType.Console;
    public override string Usage => "/pvpmatch start [minutes] | pause | end | points <team> <value>";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (args.Length == 3 && args[0] == "points" &&
            System.Enum.TryParse(args[1], ignoreCase: true, out Team team) &&
            team != Team.None && System.Enum.IsDefined(team) && int.TryParse(args[2], out int points))
        {
            if (caller.CommandType == CommandType.Console)
                ModContent.GetInstance<PointsManager>().SetTeamPoints(team, points);
            else
                TeamPointsNetHandler.Set(team, points);
            return;
        }

        int minutes = GameManager.DefaultDurationMinutes;
        if (args.Length is < 1 or > 2 ||
            (args.Length == 2 && (args[0] != "start" || !int.TryParse(args[1], out minutes))) ||
            minutes is < 1 or > GameManager.MaxDurationMinutes)
        {
            caller.Reply(Usage);
            return;
        }

        switch (args[0])
        {
            case "start": Request(ActionKind.Start, minutes, 10); break;
            case "pause": Request(ActionKind.Pause); break;
            case "end": Request(ActionKind.End); break;
            default: caller.Reply(Usage); break;
        }
    }

    internal static void Request(ActionKind action, int minutes = 0, int countdown = 0)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            Apply(action, minutes, countdown);
            return;
        }

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)Core.Net.AdventurePacketIdentifier.MatchControl);
        packet.Write((byte)action);
        packet.Write(minutes);
        packet.Write(countdown);
        packet.Send();
    }

    internal static void Handle(BinaryReader reader, int sender)
    {
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers ||
            Main.player[sender]?.active != true)
            return;

        Apply((ActionKind)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32());
    }

    private static void Apply(ActionKind action, int minutes, int countdown)
    {
        GameManager game = ModContent.GetInstance<GameManager>();
        switch (action)
        {
            case ActionKind.Start: game.Start(minutes, countdown); break;
            case ActionKind.Pause: game.TogglePause(); break;
            case ActionKind.End: game.End(); break;
        }
    }
}
