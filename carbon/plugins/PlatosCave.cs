using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Plato's Cave", "semnavmeleon", "0.1.0")]
    [Description("STUB: illusory resource nodes that vanish on approach; a special item reveals what's real. Design not finalized.")]
    public class PlatosCave : RustPlugin
    {
        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("Prefabs to mimic as illusions (undecided: which resource nodes get fake copies)")]
            public List<string> MimicPrefabs = new List<string>();

            [JsonProperty("Vanish distance (meters)")]
            public float VanishDistance = 5f;

            [JsonProperty("Reveal item shortname (undecided: which real item grants true sight)")]
            public string RevealItemShortname = "";
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
            PrintWarning("Plato's Cave is a design stub, not a working plugin yet. Still open: which resources get illusory copies, " +
                         "how the reveal item works, where illusions spawn, and what happens when a player 'sees through' one.");
        }
    }
}
