using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Game;

internal static class EndScreenService
{
    internal static void Present(EndScreenSummary summary)
    {
        var scores = summary.Scores.OrderByDescending(entry => entry.Score).ToArray();
        var rows = scores.SelectMany(score => summary.Players.Where(player => player.Team == score.Team)
            .Select(player => new ResultRow(player.Team, player.Name, player.Kills, player.Deaths,
                player.DamageDealt, player.RoleTitle, player.RoleValue))).ToArray();
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            ModContent.GetInstance<MatchResultsUI>().Show(scores, rows);
            return;
        }
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.MatchResults);
        packet.Write((byte)scores.Length);
        foreach (TeamScoreEntry score in scores)
        {
            packet.Write((byte)score.Team);
            packet.Write(score.Score);
        }
        packet.Write((byte)rows.Length);
        foreach (ResultRow row in rows)
        {
            packet.Write((byte)row.Team);
            packet.Write(row.Name);
            packet.Write(row.Kills);
            packet.Write(row.Deaths);
            packet.Write(row.Damage);
            packet.Write(row.Title);
            packet.Write(row.Detail);
        }
        packet.Send();
    }

    internal static void Receive(BinaryReader reader)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        TeamScoreEntry[] scores = new TeamScoreEntry[reader.ReadByte()];
        for (int i = 0; i < scores.Length; i++)
            scores[i] = new((Team)reader.ReadByte(), reader.ReadInt32());
        ResultRow[] rows = new ResultRow[reader.ReadByte()];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = new((Team)reader.ReadByte(), reader.ReadString(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadUInt32(), reader.ReadString(), reader.ReadString());
        ModContent.GetInstance<MatchResultsUI>().Show(scores, rows);
    }

    internal readonly record struct ResultRow(Team Team, string Name, int Kills, int Deaths, uint Damage, string Title, string Detail);
}

[Autoload(Side = ModSide.Client)]
internal sealed class MatchResultsUI : ModSystem
{
    private UserInterface ui;
    private GameTime drawTime;

    public override void Load() => ui = new UserInterface();

    internal void Show(IEnumerable<TeamScoreEntry> scores, IEnumerable<EndScreenService.ResultRow> rows)
    {
        UIState state = new();
        UIPanel panel = new() { HAlign = 0.5f, VAlign = 0.45f };
        panel.Width.Set(0, 0.8f);
        panel.MaxWidth.Set(680, 0);
        panel.Height.Set(0, 0.65f);
        panel.OnUpdate += _ =>
        {
            if (panel.IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        };
        state.Append(panel);

        panel.Append(new UIText("Match results", 1.15f));
        UITextPanel<string> close = new("Close") { HAlign = 1f };
        close.OnLeftClick += (_, _) => ui.SetState(null);
        panel.Append(close);

        UIList list = new() { ListPadding = 4, ManualSortMethod = _ => { } };
        list.Top.Set(42, 0);
        list.Width.Set(-22, 1);
        list.Height.Set(-42, 1);
        panel.Append(list);

        UIScrollbar scrollbar = new() { HAlign = 1f };
        scrollbar.Top.Set(42, 0);
        scrollbar.Height.Set(-42, 1);
        panel.Append(scrollbar);
        list.SetScrollbar(scrollbar);

        foreach (TeamScoreEntry score in scores)
        {
            if ((int)score.Team <= 0 || (int)score.Team >= Main.teamColor.Length)
                continue;
            list.Add(new UIText($"{score.Team} team · {score.Score} points") { TextColor = Main.teamColor[(int)score.Team] });
            foreach (var row in rows.Where(row => row.Team == score.Team))
                list.Add(new UIText($"{row.Name}   {row.Kills}/{row.Deaths} K/D   {row.Damage:N0} damage\n" +
                    $"{row.Title} · {row.Detail}", 0.85f));
        }

        ui.SetState(state);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        drawTime = gameTime;
        if (ModContent.GetInstance<GameManager>().IsRunning && ui.CurrentState != null)
            ui.SetState(null);
        ui.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0 && ui.CurrentState != null)
            layers.Insert(index, new LegacyGameInterfaceLayer("PvPAdventure: Match Results", Draw, InterfaceScaleType.UI));
    }

    private bool Draw()
    {
        ui.Draw(Main.spriteBatch, drawTime);
        return true;
    }

    public override void OnWorldUnload() => ui.SetState(null);
}
