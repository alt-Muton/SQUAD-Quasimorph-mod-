using System;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Raid-only display: localized Magnum PMC prefix + callsign (monster.{ProfileId}.name).
    /// Magnum roster outside dungeon stays vanilla (no remapping).
    /// </summary>
    internal static class MagnumRaidNames
    {
        /// <summary>Reentrancy guard so nested Localization.Get for callsign is not remapped.</summary>
        [ThreadStatic]
        private static int _rawLookupDepth;

        /// <summary>True only inside an active dungeon with a squad raid — never on Magnum/space UI.</summary>
        public static bool IsRaidDisplayContext()
        {
            if (_rawLookupDepth > 0)
                return false;
            if (!Plugin.Config.Enabled || !SquadRaidState.Active)
                return false;
            try
            {
                return UI.Resolve<DungeonGameMode>() != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsRaidOperativeProfile(string profileId)
        {
            if (string.IsNullOrEmpty(profileId) || !IsRaidDisplayContext())
                return false;
            if (profileId == SquadRaidState.LeaderProfileId)
                return true;
            return SquadRaidState.AllyProfileIds.Contains(profileId);
        }

        public static Gender ResolveGender(string profileId)
        {
            try
            {
                var merc = UI.Resolve<Mercenaries>()?.Get(profileId);
                if (merc?.CreatureData != null)
                    return merc.CreatureData.Gender;
            }
            catch
            {
                // ignore
            }
            return Gender.Male;
        }

        public static Localization.Lang ResolveLang(Localization.Lang? language)
        {
            if (language.HasValue)
                return language.Value;
            try
            {
                return Singleton<Localization>.Instance.CurrentLang;
            }
            catch
            {
                return Localization.Lang.EnglishUS;
            }
        }

        /// <summary>
        /// Magnum PMC operative label for every language in Settings → Language.
        /// Uses gendered forms where the language distinguishes them.
        /// </summary>
        public static string PrefixFor(Gender gender, Localization.Lang lang)
        {
            var female = gender == Gender.Female;
            switch (lang)
            {
                case Localization.Lang.Russian:
                    return female ? "Наемница ЧВК \"Магнум\"" : "Наемник ЧВК \"Магнум\"";
                case Localization.Lang.German:
                    return female ? "Magnum-PMC-Söldnerin" : "Magnum-PMC-Söldner";
                case Localization.Lang.French:
                    return "Mercenaire du PMC « Magnum »";
                case Localization.Lang.Spanish:
                    return female ? "Mercenaria del PMC Magnum" : "Mercenario del PMC Magnum";
                case Localization.Lang.Polish:
                    return female ? "Najemniczka PMC Magnum" : "Najemnik PMC Magnum";
                case Localization.Lang.Turkish:
                    return "Magnum PMC paralı askeri";
                case Localization.Lang.BrazilianPortugal:
                    return female ? "Mercenária do PMC Magnum" : "Mercenário do PMC Magnum";
                case Localization.Lang.Korean:
                    return "마그넘 PMC 용병";
                case Localization.Lang.Japanese:
                    return "マグナムPMCの傭兵";
                case Localization.Lang.ChineseSimp:
                    return "马格南PMC佣兵";
                case Localization.Lang.EnglishUS:
                default:
                    return "Magnum PMC mercenary";
            }
        }

        /// <summary>
        /// Callsign from monster.{ProfileId}.name (e.g. Паладин / Копьё).
        /// Nested Localization.Get is not remapped (_rawLookupDepth).
        /// </summary>
        public static string ResolveCallsign(string profileId, Localization.Lang? language = null)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            var callsignKey = "monster." + profileId + ".name";
            try
            {
                _rawLookupDepth++;
                string value = language.HasValue
                    ? Localization.Get(callsignKey, language.Value)
                    : Localization.Get(callsignKey);

                if (!string.IsNullOrEmpty(value) && value != callsignKey)
                    return value;
            }
            catch
            {
                // fall through
            }
            finally
            {
                _rawLookupDepth--;
            }

            try
            {
                var agent = UI.Resolve<Mercenaries>()?.Get(profileId)?.AgentName;
                if (!string.IsNullOrWhiteSpace(agent))
                    return agent.Trim();
            }
            catch
            {
                // ignore
            }

            return null;
        }

        public static string FormatRaidDisplayName(string profileId, Localization.Lang? language = null)
        {
            var lang = ResolveLang(language);
            var prefix = PrefixFor(ResolveGender(profileId), lang);
            var callsign = ResolveCallsign(profileId, language ?? lang);
            if (string.IsNullOrEmpty(callsign))
                return prefix;
            return prefix + " " + callsign;
        }

        public static bool TryResolveRaidNameKey(string key, out string displayName)
        {
            return TryResolveRaidNameKey(key, null, out displayName);
        }

        public static bool TryResolveRaidNameKey(string key, Localization.Lang? language, out string displayName)
        {
            displayName = null;
            if (!IsRaidDisplayContext() || string.IsNullOrEmpty(key))
                return false;

            string profileId = null;
            if (key.StartsWith("monster.") && key.EndsWith(".name"))
                profileId = key.Substring("monster.".Length, key.Length - "monster.".Length - ".name".Length);
            else if (key.StartsWith("spec.") && key.EndsWith(".name"))
                profileId = key.Substring("spec.".Length, key.Length - "spec.".Length - ".name".Length);

            if (string.IsNullOrEmpty(profileId) || !IsRaidOperativeProfile(profileId))
                return false;

            displayName = FormatRaidDisplayName(profileId, language);
            return !string.IsNullOrEmpty(displayName);
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(bool))]
    internal static class MagnumNameLocalizationPatch
    {
        private static void Postfix(string key, ref string __result)
        {
            if (MagnumRaidNames.TryResolveRaidNameKey(key, out var name))
                __result = name;
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(Localization.Lang))]
    internal static class MagnumNameLocalizationLangPatch
    {
        private static void Postfix(string key, Localization.Lang language, ref string __result)
        {
            if (MagnumRaidNames.TryResolveRaidNameKey(key, language, out var name))
                __result = name;
        }
    }
}
