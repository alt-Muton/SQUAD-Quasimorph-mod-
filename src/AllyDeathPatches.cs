using HarmonyLib;
using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Mid-raid ally death left squad state pointing at a dead merc. Next floor then
    /// respawned that corpse (or transferred it) as a named, non-following ghost.
    /// </summary>
    [HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.KillMonster))]
    internal static class AllyKillMonsterPatch
    {
        private static void Postfix(Creature creature)
        {
            if (!Plugin.Config.Enabled || creature == null)
                return;
            AllySpawnService.HandleAllyDied(creature);
        }
    }

    /// <summary>Vanilla floor transfer ignores Alive; never take dead squad (or any) monsters.</summary>
    [HarmonyPatch(typeof(MonsterTransfer), nameof(MonsterTransfer.TakeMonster))]
    internal static class MonsterTransferSkipDeadPatch
    {
        private static bool Prefix(Monster monster)
        {
            if (monster?.CreatureData?.Health != null && monster.CreatureData.Health.Dead)
            {
                monster.IsTransferable = false;
                Plugin.Logger.Log("Skip floor transfer of dead monster uid=" + monster.CreatureData.UniqueId);
                return false;
            }
            return true;
        }
    }
}
