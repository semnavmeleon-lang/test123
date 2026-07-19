using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Butterfly Effect", "semnavmeleon", "0.1.0")]
    [Description("STUB: an unannounced tiny trigger unpredictably sets off a large delayed event. Design not finalized.")]
    public class ButterflyEffect : RustPlugin
    {
        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("Trigger action (undecided: which specific player action starts the countdown)")]
            public string TriggerAction = "";

            [JsonProperty("Delay range, minimum (seconds)")]
            public float MinDelaySeconds = 3600f;

            [JsonProperty("Delay range, maximum (seconds)")]
            public float MaxDelaySeconds = 21600f;

            [JsonProperty("Resulting event (undecided: meteor shower, loot event, etc.)")]
            public string ResultingEvent = "";
        }

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

        private void Init()
        {
            PrintWarning("Butterfly Effect is a design stub, not a working plugin yet. Still open: what the actual trigger action is, " +
                         "how it stays hidden from players, and what the delayed payoff event actually does.");
        }
    }
}
