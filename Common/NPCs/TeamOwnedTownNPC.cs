using System;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.NPCs;

internal sealed class TeamOwnedTownNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private Team ownerTeam;

    public void SetOwnerTeam(NPC npc, Team team)
    {
        ownerTeam = team;
        npc.netUpdate = true;
    }

    public override bool? CanChat(NPC npc) =>
        npc.townNPC && ownerTeam != Team.None && Main.LocalPlayer.team != (int)ownerTeam ? false : null;

    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer) =>
        writer.Write((byte)ownerTeam);

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader) =>
        ownerTeam = ReadTeam(reader.ReadByte());

    public override void SaveData(NPC npc, TagCompound tag)
    {
        if (ownerTeam != Team.None)
            tag["ownerTeam"] = (int)ownerTeam;
    }

    public override void LoadData(NPC npc, TagCompound tag) =>
        ownerTeam = ReadTeam(tag.GetInt("ownerTeam"));

    private static Team ReadTeam(int value) => Enum.IsDefined((Team)value) ? (Team)value : Team.None;
}
