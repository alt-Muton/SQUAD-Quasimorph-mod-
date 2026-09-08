using System.Collections.Generic;
using System.Text;
using MGSC;

namespace Squad_Leader
{
    [ConsoleCommand(new[] { "squad_leader", "squadleader", "sl" })]
    public class SquadLeaderCommand
    {
        public static string Help(string command, bool verbose)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Squad Leader mod commands.");
            sb.AppendLine("  squad_leader status");
            sb.AppendLine("  squad_leader enable");
            sb.AppendLine("  squad_leader disable");
            sb.AppendLine("  squad_leader ai");
            sb.AppendLine("  squad_leader ai on|off");
            sb.AppendLine("Aliases: squadleader, sl");
            return sb.ToString().TrimEnd();
        }

        public string Execute(string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
                return Help("squad_leader", false);

            var verb = (tokens[0] ?? "").Trim().ToLowerInvariant();
            switch (verb)
            {
                case "status":
                case "info":
                    return Status();
                case "enable":
                case "on":
                    return SetEnabled(true);
                case "disable":
                case "off":
                    return SetEnabled(false);
                case "ai":
                    return Ai(tokens);
                case "help":
                    return Help("squad_leader", true);
                default:
                    return "Unknown subcommand: " + tokens[0] + ". Try: squad_leader status";
            }
        }

        public static List<string> FetchAutocompleteOptions(string command, string[] tokens)
        {
            if (tokens == null || tokens.Length <= 1)
                return new List<string> { "status", "enable", "disable", "ai", "help" };
            if (tokens.Length == 2 && (tokens[0] ?? "").Trim().ToLowerInvariant() == "ai")
                return new List<string> { "on", "off" };
            return null;
        }

        public static bool IsAvailable()
        {
            return true;
        }

        public static bool ShowInHelpAndAutocomplete()
        {
            return true;
        }

        private static string Status()
        {
            var config = Plugin.Config;
            if (config == null)
                return "Squad Leader: config not loaded yet.";

            var prep = "prepAllies=[" + string.Join(",", SquadSelectionState.SnapshotFilledAllies()) + "]";
            var raid = SquadRaidState.StatusLine();
            long dllBytes = 0;
            try
            {
                var loc = typeof(Plugin).Assembly.Location;
                if (!string.IsNullOrEmpty(loc) && System.IO.File.Exists(loc))
                    dllBytes = new System.IO.FileInfo(loc).Length;
            }
            catch
            {
                // ignore
            }

            return "Squad Leader OK. Enabled=" + config.Enabled
                   + " ExperimentalAi=" + config.ExperimentalAi
                   + " | dllBytes=" + dllBytes
                   + " | " + prep
                   + " | raid: " + raid
                   + " | config=" + Plugin.ConfigDirectories.ConfigPath;
        }

        private static string Ai(string[] tokens)
        {
            if (Plugin.Config == null)
                return "Squad Leader: config not loaded yet.";

            if (tokens.Length < 2)
                return "ExperimentalAi=" + Plugin.Config.ExperimentalAi
                       + " (MCM: Squad → Experimental combat AI). squad_leader ai on|off";

            var arg = (tokens[1] ?? "").Trim().ToLowerInvariant();
            bool enabled;
            if (arg == "on" || arg == "enable" || arg == "true" || arg == "1")
                enabled = true;
            else if (arg == "off" || arg == "disable" || arg == "false" || arg == "0")
                enabled = false;
            else
                return "Unknown ai argument: " + tokens[1] + ". Try: squad_leader ai on";

            Plugin.Config.ExperimentalAi = enabled;
            Plugin.Config.Save(Plugin.ConfigDirectories.ConfigPath);
            Plugin.Logger.Log("ExperimentalAi set to " + enabled);
            return "Squad Leader ExperimentalAi=" + enabled;
        }

        private static string SetEnabled(bool enabled)
        {
            if (Plugin.Config == null)
                return "Squad Leader: config not loaded yet.";

            Plugin.Config.Enabled = enabled;
            Plugin.Config.Save(Plugin.ConfigDirectories.ConfigPath);
            Plugin.Logger.Log("Enabled set to " + enabled);
            return "Squad Leader Enabled=" + enabled;
        }
    }
}
