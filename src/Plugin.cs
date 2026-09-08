using System.IO;
using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    public static class Plugin
    {
        public static ConfigDirectories ConfigDirectories = new ConfigDirectories();
        public static ModConfig Config { get; private set; }
        public static Logger Logger = new Logger();

        internal static State GetCachedDungeonState() => _cachedDungeonState;
        private static State _cachedDungeonState;

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void AfterConfigsLoaded(IModContext context)
        {
            Directory.CreateDirectory(ConfigDirectories.ModPersistenceFolder);
            Config = ModConfig.LoadConfig(ConfigDirectories.ConfigPath);
            SquadMcmRegistration.TryRegister(Config);
            try
            {
                var harmony = new Harmony("Squad_Leader");
                var assembly = typeof(Plugin).Assembly;
                foreach (var type in AccessTools.GetTypesFromAssembly(assembly))
                {
                    try
                    {
                        harmony.CreateClassProcessor(type).Patch();
                    }
                    catch (System.Exception typeEx)
                    {
                        Logger.LogError("Harmony patch failed for " + type.FullName);
                        Logger.LogException(typeEx);
                    }
                }
                SquadBetterAiCompat.Patch(harmony);
                Logger.Log("Squad Leader loaded. Enabled=" + Config.Enabled
                           + " ExperimentalAi=" + Config.ExperimentalAi);
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Harmony setup failed.");
                Logger.LogException(ex);
            }
        }

        [Hook(ModHookType.MainMenuStarted)]
        public static void MainMenuStarted(IModContext context)
        {
            // Exit-to-menu mid-raid: drop in-memory Magnum-name context only.
            // Keep squad_raid_*.json so LoadGame can restore allies (handoff / AP / rebind).
            if (SquadRaidState.Active || SquadRaidState.AllyProfileIds.Count > 0)
            {
                Logger.Log("MainMenuStarted: clearing in-memory squad (persistence kept).");
                TryRestoreAllyHunger(context?.State);
                SquadRaidState.Clear(deletePersistence: false);
            }
            PrepareRaidSquadController.ClearInstance();
            _cachedDungeonState = null;
        }

        [Hook(ModHookType.AfterDungeonLoaded)]
        public static void AfterDungeonLoaded(IModContext context)
        {
            if (!Config.Enabled)
                return;
            _cachedDungeonState = context.State;
            SquadRaidPersistence.SetDungeonState(context.State);
            if (SquadRaidPersistence.TryRestore(context.State))
                Logger.Log("AfterDungeonLoaded: squad restored. " + SquadRaidState.StatusLine());
            SquadRestartStageGuard.AlignSquadWithLoadedMercenaries(context.State);
        }

        [Hook(ModHookType.DungeonStarted)]
        public static void DungeonStarted(IModContext context)
        {
            if (!Config.Enabled)
                return;

            _cachedDungeonState = context.State;
            SquadRaidPersistence.SetDungeonState(context.State);

            var restartReload = SquadRestartStageGuard.PendingRestartStageReload;

            // Load path: AfterDungeonLoaded may have restored state; if not and memory empty, try file again.
            if (!SquadRaidState.Active || SquadRaidState.AllyProfileIds.Count == 0)
                SquadRaidPersistence.TryRestore(context.State);

            SquadRestartStageGuard.AlignSquadWithLoadedMercenaries(context.State);

            // File missing / wiped by older builds: rebuild from living roster mercs still on the map.
            if (SquadRaidState.AllyProfileIds.Count == 0 && !restartReload)
                SquadRaidPersistence.TryRecoverFromDungeonCreatures(context.State);

            Logger.Log("Dungeon started. " + SquadRaidState.StatusLine()
                       + (restartReload ? " [RestartStage reload]" : ""));

            // Remap transferred/loaded living allies, spawn only missing living ones (never dead clones),
            // then reaffirm follow. Empty uid map after a mid-raid death must NOT respawn corpses.
            if (SquadRaidState.AllyProfileIds.Count > 0)
            {
                SquadRaidPersistence.RemapUniqueIdsFromCreatures(context.State);
                AllySpawnService.SpawnAllies(context.State);
                SquadRestartStageGuard.PurgeLeaderGhostMonsters(context.State);
                SquadRaidPersistence.RebindAllyCreatureData(context.State);
                AllySpawnService.ReaffirmFollow(context.State);

                // Never advance side JSON on RestartStage reload — autosave is the source of truth.
                if (!restartReload)
                    SquadRaidPersistence.SaveCurrent(context.State);
            }

            if (restartReload)
                SquadRestartStageGuard.PendingRestartStageReload = false;

            AllyMovementService.SyncAllSquadAllyMovement(context.State);
        }

        [Hook(ModHookType.DungeonUpdateAfterGameLoop)]
        public static void DungeonUpdateAfterGameLoop(IModContext context)
        {
            if (!Config.Enabled)
                return;
            // Handoff recovery / last-death force runs from DungeonGameMode.Update Prefix
            // (vanilla early-returns on Dead before this hook).
            AllyMovementService.SyncAllSquadAllyMovement(context.State);
        }

        [Hook(ModHookType.DungeonFinished)]
        public static void DungeonFinished(IModContext context)
        {
            if (!Config.Enabled)
                return;

            LeaderHandoffService.ClearPendingHandoff();
            LeaderHandoffService.ClearPostHandoffRewire();

            if (SquadRaidPersistence.SuppressNextDungeonFinishedSave
                || SquadRestartStageGuard.IsFloorReloadDeath(context?.State))
            {
                SquadRaidPersistence.SuppressNextDungeonFinishedSave = false;
                Logger.Log("DungeonFinished: skip squad save (RestartStage / LoadLastSave). "
                           + SquadRaidState.StatusLine());
                return;
            }

            // Persist before floor transfer / extract so a load mid-raid still has the map.
            if (SquadRaidState.Active)
                SquadRaidPersistence.SaveCurrent(context.State);
            Logger.Log("Dungeon finished (space processing handles ally roster).");
        }

        [Hook(ModHookType.AfterSaveLoaded)]
        public static void AfterSaveLoaded(IModContext context)
        {
            // Loaded into space (not dungeon): drop any stale in-memory squad.
            if (context?.State == null)
                return;
            if (context.State.Get<DungeonGameMode>() != null)
                return;
            if (SquadRaidState.Active || SquadRaidState.AllyProfileIds.Count > 0)
            {
                Logger.Log("AfterSaveLoaded (space): clearing in-memory squad state.");
                TryRestoreAllyHunger(context.State);
                SquadRaidState.Clear(deletePersistence: false);
            }
        }

        [Hook(ModHookType.SpaceStarted)]
        public static void SpaceStarted(IModContext context)
        {
            PrepareRaidSquadController.ClearInstance();
            // ProcessFinishedDungeonData runs before SpaceStarted and clears after a finished mission.
            // Abort/evacuate without that path: ensure Magnum names never stick on Magnum UI.
            if (SquadRaidState.Active || SquadRaidState.AllyProfileIds.Count > 0)
            {
                Logger.Log("SpaceStarted: clearing leftover squad raid display state.");
                TryRestoreAllyHunger(context?.State);
                SquadRaidState.Clear(deletePersistence: true);
            }
            _cachedDungeonState = null;
        }

        private static void TryRestoreAllyHunger(State state)
        {
            try
            {
                var mercs = state?.Get<Mercenaries>() ?? _cachedDungeonState?.Get<Mercenaries>();
                AllyMovementService.RestoreHungerForProfileIds(mercs, SquadRaidState.AllyProfileIds);
            }
            catch (System.Exception ex)
            {
                Logger.LogException(ex);
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveGame))]
    internal static class SaveGameSquadPersistPatch
    {
        private static void Postfix(SaveManager __instance)
        {
            if (!Plugin.Config.Enabled || !SquadRaidState.Active)
                return;
            try
            {
                var state = Traverse.Create(__instance).Field("_state").GetValue<State>()
                            ?? Plugin.GetCachedDungeonState();
                if (state?.Get<DungeonGameMode>() == null)
                    return;
                SquadRaidPersistence.SetDungeonState(state);
                SquadRaidPersistence.SaveCurrent(state);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
