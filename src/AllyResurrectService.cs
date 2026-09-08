using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace Squad_Leader
{
    /// <summary>
    /// Vanilla defibrillator respawns a fresh MobClass monster from the corpse.
    /// Squad operatives must come back as the same Mercenary clone with the same gear.
    /// Ally corpses use MobClassId=warden for AI — never let vanilla SpawnMonsterFromMobClass see them.
    /// </summary>
    internal static class AllyResurrectService
    {
        public static bool TryResurrectSquadOperative(
            Mercenaries mercenaries,
            TurnController turnController,
            Creatures creatures,
            RaidMetadata raidMetadata,
            MapObstacles mapObstacles,
            BasePickupItem item,
            Creature user,
            MapCell cell,
            out Monster creature,
            MapObstacle preferredCorpse = null)
        {
            creature = null;
            if (!Plugin.Config.Enabled)
                return false;

            var placeable = item?.Record<PlaceableDeviceRecord>();
            if (placeable == null || creatures == null || mapObstacles == null || mercenaries == null)
                return false;

            var mapObstacle = preferredCorpse
                              ?? FindBestSquadCorpse(mapObstacles, placeable, cell.Position, mercenaries);
            if (mapObstacle?.CorpseStorage?.CreatureData == null)
                return false;

            var corpseData = mapObstacle.CorpseStorage.CreatureData;
            if (!TryResolveSquadProfileId(corpseData, mercenaries, out var profileId))
                return false;

            var merc = mercenaries.Get(profileId);
            if (merc?.CreatureData == null)
            {
                Plugin.Logger.LogError("Defib: merc missing for profile " + profileId);
                return false;
            }

            // Already alive on the map — refuse to spawn a duplicate.
            if (!merc.CreatureData.Health.Dead
                && AllySpawnService.IsProfileAliveOnMap(creatures, profileId))
            {
                Plugin.Logger.LogWarning("Defib: " + profileId + " already alive on map.");
                return false;
            }

            try
            {
                // Prefer inventory on the living merc record (may differ from corpse wrapper).
                TransferCorpseInventoryToMerc(corpseData, merc);
                ReviveMercCreatureData(merc, cell.Position, user.CreatureData.CreatureAlliance, raidMetadata);
                // Corpse is the death snapshot — keep clone actor/skin, never MobClass (warden) looks.
                CopyCorpseIdentity(corpseData, merc.CreatureData, profileId);

                // Drop stale uid map for the corpse / old body.
                SquadRaidState.UnregisterAllyByProfileId(profileId);
                EnsureAllyProfileListed(profileId);
                if (string.IsNullOrEmpty(SquadRaidState.LeaderProfileId))
                    SquadRaidState.LeaderProfileId = creatures.Player?.Mercenary?.ProfileId;
                SquadRaidState.EnsureActive();

                // AI MobClass only — never change LocalizationId / BaseHealth (vanilla defib would).
                var aiMob = AllySpawnService.ResolveAiMobClassIdPublic();
                if (!string.IsNullOrEmpty(aiMob))
                    merc.CreatureData.MobClassId = aiMob;
                AllyMovementService.ApplyBaseActionPoints(merc.CreatureData);
                AllyMovementService.DisableHunger(merc.CreatureData, refillSatiety: true);
                AllyPerkProgressService.EnsureProgressionEnabled(merc);
                AllyBackpackLayout.EnsureVipCompatibleLayout(merc.CreatureData.Inventory);

                CreatureSystem.SetBareHandSlot(merc.CreatureData);
                CreatureSystem.SetArmStumpSlot(merc.CreatureData);
                PerkSystem.RefreshPerkPassives(merc);

                var slots = AllyMovementService.GetContenderSlotCount(merc.CreatureData);
                creature = CreatureSystem.SpawnMonster(creatures, turnController, merc.CreatureData, slots);
                creature.CreatureData.CreatureAlliance = user.CreatureData.CreatureAlliance;
                // Keep LocalizationId = ProfileId for Magnum names (EnsureAllyItemUse must not wipe it).
                creature.CreatureData.LocalizationId = merc.ProfileId;
                AllySpawnService.EnsureAllyItemUseBehaviour(creature);
                creature.CreatureData.LocalizationId = merc.ProfileId;
                creature.Behaviour.StartFollowing(user is Player player ? player : creatures.Player);
                creature.IsTransferable = true;
                if (creature.Behaviour.TryGetState<FollowTarget>(out var follow))
                    follow.CanShoot = true;

                AllyMovementService.SyncMovementFromPlayer(creature, creatures.Player);
                AllyMovementService.EnsureTurnSlots(creature, turnController);

                SquadRaidState.RegisterAllyCreature(creature.CreatureData.UniqueId, profileId);
                creature.Creature3dView.RefreshItemsRender();

                // CorpseStorage.OnMapObstacleDestroyed dumps leftover corpse inventory to the floor.
                // Leader corpses share Inventory with the merc — must detach before Remove.
                DetachCorpseInventoryBeforeDestroy(corpseData, merc);
                mapObstacles.Remove(mapObstacle);
                creatures.RaiseCreatureRessurected(creature.CreatureData);

                Plugin.Logger.Log("Defib restored squad operative " + profileId
                                  + " uid=" + creature.CreatureData.UniqueId
                                  + " hp=" + creature.CreatureData.Health.Value + "/"
                                  + creature.CreatureData.Health.MaxValue
                                  + " loc=" + creature.CreatureData.LocalizationId
                                  + " actor=" + creature.CreatureData.ActorId
                                  + " body=" + creature.CreatureData.BodyTypeId
                                  + " mob=" + creature.CreatureData.MobClassId
                                  + " at " + cell.Position);
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("Defib squad resurrect failed for " + profileId);
                Plugin.Logger.LogException(ex);
                creature = null;
                return false;
            }
        }

        /// <summary>
        /// Among valid defib corpses on the cell, prefer a squad operative (never the first random NPC).
        /// </summary>
        public static MapObstacle FindBestSquadCorpse(
            MapObstacles mapObstacles,
            PlaceableDeviceRecord record,
            CellPosition position,
            Mercenaries mercenaries)
        {
            if (mapObstacles == null || record == null)
                return null;

            MapObstacle firstValid = null;
            foreach (var obstacle in mapObstacles.RaycastMapObstacles(position.X, position.Y))
            {
                if (obstacle?.CorpseStorage?.CreatureData == null)
                    continue;
                if (record.TargetObjectId != obstacle.CorpseStorage.CreatureData.CreatureClass.ToString())
                    continue;

                if (firstValid == null)
                    firstValid = obstacle;

                if (TryResolveSquadProfileId(obstacle.CorpseStorage.CreatureData, mercenaries, out var profileId))
                {
                    Plugin.Logger.Log("Defib: preferred squad corpse profile=" + profileId
                                      + " loc=" + obstacle.CorpseStorage.CreatureData.LocalizationId);
                    return obstacle;
                }
            }

            // No squad corpse on this cell — let vanilla handle NPC defib.
            return null;
        }

        public static bool TryResolveSquadProfileId(CreatureData corpseData, Mercenaries mercenaries, out string profileId)
        {
            profileId = null;
            if (corpseData == null)
                return false;

            if (SquadRaidState.TryGetAllyProfileId(corpseData.UniqueId, out var byUid)
                && !string.IsNullOrEmpty(byUid))
            {
                profileId = byUid;
                return true;
            }

            // Shared CreatureData corpse (KeepGear / leader CloneInventoryForCorpse).
            if (mercenaries != null)
            {
                foreach (var merc in EnumerateRaidMercCandidates(mercenaries))
                {
                    if (merc?.CreatureData == null)
                        continue;
                    if (ReferenceEquals(merc.CreatureData, corpseData)
                        || ReferenceEquals(merc.CreatureData.Inventory, corpseData.Inventory))
                    {
                        profileId = merc.ProfileId;
                        return true;
                    }
                }
            }

            var locId = corpseData.LocalizationId;
            if (string.IsNullOrEmpty(locId))
                return false;

            if (locId == SquadRaidState.LeaderProfileId
                || SquadRaidState.AllyProfileIds.Contains(locId))
            {
                profileId = locId;
                return true;
            }

            var byLoc = mercenaries?.Get(locId);
            // LocId is a MobClass (e.g. warden) and not a Magnum profile — not a squad corpse.
            if (byLoc == null)
                return false;

            // Roster merc with matching LocId: accept if dead / mid-raid squad context.
            if (SquadRaidState.Active
                || SquadRaidState.AllyProfileIds.Count > 0
                || byLoc.CreatureData?.Health == null
                || byLoc.CreatureData.Health.Dead)
            {
                profileId = locId;
                return true;
            }

            return false;
        }

        private static IEnumerable<Mercenary> EnumerateRaidMercCandidates(Mercenaries mercenaries)
        {
            if (!string.IsNullOrEmpty(SquadRaidState.LeaderProfileId))
            {
                var leader = mercenaries.Get(SquadRaidState.LeaderProfileId);
                if (leader != null)
                    yield return leader;
            }

            foreach (var id in SquadRaidState.AllyProfileIds)
            {
                var merc = mercenaries.Get(id);
                if (merc != null)
                    yield return merc;
            }
        }

        private static void EnsureAllyProfileListed(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return;
            if (profileId == SquadRaidState.LeaderProfileId)
                return;
            if (!SquadRaidState.AllyProfileIds.Contains(profileId))
                SquadRaidState.AllyProfileIds.Add(profileId);
        }

        private static void TransferCorpseInventoryToMerc(CreatureData corpseData, Mercenary merc)
        {
            var corpseInv = corpseData.Inventory;
            var mercInv = merc.CreatureData.Inventory;
            if (corpseInv == null || mercInv == null)
                return;

            // Player/leader corpse shares the merc inventory reference — already in place.
            if (ReferenceEquals(corpseInv, mercInv))
                return;

            foreach (var storage in corpseInv.AllContainers)
            {
                while (!storage.Empty)
                {
                    var item = storage.First;
                    storage.Remove(item, sendEvent: false);
                    if (!mercInv.TakeOrEquip(item, putIfSlotBusy: true, sendEvent: false))
                    {
                        if (!mercInv.BackpackStore.TryPutItem(item, CellPosition.Zero, hasSpecialPos: false, sendEvent: false))
                            Plugin.Logger.LogWarning("Defib: could not return item " + item.Id + " to " + merc.ProfileId);
                    }
                }
            }
        }

        /// <summary>
        /// Vanilla corpse destroy moves every remaining corpse item onto the floor.
        /// After transfer (or when inventory is shared with the merc) the corpse must not
        /// still reference that gear when MapObstacles.Remove runs.
        /// </summary>
        private static void DetachCorpseInventoryBeforeDestroy(CreatureData corpseData, Mercenary merc)
        {
            if (corpseData == null)
                return;

            var mercInv = merc?.CreatureData?.Inventory;
            if (mercInv != null && ReferenceEquals(corpseData.Inventory, mercInv))
            {
                corpseData.Inventory = new Inventory();
                return;
            }

            if (corpseData.Inventory != null && mercInv != null && !corpseData.Inventory.Empty)
                TransferCorpseInventoryToMerc(corpseData, merc);

            corpseData.Inventory = new Inventory();
        }

        /// <summary>
        /// Corpse CreatureData is a snapshot (GenerateCorpse). Merc record must keep that
        /// actor/body/hair so SpawnMonster does not show a MobClass (warden) body with stolen gear.
        /// LocalizationId is always the Magnum profile, never the AI MobClass id.
        /// </summary>
        private static void CopyCorpseIdentity(CreatureData corpse, CreatureData dest, string profileId)
        {
            if (dest == null)
                return;

            if (corpse != null && !ReferenceEquals(corpse, dest))
            {
                if (!string.IsNullOrEmpty(corpse.ActorId))
                    dest.ActorId = corpse.ActorId;
                if (!string.IsNullOrEmpty(corpse.BodyTypeId))
                    dest.BodyTypeId = corpse.BodyTypeId;
                dest.Gender = corpse.Gender;
                dest.SkinIndex = corpse.SkinIndex;
                dest.HairType = corpse.HairType;
                dest.HairColorHex = corpse.HairColorHex;
                if (!string.IsNullOrEmpty(corpse.VoiceBank))
                    dest.VoiceBank = corpse.VoiceBank;
            }

            dest.LocalizationId = profileId;
        }

        private static void ReviveMercCreatureData(
            Mercenary merc,
            CellPosition pos,
            CreatureAlliance alliance,
            RaidMetadata raidMetadata)
        {
            var data = merc.CreatureData;
            // Restore merc hit points — never leave MobClass/corpse-truncated MaxValue.
            var maxHp = Mathf.Max(1, data.BaseHealth > 0 ? data.BaseHealth : data.Health.MaxValue);
            if (data.Health.MaxValue != maxHp)
                data.Health.Reinitialize(maxHp);
            data.Health.Revive();
            data.Health.Restore(data.Health.MaxValue);
            data.Health.ReasonOfDeath = HealthInfo.DeathReason.None;
            data.Position = pos;
            data.CreatureAlliance = alliance;
            data.LocalizationId = merc.ProfileId;
            if (raidMetadata != null)
                data.UniqueId = raidMetadata.NextIdCounter;
            merc.State = MercenaryState.None;
        }
    }

    [HarmonyPatch(typeof(ItemInteractionSystem), nameof(ItemInteractionSystem.UseResurrectKit))]
    internal static class SquadOperativeDefibPatch
    {
        private static bool Prefix(
            Mercenaries mercenaries,
            PerkFactory perkFactory,
            Difficulty difficulty,
            TurnController turnController,
            Creatures creatures,
            RaidMetadata raidMetadata,
            MapObstacles mapObstacles,
            BasePickupItem item,
            Creature user,
            MapCell cell,
            ref Monster creature,
            ref ItemInteractionSystem.ResurrectResult __result)
        {
            if (!Plugin.Config.Enabled)
                return true;

            try
            {
                var placeable = item?.Record<PlaceableDeviceRecord>();
                if (placeable == null)
                    return true;

                // Prefer squad corpse even when an NPC corpse is first in raycast order.
                var squadCorpse = AllyResurrectService.FindBestSquadCorpse(
                    mapObstacles, placeable, cell.Position, mercenaries);
                if (squadCorpse?.CorpseStorage?.CreatureData == null)
                    return true;

                if (!AllyResurrectService.TryResolveSquadProfileId(
                        squadCorpse.CorpseStorage.CreatureData, mercenaries, out var profileId))
                    return true;

                Plugin.Logger.Log("Defib prefix: intercepting squad corpse " + profileId
                                  + " (skip vanilla MobClass spawn).");

                var ok = AllyResurrectService.TryResurrectSquadOperative(
                    mercenaries, turnController, creatures, raidMetadata, mapObstacles,
                    item, user, cell, out creature, preferredCorpse: squadCorpse);
                __result = ok
                    ? ItemInteractionSystem.ResurrectResult.Success
                    : ItemInteractionSystem.ResurrectResult.Fail;
                return false;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError("Defib prefix failed.");
                Plugin.Logger.LogException(ex);
                __result = ItemInteractionSystem.ResurrectResult.Fail;
                creature = null;
                return false;
            }
        }
    }
}
