using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Achilles and the Boar", "semnavmeleon", "1.0.0")]
    [Description("Zeno's paradox: the boar is always just out of reach, until it suddenly isn't.")]
    public class AchillesBoar : RustPlugin
    {
        private const string BoarPrefab = "assets/rust.ai/agents/boar/boar.prefab";

        [PluginReference] private Plugin SatoshiCoins;

        private PluginConfig _config;
        private StoredData _data;

        private readonly Dictionary<ulong, Chase> _chases = new Dictionary<ulong, Chase>();

        private class Chase
        {
            public BaseEntity Boar;
            public float ElapsedSeconds;
            public bool Caught;
        }

        private class StoredData
        {
            public Dictionary<ulong, float> Progress = new Dictionary<ulong, float>();
        }

        private class PluginConfig
        {
            [JsonProperty("Chase duration required (seconds)")]
            public float ChaseDurationSeconds = 3600f;

            [JsonProperty("Evade trigger distance (meters)")]
            public float EvadeDistance = 8f;

            [JsonProperty("Evade teleport distance (meters)")]
            public float EvadeTeleportDistance = 25f;

            [JsonProperty("Engagement radius to count as actively chasing (meters)")]
            public float EngagementRadius = 40f;

            [JsonProperty("Tick interval (seconds)")]
            public float TickInterval = 1f;

            [JsonProperty("Satoshi coins reward on catch")]
            public int CoinReward = 3;
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
                ["Usage"] = "/achilles [start|status|stop]",
                ["AlreadyChasing"] = "Ты уже гонишься за вепрем. /achilles status — проверить прогресс.",
                ["NotChasing"] = "Ты ни за кем не гонишься. /achilles start — начать погоню.",
                ["Started"] = "Вепрь бросился наутёк. Он всегда будет чуть впереди... примерно {0:0} минут, если не отстанешь.",
                ["Status"] = "В погоне: {0} из {1} минут. Осталось: {2} мин, если не отставать.",
                ["Stopped"] = "Ты сдался. Вепрь свободен, прогресс обнулён.",
                ["BoarStops"] = "Вепрь вдруг останавливается, обессилев. Похоже, время пришло.",
                ["CaughtTooSoon"] = "Вепрь погиб раньше срока — испытание не засчитано, прогресс обнулён.",
                ["StolenCatch"] = "Вепря добил кто-то другой. Прогресс обнулён — Ахиллес так и не догнал черепаху.",
                ["Caught"] = "Ты наконец догнал его. +{0} сатоши коинов.",
                ["CaughtNoCoins"] = "Ты наконец догнал его. (Монеты не начислены — проверь SatoshiCoins.)",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
            timer.Every(_config.TickInterval, Tick);
        }

        private void Unload()
        {
            foreach (var kvp in _chases)
                _data.Progress[kvp.Key] = kvp.Value.ElapsedSeconds;
            SaveData();
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            ulong ownerId = 0;
            Chase chase = null;

            foreach (var kvp in _chases)
            {
                if (kvp.Value.Boar == entity)
                {
                    ownerId = kvp.Key;
                    chase = kvp.Value;
                    break;
                }
            }

            if (chase == null) return;

            _chases.Remove(ownerId);
            _data.Progress.Remove(ownerId);
            SaveData();

            var attacker = info?.Initiator as BasePlayer;
            var rightfulKill = chase.Caught && attacker != null && attacker.userID == ownerId;

            if (!rightfulKill)
            {
                Notify(ownerId, chase.Caught ? "StolenCatch" : "CaughtTooSoon");
                return;
            }

            long rewardGiven = 0;
            if (_config.CoinReward > 0 && SatoshiCoins != null)
            {
                var result = SatoshiCoins.Call("DepositScaled", ownerId, (long)_config.CoinReward);
                rewardGiven = result != null ? Convert.ToInt64(result) : 0L;
            }

            Notify(ownerId, rewardGiven > 0 ? "Caught" : "CaughtNoCoins", rewardGiven);
        }

        #endregion

        #region Commands

        [ChatCommand("achilles")]
        private void CmdAchilles(BasePlayer player, string command, string[] args)
        {
            var sub = args.Length > 0 ? args[0].ToLower() : "start";

            switch (sub)
            {
                case "start":
                    CmdStart(player);
                    break;
                case "status":
                    CmdStatus(player);
                    break;
                case "stop":
                    CmdStop(player);
                    break;
                default:
                    SendReply(player, Lang("Usage", player.UserIDString));
                    break;
            }
        }

        private void CmdStart(BasePlayer player)
        {
            if (_chases.ContainsKey(player.userID))
            {
                SendReply(player, Lang("AlreadyChasing", player.UserIDString));
                return;
            }

            var spawnPos = player.transform.position + player.transform.forward * 5f;
            spawnPos.y = TerrainMeta.HeightMap.GetHeight(spawnPos);

            var boar = GameManager.server.CreateEntity(BoarPrefab, spawnPos, Quaternion.identity);
            if (boar == null)
            {
                PrintError("Не удалось заспавнить вепря.");
                return;
            }
            boar.Spawn();

            _data.Progress.TryGetValue(player.userID, out var savedProgress);

            _chases[player.userID] = new Chase
            {
                Boar = boar,
                ElapsedSeconds = savedProgress,
            };

            SendReply(player, Lang("Started", player.UserIDString, _config.ChaseDurationSeconds / 60f));
        }

        private void CmdStatus(BasePlayer player)
        {
            if (!_chases.TryGetValue(player.userID, out var chase))
            {
                SendReply(player, Lang("NotChasing", player.UserIDString));
                return;
            }

            var remaining = Math.Max(0, _config.ChaseDurationSeconds - chase.ElapsedSeconds);
            SendReply(player, Lang("Status", player.UserIDString,
                (int)(chase.ElapsedSeconds / 60), (int)(_config.ChaseDurationSeconds / 60), (int)(remaining / 60)));
        }

        private void CmdStop(BasePlayer player)
        {
            if (!_chases.TryGetValue(player.userID, out var chase))
            {
                SendReply(player, Lang("NotChasing", player.UserIDString));
                return;
            }

            if (chase.Boar != null && !chase.Boar.IsDestroyed)
                chase.Boar.Kill();

            _chases.Remove(player.userID);
            _data.Progress.Remove(player.userID);
            SaveData();

            SendReply(player, Lang("Stopped", player.UserIDString));
        }

        #endregion

        #region Core

        private void Tick()
        {
            if (_chases.Count == 0) return;

            foreach (var kvp in new List<KeyValuePair<ulong, Chase>>(_chases))
            {
                var playerId = kvp.Key;
                var chase = kvp.Value;

                if (chase.Boar == null || chase.Boar.IsDestroyed)
                {
                    _chases.Remove(playerId);
                    continue;
                }

                if (chase.Caught) continue;

                var player = BasePlayer.FindByID(playerId);
                if (player == null) continue;

                var distance = Vector3.Distance(player.transform.position, chase.Boar.transform.position);
                if (distance > _config.EngagementRadius) continue;

                chase.ElapsedSeconds += _config.TickInterval;

                if (distance <= _config.EvadeDistance)
                    EvadeBoar(chase.Boar, player);

                if (chase.ElapsedSeconds >= _config.ChaseDurationSeconds)
                {
                    chase.Caught = true;
                    Notify(playerId, "BoarStops");
                }
            }
        }

        private void EvadeBoar(BaseEntity boar, BasePlayer player)
        {
            var direction = boar.transform.position - player.transform.position;
            direction.y = 0;

            if (direction.sqrMagnitude < 0.01f)
            {
                var rand = UnityEngine.Random.insideUnitCircle.normalized;
                direction = new Vector3(rand.x, 0, rand.y);
            }
            else
            {
                direction = direction.normalized;
            }

            var newPos = player.transform.position + direction * _config.EvadeTeleportDistance;
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);

            boar.transform.position = newPos;
            boar.UpdateNetworkGroup();
            boar.SendNetworkUpdateImmediate();
        }

        private void Notify(ulong playerId, string key, params object[] args)
        {
            var player = BasePlayer.FindByID(playerId);
            if (player != null)
                SendReply(player, Lang(key, player.UserIDString, args));
        }

        #endregion

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
