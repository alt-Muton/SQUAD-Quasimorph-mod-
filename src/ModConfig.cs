using System;
using System.IO;
using Newtonsoft.Json;

namespace Squad_Leader
{
    public class ModConfig
    {
        /// <summary>Master enable for multi-operative briefing and ally spawn.</summary>
        public bool Enabled = true;

        /// <summary>Realtime seconds to wait on a dead leader before possessing the nearest ally. 0 = instant.</summary>
        public float HandoffDelaySeconds = 2f;

        /// <summary>
        /// Enemy-like combat for squad operatives: own vision + range (already on),
        /// ballistic friendly-fire check, hold when a shot is blocked in range.
        /// No flank / hide / grenade / scatter cone.
        /// </summary>
        public bool ExperimentalAi = false;

        public static ModConfig LoadConfig(string configPath)
        {
            var serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };

            if (File.Exists(configPath))
            {
                try
                {
                    var sourceJson = File.ReadAllText(configPath);
                    var config = JsonConvert.DeserializeObject<ModConfig>(sourceJson, serializerSettings)
                                 ?? new ModConfig();

                    var upgradeConfig = JsonConvert.SerializeObject(config, serializerSettings);
                    if (upgradeConfig != sourceJson)
                        File.WriteAllText(configPath, upgradeConfig);

                    return config;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError("Error parsing configuration. Using defaults.");
                    Plugin.Logger.LogException(ex);
                    return new ModConfig();
                }
            }

            var fresh = new ModConfig();
            File.WriteAllText(configPath, JsonConvert.SerializeObject(fresh, serializerSettings));
            return fresh;
        }

        public void Save(string configPath)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }

        public void ApplyMcmValues(System.Collections.Generic.Dictionary<string, object> values)
        {
            if (values == null)
                return;
            if (TryGetBool(values, nameof(ExperimentalAi), out var experimentalAi))
                ExperimentalAi = experimentalAi;
        }

        private static bool TryGetBool(
            System.Collections.Generic.Dictionary<string, object> values,
            string key,
            out bool result)
        {
            result = false;
            if (!values.TryGetValue(key, out var raw) || raw == null)
                return false;
            try
            {
                result = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
