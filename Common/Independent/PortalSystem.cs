using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.SpawnSelector;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Independent;

public sealed class PortalCreatorItem : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.PortalGun}";

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 20;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.useTime = Item.useAnimation = 600;
        Item.channel = true;
        Item.noMelee = true;
        Item.rare = ItemRarityID.Blue;
    }

    public override bool CanUseItem(Player player) => PortalSystem.Playing && !player.ghost &&
        !player.dead && !player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion();

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
            PortalSystem.Request(0);
        return true;
    }
}

public sealed class PortalSystem : ModSystem
{
    private ModKeybind keybind;
    internal static bool Playing =>
        ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing;

    public override void Load()
    {
        if (!Main.dedServ)
            keybind = KeybindLoader.RegisterKeybind(Mod, "UsePortalCreator", Keys.P);
    }

    internal bool Held => keybind?.Current == true;
    internal bool Pressed => keybind?.JustPressed == true;

    internal static string CreationBlockReason(Player player)
    {
        if (!player.active || player.dead || player.ghost)
            return "Only living players can create a team portal.";
        if (!Playing)
            return "Team portals can only be created during a match. F6 opens match controls.";
        if (player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion())
            return "Leave the spawn area before creating a team portal.";
        if (!player.inventory.Take(50).Any(item => item.type == ModContent.ItemType<PortalCreatorItem>()))
            return "No Portal Creator in your inventory. Before the match, use Reset kit in Starting Kit Shop (K).";
        if (player.velocity.LengthSquared() >= 0.1f)
            return "Stand still and hold the team portal key to create a portal.";
        return null;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0)
            return;
        layers.Insert(index, new LegacyGameInterfaceLayer("PvPAdventure: Portal Creation", () =>
        {
            int remaining = Main.LocalPlayer.GetModPlayer<PortalPlayer>().Remaining;
            if (remaining > 0)
                Utils.DrawBorderString(Main.spriteBatch, $"Creating portal: {(remaining + 59) / 60}s",
                    new Vector2(Main.screenWidth / 2f, Main.screenHeight - 100), Color.White,
                    anchorx: 0.5f);
            return true;
        }, InterfaceScaleType.UI));
    }

    internal static void Request(byte action, int target = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            Execute(Main.LocalPlayer, action, target);
        else
        {
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.Portal);
            packet.Write(action);
            packet.Write(target);
            packet.Send();
        }
    }

    internal static void Handle(BinaryReader reader, int sender)
    {
        byte action = reader.ReadByte();
        int target = reader.ReadInt32();
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (action == 3)
                Main.LocalPlayer.GetModPlayer<PortalPlayer>().Remaining = Math.Max(0, target);
            return;
        }
        if (sender >= 0 && sender < Main.maxPlayers)
            Execute(Main.player[sender], action, target);
    }

    private static void Execute(Player player, byte action, int target)
    {
        if (!player.active)
            return;
        PortalPlayer state = player.GetModPlayer<PortalPlayer>();
        if (action == 1)
        {
            state.Cancel();
            return;
        }
        if (!Playing || player.ghost)
            return;
        if (action == 0 && !player.dead && player.velocity.LengthSquared() < 0.1f &&
            player.HeldItem.type == ModContent.ItemType<PortalCreatorItem>() &&
            !player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion() && state.Remaining == 0)
        {
            state.Origin = player.Center;
            state.Remaining = Math.Max(1, ModContent.GetInstance<IndependentConfig>().PortalCreationSeconds * 60);
            state.Notify();
        }
        else if (action == 2 && target >= 0 && target < Main.maxNPCs &&
            Main.npc[target].ModNPC is TeamPortal portal && portal.CanTravel(player))
        {
            if (player.dead)
                state.SpawnPortal = target;
            else if (CanTravelFrom(player))
                Teleport(player, Main.npc[target]);
        }
    }

    internal static bool CanTravelFrom(Player player)
    {
        if (player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion())
            return true;
        if (player.SpawnX >= 0 && Player.CheckSpawn(player.SpawnX, player.SpawnY) &&
            Vector2.DistanceSquared(player.Center, new Vector2(player.SpawnX * 16, player.SpawnY * 16)) < 128 * 128)
            return true;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.ModNPC is TeamPortal portal && portal.CanTravel(player) &&
                Vector2.DistanceSquared(npc.Center, player.Center) < 128 * 128)
                return true;
        return false;
    }

    internal static void Create(Player player, Vector2 position)
    {
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.ModNPC is TeamPortal && (int)npc.ai[0] == player.whoAmI)
            {
                npc.active = false;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
            }
        int index = NPC.NewNPC(player.GetSource_Misc("Portal"),
            (int)position.X, (int)position.Y, ModContent.NPCType<TeamPortal>(),
            ai0: player.whoAmI, ai1: player.team);
        if (index >= 0 && index < Main.maxNPCs)
        {
            Main.npc[index].Center = position;
            Main.npc[index].netUpdate = true;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: index);
            Log.Info($"Portal created: owner={player.whoAmI}, team={player.team}, center={position}, npc={index}");
        }
    }

    internal static bool Teleport(Player player, NPC portal)
    {
        for (int offset = 0; offset <= 64; offset += 16)
        {
            Vector2 destination = new(portal.Center.X - player.width / 2f, portal.Bottom.Y - player.height - offset);
            int x = (int)destination.X / 16;
            int y = (int)destination.Y / 16;
            if (x < 1 || y < 1 || x + 3 >= Main.maxTilesX || y + 4 >= Main.maxTilesY ||
                Collision.SolidCollision(destination, player.width, player.height) ||
                Collision.LavaCollision(destination, player.width, player.height))
                continue;
            player.Teleport(destination, TeleportationStyleID.RecallPotion);
            player.velocity = Vector2.Zero;
            if (Main.netMode == NetmodeID.Server)
            {
                RemoteClient.CheckSection(player.whoAmI, destination);
                NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI,
                    number3: destination.X, number4: destination.Y, number5: TeleportationStyleID.RecallPotion);
            }
            ModContent.GetInstance<LocalMatchSystem>().RecordPortalUse(player);
            Log.Info($"Portal teleport: player={player.whoAmI}, npc={portal.whoAmI}, destination={destination}");
            return true;
        }
        return false;
    }
}

public sealed class PortalPlayer : ModPlayer
{
    internal int Remaining;
    internal Vector2 Origin;
    internal int SpawnPortal = -1;

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        PortalSystem system = ModContent.GetInstance<PortalSystem>();
        if (!system.Held)
            return;
        string reason = PortalSystem.CreationBlockReason(Player);
        if (reason != null)
        {
            if (system.Pressed)
                Main.NewText(reason, Color.OrangeRed);
            return;
        }
        int slot = Array.FindIndex(Player.inventory, 0, 50,
            item => item.type == ModContent.ItemType<PortalCreatorItem>());
        if (slot >= 0)
        {
            Player.selectedItem = slot;
            Player.controlUseItem = true;
        }
    }

    public override void PostUpdate()
    {
        if (Remaining <= 0)
            return;
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            Remaining--;
            if (Player.whoAmI == Main.myPlayer && !Player.channel)
                PortalSystem.Request(1);
            return;
        }
        if (!PortalSystem.Playing || Player.dead || Player.ghost ||
            Player.HeldItem.type != ModContent.ItemType<PortalCreatorItem>() ||
            Vector2.DistanceSquared(Player.Center, Origin) > 4f)
        {
            Cancel();
            return;
        }
        if (--Remaining == 0)
        {
            PortalSystem.Create(Player, Origin);
            Notify();
        }
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            Cancel();
        else if (Player.whoAmI == Main.myPlayer && Remaining > 0)
            PortalSystem.Request(1);
    }

    public override void OnRespawn()
    {
        int target = SpawnPortal;
        SpawnPortal = -1;
        if (Main.netMode != NetmodeID.MultiplayerClient && PortalSystem.Playing &&
            target >= 0 && target < Main.maxNPCs &&
            Main.npc[target].ModNPC is TeamPortal portal && portal.CanTravel(Player))
            PortalSystem.Teleport(Player, Main.npc[target]);
    }

    internal void Cancel()
    {
        if (Remaining <= 0)
            return;
        Remaining = 0;
        Notify();
    }

    internal void Notify()
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = Mod.GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.Portal);
        packet.Write((byte)3);
        packet.Write(Remaining);
        packet.Send(Player.whoAmI);
    }
}

public sealed class TeamPortal : ModNPC
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.PortalGun}";
    public override void SetDefaults()
    {
        NPC.width = 32;
        NPC.height = 48;
        NPC.lifeMax = ModContent.GetInstance<IndependentConfig>().PortalLife;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.knockBackResist = 0;
        NPC.damage = 0;
        NPC.aiStyle = -1;
    }

    internal bool CanTravel(Player player) => NPC.active && NPC.life > 0 &&
        ((int)NPC.ai[0] == player.whoAmI || player.team != 0 && player.team == (int)NPC.ai[1]);

    public override bool? CanBeHitByItem(Player player, Item item) =>
        player.hostile && !CanTravel(player);
    public override bool? CanBeHitByProjectile(Projectile projectile) =>
        projectile.owner >= 0 && projectile.owner < Main.maxPlayers &&
        Main.player[projectile.owner].hostile && !CanTravel(Main.player[projectile.owner]);
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool CanHitNPC(NPC target) => false;
    public override bool CheckActive() => false;
    public override void AI()
    {
        NPC.velocity = Vector2.Zero;
        int owner = (int)NPC.ai[0];
        if (Main.netMode != NetmodeID.MultiplayerClient &&
            (!PortalSystem.Playing || owner < 0 || owner >= Main.maxPlayers ||
                !Main.player[owner].active || Main.player[owner].team != (int)NPC.ai[1]))
        {
            NPC.active = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
        }
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Color color = Main.teamColor[Math.Clamp((int)NPC.ai[1], 0, Main.teamColor.Length - 1)];
        for (int i = 0; i < 24; i++)
        {
            float angle = MathHelper.TwoPi * i / 24;
            Vector2 point = NPC.Center - screenPos + new Vector2((float)Math.Cos(angle) * 15, (float)Math.Sin(angle) * 24);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, point, null, color,
                angle, Vector2.Zero, new Vector2(3, 6), SpriteEffects.None, 0);
        }
        return false;
    }
}

public sealed class PortalMapLayer : ModMapLayer
{
    public override void Draw(ref MapOverlayDrawContext context, ref string text)
    {
        if (!PortalSystem.Playing)
            return;
        Player player = Main.LocalPlayer;
        Main.instance.LoadItem(ItemID.PortalGun);
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not TeamPortal portal || !portal.CanTravel(player))
                continue;
            var result = context.Draw(TextureAssets.Item[ItemID.PortalGun].Value, npc.Center / 16,
                Main.teamColor[player.team], new SpriteFrame(1, 1), 1, 1.3f, Alignment.Center);
            if (!result.IsMouseOver)
                continue;
            text = player.dead ? "Select portal for next respawn" :
                PortalSystem.CanTravelFrom(player) ? "Teleport to team portal" : "Travel from spawn, a bed, or a team portal";
            if (Main.mapFullscreen && Main.mouseLeft && Main.mouseLeftRelease &&
                (player.dead || PortalSystem.CanTravelFrom(player)))
            {
                Main.mouseLeftRelease = false;
                PortalSystem.Request(2, npc.whoAmI);
                Main.mapFullscreen = false;
            }
        }
    }
}
