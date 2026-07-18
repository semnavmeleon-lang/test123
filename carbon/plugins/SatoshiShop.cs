using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Satoshi Shop", "semnavmeleon", "1.0.0")]
    [Description("Buy items with Satoshi Coins; prices track the live BTC rate.")]
    public class SatoshiShop : RustPlugin
    {
        [PluginReference] private Plugin SatoshiCoins;

        private PluginConfig _config;

        private class ShopItem
        {
            [JsonProperty("Display name")]
            public string DisplayName;

            [JsonProperty("Item shortname")]
            public string Shortname;

            [JsonProperty("Amount per purchase")]
            public int Amount = 1;

            [JsonProperty("Base price (satoshi)")]
            public long BasePrice;

            [JsonProperty("Skin ID")]
            public ulong SkinId;
        }

        private class PluginConfig
        {
            [JsonProperty("Items")]
            public List<ShopItem> Items = new List<ShopItem>
            {
                new ShopItem { DisplayName = "Дерево x1000", Shortname = "wood", Amount = 1000, BasePrice = 50 },
                new ShopItem { DisplayName = "Камень x1000", Shortname = "stones", Amount = 1000, BasePrice = 50 },
                new ShopItem { DisplayName = "Металл x500", Shortname = "metal.fragments", Amount = 500, BasePrice = 80 },
                new ShopItem { DisplayName = "Заряд ВВ", Shortname = "explosive.timed", Amount = 1, BasePrice = 500 },
            };
        }

        #region Config

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) throw new Exception("null config");
            }
            catch
            {
                PrintWarning("Config is corrupt, loading defaults.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ShopHeader"] = "Магазин Сатоши Коинов (цены плавают вместе с курсом BTC):",
                ["ShopLine"] = "{0}. {1} — {2} сатоши коинов ({3} шт., /shop buy {0})",
                ["ShopFooter"] = "Купить: /shop buy <номер>",
                ["ItemNotFound"] = "Такого товара нет. Введи /shop, чтобы увидеть список.",
                ["InsufficientFunds"] = "Недостаточно сатоши коинов. Нужно {0}, у тебя {1}.",
                ["Purchased"] = "Куплено: {0} за {1} сатоши коинов.",
                ["ShopUnavailable"] = "SatoshiCoins не загружен, магазин недоступен.",
                ["PurchaseFailed"] = "Не удалось выдать предмет, монеты возвращены.",
            }, this);
        }

        [ChatCommand("shop")]
        private void CmdShop(BasePlayer player, string command, string[] args)
        {
            if (SatoshiCoins == null)
            {
                SendReply(player, Lang("ShopUnavailable", player.UserIDString));
                return;
            }

            if (args.Length >= 2 && args[0].ToLower() == "buy")
            {
                BuyItem(player, args[1]);
                return;
            }

            ListItems(player);
        }

        private void ListItems(BasePlayer player)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Lang("ShopHeader", player.UserIDString));

            for (var i = 0; i < _config.Items.Count; i++)
            {
                var shopItem = _config.Items[i];
                var price = GetScaledPrice(shopItem.BasePrice);
                sb.AppendLine(Lang("ShopLine", player.UserIDString, i + 1, shopItem.DisplayName, price, shopItem.Amount));
            }

            sb.Append(Lang("ShopFooter", player.UserIDString));
            SendReply(player, sb.ToString());
        }

        private void BuyItem(BasePlayer player, string indexArg)
        {
            if (!int.TryParse(indexArg, out var index) || index < 1 || index > _config.Items.Count)
            {
                SendReply(player, Lang("ItemNotFound", player.UserIDString));
                return;
            }

            var shopItem = _config.Items[index - 1];
            var price = GetScaledPrice(shopItem.BasePrice);
            var balance = GetBalance(player.userID);

            if (balance < price)
            {
                SendReply(player, Lang("InsufficientFunds", player.UserIDString, price, balance));
                return;
            }

            var withdrawResult = SatoshiCoins.Call("Withdraw", player.userID, price);
            if (withdrawResult == null || !Convert.ToBoolean(withdrawResult))
            {
                SendReply(player, Lang("InsufficientFunds", player.UserIDString, price, balance));
                return;
            }

            var item = ItemManager.CreateByName(shopItem.Shortname, shopItem.Amount, shopItem.SkinId);
            if (item == null)
            {
                SatoshiCoins.Call("Deposit", player.userID, price);
                SendReply(player, Lang("PurchaseFailed", player.UserIDString));
                PrintError($"Не удалось создать предмет: {shopItem.Shortname}");
                return;
            }

            player.GiveItem(item);
            SendReply(player, Lang("Purchased", player.UserIDString, shopItem.DisplayName, price));
        }

        private long GetScaledPrice(long basePrice)
        {
            var result = SatoshiCoins?.Call("PriceScaled", basePrice);
            return result != null ? Convert.ToInt64(result) : basePrice;
        }

        private long GetBalance(ulong playerId)
        {
            var result = SatoshiCoins?.Call("Balance", playerId);
            return result != null ? Convert.ToInt64(result) : 0L;
        }

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
