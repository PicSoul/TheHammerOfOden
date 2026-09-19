using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Changes how far a crafting station lets you build, and remembers it.
    /// </summary>
    /// <remarks>
    /// A workbench covers a fixed circle and everything inside it can be built on. That
    /// circle is usually too small for the thing you are actually building, so the ritual is
    /// to plant spare workbenches around the site and pull them up afterwards. Making the
    /// circle itself adjustable removes the ritual.
    ///
    /// The range lives on the station's ZDO rather than in the config, because it is a
    /// property of that particular workbench and not of the game: two benches in the same
    /// base can reasonably want different radii, and the value travels to other players and
    /// survives the world being reloaded without this mod having to be installed to read it
    /// back sensibly. This is the same mechanism the piece scale uses, which is already
    /// proven across zone reloads and restarts.
    ///
    /// m_rangeBuild is not networked by vanilla, so it is reapplied whenever the station
    /// spawns - the same reason ZNetView.Awake has to reapply a stored scale.
    /// </remarks>
    internal static class StationRange
    {
        /// <summary>Distinct from anything vanilla stores, so nothing else reads or writes it.</summary>
        private const string Key = "HoO_rangeBuild";

        private static readonly AccessTools.FieldRef<List<CraftingStation>> AllStations =
            ResolveAllStations();

        /// <summary>
        /// Binds the game's own register of live stations.
        /// </summary>
        /// <remarks>
        /// A delegate rather than FieldInfo.GetValue, because the extended reach consults it
        /// every frame, and FindObjectsOfType would cost far more than either.
        ///
        /// CraftingStation.Instances looks like the public answer to this and is not: it is
        /// the IMonoUpdater registry, which holds every updating object in the game.
        /// </remarks>
        private static AccessTools.FieldRef<List<CraftingStation>> ResolveAllStations()
        {
            try
            {
                System.Reflection.FieldInfo field =
                    AccessTools.Field(typeof(CraftingStation), "m_allStations");

                if (field == null)
                {
                    HammerOfOdenPlugin.Error(
                        "CraftingStation.m_allStations was not found, so station range adjustment "
                        + "and extended reach are disabled. Valheim has probably changed.");
                    return null;
                }

                return AccessTools.StaticFieldRefAccess<List<CraftingStation>>(field);
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not reach CraftingStation.m_allStations, so station range adjustment "
                    + "and extended reach are disabled. " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Moves the range of the station you mean by one step.
        /// </summary>
        /// <returns>True if a station was found and changed.</returns>
        internal static bool Adjust(Player player, int sign)
        {
            CraftingStation station = Target(player);

            if (station == null)
            {
                Notify.Show(player, "No crafting station in reach");
                return false;
            }

            ZNetView view = station.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return false;
            }

            // Writing to a ZDO you do not own is discarded, and for a station somebody else
            // placed that is the normal case rather than the exception.
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            float wanted = Mathf.Clamp(
                station.m_rangeBuild + sign * ModConfig.StationRangeStep.Value,
                ModConfig.StationRangeMin.Value,
                ModConfig.StationRangeMax.Value);

            if (Mathf.Approximately(wanted, station.m_rangeBuild))
            {
                return false;
            }

            station.m_rangeBuild = wanted;

            ZDO zdo = view.GetZDO();
            zdo.Set(Key, wanted);

            // Read straight back. A write to a ZDO you do not own is discarded silently, and
            // silently is the worst way to find that out - it looks like it worked until the
            // world reloads.
            float stored = zdo.GetFloat(Key, -1f);
            if (!Mathf.Approximately(stored, wanted))
            {
                HammerOfOdenPlugin.Error(
                    $"Station range did not stick: wrote {wanted:0.##} and read back {stored:0.##}. "
                    + $"owner={view.IsOwner()}. The range will work now but be lost on reload.");
            }

            if (ModConfig.ShowStationArea.Value)
            {
                station.ShowAreaMarker();
            }

            Notify.Show(player, $"{station.m_name}: {wanted:0.#}m");
            HammerOfOdenPlugin.Debug($"Station '{station.m_name}' build range -> {wanted:0.##}m.");
            return true;
        }

        /// <summary>
        /// The station the player means: the one being looked at, else the nearest.
        /// </summary>
        /// <remarks>
        /// Looking at one is preferred because it is unambiguous where several overlap, which
        /// in a built-up base is most of the time. Falling back to the nearest keeps it usable
        /// when the bench is behind a wall or buried in clutter.
        /// </remarks>
        private static CraftingStation Target(Player player)
        {
            if (player == null)
            {
                return null;
            }

            Piece hovering = player.GetHoveringPiece();
            if (hovering != null)
            {
                CraftingStation looked = hovering.GetComponentInParent<CraftingStation>();
                if (looked != null)
                {
                    return looked;
                }
            }

            if (ModConfig.RequireLookingAtStation.Value)
            {
                return null;
            }

            return Nearest(player.transform.position, ModConfig.StationAdjustDistance.Value);
        }

        private static CraftingStation Nearest(Vector3 point, float within)
        {
            List<CraftingStation> stations = AllStations?.Invoke();
            if (stations == null)
            {
                return null;
            }

            CraftingStation best = null;
            float bestDistance = within * within;

            foreach (CraftingStation station in stations)
            {
                if (station == null)
                {
                    continue;
                }

                float distance = (station.transform.position - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = station;
                }
            }

            return best;
        }

        /// <summary>
        /// The largest build range among the stations whose circle the player is standing in.
        /// </summary>
        /// <remarks>
        /// Returns zero when standing in none. The point of this is that a workbench set to
        /// cover thirty metres should let you build across all thirty, rather than the eight
        /// or so vanilla lets you reach from where you stand - the station's circle is the
        /// permission, and the arm's length is an unrelated limit that happens to be smaller.
        /// </remarks>
        internal static float CoveringRange(Vector3 point)
        {
            List<CraftingStation> stations = AllStations?.Invoke();
            if (stations == null)
            {
                return 0f;
            }

            float best = 0f;

            foreach (CraftingStation station in stations)
            {
                if (station == null)
                {
                    continue;
                }

                // Asked for rather than read off m_rangeBuild, because this is the figure
                // vanilla itself checks against, whatever it chooses to fold into it.
                float range = station.GetStationBuildRange();
                if (range <= best)
                {
                    continue;
                }

                if ((station.transform.position - point).sqrMagnitude <= range * range)
                {
                    best = range;
                }
            }

            return best;
        }

        /// <summary>
        /// Reapplies a stored range as a station comes into the world.
        /// </summary>
        /// <remarks>
        /// Hooked on CraftingStation.Start rather than ZNetView.Awake, which is where the
        /// stored scale is restored. The component is then guaranteed to be the right one,
        /// instead of depending on it sharing a GameObject with the ZNetView - true for a
        /// workbench, but not a promise - and Start runs after every Awake in the scene, so
        /// the ZDO is certain to be there to read.
        ///
        /// CraftingStation has no Awake. Patching one silently disabled this whole feature:
        /// the range could be changed and the change was written, and nothing ever read it
        /// back, which looked exactly like the write failing.
        /// </remarks>
        internal static void Restore(CraftingStation station)
        {
            if (station == null)
            {
                return;
            }

            ZNetView view = station.GetComponentInParent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                HammerOfOdenPlugin.Debug(
                    $"Station '{station.name}' has no usable ZNetView at Awake; range not restored.");
                return;
            }

            float stored = view.GetZDO().GetFloat(Key, 0f);
            if (stored <= 0f)
            {
                return;
            }

            float applied = Mathf.Clamp(
                stored, ModConfig.StationRangeMin.Value, ModConfig.StationRangeMax.Value);

            station.m_rangeBuild = applied;

            HammerOfOdenPlugin.Debug(
                $"Restored '{station.m_name}' build range to {applied:0.##}m (stored {stored:0.##}).");
        }
    }
}
