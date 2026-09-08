using MGSC;

namespace Squad_Leader
{
    /// <summary>
    /// Equal to vanilla enemy <see cref="AiBehaviour.ShootTargetReachable"/> plus the
    /// Better AI center-path ally check. No scatter cone, cover scoring, or flank.
    /// The player is never treated as adjacent cover (operatives stand next to the leader).
    /// </summary>
    internal static class SquadShotGeometry
    {
        public static bool IsActive(Monster monster)
        {
            if (monster?.CreatureData == null || Plugin.Config == null)
                return false;
            if (!Plugin.Config.Enabled || !Plugin.Config.ExperimentalAi)
                return false;
            return AllyMovementService.IsSquadAlly(monster);
        }

        public static bool CanFire(Monster shooter, CellPosition targetPos)
        {
            if (shooter?.Behaviour == null)
                return true;

            if (!shooter.Behaviour.ShootTargetReachable(shooter.CreatureData.Position, targetPos))
                return false;

            return !HasAllyOnCenterPath(shooter, targetPos);
        }

        public static bool HasAllyOnCenterPath(Monster shooter, CellPosition targetPos)
        {
            if (shooter?.CreatureData == null)
                return false;

            var state = Plugin.GetCachedDungeonState();
            var creatures = state?.Get<Creatures>();
            var grid = state?.Get<MapGrid>();
            var obstacles = state?.Get<MapObstacles>();
            var renderer = state?.Get<MapRenderer>();
            if (creatures == null || grid == null || obstacles == null || renderer == null)
                return false;

            var weapon = shooter.CreatureData.Inventory?.CurrentWeapon;
            if (weapon == null)
                return false;

            BallisticPath path;
            try
            {
                var scratch = new BallisticPath();
                path = FirearmSystem.GetShootPath(
                    renderer, grid, obstacles, shooter, scratch, weapon, targetPos,
                    out _, isPrecise: true, preciseScatterAngle: 0f);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("EqualAI: GetShootPath failed: " + ex.Message);
                return false;
            }

            if (path?.Path == null)
                return false;

            var from = shooter.CreatureData.Position;
            foreach (var cell in path.Path)
            {
                if (cell.Equals(from) || cell.Equals(targetPos))
                    continue;

                var creature = creatures.GetCreature(cell.X, cell.Y);
                if (creature == null || creature == shooter)
                    continue;
                if (!creature.IsAlly(shooter))
                    continue;
                if (IsAllowedAllyCover(shooter, creature))
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Same adjacency rule as <see cref="FirearmSystem.CanMonsterBeAllyCover"/>,
        /// except the player is never cover.
        /// </summary>
        public static bool IsAllowedAllyCover(Creature shooter, Creature other)
        {
            if (shooter == null || other == null || shooter == other)
                return false;
            if (other is Player)
                return false;
            return FirearmSystem.CanMonsterBeAllyCover(shooter, other);
        }
    }
}
