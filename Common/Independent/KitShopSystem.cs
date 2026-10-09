using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PvPAdventure.Common.GameTimer;
using PvPAdventure.Common.SSC;
using PvPAdventure.Common.SpawnSelector;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Net;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent.UI.Elements;

namespace PvPAdventure.Common.Independent;

public sealed class KitShopSystem : ModSystem
{
    private ModKeybind keybind;
    private UserInterface ui;
    private KitMenu menu;
    internal static int Balance;
    internal static bool Initialized;
    private static bool awaitingBudget;
    internal static Dictionary<int, int> Purchases = [];
    private static Dictionary<int, int>[] presets = new Dictionary<int, int>[3];
    private static string PresetPath => Path.Combine(Main.SavePath, "PvPAdventure", "StartingLoadouts.json");
    internal static IndependentConfig Config => ModContent.GetInstance<IndependentConfig>();
    internal static bool CanShop(Player player) => BlockReason(player) == null;

    internal static string BudgetText() => awaitingBudget ? "Loading starting kit budget..." :
        Initialized ? $"Starting kit budget: {Balance / 10000f:0.####} gold" :
        $"Starting kit: {Config.StartingGold} gold (Reset kit to activate)";

    public override void OnWorldLoad()
    {
        Balance = 0;
        Initialized = awaitingBudget = false;
        Purchases = [];
        ui?.SetState(null);
    }

    internal static string BlockReason(Player player)
    {
        if (!Config.StartingShopEnabled)
            return "Starting Kit Shop is disabled on this server.";
        if (!player.active || player.dead || player.ghost)
            return "Starting Kit Shop is only available to living players.";
        if (ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Waiting)
            return "Starting Kit Shop is only available before a match. F6 opens match controls.";
        if (!player.GetModPlayer<SpawnPlayer>().IsPlayerInSpawnRegion())
            return "Return to the spawn area before opening Starting Kit Shop.";
        return null;
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;
        keybind = KeybindLoader.RegisterKeybind(Mod, "StartingShop", Keys.K);
        ui = new();
        if (File.Exists(PresetPath))
        {
            try
            {
                var saved = JsonSerializer.Deserialize<Dictionary<int, int>[]>(File.ReadAllText(PresetPath));
                if (saved?.Length == 3)
                    presets = saved;
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                Log.Warn($"Could not load starting presets: {exception.Message}");
            }
        }
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!CanShop(Main.LocalPlayer))
            ui?.SetState(null);
        ui?.Update(gameTime);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0)
            layers.Insert(index, new LegacyGameInterfaceLayer("PvPAdventure: Starting Shop",
                () => { ui?.Draw(Main.spriteBatch, new GameTime()); return true; }, InterfaceScaleType.UI));
    }

    internal void Toggle()
    {
        string reason = BlockReason(Main.LocalPlayer);
        if (reason != null)
        {
            Main.NewText(reason, Color.OrangeRed);
            return;
        }
        if (ui.CurrentState != null)
            ui.SetState(null);
        else
        {
            awaitingBudget = true;
            menu = new();
            ui.SetState(menu);
            Request(3);
        }
    }

    internal bool Pressed => keybind?.JustPressed == true;

    internal static void Request(byte action, int item = 0, Dictionary<int, int> preset = null)
    {
        if (!CanShop(Main.LocalPlayer))
            return;
        if (action == 2 && preset == null)
            return;
        if (action == 0 && !Initialized)
        {
            Main.NewText("Reset kit first to activate your budget. This clears your current gear.", Color.OrangeRed);
            return;
        }
        if (action is 1 or 2 && !Main.mouseItem.IsAir)
        {
            Main.NewText("Put the item on your cursor away before resetting or loading a kit.");
            return;
        }
        if (Main.netMode == NetmodeID.SinglePlayer)
            Execute(Main.LocalPlayer, action, item, preset);
        else
        {
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.KitShop);
            packet.Write(action);
            if (action == 0)
                packet.Write(item);
            if (action == 2)
                WriteItems(packet, preset);
            packet.Send();
        }
    }

    internal static void Handle(BinaryReader reader, int sender)
    {
        byte action = reader.ReadByte();
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (action == 5)
            {
                awaitingBudget = false;
                Main.NewText(reader.ReadString(), Color.OrangeRed);
                ModContent.GetInstance<KitShopSystem>().ui?.SetState(null);
                return;
            }
            if (action != 4)
                return;
            Initialized = reader.ReadBoolean();
            Balance = reader.ReadInt32();
            Purchases = ReadItems(reader);
            awaitingBudget = false;
            Log.Info($"Starting kit sync: initialized={Initialized}, balance={Balance}");
            ModContent.GetInstance<KitShopSystem>().menu?.Refresh();
            return;
        }
        if (sender < 0 || sender >= Main.maxPlayers || action > 3)
            return;
        int item = action == 0 ? reader.ReadInt32() : 0;
        Dictionary<int, int> preset = action == 2 ? ReadItems(reader) : null;
        Execute(Main.player[sender], action, item, preset);
    }

    private static void Execute(Player player, byte action, int item, Dictionary<int, int> preset)
    {
        string reason = BlockReason(player);
        if (reason != null)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket rejection = ModContent.GetInstance<PvPAdventure>().GetPacket();
                rejection.Write((byte)AdventurePacketIdentifier.KitShop);
                rejection.Write((byte)5);
                rejection.Write(reason);
                rejection.Send(player.whoAmI);
            }
            return;
        }
        KitShopPlayer kit = player.GetModPlayer<KitShopPlayer>();
        if (action == 1)
            Reset(player, kit);
        else if (action == 0 && kit.Initialized)
            Buy(player, kit, item);
        else if (action == 2 && TryPrice(preset, Config.StartingGold * 10000,
            49 - ModContent.GetInstance<SSCConfig>().StartItems.Count(entry => !entry.Item.IsUnloaded && entry.Item.Type > 0), out _))
        {
            Reset(player, kit);
            foreach (var entry in preset)
                for (int count = 0; count < entry.Value; count++)
                    Buy(player, kit, entry.Key);
        }
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Initialized = kit.Initialized;
            Balance = kit.Balance;
            Purchases = new(kit.Purchases);
            awaitingBudget = false;
            ModContent.GetInstance<KitShopSystem>().menu?.Refresh();
        }
        else
        {
            Log.Info($"Starting kit: player={player.whoAmI}, action={action}, initialized={kit.Initialized}, balance={kit.Balance}, startingGold={Config.StartingGold}");
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.KitShop);
            packet.Write((byte)4);
            packet.Write(kit.Initialized);
            packet.Write(kit.Balance);
            WriteItems(packet, kit.Purchases);
            packet.Send(player.whoAmI);
            if (action != 3)
                SyncItems(player);
        }
    }

    private static void Reset(Player player, KitShopPlayer kit)
    {
        foreach (Item item in player.inventory.Concat(player.armor).Concat(player.dye)
            .Concat(player.miscEquips).Concat(player.miscDyes))
            item.TurnToAir();
        player.trashItem.TurnToAir();
        StartingItems.ApplyStartItems(player);
        kit.Initialized = true;
        kit.Balance = Config.StartingGold * 10000;
        kit.Purchases.Clear();
    }

    private static void Buy(Player player, KitShopPlayer kit, int type)
    {
        var entry = Config.StartingShop.FirstOrDefault(value => value.Item.Type == type);
        int slot = Array.FindIndex(player.inventory, 0, 50, value => value.IsAir);
        if (slot < 0 || !TryPrice(new Dictionary<int, int> { [type] = 1 }, kit.Balance, 1, out int price))
            return;
        Item item = new(type);
        item.stack = entry.Stack;
        player.inventory[slot] = item;
        kit.Balance -= price;
        kit.Purchases[type] = kit.Purchases.GetValueOrDefault(type) + 1;
    }

    private static bool TryPrice(Dictionary<int, int> preset, int budget, int slots, out int cost)
    {
        return KitBudget.TryPrice(Config.StartingShop.Where(entry => entry != null && !entry.Item.IsUnloaded)
            .Select(entry => new KitBudget.Offer(entry.Item.Type, entry.Stack,
                new Item(entry.Item.Type).maxStack, entry.Price)), preset, budget, slots, out cost);
    }

    private static void WriteItems(BinaryWriter writer, Dictionary<int, int> items)
    {
        writer.Write(items?.Count ?? 0);
        if (items != null)
            foreach (var item in items)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
    }

    private static Dictionary<int, int> ReadItems(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 100)
            return null;
        Dictionary<int, int> items = [];
        for (int i = 0; i < count; i++)
        {
            int type = reader.ReadInt32();
            int quantity = reader.ReadInt32();
            if (!items.TryAdd(type, quantity))
                return null;
        }
        return items;
    }

    private static void SyncItems(Player player)
    {
        for (int i = 0; i < player.inventory.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: i);
        for (int i = 0; i < player.armor.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: PlayerItemSlotID.Armor0 + i);
        for (int i = 0; i < player.dye.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: PlayerItemSlotID.Dye0 + i);
        for (int i = 0; i < player.miscEquips.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: PlayerItemSlotID.Misc0 + i);
        for (int i = 0; i < player.miscDyes.Length; i++)
            NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: PlayerItemSlotID.MiscDye0 + i);
    }

    private sealed class KitMenu : UIState
    {
        private UIText title;
        public override void OnInitialize()
        {
            UIPanel panel = new();
            panel.Width.Set(520, 0);
            panel.Height.Set(500, 0);
            panel.HAlign = 0.5f;
            panel.VAlign = 0.5f;
            Append(panel);
            title = new UIText("");
            panel.Append(title);
            Button(panel, "Reset kit / clear gear", 0, 32, () => Request(1));
            Button(panel, "Close", 340, 32, () => ModContent.GetInstance<KitShopSystem>().ui.SetState(null));
            for (int slot = 0; slot < 3; slot++)
            {
                int index = slot;
                Button(panel, $"Save {slot + 1}", slot * 165, 64, () => Save(index));
                Button(panel, $"Load {slot + 1}", slot * 165, 96, () => Request(2, preset: presets[index]));
            }
            UIList list = new();
            list.Top.Set(136, 0);
            list.Width.Set(-24, 1);
            list.Height.Set(-140, 1);
            panel.Append(list);
            UIScrollbar scrollbar = new();
            scrollbar.HAlign = 1;
            scrollbar.Top.Set(136, 0);
            scrollbar.Height.Set(-140, 1);
            panel.Append(scrollbar);
            list.SetScrollbar(scrollbar);
            foreach (var entry in Config.StartingShop.Where(entry => !entry.Item.IsUnloaded))
            {
                int type = entry.Item.Type;
                UITextPanel<string> row = new($"{Lang.GetItemNameValue(type)} x{entry.Stack} — {entry.Price / 10000f:0.####} gold");
                row.Width.Set(0, 1);
                row.Height.Set(32, 0);
                row.OnLeftClick += (_, _) => Request(0, type);
                list.Add(row);
            }
            Refresh();
        }

        private static void Button(UIElement parent, string text, int x, int y, Action action)
        {
            UITextPanel<string> button = new(text, 0.8f);
            button.Left.Set(x, 0);
            button.Top.Set(y, 0);
            button.Width.Set(155, 0);
            button.Height.Set(28, 0);
            button.OnLeftClick += (_, _) => action();
            parent.Append(button);
        }

        private static void Save(int slot)
        {
            if (Purchases == null)
                return;
            presets[slot] = new(Purchases);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PresetPath));
                File.WriteAllText(PresetPath, JsonSerializer.Serialize(presets));
                Main.NewText($"Saved starting kit {slot + 1}.");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.Error($"Could not save starting kit: {exception}");
            }
        }

        internal void Refresh() => title?.SetText(BudgetText());
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            Main.LocalPlayer.mouseInterface = true;
        }
    }
}

public sealed class KitShopPlayer : ModPlayer
{
    internal bool Initialized;
    internal int Balance;
    internal Dictionary<int, int> Purchases = [];

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        KitShopSystem shop = ModContent.GetInstance<KitShopSystem>();
        if (shop.Pressed)
            shop.Toggle();
    }
}
