using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.Independent;
using PvPAdventure.Common.SpawnSelector.UI;
using PvPAdventure.Core.Debug;
using PvPAdventure.Core.Net;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;

namespace PvPAdventure.Common.SpawnSelector;

[Autoload(Side = ModSide.Client)]
public class SpawnSystem : ModSystem
{
    public UserInterface ui;
    public UISpawnState spawnState;

    public static bool Enabled { get; private set; }
    public static bool CanTeleport { get; private set; }

    public static void SetCanTeleport(bool value) => CanTeleport = value;

    private ulong _nextUiRebuildTick;
    private const ulong UiRebuildIntervalTicks = 60 * 5;

    public static bool IsUiOpen
    {
        get
        {
            var sys = ModContent.GetInstance<SpawnSystem>();
            return sys != null && sys.ui?.CurrentState == sys.spawnState;
        }
    }
    private bool wasInSpawnRegion;

    public override void UpdateUI(GameTime gameTime)
    {
        Player local = Main.LocalPlayer;
        if (local == null || local.ghost)
        {
            ui?.SetState(null);
            return;
        }

        SpawnPlayer sp = local.GetModPlayer<SpawnPlayer>();

        bool playing = ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing;
        bool inSubworld = SubworldSystem.AnyActive();
        bool inSpawnRegion = sp.IsPlayerInSpawnRegion();
        bool enteredSpawnRegion = inSpawnRegion && !wasInSpawnRegion;
        wasInSpawnRegion = inSpawnRegion;

        if (enteredSpawnRegion && !local.dead)
            sp.ClearSelection();

        bool inTravelRegion = !local.dead && PortalSystem.CanTravelFrom(local);

        Enabled = (playing || inSubworld) && inTravelRegion;

        bool show = (playing || inSubworld) && (local.dead || Enabled);
        if (!show)
        {
            SetCanTeleport(false);
            ui.SetState(null);
            return;
        }

        SetCanTeleport(local.dead ? local.respawnTimer <= 2 : Enabled);

        if (!CanTeleport)
            sp.ClearExecuteRequest();

        if (!local.dead && CanTeleport)
            TryExecuteSelection(local, sp);

        if (ui.CurrentState != spawnState)
        {
            spawnState = new UISpawnState();
            ui.SetState(spawnState);

            _nextUiRebuildTick = Main.GameUpdateCount + UiRebuildIntervalTicks; // initialize / reset rebuild

        }

        if (ui.CurrentState == spawnState && Main.GameUpdateCount >= _nextUiRebuildTick)
        {
            _nextUiRebuildTick = Main.GameUpdateCount + UiRebuildIntervalTicks;
            spawnState.RequestRebuild();
        }

        ui.Update(gameTime);
    }

    public static bool IsValidTeammateIndex(Player requester, int idx)
    {
        if (requester == null || !requester.active)
            return false;

        if (idx < 0 || idx >= Main.maxPlayers)
            return false;

        Player t = Main.player[idx];
        if (t == null || !t.active || t.dead)
            return false;

        if (requester.team == 0 || t.team != requester.team)
            return false;

        return true;
    }

    public static bool IsValidTeammateIndex(int idx) => IsValidTeammateIndex(Main.LocalPlayer, idx);

    private void TryExecuteSelection(Player p, SpawnPlayer sp)
    {
        if (sp.SelectedType == SpawnType.None)
            return;

        if (p.whoAmI == Main.myPlayer)
            Log.Chat("Executing spawn: " + DescribeSelection(sp.SelectedType, sp.SelectedPlayerIndex));

        bool executed = PerformTeleport(p, sp.SelectedType, sp.SelectedPlayerIndex);
        if (!executed)
            return;

        sp.ClearExecuteRequest();
        OnTeleportExecuted(p);
    }

    private static bool PerformTeleport(Player p, SpawnType type, int idx)
    {
        // MP client: always request (no local execution)
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            SendTeleportRequest(type, idx);
            return true;
        }

        switch (type)
        {
            case SpawnType.World:
                p.Teleport(WorldSpawnWorldPos(), TeleportationStyleID.RecallPotion);
                return true;

            case SpawnType.MyBed:
                return TryTeleportToBed(p, p);

            case SpawnType.Random:
                p.TeleportationPotion();
                SoundEngine.PlaySound(SoundID.Item6, p.Center);
                return true;

            case SpawnType.Teammate:
                if (!IsValidTeammateIndex(p, idx))
                    return false;

                Player t = Main.player[idx];
                if (t == null || !t.active || t.dead)
                    return false;

                p.UnityTeleport(t.position);
                return true;

            case SpawnType.TeammateBed:
                if (idx < 0 || idx >= Main.maxPlayers)
                    return false;

                Player bedOwner = Main.player[idx];
                if (bedOwner == null || !bedOwner.active)
                    return false;

                return TryTeleportToBed(p, bedOwner);

            default:
                return false;
        }
    }

    private static bool TryTeleportToBed(Player requester, Player bedOwner)
    {
        if (bedOwner.SpawnX < 0 || bedOwner.SpawnY < 0)
            return false;

        if (!Player.CheckSpawn(bedOwner.SpawnX, bedOwner.SpawnY))
            return false;

        Vector2 pos = new Vector2(bedOwner.SpawnX, bedOwner.SpawnY - 6).ToWorldCoordinates();
        requester.Teleport(pos, TeleportationStyleID.RecallPotion);
        return true;
    }

    private static void SendTeleportRequest(SpawnType type, int idx)
    {
        var pkt = ModContent.GetInstance<PvPAdventure>().GetPacket();
        pkt.Write((byte)AdventurePacketIdentifier.TeleportRequest);
        pkt.Write((byte)Main.myPlayer);
        pkt.Write((byte)type);

        if (type == SpawnType.Teammate || type == SpawnType.TeammateBed)
            pkt.Write((short)idx);

        pkt.Send();
    }

    private static Vector2 WorldSpawnWorldPos() =>
        new Vector2(Main.spawnTileX, Main.spawnTileY - 3).ToWorldCoordinates();

    private static string DescribeSelection(SpawnType type, int idx)
    {
        if (type != SpawnType.Teammate && type != SpawnType.TeammateBed)
            return type.ToString();

        return type.ToString() + " (" + GetPlayerNameSafe(idx) + ")";
    }

    private static string GetPlayerNameSafe(int idx)
    {
        if (idx < 0 || idx >= Main.maxPlayers)
            return "<unknown>";

        Player p = Main.player[idx];
        if (p == null)
            return "<unknown>";

        return p.name;
    }

    private static void OnTeleportExecuted(Player p)
    {
        SetCanTeleport(false);

        SpectateSystem.MapRestore = false;
        Main.mapFullscreen = false;

        p.GetModPlayer<SpawnPlayer>().ClearSelection();
    }

    public override void OnWorldLoad()
    {
        ui = new UserInterface();
        spawnState = new UISpawnState();

        SetCanTeleport(false);
        Main.OnPostFullscreenMapDraw += DrawOnFullscreenMap;
    }

    public override void Unload()
    {
        SetCanTeleport(false);
        Main.OnPostFullscreenMapDraw -= DrawOnFullscreenMap;
    }

    private void DrawOnFullscreenMap(Vector2 mapPos, float mapScale)
    {
        if (!Main.mapFullscreen || ui?.CurrentState == null)
            return;

        SpriteBatch sb = Main.spriteBatch;
        sb.Begin(SpriteSortMode.Deferred,BlendState.AlphaBlend,SamplerState.LinearClamp,DepthStencilState.None,RasterizerState.CullCounterClockwise, null,Main.UIScaleMatrix);
        ui.Draw(sb, Main._drawInterfaceGameTime);

        sb.End();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int idx = layers.FindIndex(l => l.Name == "Vanilla: Death Text");

        if (IsAnyConfigUIOpen())
            idx = layers.FindIndex(l => l.Name == "Vanilla: Interface Logic 1");

        if (idx == -1)
            return;

        layers.Insert(idx, new LegacyGameInterfaceLayer(
            "PvPAdventure: SpawnSystem UI",
            delegate
            {
                if (Main.mapFullscreen || ui?.CurrentState == null)
                    return true;

                SpriteBatch sb = Main.spriteBatch;
                ui.Draw(sb, Main._drawInterfaceGameTime);
                return true;
            },
            InterfaceScaleType.UI
        ));
    }

    private static bool IsAnyConfigUIOpen()
    {
        UIState s = Main.InGameUI?._currentState;
        return s is UIModConfig || s is UIModConfigList || Main.ingameOptionsWindow;
    }
}
