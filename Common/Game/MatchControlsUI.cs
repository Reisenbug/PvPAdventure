using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Game;

[Autoload(Side = ModSide.Client)]
internal sealed class MatchControlsUI : ModSystem
{
    private UserInterface ui;
    private GameTime drawTime;
    private bool wasRunning;
    private bool wasPaused;

    public override void Load() => ui = new UserInterface();

    internal void Toggle()
    {
        if (ui.CurrentState != null)
            ui.SetState(null);
        else
            Show(ModContent.GetInstance<GameManager>());
    }

    private void Show(GameManager game)
    {
        wasRunning = game.IsRunning;
        wasPaused = game.IsPaused;
        UIState state = new();
        UIPanel panel = new() { HAlign = 0.5f };
        panel.Top.Set(42, 0);
        panel.Width.Set(280, 0);
        panel.Height.Set(60, 0);
        panel.OnUpdate += _ =>
        {
            if (panel.IsMouseHovering)
                Main.LocalPlayer.mouseInterface = true;
        };
        state.Append(panel);
        if (game.IsRunning)
        {
            AddButton(panel, game.IsPaused ? "Resume" : "Pause", 0,
                () => MatchCommand.Request(MatchCommand.ActionKind.Pause));
            AddButton(panel, "End", 0.5f, () => MatchCommand.Request(MatchCommand.ActionKind.End));
        }
        else
            AddButton(panel, "Start", 0, () =>
                MatchCommand.Request(MatchCommand.ActionKind.Start, GameManager.DefaultDurationMinutes, 10));
        AddButton(panel, "Close", 1, () => ui.SetState(null));
        ui.SetState(state);
    }

    private static void AddButton(UIPanel panel, string label, float align, Action click)
    {
        UITextPanel<string> button = new(label, 0.85f) { HAlign = align, VAlign = 0.5f };
        button.OnLeftClick += (_, _) => click();
        panel.Append(button);
    }

    public override void UpdateUI(GameTime gameTime)
    {
        drawTime = gameTime;
        if (ui.CurrentState == null)
            return;
        GameManager game = ModContent.GetInstance<GameManager>();
        if (!game.IsSelected || Main.mapFullscreen)
            ui.SetState(null);
        else if (wasRunning != game.IsRunning || wasPaused != game.IsPaused)
            Show(game);
        ui.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0 && ui.CurrentState != null)
            layers.Insert(index, new LegacyGameInterfaceLayer("PvPAdventure: Match Controls", Draw, InterfaceScaleType.UI));
    }

    private bool Draw()
    {
        ui.Draw(Main.spriteBatch, drawTime);
        return true;
    }

    public override void OnWorldUnload() => ui.SetState(null);
}
