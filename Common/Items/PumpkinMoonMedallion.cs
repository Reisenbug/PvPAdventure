using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Items;

public sealed class PumpkinMoonMedallion : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.PumpkinMoonMedallion;

    public override void Load() => On_Player.ItemCheck_UseEventItems += SkipVanillaEvent;
    public override void Unload() => On_Player.ItemCheck_UseEventItems -= SkipVanillaEvent;

    private static void SkipVanillaEvent(On_Player.orig_ItemCheck_UseEventItems orig, Player player, Item item)
    {
        if (item.type != ItemID.PumpkinMoonMedallion)
            orig(player, item);
    }

    public override void SetStaticDefaults()
    {
        NPCID.Sets.MPAllowedEnemies[NPCID.MourningWood] = true;
        NPCID.Sets.MPAllowedEnemies[NPCID.Pumpking] = true;
    }

    private static int BossType(Player player) => player.ZoneForest ? NPCID.MourningWood : NPCID.Pumpking;

    public override bool CanUseItem(Item item, Player player) =>
        !Main.dayTime && player.ZoneOverworldHeight && !NPC.AnyNPCs(BossType(player));

    public override bool? UseItem(Item item, Player player)
    {
        if (player.whoAmI != Main.myPlayer)
            return true;
        int boss = BossType(player);
        SoundEngine.PlaySound(SoundID.Roar, player.position);
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: boss);
        else
            NPC.SpawnOnPlayer(player.whoAmI, boss);
        return true;
    }

    public override void ModifyTooltips(Item item, System.Collections.Generic.List<TooltipLine> tooltips)
    {
        foreach (var line in tooltips)
            if (line.Mod == "Terraria" && line.Name == "Tooltip0")
                line.Text = "夜晚地表使用：森林召唤哀木，其他区域召唤南瓜王";
    }
}
