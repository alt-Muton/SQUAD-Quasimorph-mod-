using System.Collections.Generic;
using System.Linq;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    internal static class AllySpawnService
    {
        private static string _cachedAiMobClassId;

        public static void SpawnAllies(State state)
        {
            if (!Plugin.Config.Enabled || state == null)
                return;
            if (SquadRaidState.AllyProfileIds.Count == 0)
                return;

            var creatures = state.Get<Creatures>();
            var turnController = state.Get<TurnController>();
            var raidMetadata = state.Get<RaidMetadata>();
            var mapGrid = state.Get<MapGrid>();
            var mercenaries = state.Get<Mercenaries>();
            var player = creatures?.Player;
            if (player == null || turnController == null || raidMetadata == null || mapGrid == null || mercenaries == null)
            {
                Plugin.Logger.LogError("AllySpawnService: missing dungeon deps.");
                return;
            }

            // Leader corpse/defib resolves by LocalizationId == ProfileId.
            if (player.Mercenary?.CreatureData != null && !string.IsNullOrEmpty(player.Mercenary.ProfileId))
                player.Mercenary.CreatureData.LocalizationId = player.Mercenary.ProfileId;

            var inRaidId = mercenaries.MercenaryInRaid?.ProfileId ?? player.Mercenary?.ProfileId;

            var aiMobClass = ResolveAiMobClassId();
            if (string.IsNullOrEmpty(aiMobClass))
            {
                Plugin.Logger.LogError("AllySpawnService: no MobClass with AiPresetId found.");
                return;
            }

            var mapObstacles = state.Get<MapObstacles>();
            var needed = CountAlliesNeedingSpawn(creatures, mercenaries, player, inRaidId);
            var spawnCells = FindSpawnCellsAroundShuttle(mapGrid, creatures, mapObstacles, player, needed);
            var cellIndex = 0;

            foreach (var profileId in SquadRaidState.AllyProfileIds.ToList())
            {
                // Never spawn a second body for the controlled operative (RestartStage / floor ghost).
                if (!string.IsNullOrEmpty(inRaidId) && profileId == inRaidId)
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip InRaid profile spawn " + profileId);
                    SquadRaidState.UnregisterAllyByProfileId(profileId);
                    continue;
                }

                if (!string.IsNullOrEmpty(player.Mercenary?.ProfileId)
                    && profileId == player.Mercenary.ProfileId)
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip player merc profile spawn " + profileId);
                    SquadRaidState.UnregisterAllyByProfileId(profileId);
                    continue;
                }

                var merc = mercenaries.Get(profileId);
                if (merc == null)
                {
                    Plugin.Logger.LogWarning("AllySpawnService: merc missing " + profileId);
                    continue;
                }

                if (ReferenceEquals(merc.CreatureData, player.CreatureData)
                    || ReferenceEquals(merc, mercenaries.MercenaryInRaid))
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip shared/InRaid CreatureData spawn " + profileId);
                    SquadRaidState.UnregisterAllyByProfileId(profileId);
                    continue;
                }

                if (merc.State == MercenaryState.InRaid)
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip MercenaryState.InRaid " + profileId);
                    SquadRaidState.UnregisterAllyByProfileId(profileId);
                    continue;
                }

                // Dead mid-raid: keep profile for raid-end processing, never respawn a corpse clone.
                if (merc.CreatureData?.Health == null || merc.CreatureData.Health.Dead)
                {
                    SquadRaidState.UnregisterAllyByProfileId(profileId);
                    Plugin.Logger.Log("AllySpawnService: skip dead ally " + profileId);
                    continue;
                }

                if (AllyMovementService.IsProtectedQuestAlly(merc.CreatureData))
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip quest CreatureData spawn " + profileId);
                    continue;
                }

                // Floor transfer already placed this ally — never assign a new UniqueId / second body.
                if (IsAllyAlreadyOnMap(creatures, profileId))
                {
                    Plugin.Logger.Log("AllySpawnService: already on map, skip spawn " + profileId);
                    continue;
                }

                // CreatureData already live on the map (ghost / wrong bind) — do not mutate UniqueId.
                if (IsCreatureDataLiveOnMap(creatures, merc.CreatureData, player))
                {
                    Plugin.Logger.LogWarning("AllySpawnService: skip spawn — CreatureData already live on map "
                                            + profileId);
                    continue;
                }

                if (cellIndex >= spawnCells.Count)
                {
                    Plugin.Logger.LogError("AllySpawnService: no free spawn cells for " + profileId
                                          + " (have " + spawnCells.Count + " need=" + needed + ")");
                    break;
                }

                var cell = spawnCells[cellIndex++];

                AllyMovementService.PrepareMercCreatureData(merc, aiMobClass);
                AllyPerkProgressService.EnsureProgressionEnabled(merc);

                merc.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
                // LocalizationId = ProfileId so save/load remap and Magnum display names resolve.
                merc.CreatureData.LocalizationId = merc.ProfileId;
                // Never overwrite the player's UniqueId (breaks VIP FollowTarget target id).
                if (player.CreatureData != null
                    && merc.CreatureData.UniqueId != 0
                    && merc.CreatureData.UniqueId == player.CreatureData.UniqueId)
                {
                    Plugin.Logger.LogWarning("AllySpawnService: abort spawn — merc UniqueId matches player "
                                            + profileId);
                    continue;
                }
                merc.CreatureData.UniqueId = raidMetadata.NextIdCounter;
                merc.CreatureData.Position = cell.Position;
                merc.CreatureData.LookDirection = Direction.Down;
                merc.CreatureData.Health.ReasonOfDeath = HealthInfo.DeathReason.None;

                // Merc inventories can have empty CurrentWeapon; bare hands prevent ReloadManeuver NRE.
                CreatureSystem.SetBareHandSlot(merc.CreatureData);
                CreatureSystem.SetArmStumpSlot(merc.CreatureData);

                PerkSystem.RefreshPerkPassives(merc);
                AllyMovementService.ApplyBaseActionPoints(merc.CreatureData);

                // Contender slots = max possible (like player); actual AP per turn comes from MovementState.
                var slots = AllyMovementService.GetContenderSlotCount(merc.CreatureData);

                Monster monster;
                try
                {
                    monster = CreatureSystem.SpawnMonster(creatures, turnController, merc.CreatureData, slots);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError("AllySpawnService: SpawnMonster failed for " + profileId);
                    Plugin.Logger.LogException(ex);
                    continue;
                }

                monster.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
                EnsureAllyItemUseBehaviour(monster);
                monster.Behaviour.StartFollowing(player);
                monster.IsTransferable = true;
                if (monster.Behaviour.TryGetState<FollowTarget>(out var follow))
                    follow.CanShoot = true;

                AllyMovementService.SyncMovementFromPlayer(monster, player);
                AllyMovementService.EnsureTurnSlots(monster, turnController);

                SquadRaidState.RegisterAllyCreature(monster.CreatureData.UniqueId, profileId);
                AllyBackpackLayout.EnsureVipCompatibleLayout(merc.CreatureData.Inventory);
                Plugin.Logger.Log("Spawned ally " + profileId + " uid=" + monster.CreatureData.UniqueId
                                  + " at " + cell.Position
                                  + " mode=" + monster.MovementState
                                  + " modeAP=" + AllyMovementService.GetModeActionPointsWithAllyBonuses(monster, monster.MovementState)
                                  + " slots=" + slots
                                  + " backpack=" + merc.CreatureData.Inventory.BackpackStore.Width + "x"
                                  + merc.CreatureData.Inventory.BackpackStore.Height
                                  + " CanUseItems=" + (monster.Behaviour?.AiPreset?.CanUseItems ?? false));
            }
        }

        /// <summary>
        /// Ally died mid-raid: block floor transfer, but keep profile + uid map so defibrillator
        /// can restore the same clone until the mission ends.
        /// </summary>
        public static void HandleAllyDied(Creature creature)
        {
            if (!Plugin.Config.Enabled || creature?.CreatureData == null)
                return;

            var profileId = ResolveSquadAllyProfileId(creature);
            if (string.IsNullOrEmpty(profileId))
                return;

            if (creature is Monster monster)
                monster.IsTransferable = false;

            // Keep UniqueIdToProfileId (corpse reuses UniqueId) and AllyProfileIds for defib / raid-end.
            if (!SquadRaidState.AllyProfileIds.Contains(profileId)
                && profileId != SquadRaidState.LeaderProfileId)
                SquadRaidState.AllyProfileIds.Add(profileId);

            // No SaveCurrent: mid-raid JSON must stay aligned with last autosave for RestartStage.
            Plugin.Logger.Log("Ally died mid-raid; kept clone data for possible defib. profile=" + profileId
                              + " uid=" + creature.CreatureData.UniqueId);
        }

        public static bool IsProfileAliveOnMap(Creatures creatures, string profileId)
        {
            return IsAllyAlreadyOnMap(creatures, profileId);
        }

        public static string ResolveAiMobClassIdPublic()
        {
            return ResolveAiMobClassId();
        }

        private static string ResolveSquadAllyProfileId(Creature creature)
        {
            if (creature?.CreatureData == null)
                return null;
            if (AllyMovementService.IsProtectedQuestAlly(creature.CreatureData))
                return null;

            if (SquadRaidState.TryGetAllyProfileId(creature.CreatureData.UniqueId, out var byUid))
                return byUid;

            var locId = creature.CreatureData.LocalizationId;
            if (!string.IsNullOrEmpty(locId) && SquadRaidState.AllyProfileIds.Contains(locId))
                return locId;

            return null;
        }

        private static bool IsAllyAlreadyOnMap(Creatures creatures, string profileId)
        {
            foreach (var kv in SquadRaidState.UniqueIdToProfileId)
            {
                if (kv.Value != profileId)
                    continue;
                var monster = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (monster != null && !monster.CreatureData.Health.Dead
                    && !AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                    return true;
            }

            foreach (var monster in creatures.Monsters)
            {
                if (monster?.CreatureData == null || monster.CreatureData.Health.Dead)
                    continue;
                if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                    continue;
                if (monster.CreatureData.CreatureAlliance != CreatureAlliance.PlayerAlliance)
                    continue;
                if (monster.CreatureData.LocalizationId == profileId)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True if this CreatureData is already the body of a living map creature (or the player).
        /// Mutating UniqueId on it would ghost-clone / break VIP FollowTarget.
        /// </summary>
        private static bool IsCreatureDataLiveOnMap(Creatures creatures, CreatureData data, Player player)
        {
            if (data == null || creatures == null)
                return false;
            if (player?.CreatureData != null && ReferenceEquals(data, player.CreatureData))
                return true;
            if (data.UniqueId == 0)
                return false;
            var existing = CreatureSystem.GetCreatureByUniqueId(data.UniqueId, creatures);
            return existing != null && existing.CreatureData != null && !existing.CreatureData.Health.Dead;
        }

        public static void ReaffirmFollow(State state)
        {
            if (!Plugin.Config.Enabled || !SquadRaidState.Active || state == null)
                return;
            var creatures = state.Get<Creatures>();
            var turnController = state.Get<TurnController>();
            var player = creatures?.Player;
            if (player == null)
                return;

            foreach (var kv in SquadRaidState.UniqueIdToProfileId.ToList())
            {
                var creature = CreatureSystem.GetCreatureByUniqueId(kv.Key, creatures) as Monster;
                if (creature == null || creature.CreatureData.Health.Dead)
                {
                    // Stale uid after death / failed transfer — drop so we never reaffirm a corpse.
                    if (creature != null)
                        creature.IsTransferable = false;
                    SquadRaidState.UnregisterAllyCreature(kv.Key);
                    continue;
                }

                // Never rewrite VIP / quest follow AI or MobClass.
                if (AllyMovementService.IsProtectedQuestAlly(creature.CreatureData)
                    || ReferenceEquals(creature.CreatureData, player.CreatureData))
                {
                    Plugin.Logger.LogWarning("ReaffirmFollow: skip protected/player uid=" + kv.Key);
                    SquadRaidState.UnregisterAllyCreature(kv.Key);
                    continue;
                }

                AllyMovementService.ApplyBaseActionPoints(creature.CreatureData);
                AllyMovementService.DisableHunger(creature.CreatureData);
                EnsureAllyItemUseBehaviour(creature);
                creature.Behaviour.StartFollowing(player);
                creature.IsTransferable = true;
                if (creature.Behaviour.TryGetState<FollowTarget>(out var follow))
                    follow.CanShoot = true;

                AllyMovementService.ApplyFollowOrIdleMovement(creature, player);
                if (turnController != null)
                    AllyMovementService.EnsureTurnSlots(creature, turnController);

                Plugin.Logger.Log("Reaffirm ally uid=" + creature.CreatureData.UniqueId
                                  + " mode=" + creature.MovementState
                                  + " modeAP=" + AllyMovementService.GetModeActionPointsWithAllyBonuses(creature, creature.MovementState)
                                  + " slots=" + creature.ActionPoints
                                  + " CanUseItems=" + (creature.Behaviour?.AiPreset?.CanUseItems ?? false));
            }
        }

        /// <summary>
        /// Vanilla HealSelf/FixateSelf require AiPreset.CanUseItems (same gate as escort VIP).
        /// Prefer a MobClass whose preset allows items; rebind behaviour if the ally still cannot.
        /// Never touch quest VIP MobClassId — escort fail/win checks that id.
        /// </summary>
        public static void EnsureAllyItemUseBehaviour(Monster monster)
        {
            if (monster?.CreatureData == null)
                return;
            if (AllyMovementService.IsProtectedQuestAlly(monster.CreatureData))
                return;
            if (monster.Behaviour?.AiPreset != null && monster.Behaviour.AiPreset.CanUseItems)
                return;

            var mobClassId = ResolveAiMobClassId();
            if (string.IsNullOrEmpty(mobClassId))
                return;

            var record = Data.MobClasses.GetRecord(mobClassId);
            if (record == null || string.IsNullOrEmpty(record.AiPresetId))
                return;
            if (!AiPresetAllowsItems(record.AiPresetId))
            {
                Plugin.Logger.LogWarning("EnsureAllyItemUseBehaviour: no CanUseItems AiPreset available.");
                return;
            }

            try
            {
                monster.CreatureData.MobClassId = mobClassId;
                var behaviour = SingletonMonoBehaviour<CreatureFactory>.Instance.CreateBehaviour(monster, record.AiPresetId);
                monster.ConfigureBehaviour(behaviour);
                Plugin.Logger.Log("Ally AI rebound for items. uid=" + monster.CreatureData.UniqueId
                                  + " MobClass=" + mobClassId
                                  + " AiPreset=" + record.AiPresetId);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("EnsureAllyItemUseBehaviour failed.");
                Plugin.Logger.LogException(ex);
            }
        }

        private static string ResolveAiMobClassId()
        {
            if (!string.IsNullOrEmpty(_cachedAiMobClassId))
            {
                var cached = Data.MobClasses.GetRecord(_cachedAiMobClassId);
                if (cached != null && AiPresetAllowsItems(cached.AiPresetId))
                    return _cachedAiMobClassId;
                _cachedAiMobClassId = null;
            }

            string fallback = null;
            foreach (var id in Data.MobClasses.Ids)
            {
                var rec = Data.MobClasses.GetRecord(id);
                if (rec == null || string.IsNullOrEmpty(rec.AiPresetId))
                    continue;

                if (fallback == null)
                    fallback = id;

                if (!AiPresetAllowsItems(rec.AiPresetId))
                    continue;

                _cachedAiMobClassId = id;
                Plugin.Logger.Log("Ally AI MobClassId=" + id + " AiPreset=" + rec.AiPresetId + " CanUseItems=true");
                return id;
            }

            if (fallback != null)
            {
                _cachedAiMobClassId = fallback;
                var fb = Data.MobClasses.GetRecord(fallback);
                Plugin.Logger.LogWarning("Ally AI: no CanUseItems preset; fallback MobClassId=" + fallback
                                         + " AiPreset=" + (fb?.AiPresetId ?? "?"));
                return fallback;
            }

            return null;
        }

        private static bool AiPresetAllowsItems(string aiPresetId)
        {
            if (string.IsNullOrEmpty(aiPresetId))
                return false;
            var preset = Data.AiPresets.GetRecord(aiPresetId);
            return preset != null && preset.CanUseItems;
        }

        /// <summary>
        /// Arrival shuttle boarding pad is 3×4. Player stands on the bottom-center cell.
        /// Ally slots fill the remaining pad cells in this order (dx, dy from player;
        /// +Y is into the shuttle body / toward the obstacle).
        /// </summary>
        private static readonly (int dx, int dy)[] ShuttlePadAllyOffsets =
        {
            (-1, 0), (1, 0),             // left, right of player
            (-1, 1), (1, 1),             // left-up, right-up
            (-1, 2), (0, 2), (1, 2),     // next row up (3 cells)
            (0, 1),                      // up toward center
            (0, 3),                      // further up center
            (-1, 3), (1, 3),             // top corners
        };

        /// <summary>
        /// How many roster allies still need a body this floor (excludes leader / dead / already mapped).
        /// </summary>
        private static int CountAlliesNeedingSpawn(
            Creatures creatures,
            Mercenaries mercenaries,
            Player player,
            string inRaidId)
        {
            var count = 0;
            foreach (var profileId in SquadRaidState.AllyProfileIds)
            {
                if (string.IsNullOrEmpty(profileId))
                    continue;
                if (!string.IsNullOrEmpty(inRaidId) && profileId == inRaidId)
                    continue;
                if (!string.IsNullOrEmpty(player.Mercenary?.ProfileId)
                    && profileId == player.Mercenary.ProfileId)
                    continue;

                var merc = mercenaries.Get(profileId);
                if (merc == null)
                    continue;
                if (ReferenceEquals(merc.CreatureData, player.CreatureData)
                    || ReferenceEquals(merc, mercenaries.MercenaryInRaid)
                    || merc.State == MercenaryState.InRaid)
                    continue;
                if (merc.CreatureData?.Health == null || merc.CreatureData.Health.Dead)
                    continue;
                if (AllyMovementService.IsProtectedQuestAlly(merc.CreatureData))
                    continue;
                if (IsAllyAlreadyOnMap(creatures, profileId))
                    continue;
                if (IsCreatureDataLiveOnMap(creatures, merc.CreatureData, player))
                    continue;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Place allies on the shuttle's 3×4 boarding pad (player = bottom center).
        /// Escort VIP / shuttle hull often leave only 1–2 pad cells; keep searching rings
        /// around the player until every pending ally has a free tile.
        /// </summary>
        private static List<MapCell> FindSpawnCellsAroundShuttle(
            MapGrid mapGrid,
            Creatures creatures,
            MapObstacles mapObstacles,
            Player player,
            int needed)
        {
            var passMask = BuildAllySpawnPassMask();
            var playerPos = player.CreatureData.Position;
            var padOrigin = ResolveShuttlePadOrigin(mapObstacles, playerPos);
            var result = new List<MapCell>();
            var seen = new HashSet<long>();
            var want = needed;

            void TryAdd(CellPosition pos)
            {
                if (pos.Equals(playerPos))
                    return;
                var key = ((long)pos.X << 32) ^ (uint)pos.Y;
                if (!seen.Add(key))
                    return;
                if (!IsValidAllySpawnCell(mapGrid, creatures, passMask, pos))
                    return;
                result.Add(mapGrid.GetCell(pos));
            }

            // Primary: fixed shuttle-pad slots relative to bottom-center spawn.
            foreach (var (dx, dy) in ShuttlePadAllyOffsets)
                TryAdd(new CellPosition(padOrigin.X + dx, padOrigin.Y + dy));

            var padCount = result.Count;

            // Pad-only used to return here if Count > 0, so escort VIP (radius 3) ate the
            // 3rd ally: hull blocks the rest of the 3×4, two side cells fill, spawn stops.
            const int maxRing = 8;
            for (var ring = 1; ring <= maxRing && result.Count < want; ring++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    for (var dy = -ring; dy <= ring; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring)
                            continue;
                        TryAdd(new CellPosition(playerPos.X + dx, playerPos.Y + dy));
                    }
                }
            }

            Plugin.Logger.Log("Ally spawn: " + result.Count + " cells (pad=" + padCount
                              + " need=" + needed + ") origin=" + padOrigin
                              + " player=" + playerPos);
            return result;
        }

        /// <summary>
        /// Bottom-center of the 3×4 pad — same cell vanilla uses for the player under the shuttle.
        /// </summary>
        private static CellPosition ResolveShuttlePadOrigin(MapObstacles mapObstacles, CellPosition playerPos)
        {
            if (mapObstacles != null)
            {
                foreach (var obstacle in mapObstacles.Obstacles)
                {
                    if (obstacle == null || !obstacle.HasComponent<Shuttle>())
                        continue;
                    var shuttle = obstacle.GetComponent<Shuttle>();
                    if (shuttle == null)
                        continue;
                    var spawn = shuttle.GetSpawnPosition();
                    // Prefer live player cell if they already stand on the pad (arrival).
                    if (CellPosition.Distance(spawn, playerPos) <= 1)
                        return playerPos;
                    return spawn;
                }
            }

            return playerPos;
        }

        private static ObstacleType BuildAllySpawnPassMask()
        {
            var passMask = MapCell.AllObstacles;
            passMask &= ~ObstacleType.BlockedDoor;
            passMask &= ~ObstacleType.ClosedDoor;
            passMask &= ~ObstacleType.Wall;
            passMask &= ~ObstacleType.Monster;
            passMask &= ~ObstacleType.Player;
            passMask &= ~ObstacleType.PlayerAlly;
            passMask &= ~ObstacleType.Obj;
            passMask &= ~ObstacleType.Deep;
            passMask &= ~ObstacleType.Indestructable;
            return passMask;
        }

        private static bool IsValidAllySpawnCell(
            MapGrid mapGrid,
            Creatures creatures,
            ObstacleType passMask,
            CellPosition pos)
        {
            if (mapGrid == null || pos.X < 0 || pos.Y < 0
                || pos.X >= mapGrid.MaxWidth || pos.Y >= mapGrid.MaxHeight)
                return false;

            var cell = mapGrid.GetCell(pos);
            if (cell == null || !cell.IsPassable(passMask))
                return false;

            return creatures.GetCreature(pos.X, pos.Y) == null;
        }
    }
}
