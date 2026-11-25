using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PvPAdventure.Common.Statistics.UI;

/// <summary>Displays the Adventure match timer and team points.</summary>
[Autoload(Side = ModSide.Client)]
public class Scoreline : ModSystem
{
    private readonly Interface _interface = new();

    private static float Scale => ModContent.GetInstance<ClientConfig>().ScorelineUISize switch
    {
        ClientConfig.ScorelineSize.Small => .9f,
        ClientConfig.ScorelineSize.Large => 1.15f,
        ClientConfig.ScorelineSize.VeryLarge => 1.5f,
        _ => 1f
    };

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0 && !layers.Contains(_interface)) layers.Insert(index, _interface);
    }

    public sealed class Interface() : GameInterfaceLayer("PvPAdventure: Team Points", InterfaceScaleType.UI)
    {
        protected override bool DrawSelf()
        {
            GameManager game = ModContent.GetInstance<GameManager>();
            if (Main.gameMenu || !game.IsSelected) return true;

            float scale = Scale;
            int pointWidth = (int)(50 * scale);
            int pointHeight = (int)(30 * scale);
            var teams = Main.player.Where(player => player?.active == true && (Team)player.team != Team.None)
                .Select(player => (Team)player.team).Distinct().OrderBy(team => team).ToArray();
            int timerWidth = (int)(96 * scale);
            Rectangle timer = new((Main.screenWidth - timerWidth) / 2, 4, timerWidth, pointHeight);
            if (!Main.mapFullscreen && timer.Contains(Main.mouseX, Main.mouseY))
            {
                Main.LocalPlayer.mouseInterface = true;
                Main.instance.MouseText("Match controls");
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    ModContent.GetInstance<MatchControlsUI>().Toggle();
                }
            }
            int leftCount = (teams.Length + 1) / 2;
            string timerText = game.IsPaused ? "Paused" : game.CountdownTicks > 0
                ? ((game.CountdownTicks + 59) / 60).ToString()
                : game.CurrentPhase == GameManager.Phase.Playing
                    ? System.TimeSpan.FromSeconds((game.RemainingTicks + 59) / 60).ToString(@"h\:mm\:ss")
                    : "Waiting";
            Utils.DrawInvBG(Main.spriteBatch, timer);
            Vector2 timerSize = ChatManager.GetStringSize(FontAssets.MouseText.Value, timerText, Vector2.One * scale);
            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, timerText,
                new Vector2(timer.Center.X - timerSize.X / 2f, timer.Y + 6f * scale),
                Color.White, 0f, Vector2.Zero, Vector2.One * scale);

            for (int index = 0; index < teams.Length; index++)
            {
                Team team = teams[index];
                int x = index < leftCount ? timer.X - (leftCount - index) * pointWidth
                    : timer.Right + (index - leftCount) * pointWidth;
                Rectangle panel = new(x, timer.Y, pointWidth, pointHeight);
                Utils.DrawInvBG(Main.spriteBatch, panel, Main.teamColor[(int)team] * .7f);
                string text = ModContent.GetInstance<PointsManager>().Points[team].ToString();
                Vector2 size = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One * scale);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, text,
                    new Vector2(panel.Center.X - size.X / 2f, panel.Y + 6f * scale), Color.White,
                    0f, Vector2.Zero, Vector2.One * scale);
            }
            return true;
        }
    }
}
