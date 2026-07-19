using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Abilene Paradox", "semnavmeleon", "1.0.0")]
    [Description("Private truth vs public vote -- reveals when the group did something nobody actually wanted.")]
    public class AbileneParadox : RustPlugin
    {
        private PluginConfig _config;
        private StoredData _data;

        private VoteSession _session;

        private class VoteSession
        {
            public string Question;
            public bool PublicPhase;
            public Dictionary<ulong, bool> PrivateTruth = new Dictionary<ulong, bool>();
            public Dictionary<ulong, bool> PublicVote = new Dictionary<ulong, bool>();
        }

        private class StoredData
        {
            public int TotalVotes;
            public int TotalParadoxes;
        }

        private class PluginConfig
        {
            [JsonProperty("Private phase duration (seconds)")]
            public float PrivateWindowSeconds = 30f;

            [JsonProperty("Public phase duration (seconds)")]
            public float PublicWindowSeconds = 60f;
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
                ["Usage"] = "/abilene start <вопрос> | truth yes|no | vote yes|no | stats",
                ["AlreadyRunning"] = "Голосование уже идёт: \"{0}\".",
                ["Started"] = "ТАЙНЫЙ этап начался: \"{0}\". У тебя есть {1} сек — ответь честно: /abilene truth yes|no (никто не увидит твой ответ).",
                ["TruthRecorded"] = "Записано тайно. Никто не узнает, пока не закончится голосование.",
                ["NotPrivatePhase"] = "Сейчас не время для честного ответа.",
                ["PublicPhaseStarted"] = "ПУБЛИЧНЫЙ этап начался: \"{0}\". Голосуй в открытую: /abilene vote yes|no — {1} сек, все видят.",
                ["NotPublicPhase"] = "Сейчас не время для публичного голосования.",
                ["PublicVoteCast"] = "{0} проголосовал(а) публично: {1}",
                ["Yes"] = "ЗА",
                ["No"] = "ПРОТИВ",
                ["Resolved"] = "Голосование окончено: \"{0}\". Публично: {1}. Решение: {2}.",
                ["ParadoxRevealed"] = "Из {0} проголосовавших {1} тайно думали иначе, чем проголосовали публично. Итог не совпал с честным большинством ({2}) — классический парадокс Абилина.",
                ["NoParadox"] = "Из {0} проголосовавших {1} тайно думали иначе, чем проголосовали публично. Но итог всё равно совпал с честным большинством.",
                ["Stats"] = "Абилинометр: {0} голосований, {1} настоящих парадоксов (группа сделала то, чего честно не хотела).",
                ["NeedQuestion"] = "Укажи вопрос: /abilene start <вопрос>",
            }, this);
        }

        private void OnServerInitialized()
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name) ?? new StoredData();
        }

        private void Unload() => SaveData();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

        #endregion

        #region Commands

        [ChatCommand("abilene")]
        private void CmdAbilene(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, Lang("Usage", player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    CmdStart(player, args);
                    break;
                case "truth":
                    CmdTruth(player, args);
                    break;
                case "vote":
                    CmdVote(player, args);
                    break;
                case "stats":
                    SendReply(player, Lang("Stats", player.UserIDString, _data.TotalVotes, _data.TotalParadoxes));
                    break;
                default:
                    SendReply(player, Lang("Usage", player.UserIDString));
                    break;
            }
        }

        private void CmdStart(BasePlayer player, string[] args)
        {
            if (_session != null)
            {
                SendReply(player, Lang("AlreadyRunning", player.UserIDString, _session.Question));
                return;
            }

            if (args.Length < 2)
            {
                SendReply(player, Lang("NeedQuestion", player.UserIDString));
                return;
            }

            var question = string.Join(" ", args, 1, args.Length - 1);
            _session = new VoteSession { Question = question, PublicPhase = false };

            PrintToChat(Lang("Started", null, question, _config.PrivateWindowSeconds));
            timer.Once(_config.PrivateWindowSeconds, StartPublicPhase);
        }

        private void CmdTruth(BasePlayer player, string[] args)
        {
            if (_session == null || _session.PublicPhase)
            {
                SendReply(player, Lang("NotPrivatePhase", player.UserIDString));
                return;
            }

            if (!TryParseYesNo(args, out var value))
            {
                SendReply(player, Lang("Usage", player.UserIDString));
                return;
            }

            _session.PrivateTruth[player.userID] = value;
            SendReply(player, Lang("TruthRecorded", player.UserIDString));
        }

        private void CmdVote(BasePlayer player, string[] args)
        {
            if (_session == null || !_session.PublicPhase)
            {
                SendReply(player, Lang("NotPublicPhase", player.UserIDString));
                return;
            }

            if (!TryParseYesNo(args, out var value))
            {
                SendReply(player, Lang("Usage", player.UserIDString));
                return;
            }

            _session.PublicVote[player.userID] = value;
            var label = value ? Lang("Yes", player.UserIDString) : Lang("No", player.UserIDString);
            PrintToChat(Lang("PublicVoteCast", null, player.displayName, label));
        }

        #endregion

        #region Core

        private void StartPublicPhase()
        {
            if (_session == null) return;

            _session.PublicPhase = true;
            PrintToChat(Lang("PublicPhaseStarted", null, _session.Question, _config.PublicWindowSeconds));
            timer.Once(_config.PublicWindowSeconds, ResolveVote);
        }

        private void ResolveVote()
        {
            if (_session == null) return;

            var publicYes = 0;
            var publicNo = 0;
            foreach (var value in _session.PublicVote.Values)
            {
                if (value) publicYes++; else publicNo++;
            }

            var truthYes = 0;
            var truthNo = 0;
            foreach (var value in _session.PrivateTruth.Values)
            {
                if (value) truthYes++; else truthNo++;
            }

            var publicResult = publicYes > publicNo;
            var truthMajority = truthYes > truthNo;

            var mismatchCount = 0;
            foreach (var kvp in _session.PublicVote)
            {
                if (_session.PrivateTruth.TryGetValue(kvp.Key, out var truth) && truth != kvp.Value)
                    mismatchCount++;
            }

            var resultLabel = publicResult ? Lang("Yes", null) : Lang("No", null);
            var publicTally = $"{publicYes}:{publicNo}";

            PrintToChat(Lang("Resolved", null, _session.Question, publicTally, resultLabel));

            var isParadox = _session.PublicVote.Count > 0 && publicResult != truthMajority;
            _data.TotalVotes++;
            if (isParadox) _data.TotalParadoxes++;
            SaveData();

            var truthLabel = truthMajority ? Lang("Yes", null) : Lang("No", null);
            if (isParadox)
                PrintToChat(Lang("ParadoxRevealed", null, _session.PublicVote.Count, mismatchCount, truthLabel));
            else
                PrintToChat(Lang("NoParadox", null, _session.PublicVote.Count, mismatchCount));

            _session = null;
        }

        private bool TryParseYesNo(string[] args, out bool value)
        {
            value = false;
            if (args.Length < 2) return false;

            var arg = args[1].ToLower();
            if (arg == "yes" || arg == "да") { value = true; return true; }
            if (arg == "no" || arg == "нет") { value = false; return true; }
            return false;
        }

        #endregion

        private string Lang(string key, string userId, params object[] args) =>
            string.Format(lang.GetMessage(key, this, userId), args);
    }
}
