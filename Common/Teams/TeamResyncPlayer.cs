namespace PvPAdventure.Common.Teams;

using global::PvPAdventure.Core.Net;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

/// <summary>
/// I really dislike having to do it this way,
/// but this hotfix ensures team resyncs properly when players join.
/// </summary>
public class TeamResyncPlayer : ModPlayer
{
    private int _resyncTimer;
    private bool _pendingResync;
    internal bool RandomTeamAssigned;

    public override void PlayerDisconnect()
    {
        RandomTeamAssigned = false;
        _pendingResync = false;
    }

    public override void OnEnterWorld()
    {
        _pendingResync = false;
        if (Common.SSC.SSC.IsEnabled && !Main.ActivePlayerFileData.Path.EndsWith(".SSC", StringComparison.OrdinalIgnoreCase))
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            _pendingResync = true;
            _resyncTimer = 0;
        }
    }

    public override void PostUpdate()
    {

        if (!_pendingResync || Main.netMode != NetmodeID.MultiplayerClient)
            return;

        // wait 0.5s
        if (++_resyncTimer < 30)
            return;

        _pendingResync = false;

        var packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.RandomTeamRequest);
        packet.Send();
    }
}

