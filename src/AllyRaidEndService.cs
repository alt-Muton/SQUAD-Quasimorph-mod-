using System.Linq;
using MGSC;

namespace Squad_Leader
{
    internal static class AllyRaidEndService
    {
        public static void ProcessAllies(
            Mercenaries mercenaries,
            MagnumCargo magnumCargo,
            MagnumProgression magnumSpaceship,
            MagnumProjects magnumProjects,
            SpaceTime spaceTime,
            Difficulty difficulty,
            PerkFactory perkFactory)
        {
            if (mercenaries == null || difficulty == null)
            {
                Plugin.Logger.LogError("AllyRaidEndService: missing deps.");
                SquadRaidState.Clear();
                return;
            }

            foreach (var profileId in SquadRaidState.AllyProfileIds.ToArray())
            {
                var merc = mercenaries.Get(profileId);
                if (merc == null)
                {
                    Plugin.Logger.LogWarning("AllyRaidEndService: merc gone " + profileId);
                    continue;
                }

                // Current InRaid leader is handled by vanilla MercDied / RestoreStateAfterMission.
                if (merc.State == MercenaryState.InRaid)
                {
                    Plugin.Logger.Log("AllyRaidEndService: skip current InRaid " + profileId);
                    continue;
                }

                try
                {
                    // Re-enable hunger so the clone is normal if later used as Player.
                    AllyMovementService.RestoreHunger(merc.CreatureData);
                    AllyBackpackLayout.RestoreNativeBackpackLayout(merc.CreatureData?.Inventory);

                    var dead = merc.CreatureData != null && merc.CreatureData.Health != null && merc.CreatureData.Health.Dead;
                    if (dead)
                        ProcessAllyDeath(mercenaries, merc, magnumCargo, magnumSpaceship, magnumProjects, spaceTime, difficulty, perkFactory);
                    else
                        MercenarySystem.RestoreStateAfterMission(spaceTime, magnumSpaceship, merc, difficulty, perkFactory, magnumCargo);

                    Plugin.Logger.Log("Ally raid-end processed " + profileId + " dead=" + dead);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogError("AllyRaidEndService failed for " + profileId);
                    Plugin.Logger.LogException(ex);
                }
            }

            SquadRaidState.Clear();
            SquadSelectionState.Clear();
        }

        private static void ProcessAllyDeath(
            Mercenaries mercenaries,
            Mercenary merc,
            MagnumCargo magnumCargo,
            MagnumProgression magnumSpaceship,
            MagnumProjects magnumProjects,
            SpaceTime spaceTime,
            Difficulty difficulty,
            PerkFactory perkFactory)
        {
            var drop = difficulty.Preset.DropPenalty;
            var revive = difficulty.Preset.RevivePenalty;
            Plugin.Logger.Log("Ally death settle " + merc.ProfileId
                              + " DropPenalty=" + drop
                              + " RevivePenalty=" + revive);

            MercenarySystem.SetDefaultFireMode(merc);
            // None → all gear+implants to Magnum cargo; Bag → equipped only; FullDrop → leave empty / lost.
            // Requires AllyDifficultyLoot so None/Bag still have items on the merc after Monster death.
            MercenarySystem.DropOnDeathPenalty(merc, magnumCargo, difficulty, spaceTime);

            if (revive == RevivePenalty.PermanentDeath)
            {
                MercenarySystem.DeleteMercenary(mercenaries, merc.ProfileId);
                if (mercenaries.Values.Count == 0)
                {
                    var bossId = Data.MercenaryProfiles.Ids.FirstOrDefault(id => id.EndsWith("_boss"));
                    if (!string.IsNullOrEmpty(bossId))
                    {
                        MercenarySystem.CloneMercenary(
                            spaceTime, magnumProjects, magnumSpaceship, mercenaries, bossId,
                            true, difficulty, perkFactory);
                    }
                }
            }
            else
            {
                MercenarySystem.CloneMercenary(
                    spaceTime,
                    magnumProjects,
                    magnumSpaceship,
                    mercenaries,
                    merc.ProfileId,
                    revive == RevivePenalty.NoPenalty,
                    difficulty,
                    perkFactory);
            }
        }
    }
}
