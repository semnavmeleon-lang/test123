using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Satoshi Coins", "semnavmeleon", "1.0.0")]
    [Description("Standalone currency plugin: Satoshi Coins.")]
    public class SatoshiCoins : RustPlugin
    {
        private const string PermAdmin = "satoshicoins.admin";

        private StoredData _data;

        private class StoredData
        {
            public Dictionary<ulong, long> Balances = new Dictionary<ulong, long>();
        }

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermAdmin, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Balance"] = "У тебя {0} сатоши коинов.",
                ["BalanceOther"] = "{0}: {1} сатоши коинов.",
                ["InsufficientFunds"] = "Недостаточно сатоши коинов.",
                ["NoPermission"] = "У тебя нет прав на это.",
                ["PlayerNotFound"] = "Игрок не найден: {0}",
                ["PayUsage"] = "/coins pay <игрок> <сумма>",
                ["PaySuccess"] = "Ты перевёл {0} сатоши коинов игроку {1}.",
                ["PayReceived"] = "{0} перевёл(а) тебе {1} сатоши коинов.",
                ["InvalidAmount"] = "Некорректная сумма.",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
        }

        private void Unload() => SaveData();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

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
            _data.Balances[playerId] = balance + amount;
            SaveData();
            return true;
        }

        public bool Withdraw(ulong playerId, long amount)
        {
            if (amount <= 0) return false;
            _data.Balances.TryGetValue(playerId, out var balance);
            if (balance < amount) return false;
            _data.Balances[playerId] = balance - amount;
            SaveData();
            return true;
        }

        #endregion

        #region Commands

        [ChatCommand("coins")]
        private void CmdCoins(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, Lang("Balance", player.UserIDString, Balance(player.userID)));
                return;
            }

            switch (args[0].ToLower())
            {
                case "pay":
                    CmdPay(player, args);
                    break;

                case "give":
                    CmdGive(player, args, true);
                    break;

                case "take":
                    CmdGive(player, args, false);
                    break;

                case "balance":
                    var target = args.Length > 1 ? BasePlayer.Find(args[1]) : player;
                    if (target == null)
                    {
                        SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                        return;
                    }
                    SendReply(player, Lang("BalanceOther", player.UserIDString, target.displayName, Balance(target.userID)));
                    break;
            }
        }

        private void CmdPay(BasePlayer player, string[] args)
        {
            if (args.Length < 3 || !long.TryParse(args[2], out var amount) || amount <= 0)
            {
                SendReply(player, Lang("PayUsage", player.UserIDString));
                return;
            }

            var target = BasePlayer.Find(args[1]);
            if (target == null)
            {
                SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                return;
            }

            if (!Withdraw(player.userID, amount))
            {
                SendReply(player, Lang("InsufficientFunds", player.UserIDString));
                return;
            }

            Deposit(target.userID, amount);

            SendReply(player, Lang("PaySuccess", player.UserIDString, amount, target.displayName));
            SendReply(target, Lang("PayReceived", target.UserIDString, player.displayName, amount));
        }

        private void CmdGive(BasePlayer player, string[] args, bool give)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length < 3 || !long.TryParse(args[2], out var amount) || amount <= 0)
            {
                SendReply(player, Lang("InvalidAmount", player.UserIDString));
                return;
            }

            var target = BasePlayer.Find(args[1]);
            if (target == null)
            {
                SendReply(player, Lang("PlayerNotFound", player.UserIDString, args[1]));
                return;
            }

            if (give)
                Deposit(target.userID, amount);
            else
                Withdraw(target.userID, amount);

            SendReply(player, Lang("BalanceOther", player.UserIDString, target.displayName, Balance(target.userID)));
        }

        #endregion

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
