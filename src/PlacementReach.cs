using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lets you build anywhere the workbench reaches, not just anywhere your arm reaches.
    /// </summary>
    /// <remarks>
    /// Valheim applies two separate limits and only one of them is about permission. A
    /// crafting station's circle says where you are allowed to build; Player.m_maxPlaceDistance
    /// says how far from yourself the aiming ray will travel, and is a little over eight
    /// metres regardless. The second is the one you feel, and it has nothing to do with the
    /// first: standing in the middle of a workbench covering thirty metres, you can still
    /// only place within arm's reach, so building a wall means walking the whole length of it.
    ///
    /// Raising the ray's reach to match the station's circle makes the circle mean what it
    /// looks like it means. Nothing about what is permitted changes - the station still has
    /// to cover the spot, and every other placement rule still applies - so this grants no
    /// ability that was not already there, only the ability to use it without walking.
    ///
    /// The vanilla value is captured once and restored on leaving build mode, because the
    /// same field governs how far away pieces can be removed and repaired.
    /// </remarks>
    internal static class PlacementReach
    {
        private static float _vanilla = -1f;

        /// <summary>
        /// Sets the reach for this frame, from the station the player is standing in.
        /// </summary>
        internal static void Apply(Player player, ref float maxPlaceDistance)
        {
            if (_vanilla < 0f)
            {
                _vanilla = maxPlaceDistance;
            }

            if (!ModConfig.IsEnabled || !ModConfig.ExtendReachToStation.Value || player == null)
            {
                maxPlaceDistance = _vanilla;
                return;
            }

            float covering = StationRange.CoveringRange(player.transform.position);

            // Never below vanilla: standing outside every station's circle should not be
            // worse than not having the mod, and a station smaller than arm's reach should
            // not shorten it.
            maxPlaceDistance = Mathf.Max(
                _vanilla,
                Mathf.Min(covering, ModConfig.ReachLimit.Value));
        }

        /// <summary>Put the vanilla reach back when leaving build mode.</summary>
        internal static void Restore(ref float maxPlaceDistance)
        {
            if (_vanilla >= 0f)
            {
                maxPlaceDistance = _vanilla;
            }
        }
    }
}
