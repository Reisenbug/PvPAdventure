using Microsoft.Xna.Framework;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Core.Net;
using PvPAdventure.UI;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.AdminTools.Tools.StartGameTool;

/// <summary>
/// The main UI element for starting a game (draggable title + content panel)
/// </summary>
internal class StartGamePanel : UIDraggablePanel
{
    private readonly UITextPanel<string> _startButton;
    private readonly UITextPanel<string> _pauseButton;
    private readonly UISliderElement _gameTimeSlider;
    private readonly UISliderElement _countdownSlider;

    private int _countdownTimeInSeconds = 10;
    private int _gameTimeInFrames = 195 * 60 * 60;
    private static bool HasMatch => ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing ||
        ModContent.GetInstance<GameManager>()._startGameCountdown.HasValue;

    protected override float MinResizeH => 155f;
    protected override float MinResizeW => 220f;

    public StartGamePanel() : base(Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.DisplayName"))
    {
        Width.Set(360, 0);
        Height.Set(180, 0);
        HAlign = 0.5f;
        VAlign = 0.7f;
        ContentPanel.SetPadding(12);

        _gameTimeSlider = new UI.UISliderElement(
            label: Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.Time"),
            min: 0f,
            max: 195f,
            defaultValue: 195f,
            step: 1f,
            onValueChanged: value =>
            {
                int totalMinutes = (int)value;
                _gameTimeInFrames = totalMinutes * 60 * 60;
            }
        );
        ContentPanel.Append(_gameTimeSlider);

        _countdownSlider = new UI.UISliderElement(
            label: Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.Countdown"),
            min: 0f,
            max: 10f,
            defaultValue: 10f,
            step: 1f,
            onValueChanged: value =>
            {
                _countdownTimeInSeconds = (int)value;
            }
        )
        {
            Top = { Pixels = 26f }
        };
        ContentPanel.Append(_countdownSlider);

        _startButton = new UITextPanel<string>(Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.StartExclamation"))
        {
            Width = { Pixels = 140f },
            Height = { Pixels = 40f },
            HAlign = 0.15f,
            VAlign = 1f
        };
        _startButton.OnMouseOver += (_, _) => _startButton.BorderColor = Color.Yellow;
        _startButton.OnMouseOut += (_, _) => _startButton.BorderColor = Color.Black;
        _startButton.OnLeftClick += (_, _) =>
        {
            if (HasMatch)
            {
                RequestControl(GameTimerNetHandler.GameTimerPacketType.EndGame);
                ModContent.GetInstance<StartGameSystem>().Hide();
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                var gm = ModContent.GetInstance<GameManager>();
                gm.StartGame(time: _gameTimeInFrames, countdownTimeInSeconds: _countdownTimeInSeconds);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                var packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
                packet.Write((byte)AdventurePacketIdentifier.GameTimer);
                packet.Write((byte)GameTimerNetHandler.GameTimerPacketType.StartGame);
                packet.Write(_gameTimeInFrames);
                packet.Write(_countdownTimeInSeconds);
                packet.Send();
            }

            ModContent.GetInstance<StartGameSystem>().Hide();
        };
        ContentPanel.Append(_startButton);
        _pauseButton = new UITextPanel<string>("Pause")
        {
            Width = { Pixels = 120f },
            Height = { Pixels = 40f },
            HAlign = 0.95f,
            VAlign = 1f
        };
        _pauseButton.OnLeftClick += (_, _) =>
        {
            if (HasMatch)
                RequestControl(GameTimerNetHandler.GameTimerPacketType.PauseGame);
        };
        ContentPanel.Append(_pauseButton);
    }

    public override void Update(GameTime gameTime)
    {
        _startButton.SetText(HasMatch ? "End / cancel" : Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.StartExclamation"));
        _pauseButton.SetText(ModContent.GetInstance<PauseManager>().IsPaused ? "Resume" : "Pause");
        base.Update(gameTime);
    }

    private static void RequestControl(GameTimerNetHandler.GameTimerPacketType action)
    {
        if (action != GameTimerNetHandler.GameTimerPacketType.EndGame && !Common.Security.AdminPermissions.RequireLocal())
            return;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            var packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.GameTimer);
            packet.Write((byte)action);
            packet.Send();
        }
        else if (action == GameTimerNetHandler.GameTimerPacketType.EndGame)
            ModContent.GetInstance<GameManager>().EndGame();
        else
            ModContent.GetInstance<PauseManager>().TogglePause();
    }

    protected override void OnClosePanelLeftClick()
    {
        ModContent.GetInstance<StartGameSystem>().Hide();
    }
}
