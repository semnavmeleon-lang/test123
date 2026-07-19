using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Diogenes' Barrel", "semnavmeleon", "1.0.0")]
    [Description("Rewards players for owning almost nothing, mocking the hoard-everything economy.")]
    public class DiogenesBarrel : RustPlugin
    {
        [PluginReference] private Plugin SatoshiCoins;

        private PluginConfig _config;
        private StoredData _data;

        private class Tier
        {
            public float Seconds;
            public string Title;
            public int CoinReward;
        }

        private class PlayerProgress
        {
            public float CumulativeSeconds;
            public int HighestTierAwarded;
        }

        private class StoredData
        {
            public Dictionary<ulong, PlayerProgress> Progress = new Dictionary<ulong, PlayerProgress>();
        }

        private class PluginConfig
        {
            [JsonProperty("Max items to count as \"owning nothing\"")]
            public int MaxItems = 3;

            [JsonProperty("Check interval (seconds)")]
            public float CheckInterval = 30f;

            [JsonProperty("Tiers")]
            public List<Tier> Tiers = new List<Tier>
            {
                new Tier { Seconds = 3600f, Title = "Кинический ученик", CoinReward = 10 },
                new Tier { Seconds = 21600f, Title = "Почти философ", CoinReward = 50 },
                new Tier { Seconds = 86400f, Title = "Диоген во плоти", CoinReward = 200 },
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

        #region Hooks

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Status"] = "Владеешь почти ничем: {0} из {1} предметов. Накоплено: {2} мин. Титул: {3}.",
                ["NoTitle"] = "пока никакого",
                ["TierReached"] = "Ты достиг титула \"{0}\"! +{1} сатоши коинов за отказ от собственности.",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
            timer.Every(_config.CheckInterval, Tick);
        }

        private void Unload() => SaveData();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        #endregion

        #region Commands

        [ChatCommand("diogenes")]
        private void CmdDiogenes(BasePlayer player, string command, string[] args)
        {
            _data.Progress.TryGetValue(player.userID, out var progress);
            var itemCount = CountItems(player);
            var title = progress != null && progress.HighestTierAwarded > 0
                ? _config.Tiers[progress.HighestTierAwarded - 1].Title
                : Lang("NoTitle", player.UserIDString);

            var minutes = progress != null ? (int)(progress.CumulativeSeconds / 60) : 0;
            SendReply(player, Lang("Status", player.UserIDString, itemCount, _config.MaxItems, minutes, title));
        }

        #endregion

        #region Core

        private void Tick()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;

                var itemCount = CountItems(player);
                if (itemCount > _config.MaxItems) continue;

                _data.Progress.TryGetValue(player.userID, out var progress);
                if (progress == null)
                {
                    progress = new PlayerProgress();
                    _data.Progress[player.userID] = progress;
                }

                progress.CumulativeSeconds += _config.CheckInterval;

                for (var i = progress.HighestTierAwarded; i < _config.Tiers.Count; i++)
                {
                    var tier = _config.Tiers[i];
                    if (progress.CumulativeSeconds < tier.Seconds) break;

                    progress.HighestTierAwarded = i + 1;

                    long rewardGiven = 0;
                    if (tier.CoinReward > 0 && SatoshiCoins != null)
                    {
                        var result = SatoshiCoins.Call("DepositScaled", player.userID, (long)tier.CoinReward);
                        rewardGiven = result != null ? Convert.ToInt64(result) : 0L;
                    }

                    SendReply(player, Lang("TierReached", player.UserIDString, tier.Title, rewardGiven));
                }
            }

            SaveData();
        }

        private int CountItems(BasePlayer player)
        {
            var count = 0;
            if (player.inventory?.containerMain != null) count += player.inventory.containerMain.itemList.Count;
            if (player.inventory?.containerBelt != null) count += player.inventory.containerBelt.itemList.Count;
            if (player.inventory?.containerWear != null) count += player.inventory.containerWear.itemList.Count;
            return count;
        }

        #endregion

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
