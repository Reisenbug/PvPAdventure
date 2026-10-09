using System;
using System.IO;
using PvPAdventure.Content.Buffs;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Combat;

public sealed class ArmorEnergyPlayer : ModPlayer
{
    public int CrossEnergy { get; private set; }
    public int BeetleEnergy { get; private set; }
    private int syncTimer;

    public bool HasBeetleArmor => Player.armor[0].type == ItemID.BeetleHelmet &&
        Player.armor[1].type == ItemID.BeetleScaleMail && Player.armor[2].type == ItemID.BeetleLeggings;

    private bool HasHallowedArmor =>
        Player.armor[0].type is ItemID.HallowedHelmet or ItemID.HallowedHeadgear or ItemID.HallowedMask or ItemID.HallowedHood &&
        Player.armor[1].type == ItemID.HallowedPlateMail && Player.armor[2].type == ItemID.HallowedGreaves;

    public bool HasCrossEquipment
    {
        get
        {
            for (int i = 3; i <= 9; i++)
                if (Player.IsItemSlotUnlockedAndUsable(i) &&
                    Player.armor[i].type is ItemID.CrossNecklace or ItemID.StarVeil)
                    return true;

            return HasHallowedArmor;
        }
    }

    public bool Protected => HasCrossEquipment && !Player.dead && CrossEnergy >= Player.statLifeMax2;
    public int BeetleTier => !HasBeetleArmor || Player.dead ? 0 :
        BeetleEnergy > 3000 ? 3 : BeetleEnergy >= 1440 ? 2 : BeetleEnergy >= 600 ? 1 : 0;

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (Protected)
            modifiers.FinalDamage *= 0.5f;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !info.PvP || info.Damage <= 0 || Player.ghost)
            return;

        if (HasCrossEquipment)
        {
            CrossEnergy += info.Damage * 2;
            Send(-1);
        }

        int index = info.DamageSource.SourcePlayerIndex;
        if ((uint)index >= (uint)Main.maxPlayers || index == Player.whoAmI)
            return;

        Player attacker = Main.player[index];
        if (!attacker.active || attacker.dead || attacker.ghost ||
            attacker.team != 0 && attacker.team == Player.team)
            return;

        bool melee = info.DamageSource.SourceItem?.CountsAsClass(DamageClass.Melee) ??
            (ContentSamples.ProjectilesByType.TryGetValue(info.DamageSource.SourceProjectileType, out Projectile sample) &&
             sample.CountsAsClass(DamageClass.Melee));
        var energy = attacker.GetModPlayer<ArmorEnergyPlayer>();
        if (melee && energy.HasBeetleArmor)
        {
            energy.BeetleEnergy = Math.Min(3600, energy.BeetleEnergy + info.Damage * 9);
            energy.Send(-1);
        }
    }

    public override void PostUpdate()
    {
        bool protectedBefore = Protected;
        int tierBefore = BeetleTier;
        int previousCross = CrossEnergy;
        int previousBeetle = BeetleEnergy;
        CrossEnergy = HasCrossEquipment && !Player.dead ? Math.Max(0, CrossEnergy - 1) : 0;
        BeetleEnergy = HasBeetleArmor && !Player.dead ? Math.Max(0, BeetleEnergy - 1) : 0;

        if (Protected)
            Player.AddBuff(ModContent.BuffType<GodsProtection>(), 2, quiet: true);
        else
            Player.ClearBuff(ModContent.BuffType<GodsProtection>());

        int tier = BeetleTier;
        for (int i = 1; i <= 3; i++)
            if (i != tier)
                Player.ClearBuff(BuffID.BeetleMight1 + i - 1);
        if (tier > 0)
            Player.AddBuff(BuffID.BeetleMight1 + tier - 1, 2, quiet: true);

        bool cleared = previousCross > 0 && CrossEnergy == 0 || previousBeetle > 0 && BeetleEnergy == 0;
        if (Main.netMode == NetmodeID.Server &&
            (protectedBefore != Protected || tierBefore != tier || cleared ||
             (CrossEnergy > 0 || BeetleEnergy > 0) && ++syncTimer >= 60))
        {
            syncTimer = 0;
            Send(-1);
        }
    }

    public override void PostUpdateEquips()
    {
        if (HasHallowedArmor)
        {
            Player.setBonus = Language.GetTextValue("Mods.PvPAdventure.ArmorEnergy.ProtectionDescription");
            Player.onHitDodge = Player.shadowDodge = false;
            Player.ClearBuff(BuffID.ShadowDodge);
        }
        if (BeetleTier == 3)
            Player.yoraiz0rEye = 33;
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) => Clear();
    public override void PlayerDisconnect() => Clear();

    private void Clear()
    {
        CrossEnergy = BeetleEnergy = 0;
        Send(-1);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => Send(toWho);

    private void Send(int target)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = Mod.GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.ArmorEnergy);
        packet.Write((byte)Player.whoAmI);
        packet.Write(CrossEnergy);
        packet.Write(BeetleEnergy);
        packet.Send(target);
    }

    internal static void Handle(BinaryReader reader)
    {
        int index = reader.ReadByte();
        int cross = reader.ReadInt32();
        int beetle = reader.ReadInt32();
        if (Main.netMode != NetmodeID.MultiplayerClient || index >= Main.maxPlayers)
            return;
        var energy = Main.player[index].GetModPlayer<ArmorEnergyPlayer>();
        energy.CrossEnergy = Math.Max(0, cross);
        energy.BeetleEnergy = Math.Clamp(beetle, 0, 3600);
    }
}
