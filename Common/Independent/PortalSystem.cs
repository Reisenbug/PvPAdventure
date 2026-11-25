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
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Independent;

public sealed class PortalCreatorItem : ModItem
{
    public override string Texture => "PvPAdventure/Assets/Portals/PortalCreator_NoTeam";

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 20;
        Item.useStyle = ItemUseStyleID.HoldUp;
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

    public override bool PreDrawInInventory(SpriteBatch batch, Vector2 position, Rectangle frame, Color drawColor,
        Color itemColor, Vector2 origin, float scale)
    {
        batch.Draw(PortalVisuals.Texture("PortalCreator", Main.LocalPlayer.team), position, null, drawColor,
            0, origin, scale, SpriteEffects.None, 0);
        return false;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new TooltipLine(Mod, "PortalUse", "比赛中离开出生区，站稳并按住传送门快捷键或使用物品"));
        tooltips.Add(new TooltipLine(Mod, "PortalCancel", "移动、受伤或松开按键会中断；同队可用，敌人可摧毁"));
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
            return "No Portal Creator in your inventory. It is included with the server's starting items.";
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
            PortalPlayer state = Main.LocalPlayer.GetModPlayer<PortalPlayer>();
            int remaining = state.Remaining;
            if (remaining > 0)
            {
                PortalVisuals.DrawBar(Main.spriteBatch, new Vector2(Main.screenWidth / 2f, Main.screenHeight - 72),
                    state.Progress, Main.teamColor[Main.LocalPlayer.team]);
                Utils.DrawBorderString(Main.spriteBatch, $"正在创建传送门：{(remaining + 59) / 60} 秒（移动、受伤或松键取消）",
                    new Vector2(Main.screenWidth / 2f, Main.screenHeight - 100), Color.White,
                    anchorx: 0.5f);
            }
            if (!Main.mapFullscreen && !Main.LocalPlayer.mouseInterface)
                foreach (NPC npc in Main.ActiveNPCs)
                    if (npc.ModNPC is TeamPortal portal && npc.Hitbox.Contains(Main.MouseWorld.ToPoint()))
                    {
                        string owner = Main.player[Math.Clamp((int)npc.ai[0], 0, Main.maxPlayers - 1)].name;
                        Main.instance.MouseText($"{owner} 的传送门 · {npc.life}/{npc.lifeMax} · " +
                            (portal.CanTravel(Main.LocalPlayer) ? "在传送入口打开地图使用" : "敌队传送门，可攻击摧毁"));
                        break;
                    }
            return true;
        }, InterfaceScaleType.UI));
    }

    public override void PostDrawTiles()
    {
        if (Main.dedServ || !Playing)
            return;
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        foreach (Player player in Main.ActivePlayers)
        {
            PortalPlayer state = player.GetModPlayer<PortalPlayer>();
            if (state.Remaining > 0)
                PortalVisuals.Draw(Main.spriteBatch, state.Origin, player.team, state.Progress);
        }
        Main.spriteBatch.End();
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
            {
                int owner = reader.ReadByte();
                Vector2 origin = new(reader.ReadSingle(), reader.ReadSingle());
                int duration = reader.ReadInt32();
                if (owner >= Main.maxPlayers)
                    return;
                PortalPlayer state = Main.player[owner].GetModPlayer<PortalPlayer>();
                if (target > 0 && state.Remaining == 0)
                    PortalVisuals.Burst(origin, Main.player[owner].team, inward: true);
                state.Origin = origin;
                state.CreationFrames = Math.Max(1, duration);
                state.Remaining = Math.Max(0, target);
                if (state.Remaining == 0)
                    state.Cancel();
            }
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
            state.CreationFrames = state.Remaining;
            PortalVisuals.Burst(state.Origin, player.team, inward: true);
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
        const float radiusSquared = 128 * 128;
        if (player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion())
            return true;
        foreach (Player owner in Main.ActivePlayers)
            if ((owner == player || player.team != 0 && owner.team == player.team) &&
                owner.SpawnX >= 0 && owner.SpawnY >= 0 && Player.CheckSpawn(owner.SpawnX, owner.SpawnY) &&
                Vector2.DistanceSquared(player.Center, new Vector2(owner.SpawnX * 16 + 8, owner.SpawnY * 16 + 8)) <= radiusSquared)
                return true;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.ModNPC is TeamPortal portal && portal.CanTravel(player) &&
                Vector2.DistanceSquared(npc.Center, player.Center) <= radiusSquared)
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
        for (int column = 0; column <= 8; column++)
        for (int offset = 0; offset <= 64; offset += 16)
        {
            int horizontalOffset = ((column + 1) / 2) * 16 * (column % 2 == 0 ? 1 : -1);
            Vector2 destination = new(portal.Center.X - player.width / 2f + horizontalOffset,
                portal.Bottom.Y - player.height - offset);
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

        const string noLanding = "传送门附近没有安全落点，请清理附近方块后再试。";
        if (Main.netMode == NetmodeID.Server)
            ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(noLanding), Color.Gold, player.whoAmI);
        else
            Main.NewText(noLanding, Color.Gold);

        Log.Info($"Portal teleport blocked: player={player.whoAmI}, npc={portal.whoAmI}, no safe landing");
        return false;
    }
}

public sealed class PortalPlayer : ModPlayer
{
    internal int Remaining;
    internal int CreationFrames;
    internal float Progress => MathHelper.Clamp(1f - Remaining / (float)Math.Max(1, CreationFrames), 0, 1);
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
        if (Player.whoAmI == Main.myPlayer && Player.HeldItem.type == ModContent.ItemType<PortalCreatorItem>() &&
            !Player.controlUseItem && (Player.itemAnimation > 0 || Player.channel || Remaining > 0))
        {
            bool active = Remaining > 0;
            Cancel();
            if (active && Main.netMode == NetmodeID.MultiplayerClient)
                PortalSystem.Request(1);
        }
        if (Remaining <= 0)
            return;
        PortalVisuals.Ambient(Origin, Player.team, Progress);
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            Remaining--;
            if (Player.whoAmI == Main.myPlayer && !Player.channel)
            {
                Cancel();
                PortalSystem.Request(1);
            }
            else if (Remaining == 0 && Player.whoAmI == Main.myPlayer)
                StopUse();
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
            StopUse();
            Notify();
        }
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            Cancel();
        else if (Player.whoAmI == Main.myPlayer)
        {
            bool active = Remaining > 0;
            Cancel();
            if (active)
                PortalSystem.Request(1);
        }
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
        StopUse();
        if (Remaining <= 0)
            return;
        Remaining = 0;
        Notify();
    }

    private void StopUse()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
            return;
        if (Player.HeldItem.type != ModContent.ItemType<PortalCreatorItem>())
            return;
        Player.channel = false;
        Player.controlUseItem = false;
        Player.itemAnimation = Player.itemAnimationMax = 0;
        Player.itemTime = Player.itemTimeMax = 0;
    }

    internal void Notify()
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = Mod.GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.Portal);
        packet.Write((byte)3);
        packet.Write(Remaining);
        packet.Write((byte)Player.whoAmI);
        packet.Write(Origin.X);
        packet.Write(Origin.Y);
        packet.Write(CreationFrames);
        packet.Send();
    }
}

public sealed class TeamPortal : ModNPC
{
    private static int PortalMaxHealth => NPC.downedPlantBoss ? 300 : Main.hardMode ? 150 : 50;
    public override string Texture => "PvPAdventure/Assets/Portals/Portal_NoTeam";
    public override void SetStaticDefaults() => Main.npcFrameCount[Type] = PortalVisuals.FrameCount;
    public override void SetDefaults()
    {
        NPC.width = 32;
        NPC.height = 48;
        NPC.lifeMax = PortalMaxHealth;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.knockBackResist = 0;
        NPC.damage = 0;
        NPC.aiStyle = -1;
        NPC.ShowNameOnHover = false;
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
        if (NPC.lifeMax != PortalMaxHealth)
        {
            NPC.lifeMax = PortalMaxHealth;
            NPC.life = NPC.lifeMax;
            NPC.netUpdate = true;
        }
        NPC.velocity = Vector2.Zero;
        if (!Main.dedServ)
        {
            if (NPC.localAI[0] == 0)
            {
                NPC.localAI[0] = 1;
                PortalVisuals.Burst(NPC.Center, (int)NPC.ai[1]);
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
            }
            NPC.localAI[1] = Math.Max(0, NPC.localAI[1] - 1);
            PortalVisuals.Ambient(NPC.Center, (int)NPC.ai[1]);
        }
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
        PortalVisuals.Draw(spriteBatch, NPC.Center, (int)NPC.ai[1], flash: NPC.localAI[1] / 12f);
        PortalVisuals.DrawBar(spriteBatch, NPC.Center - screenPos + new Vector2(0, 28),
            NPC.life / (float)NPC.lifeMax, Color.Lerp(Color.Red, Color.LimeGreen, NPC.life / (float)NPC.lifeMax));
        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

    public override void HitEffect(NPC.HitInfo hit)
    {
        NPC.localAI[1] = 12;
        if (NPC.life <= 0)
            PortalVisuals.Burst(NPC.Center, (int)NPC.ai[1]);
    }
}

public sealed class PortalMapLayer : ModMapLayer
{
    public override void Draw(ref MapOverlayDrawContext context, ref string text)
    {
        if (!PortalSystem.Playing)
            return;
        Player player = Main.LocalPlayer;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.ModNPC is not TeamPortal portal || !portal.CanTravel(player))
                continue;
            var result = context.Draw(PortalVisuals.Texture("PortalMinimap", (int)npc.ai[1]), npc.Center / 16,
                Color.White, new SpriteFrame(1, 1), 1, 1.3f, Alignment.Center);
            if (!result.IsMouseOver)
                continue;
            string owner = Main.player[Math.Clamp((int)npc.ai[0], 0, Main.maxPlayers - 1)].name;
            text = $"{owner} 的传送门 · {npc.life}/{npc.lifeMax} · " + (player.dead ? "点击选择复活目标" :
                PortalSystem.CanTravelFrom(player) ? "点击传送" : "请从出生区、床或同队传送门旁使用");
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
