using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// When the controlled leader dies mid-raid, promote the nearest living squad ally onto
    /// the same <see cref="Player"/> instance so the mission continues.
    /// </summary>
    internal static class LeaderHandoffService
    {
        /// <summary>
        /// Monster.OnDestroy clears CreatureData deps end-of-frame; rewire player after that.
        /// </summary>
        private static int _postHandoffRewireFrames;

        private static bool _pendingHandoff;
        private static float _pendingHandoffAt;
        private static bool _pendingDeathVisualsPlayed;

        public static bool IsHandoffPending => _pendingHandoff;

        public static void ClearPendingHandoff()
        {
            _pendingHandoff = false;
            _pendingHandoffAt = 0f;
            _pendingDeathVisualsPlayed = false;
        }

        public static void ClearPostHandoffRewire()
        {
            _postHandoffRewireFrames = 0;
        }

        /// <summary>
        /// Begin handoff now, or schedule after <see cref="ModConfig.HandoffDelaySeconds"/>.
        /// Returns true if death should not run the vanilla raid-end path.
        /// </summary>
        public static bool TryBeginHandoffOrSchedule(Player player)
        {
            if (!CanAttemptHandoff(player))
                return false;

            var delay = Plugin.Config.HandoffDelaySeconds;
            if (delay <= 0.01f)
                return TryHandoff(player);

            if (_pendingHandoff)
                return true;

            _pendingHandoff = true;
            _pendingHandoffAt = Time.realtimeSinceStartup + delay;
            // Vanilla OnDead is skipped (would end the raid / hide HUD), so play death anim ourselves.
            PlayPendingHandoffDeathPresentation(player);
            Plugin.Logger.Log("LeaderHandoff scheduled in " + delay.ToString("0.##") + "s. "
                              + SquadRaidState.StatusLine());
            return true;
        }

        /// <summary>
        /// Visual death beat while waiting to promote an ally — no corpse, no PlayerDied, no HUD wipe.
        /// </summary>
        private static void PlayPendingHandoffDeathPresentation(Player player)
        {
            if (player?.CreatureData == null || _pendingDeathVisualsPlayed)
                return;

            _pendingDeathVisualsPlayed = true;

            try
            {
                var turnController = Plugin.GetCachedDungeonState()?.Get<TurnController>();
                turnController?.RemoveContender(player);
                player.IsProcessing = false;

                var view = player.Creature3dView;
                if (view == null)
                    return;

                view.Highlight(val: false, isFriendly: false);

                var dir = player.CreatureData.LookDirection;
                if (dir == Direction.Center)
                    dir = Direction.Down;
                view.UpdateDirection(dir);

                if (view.CanPlayAnimation(AnimationType.Death))
                    view.PlayAnimation(AnimationType.Death);

                try
                {
                    view.FadeOutShadow();
                }
                catch
                {
                    // Shadow tween optional if preset keyframes missing.
                }

                view.PlayDamageFade();
                Plugin.Logger.Log("LeaderHandoff: death animation started during delay.");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("LeaderHandoff: death presentation failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        private static bool CanAttemptHandoff(Player player)
        {
            if (!Plugin.Config.Enabled || player?.CreatureData == null)
                return false;
            if (!SquadRaidState.Active)
                return false;
            if (SquadRaidState.UniqueIdToProfileId.Count == 0)
                return false;
            if (player.MutatedQuasimorph)
                return false;
            if (Plugin.GetCachedDungeonState() == null)
                return false;

            var creatures = Plugin.GetCachedDungeonState().Get<Creatures>();
            return HasLivingSquadAlly(creatures);
        }

        public static bool TryHandoff(Player player)
        {
            ClearPendingHandoff();

            if (!Plugin.Config.Enabled || player?.CreatureData == null)
                return false;
            if (!SquadRaidState.Active)
            {
                Plugin.Logger.Log("LeaderHandoff skip: squad inactive.");
                return false;
            }
            if (SquadRaidState.UniqueIdToProfileId.Count == 0)
            {
                Plugin.Logger.Log("LeaderHandoff skip: no living ally uid map. " + SquadRaidState.StatusLine());
                return false;
            }

            // Quasimorph body-swap must finish first.
            if (player.MutatedQuasimorph)
            {
                Plugin.Logger.Log("LeaderHandoff skip: MutatedQuasimorph.");
                return false;
            }

            // Prefer squad handoff over SecondChance when allies remain (vanilla HideAll + 1 HP softlocks UX).
            // Solo leaders still get vanilla SecondChance via normal OnDead.

            var state = Plugin.GetCachedDungeonState();
            if (state == null)
            {
                Plugin.Logger.LogError("LeaderHandoff skip: no cached dungeon state.");
                return false;
            }

            var creatures = state.Get<Creatures>();
            var mercenaries = state.Get<Mercenaries>();
            var turnController = state.Get<TurnController>();
            var mapObstacles = state.Get<MapObstacles>();
            var mapRenderer = state.Get<MapRenderer>();
            var mapController = state.Get<MapController>();
            var mapGrid = state.Get<MapGrid>();
            var itemsOnFloor = state.Get<ItemsOnFloor>();
            var mapEntities = state.Get<MapEntities>();
            var fireController = state.Get<FireController>();
            var visibilities = state.Get<Visibilities>();
            var gameCamera = state.Get<GameCamera>();

            if (creatures == null || mercenaries == null || turnController == null || mapObstacles == null)
            {
                Plugin.Logger.LogError("LeaderHandoff: missing dungeon deps.");
                return false;
            }

            var allyMonster = FindNearestLivingAlly(creatures, player.CreatureData.Position);
            if (allyMonster == null)
            {
                Plugin.Logger.Log("LeaderHandoff skip: no living ally monster on map. " + SquadRaidState.StatusLine());
                return false;
            }

            if (!SquadRaidState.TryGetAllyProfileId(allyMonster.CreatureData.UniqueId, out var newLeaderProfileId)
                || string.IsNullOrEmpty(newLeaderProfileId))
            {
                Plugin.Logger.LogError("LeaderHandoff: ally has no profile mapping.");
                return false;
            }

            var newLeader = mercenaries.Get(newLeaderProfileId);
            if (newLeader?.CreatureData == null || newLeader.CreatureData.Health.Dead)
            {
                Plugin.Logger.LogError("LeaderHandoff: new leader merc missing/dead " + newLeaderProfileId);
                return false;
            }

            var oldLeader = player.Mercenary;
            var oldLeaderProfileId = oldLeader?.ProfileId ?? SquadRaidState.LeaderProfileId;
            var deathPos = player.CreatureData.Position;
            var deathLook = player.CreatureData.LookDirection;
            var allyPos = allyMonster.CreatureData.Position;
            var allyLook = allyMonster.CreatureData.LookDirection;

            Plugin.Logger.Log("LeaderHandoff: " + (oldLeaderProfileId ?? "?") + " -> " + newLeaderProfileId
                              + " at " + allyPos);

            try
            {
                // Tag corpse with ProfileId so defibrillator can restore this exact clone.
                if (oldLeader != null && player.CreatureData != null)
                    player.CreatureData.LocalizationId = oldLeaderProfileId;

                // Corpse of old leader at death tile (shares inventory via Player.CloneInventoryForCorpse).
                try
                {
                    MapObstacleSystem.SpawnCorpse(mapObstacles, player, deathLook, 0f);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError("LeaderHandoff: SpawnCorpse failed.");
                    Plugin.Logger.LogException(ex);
                }

                // Tear down ally Monster wrapper; keep living CreatureData on the merc.
                // CRITICAL: Monster.OnDestroy calls CreatureData.ClearDependencies() — that wipes
                // Health.Killed + effect binds on the SAME CreatureData the merc/player will use.
                // Detach a disposable dummy before Destroy so the living data survives.
                allyMonster.IsTransferable = false;
                allyMonster.Behaviour?.StopFollowing();
                allyMonster.CreatureData = CreateDespawnDummyCreatureData();
                CreatureSystem.DespawnCreature(turnController, creatures, allyMonster);

                // Corpse owns the old body; merc record must not respawn as a living ally clone.
                if (oldLeader?.CreatureData != null && oldLeader.CreatureData.Health.Alive)
                    oldLeader.CreatureData.Health.Value = 0;

                // Mercenary InRaid swap.
                if (oldLeader != null)
                    oldLeader.State = MercenaryState.None;
                newLeader.State = MercenaryState.InRaid;
                mercenaries.ChangedMercenary = null;

                SquadRaidState.OnLeaderHandoff(oldLeaderProfileId, newLeaderProfileId);

                // New leader is controlled player — enable hunger; remaining allies stay hunger-immune.
                AllyMovementService.RestoreHunger(newLeader.CreatureData);
                newLeader.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
                newLeader.CreatureData.Position = allyPos;
                newLeader.CreatureData.LookDirection = allyLook;
                newLeader.CreatureData.UniqueId = 0;
                newLeader.CreatureData.LocalizationId = newLeader.ProfileId;

                AllyBackpackLayout.RestoreNativeBackpackLayout(newLeader.CreatureData.Inventory);

                var oldView = player.Creature3dView;
                player.ConfigureMercenary(newLeader);
                RewirePlayerAfterHandoff(player);
                _postHandoffRewireFrames = 3;
                var newView = SingletonMonoBehaviour<CreatureFactory>.Instance.CreateCreature3dView(
                    player.transform,
                    newLeader.CreatureData.ActorId,
                    newLeader.CreatureData.SkinIndex,
                    newLeader.CreatureData.HairType,
                    newLeader.CreatureData.HairColorHex);
                player.Configure3dView(newView);
                if (oldView != null)
                    Object.Destroy(oldView.gameObject);

                CreatureSystem.SetBareHandSlot(newLeader.CreatureData);
                CreatureSystem.SetArmStumpSlot(newLeader.CreatureData);
                CreatureSystem.UpdatePosition(mapRenderer, player, allyPos);
                player.IsProcessing = false;
                player.MovementState = CreatureMovementState.Normal;

                // Turn slots for the living body.
                turnController.RemoveContender(player);
                var slots = AllyMovementService.GetContenderSlotCount(newLeader.CreatureData);
                for (var i = 0; i < slots; i++)
                    turnController.InsertFirstContender(player);
                player.ResetActionPointsProcessed();

                AllySpawnService.ReaffirmFollow(state);
                AllyMovementService.SyncAllSquadAllyMovement(state);

                try
                {
                    // Ensure HUD is visible (vanilla OnDead may have been skipped, or prior HideAll left UI blank).
                    if (!UI.IsShowing<DungeonHudScreen>())
                        UI.Chain<DungeonHudScreen>().Show();
                    UI.Get<DungeonHudScreen>()?.RefreshAllHud();
                }
                catch
                {
                    // HUD may be mid-hide from damage path
                }

                if (mapController != null && mapRenderer != null && mapGrid != null
                    && itemsOnFloor != null && mapEntities != null && fireController != null && visibilities != null)
                {
                    MapSystem.RefreshFogAndMinimap(
                        mapController, creatures, itemsOnFloor, mapObstacles, mapRenderer,
                        mapEntities, mapGrid, fireController, visibilities);
                }

                if (gameCamera != null)
                {
                    gameCamera.Configure(player.transform);
                    gameCamera.GetComponent<GraphicChangedTracker>()?.ManualRefresh();
                }

                // Do NOT SaveCurrent here: RestartStage loads last autosave without SaveGame.
                // Overwriting squad JSON after handoff desyncs roster vs dungeon and spawns ghosts.
                Plugin.Logger.Log("LeaderHandoff complete. " + SquadRaidState.StatusLine());
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("LeaderHandoff failed.");
                Plugin.Logger.LogException(ex);
                return false;
            }
        }

        private static Monster FindNearestLivingAlly(Creatures creatures, CellPosition from)
        {
            Monster best = null;
            var bestDist = int.MaxValue;
            foreach (var monster in AllyMovementService.EnumerateSquadAllies(creatures))
            {
                if (monster?.CreatureData == null || monster.CreatureData.Health.Dead)
                    continue;
                var dist = CellPosition.Distance(from, monster.CreatureData.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = monster;
                }
            }
            return best;
        }

        public static bool HasLivingSquadAlly(Creatures creatures)
        {
            return FindNearestLivingAlly(creatures, creatures?.Player?.CreatureData?.Position ?? CellPosition.Zero) != null;
        }

        /// <summary>
        /// Disposable data for a despawning Monster so OnDestroy does not clear the living merc data.
        /// </summary>
        public static CreatureData CreateDespawnDummyCreatureData()
        {
            return new CreatureData
            {
                Health = new HealthInfo(1),
                EffectsController = new EffectsController(),
                Inventory = new Inventory()
            };
        }

        /// <summary>
        /// Re-bind effects to Player after ConfigureMercenary / deferred Monster.OnDestroy.
        /// Do NOT call ConfigureData here — it ClearDependencies() and wipes EffectsView HUD
        /// subscriptions (stuck wound icons, silent status updates, runaway pain).
        /// </summary>
        public static void RewirePlayerAfterHandoff(Player player)
        {
            if (player?.CreatureData == null)
                return;

            RebindEffectDependencies(player);
            RefreshPlayerEffectsHud(player);
        }

        /// <summary>
        /// Ally Monster effects may lack Player-path injects after promotion; re-resolve State deps.
        /// </summary>
        private static void RebindEffectDependencies(Player player)
        {
            var effects = player?.CreatureData?.EffectsController;
            if (effects == null)
                return;

            // Initialize sets _creature on controller + each effect without wiping HUD events.
            effects.Initialize(player);
            var factory = Singleton<EffectFactory>.Instance;
            if (factory == null)
                return;

            foreach (var effect in effects.Effects)
            {
                if (effect == null)
                    continue;
                try
                {
                    factory.GrabDependencies(effect);
                    effect.SetCreature(player);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogWarning("LeaderHandoff: effect rebind failed " + effect.GetType().Name);
                    Plugin.Logger.LogException(ex);
                }
            }

            try
            {
                effects.RefreshVisualState();
            }
            catch
            {
                // visual refresh is best-effort
            }
        }

        private static void RefreshPlayerEffectsHud(Player player)
        {
            try
            {
                var hud = UI.Get<DungeonHudScreen>();
                if (hud == null || player?.CreatureData?.EffectsController == null)
                    return;

                hud.Effects?.Refresh(player.CreatureData.EffectsController, blinkOnRefresh: false);
                hud.Effects?.RefreshBodyPartWoundView();
                hud.RefreshAllHud(refreshOnEnable: false);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("LeaderHandoff: HUD effects refresh failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        /// <summary>
        /// Called from dungeon Update: delayed rewire + softlock recovery when last operative is Dead.
        /// </summary>
        public static void TickDungeonFrame(State state)
        {
            if (!Plugin.Config.Enabled || state == null || !SquadRaidState.Active)
                return;

            var creatures = state.Get<Creatures>();
            var player = creatures?.Player;
            if (player?.CreatureData == null)
                return;

            if (_postHandoffRewireFrames > 0 && player.CreatureData.Health.Alive)
            {
                _postHandoffRewireFrames--;
                RewirePlayerAfterHandoff(player);
                if (_postHandoffRewireFrames == 0)
                    Plugin.Logger.Log("LeaderHandoff: post-destroy player rewire done.");
            }

            if (!player.CreatureData.Health.Dead)
                return;

            // Wait out the death beat before swapping bodies.
            if (_pendingHandoff)
            {
                if (Time.realtimeSinceStartup < _pendingHandoffAt)
                    return;

                Plugin.Logger.Log("LeaderHandoff delay elapsed — promoting.");
                if (TryHandoff(player))
                    return;

                // Ally may have died during the wait.
                if (!HasLivingSquadAlly(creatures))
                    TryForceLastOperativeDeathScreen(state, player);
                return;
            }

            if (HasLivingSquadAlly(creatures))
            {
                Plugin.Logger.Log("LeaderHandoff recovery: player dead with living allies.");
                TryBeginHandoffOrSchedule(player);
                return;
            }

            TryForceLastOperativeDeathScreen(state, player);
        }

        /// <summary>
        /// Recovery if HP hit 0 but OnDead handoff did not run (e.g. mid-tick exception).
        /// </summary>
        public static void TryRecoverIfPlayerDeadWithAllies(State state)
        {
            TickDungeonFrame(state);
        }

        /// <summary>
        /// If Health.Killed was wiped, HP can hit 0 with no ProcessDeath — force the raid-fail UI.
        /// </summary>
        private static void TryForceLastOperativeDeathScreen(State state, Player player)
        {
            try
            {
                if (UI.IsShowing<DeathInRaidScreen>() || UI.IsShowing<PreDeathBlockScreen>()
                    || UI.IsShowing<RaidFinishedScreen>())
                    return;

                var dungeon = state.Get<DungeonGameMode>()
                               ?? SingletonMonoBehaviour<DungeonGameMode>.Instance;
                if (dungeon == null)
                    return;
                if (Traverse.Create(dungeon).Field("_finished").GetValue<bool>())
                    return;

                Plugin.Logger.Log("LeaderHandoff: last operative dead without death pipeline — forcing PlayerAlreadyDead.");
                dungeon.PlayerAlreadyDead(player.CreatureData.Health.ReasonOfDeath);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("LeaderHandoff: force last-operative death failed.");
                Plugin.Logger.LogException(ex);
            }
        }
    }
}
