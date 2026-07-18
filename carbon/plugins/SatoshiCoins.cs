using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Satoshi Coins", "semnavmeleon", "1.2.0")]
    [Description("Standalone currency plugin: Satoshi Coins, pegged to the live BTC rate.")]
    public class SatoshiCoins : RustPlugin
    {
        private const string PermBalance = "satoshicoins.balance";
        private const string PermGive = "satoshicoins.give";
        private const string PermGiveAll = "satoshicoins.giveall";
        private const string PermTake = "satoshicoins.take";
        private const string PermTakeAll = "satoshicoins.takeall";
        private const string PermSet = "satoshicoins.set";
        private const string PermSetAll = "satoshicoins.setall";
        private const string PermPay = "satoshicoins.pay";
        private const string PermPayAll = "satoshicoins.payall";
        private const string PermWipe = "satoshicoins.wipe";

        private const long SatoshisPerBtc = 100_000_000L;

        private PluginConfig _config;
        private StoredData _data;

        private double _btcUsd;
        private double _btcRub;
        private double _btcUsd24hChange;
        private DateTime _lastRateUpdate = DateTime.MinValue;

        private enum AdminOp { Give, Take, Set }

        private class StoredData
        {
            public Dictionary<ulong, long> Balances = new Dictionary<ulong, long>();
        }

        private class PluginConfig
        {
            [JsonProperty("Price API URL (CoinGecko simple/price, bitcoin vs usd,rub, with 24h change)")]
            public string PriceApiUrl = "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd,rub&include_24hr_change=true";

            [JsonProperty("Rate refresh interval (seconds)")]
            public float RefreshInterval = 300f;
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

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermBalance, this);
            permission.RegisterPermission(PermGive, this);
            permission.RegisterPermission(PermGiveAll, this);
            permission.RegisterPermission(PermTake, this);
            permission.RegisterPermission(PermTakeAll, this);
            permission.RegisterPermission(PermSet, this);
            permission.RegisterPermission(PermSetAll, this);
            permission.RegisterPermission(PermPay, this);
            permission.RegisterPermission(PermPayAll, this);
            permission.RegisterPermission(PermWipe, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Balance"] = "У тебя {0} сатоши коинов{1}.",
                ["BalanceOther"] = "{0}: {1} сатоши коинов{2}.",
                ["InsufficientFunds"] = "Недостаточно сатоши коинов.",
                ["NoPermission"] = "У тебя нет прав на это.",
                ["PlayerNotFound"] = "Игрок не найден: {0}",
                ["PayUsage"] = "/coins pay <игрок|*> <сумма>",
                ["PaySuccess"] = "Ты перевёл {0} сатоши коинов игроку {1}.",
                ["PayReceived"] = "{0} перевёл(а) тебе {1} сатоши коинов.",
                ["InvalidAmount"] = "Некорректная сумма.",
                ["AllPlayersDone"] = "Готово: применено к игрокам онлайн ({0}).",
                ["WipeConfirm"] = "Это удалит ВСЕ балансы безвозвратно. Введи /coins wipe confirm для подтверждения.",
                ["WipeDone"] = "Все балансы сатоши коинов обнулены.",
                ["Rate"] = "Курс BTC: ${0} / {1}₽, за 24ч {2:+0.##;-0.##;0}% (CoinGecko, обновлено {3}).",
                ["RateUnavailable"] = "Курс BTC ещё не получен, попробуй через минуту.",
                ["Usage"] = "/coins [balance <игрок>] | pay <игрок|*> <сумма> | give <игрок|*> <сумма> | take <игрок|*> <сумма> | set <игрок|*> <сумма> | wipe confirm | rate",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();

            FetchBtcRate();
            timer.Every(_config.RefreshInterval, FetchBtcRate);
        }

        private void Unload() => SaveData();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        #endregion

        #region BTC rate

        private void FetchBtcRate()
        {
            if (string.IsNullOrEmpty(_config.PriceApiUrl)) return;

            webrequest.Enqueue(_config.PriceApiUrl, null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response))
                {
                    PrintWarning($"Не удалось получить курс BTC (HTTP {code}). Использую последний известный курс.");
                    return;
                }

                try
                {
                    var bitcoin = JObject.Parse(response)["bitcoin"];
                    if (bitcoin == null) return;

                    var usd = bitcoin["usd"]?.Value<double>() ?? 0;
                    var rub = bitcoin["rub"]?.Value<double>() ?? 0;
                    var usd24hChange = bitcoin["usd_24h_change"]?.Value<double>();

                    if (usd > 0) _btcUsd = usd;
                    if (rub > 0) _btcRub = rub;
                    if (usd24hChange.HasValue) _btcUsd24hChange = usd24hChange.Value;
                    _lastRateUpdate = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    PrintWarning($"Ошибка разбора курса BTC: {ex.Message}");
                }
            }, this, RequestMethod.GET, null, 10f);
        }

        public double BtcPriceUsd() => _btcUsd;

        public double BtcPriceRub() => _btcRub;

        public double ValueUsd(long satoshis) => _btcUsd <= 0 ? 0 : satoshis * (_btcUsd / SatoshisPerBtc);

        public double ValueRub(long satoshis) => _btcRub <= 0 ? 0 : satoshis * (_btcRub / SatoshisPerBtc);

        public double BtcChange24h() => _btcUsd24hChange;

        private string FiatSuffix(long satoshis)
        {
            if (_btcUsd <= 0) return "";

            var usd = ValueUsd(satoshis);
            var rub = ValueRub(satoshis);

            return rub > 0
                ? $" (≈ ${usd:0.####} / ≈ {rub:0.##}₽)"
                : $" (≈ ${usd:0.####})";
        }

        #endregion

        #region Public API

        public long Balance(ulong playerId)
        {
            _data.Balances.TryGetValue(playerId, out var balance);
            return balance;
        }

        public bool Deposit(ulong playerId, long amount)
        {
            if (amount <= 0) return false;
            _data.Balances.TryGetValue(playerId, out var balance);
            balance += amount;
            _data.Balances[playerId] = balance;
            SaveData();
            Interface.Oxide.CallHook("OnSatoshiCoinsDeposit", playerId, amount, balance);
            return true;
        }

        public bool Withdraw(ulong playerId, long amount)
        {
            if (amount <= 0) return false;
            _data.Balances.TryGetValue(playerId, out var balance);
            if (balance < amount) return false;
            balance -= amount;
            _data.Balances[playerId] = balance;
            SaveData();
            Interface.Oxide.CallHook("OnSatoshiCoinsWithdraw", playerId, amount, balance);
            return true;
        }

        public bool SetBalance(ulong playerId, long amount)
        {
            if (amount < 0) return false;
            _data.Balances[playerId] = amount;
            SaveData();
            Interface.Oxide.CallHook("OnSatoshiCoinsSet", playerId, amount);
            return true;
        }

        public bool Transfer(ulong fromId, ulong toId, long amount)
        {
            if (amount <= 0) return false;
            if (!Withdraw(fromId, amount)) return false;
            Deposit(toId, amount);
            Interface.Oxide.CallHook("OnSatoshiCoinsTransfer", fromId, toId, amount);
            return true;
        }

        public void WipeAll()
        {
            _data.Balances.Clear();
            SaveData();
            Interface.Oxide.CallHook("OnSatoshiCoinsWipe");
        }

        // Scales baseAmount by BTC's live 24h % change before depositing; returns the amount actually deposited.
        public long DepositScaled(ulong playerId, long baseAmount)
        {
            if (baseAmount <= 0) return 0;

            var scaled = ScaleByRate(baseAmount);
            if (scaled <= 0) return 0;

            Deposit(playerId, scaled);
            return scaled;
        }

        // Same 24h-change scaling as DepositScaled, but a pure calculation (e.g. for shop prices) with no balance change.
        public long PriceScaled(long baseAmount) => ScaleByRate(baseAmount);

        private long ScaleByRate(long baseAmount)
        {
            var multiplier = 1.0 + _btcUsd24hChange / 100.0;
            if (multiplier < 0) multiplier = 0;
            return (long)Math.Round(baseAmount * multiplier, MidpointRounding.AwayFromZero);
        }

        #endregion

        #region Commands

        [ChatCommand("coins")]
        private void CmdCoins(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                var amount = Balance(player.userID);
                SendReply(player, Lang("Balance", player.UserIDString, amount, FiatSuffix(amount)));
                return;
            }

            switch (args[0].ToLower())
            {
                case "balance":
                    CmdBalance(player, args);
                    break;

                case "pay":
                    CmdPay(player, args);
                    break;

                case "give":
                    CmdAdminOp(player, args, AdminOp.Give);
                    break;

                case "take":
                    CmdAdminOp(player, args, AdminOp.Take);
                    break;

                case "set":
                    CmdAdminOp(player, args, AdminOp.Set);
                    break;

                case "wipe":
                    CmdWipe(player, args);
                    break;

                case "rate":
                    CmdRate(player);
                    break;

                default:
                    SendReply(player, Lang("Usage", player.UserIDString));
                    break;
            }
        }

        private void CmdBalance(BasePlayer player, string[] args)
        {
            if (args.Length < 2)
            {
                var own = Balance(player.userID);
                SendReply(player, Lang("Balance", player.UserIDString, own, FiatSuffix(own)));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermBalance))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            var target = BasePlayer.Find(args[1]);
            if (target == null)
            {
                SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                return;
            }

            var targetBalance = Balance(target.userID);
            SendReply(player, Lang("BalanceOther", player.UserIDString, target.displayName, targetBalance, FiatSuffix(targetBalance)));
        }

        private void CmdPay(BasePlayer player, string[] args)
        {
            if (args.Length < 3 || !long.TryParse(args[2], out var amount) || amount <= 0)
            {
                SendReply(player, Lang("PayUsage", player.UserIDString));
                return;
            }

            var wildcard = args[1] == "*";

            if (!permission.UserHasPermission(player.UserIDString, wildcard ? PermPayAll : PermPay))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (wildcard)
            {
                var count = 0;
                foreach (var target in BasePlayer.activePlayerList)
                {
                    if (target.userID == player.userID) continue;
                    if (Transfer(player.userID, target.userID, amount))
                        count++;
                }
                SendReply(player, Lang("AllPlayersDone", player.UserIDString, count));
                return;
            }

            var single = BasePlayer.Find(args[1]);
            if (single == null)
            {
                SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                return;
            }

            if (!Transfer(player.userID, single.userID, amount))
            {
                SendReply(player, Lang("InsufficientFunds", player.UserIDString));
                return;
            }

            SendReply(player, Lang("PaySuccess", player.UserIDString, amount, single.displayName));
            SendReply(single, Lang("PayReceived", single.UserIDString, player.displayName, amount));
        }

        private void CmdAdminOp(BasePlayer player, string[] args, AdminOp op)
        {
            if (args.Length < 3 || !long.TryParse(args[2], out var amount) || amount < 0)
            {
                SendReply(player, Lang("InvalidAmount", player.UserIDString));
                return;
            }

            var wildcard = args[1] == "*";
            string permSingle;
            string permAll;

            switch (op)
            {
                case AdminOp.Give:
                    permSingle = PermGive;
                    permAll = PermGiveAll;
                    break;
                case AdminOp.Take:
                    permSingle = PermTake;
                    permAll = PermTakeAll;
                    break;
                default:
                    permSingle = PermSet;
                    permAll = PermSetAll;
                    break;
            }

            if (!permission.UserHasPermission(player.UserIDString, wildcard ? permAll : permSingle))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (wildcard)
            {
                var count = 0;
                foreach (var target in BasePlayer.activePlayerList)
                {
                    ApplyAdminOp(op, target.userID, amount);
                    count++;
                }
                SendReply(player, Lang("AllPlayersDone", player.UserIDString, count));
                return;
            }

            var single = BasePlayer.Find(args[1]);
            if (single == null)
            {
                SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                return;
            }

            ApplyAdminOp(op, single.userID, amount);
            var newBalance = Balance(single.userID);
            SendReply(player, Lang("BalanceOther", player.UserIDString, single.displayName, newBalance, FiatSuffix(newBalance)));
        }

        private void ApplyAdminOp(AdminOp op, ulong targetId, long amount)
        {
            switch (op)
            {
                case AdminOp.Give:
                    Deposit(targetId, amount);
                    break;
                case AdminOp.Take:
                    Withdraw(targetId, amount);
                    break;
                case AdminOp.Set:
                    SetBalance(targetId, amount);
                    break;
            }
        }

        private void CmdWipe(BasePlayer player, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermWipe))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length < 2 || args[1].ToLower() != "confirm")
            {
                SendReply(player, Lang("WipeConfirm", player.UserIDString));
                return;
            }

            WipeAll();
            SendReply(player, Lang("WipeDone", player.UserIDString));
        }

        private void CmdRate(BasePlayer player)
        {
            if (_btcUsd <= 0)
            {
                SendReply(player, Lang("RateUnavailable", player.UserIDString));
                return;
            }

            SendReply(player, Lang("Rate", player.UserIDString, _btcUsd.ToString("N2"), _btcRub.ToString("N0"), _btcUsd24hChange, _lastRateUpdate.ToString("HH:mm:ss")));
        }

        #endregion

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
