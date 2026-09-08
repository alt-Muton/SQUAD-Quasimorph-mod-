using System;
using System.Collections.Generic;
using ModConfigMenu;
using ModConfigMenu.Contracts;
using ModConfigMenu.Objects;

namespace Squad_Leader
{
    /// <summary>
    /// Soft MCM registration — Squad Leader still loads if Crynano_ModConfigMenu is absent.
    /// </summary>
    public static class SquadMcmRegistration
    {
        private const string ModDisplayName = "Squad";

        public static void TryRegister(ModConfig config)
        {
            try
            {
                Register(config);
            }
            catch (Exception ex) when (IsMcmMissing(ex))
            {
                Plugin.Logger.Log("MCM not found — ExperimentalAi stays in config.json / console only.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("MCM registration failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        private static void Register(ModConfig config)
        {
            if (config == null)
                return;

            var values = new List<IConfigValue>
            {
                new ConfigValue(
                    nameof(ModConfig.ExperimentalAi),
                    config.ExperimentalAi,
                    "Combat",
                    false,
                    "Allies will no longer shoot you in the back!",
                    "Experimental combat AI")
            };

            ModConfigMenuAPI.RegisterModConfig(ModDisplayName, values, OnMcmSave);
            Plugin.Logger.Log("Registered with MCM (" + ModDisplayName + ").");
        }

        private static bool OnMcmSave(Dictionary<string, object> saved, out string feedback)
        {
            feedback = string.Empty;
            try
            {
                var config = Plugin.Config;
                if (config == null)
                {
                    feedback = "Squad config not loaded.";
                    return false;
                }

                config.ApplyMcmValues(saved);
                config.Save(Plugin.ConfigDirectories.ConfigPath);
                feedback = "Squad settings saved.";
                Plugin.Logger.Log("MCM saved. ExperimentalAi=" + config.ExperimentalAi);
                return true;
            }
            catch (Exception ex)
            {
                feedback = ex.Message;
                Plugin.Logger.LogException(ex);
                return false;
            }
        }

        private static bool IsMcmMissing(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is System.IO.FileNotFoundException fnf &&
                    fnf.FileName != null &&
                    fnf.FileName.IndexOf("MCM", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return ex is System.IO.FileNotFoundException ||
                   ex is TypeLoadException;
        }
    }
}
