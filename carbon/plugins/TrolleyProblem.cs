using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Trolley Problem", "semnavmeleon", "0.1.0")]
    [Description("STUB: runaway-cart event with a lever choice between two outcomes. Design not finalized.")]
    public class TrolleyProblem : RustPlugin
    {
        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("What the two tracks affect (undecided: NPCs, resources, or players)")]
            public string TrackTargetType = "";

            [JsonProperty("Vote/decision window (seconds)")]
            public float DecisionWindowSeconds = 30f;
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
            PrintWarning("Trolley Problem is a design stub, not a working plugin yet. Still open: what triggers the event, " +
                         "what the two tracks actually put at risk, who decides (one player or a vote), and how choices get logged.");
        }
    }
}
