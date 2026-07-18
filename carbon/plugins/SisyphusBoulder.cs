using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Sisyphus Boulder", "semnavmeleon", "1.0.0")]
    [Description("Curses a player to push a car up a hill, forever.")]
    public class SisyphusBoulder : RustPlugin
    {
        private const string PermAdmin = "sisyphusboulder.admin";

        [PluginReference] private Plugin SatoshiCoins;

        private PluginConfig _config;
        private StoredData _data;

        private readonly Dictionary<ulong, Boulder> _active = new Dictionary<ulong, Boulder>();
        private readonly Dictionary<ulong, Vector3> _pendingStart = new Dictionary<ulong, Vector3>();
        private readonly Dictionary<ulong, Quaternion> _pendingStartRot = new Dictionary<ulong, Quaternion>();
        private readonly Dictionary<ulong, Vector3> _pendingSummit = new Dictionary<ulong, Vector3>();

        #region Classes

        private class Boulder
        {
            public BaseEntity Car;
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public Vector3 SummitPosition;
        }

        private class CurseData
        {
            public ulong PlayerId;
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public Vector3 SummitPosition;
        }

        private class StoredData
        {
            public Dictionary<ulong, int> PushCounts = new Dictionary<ulong, int>();
            public List<CurseData> Curses = new List<CurseData>();
        }

        private class PluginConfig
        {
            [JsonProperty("Chassis prefab")]
            public string ChassisPrefab = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab";

            [JsonProperty("Summit trigger radius (meters)")]
            public float SummitRadius = 4f;

            [JsonProperty("Check interval (seconds)")]
            public float CheckInterval = 2f;

            [JsonProperty("Respawn car if destroyed")]
            public bool RespawnIfDestroyed = true;

            [JsonProperty("Satoshi coins reward per completed climb")]
            public int CoinReward = 1;
        }

        #endregion

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
            permission.RegisterPermission(PermAdmin, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "У тебя нет прав на это.",
                ["PlayerNotFound"] = "Игрок не найден: {0}",
                ["StartSet"] = "Подножие горы отмечено. Встань на вершине и введи /sisyphus setsummit",
                ["SummitSet"] = "Вершина отмечена. Теперь /sisyphus curse <игрок>",
                ["NeedPoints"] = "Сначала отметь точки: /sisyphus setstart и /sisyphus setsummit",
                ["AlreadyCursed"] = "{0} уже несёт свой валун в гору.",
                ["Cursed"] = "{0} проклят(а) вечно катить валун в гору.",
                ["YouAreCursed"] = "Боги прокляли тебя. Теперь ты будешь толкать этот автомобиль в гору. Вечно.",
                ["NotCursed"] = "{0} не проклят(а).",
                ["Freed"] = "{0} освобождён(а) от валуна. Пока что.",
                ["Reset"] = "Валун срывается вниз и катится к подножию... Попытка №{0}.",
                ["CoinsEarned"] = "+{0} сатоши коинов.",
                ["Stats"] = "{0}: попыток закатить валун на вершину — {1}. Вершина всё так же далека.",
                ["Usage"] = "/sisyphus setstart | setsummit | curse <игрок> | free <игрок> | stats [игрок]",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();

            foreach (var curse in _data.Curses)
            {
                SpawnBoulderFor(curse.PlayerId, curse.StartPosition, curse.StartRotation, curse.SummitPosition);
            }

            timer.Every(_config.CheckInterval, TickBoulders);
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            var car = entity as BaseEntity;
            if (car == null) return;

            foreach (var boulder in _active.Values)
            {
                if (boulder.Car == car)
                {
                    boulder.Car = null;
                    return;
                }
            }
        }

        #endregion

        #region Commands

        [ChatCommand("sisyphus")]
        private void CmdSisyphus(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                SendReply(player, Lang("NoPermission", player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, Lang("Usage", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "setstart":
                    _pendingStart[player.userID] = player.transform.position;
                    _pendingStartRot[player.userID] = player.transform.rotation;
                    SendReply(player, Lang("StartSet", player.UserIDString));
                    break;

                case "setsummit":
                    _pendingSummit[player.userID] = player.transform.position;
                    SendReply(player, Lang("SummitSet", player.UserIDString));
                    break;

                case "curse":
                    if (args.Length < 2)
                    {
                        SendReply(player, Lang("Usage", player.UserIDString));
                        return;
                    }
                    CurseTarget(player, args[1]);
                    break;

                case "free":
                    if (args.Length < 2)
                    {
                        SendReply(player, Lang("Usage", player.UserIDString));
                        return;
                    }
                    FreeTarget(player, args[1]);
                    break;

                case "stats":
                    var target = args.Length > 1 ? BasePlayer.Find(args[1]) : player;
                    ShowStats(player, target, args.Length > 1 ? args[1] : player.displayName);
                    break;

                default:
                    SendReply(player, Lang("Usage", player.UserIDString));
                    break;
            }
        }

        private void CurseTarget(BasePlayer admin, string nameOrId)
        {
            if (!_pendingStart.TryGetValue(admin.userID, out var start) ||
                !_pendingSummit.TryGetValue(admin.userID, out var summit))
            {
                SendReply(admin, Lang("NeedPoints", admin.UserIDString));
                return;
            }

            var target = BasePlayer.Find(nameOrId);
            if (target == null)
            {
                SendReply(admin, Lang("PlayerNotFound", admin.UserIDString, nameOrId));
                return;
            }

            if (_active.ContainsKey(target.userID))
            {
                SendReply(admin, Lang("AlreadyCursed", admin.UserIDString, target.displayName));
                return;
            }

            var rot = _pendingStartRot.TryGetValue(admin.userID, out var r) ? r : Quaternion.identity;

            SpawnBoulderFor(target.userID, start, rot, summit);
            _data.Curses.Add(new CurseData
            {
                PlayerId = target.userID,
                StartPosition = start,
                StartRotation = rot,
                SummitPosition = summit,
            });
            SaveData();

            _pendingStart.Remove(admin.userID);
            _pendingStartRot.Remove(admin.userID);
            _pendingSummit.Remove(admin.userID);

            SendReply(admin, Lang("Cursed", admin.UserIDString, target.displayName));
            SendReply(target, Lang("YouAreCursed", target.UserIDString));
        }

        private void FreeTarget(BasePlayer admin, string nameOrId)
        {
            var target = BasePlayer.Find(nameOrId);
            if (target == null || !_active.ContainsKey(target.userID))
            {
                SendReply(admin, Lang("NotCursed", admin.UserIDString, nameOrId));
                return;
            }

            if (_active.TryGetValue(target.userID, out var boulder))
            {
                if (boulder.Car != null && !boulder.Car.IsDestroyed)
                    boulder.Car.Kill();
                _active.Remove(target.userID);
            }

            _data.Curses.RemoveAll(c => c.PlayerId == target.userID);
            SaveData();

            SendReply(admin, Lang("Freed", admin.UserIDString, target.displayName));
        }

        private void ShowStats(BasePlayer requester, BasePlayer target, string label)
        {
            if (target == null)
            {
                SendReply(requester, Lang("PlayerNotFound", requester.UserIDString, label));
                return;
            }

            _data.PushCounts.TryGetValue(target.userID, out var count);
            SendReply(requester, Lang("Stats", requester.UserIDString, target.displayName, count));
        }

        #endregion

        #region Core

        private void SpawnBoulderFor(ulong playerId, Vector3 start, Quaternion rot, Vector3 summit)
        {
            var car = GameManager.server.CreateEntity(_config.ChassisPrefab, start, rot);
            if (car == null)
            {
                PrintError($"Не удалось заспавнить префаб: {_config.ChassisPrefab}");
                return;
            }

            car.OwnerID = playerId;
            car.Spawn();

            _active[playerId] = new Boulder
            {
                Car = car,
                StartPosition = start,
                StartRotation = rot,
                SummitPosition = summit,
            };
        }

        private void TickBoulders()
        {
            foreach (var kvp in _active)
            {
                var playerId = kvp.Key;
                var boulder = kvp.Value;

                if (boulder.Car == null || boulder.Car.IsDestroyed)
                {
                    if (_config.RespawnIfDestroyed)
                        SpawnBoulderFor(playerId, boulder.StartPosition, boulder.StartRotation, boulder.SummitPosition);
                    continue;
                }

                var distance = Vector3.Distance(boulder.Car.transform.position, boulder.SummitPosition);
                if (distance <= _config.SummitRadius)
                    ResetBoulder(playerId, boulder);
            }
        }

        private void ResetBoulder(ulong playerId, Boulder boulder)
        {
            var car = boulder.Car;
            car.transform.position = boulder.StartPosition;
            car.transform.rotation = boulder.StartRotation;

            var rb = car.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            car.UpdateNetworkGroup();
            car.SendNetworkUpdateImmediate();

            _data.PushCounts.TryGetValue(playerId, out var count);
            count++;
            _data.PushCounts[playerId] = count;
            SaveData();

            var player = BasePlayer.FindByID(playerId);

            if (_config.CoinReward > 0 && SatoshiCoins != null)
            {
                SatoshiCoins.Call("Deposit", playerId, (long)_config.CoinReward);
                if (player != null)
                    SendReply(player, Lang("CoinsEarned", player.UserIDString, _config.CoinReward));
            }

            if (player != null)
                SendReply(player, Lang("Reset", player.UserIDString, count));
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);

        #endregion
    }
}
