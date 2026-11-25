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
    public override string Usage => "/pvpmatch start [minutes] [countdown seconds] | pause | end | points <team> <value>";

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
        int countdown = 10;
        if (args.Length is < 1 or > 3 ||
            (args.Length >= 2 && (args[0] != "start" || !int.TryParse(args[1], out minutes))) ||
            (args.Length == 3 && !int.TryParse(args[2], out countdown)) ||
            minutes is < 1 or > GameManager.MaxDurationMinutes || countdown is < 0 or > 60)
        {
            caller.Reply(Usage);
            return;
        }

        switch (args[0])
        {
            case "start": Request(ActionKind.Start, minutes, countdown, caller); break;
            case "pause": Request(ActionKind.Pause, caller: caller); break;
            case "end": Request(ActionKind.End, caller: caller); break;
            default: caller.Reply(Usage); break;
        }
    }

    internal static void Request(ActionKind action, int minutes = 0, int countdown = 0, CommandCaller caller = null)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            bool changed = Apply(action, minutes, countdown);
            caller?.Reply(changed ? "Match control accepted." : "That match control is unavailable right now.");
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

    private static bool Apply(ActionKind action, int minutes, int countdown)
    {
        GameManager game = ModContent.GetInstance<GameManager>();
        return action switch
        {
            ActionKind.Start => game.Start(minutes, countdown),
            ActionKind.Pause => game.TogglePause(),
            ActionKind.End => game.End(),
            _ => false
        };
    }
}
