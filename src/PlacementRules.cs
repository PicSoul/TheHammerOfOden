using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum PlacementFreedom
    {
        /// <summary>Every vanilla rule stands. Free placement only affects snapping.</summary>
        Vanilla = 0,

        /// <summary>
        /// Ignore rules about what a piece may stand on: ground-only, not-on-wood,
        /// tilting surfaces, cultivated soil and the like.
        /// </summary>
        Surfaces = 1,

        /// <summary>Also ignore spacing rules, such as workbench extensions crowding each other.</summary>
        SurfacesAndSpacing = 2,

        /// <summary>Also ignore biome, dungeon and weather restrictions.</summary>
        Everything = 3
    }

    /// <summary>
    /// Relaxes vanilla's placement rules while free placement is active.
    /// </summary>
    /// <remarks>
    /// Vanilla refuses a placement for around fifteen different reasons, and most are rules
    /// about tidiness rather than correctness: stone may not rest on wood, a forge extension
    /// must keep its distance from other extensions, a piece must sit on bare ground. Free
    /// placement is the natural home for setting those aside, since it already means "put it
    /// where I say".
    ///
    /// Three of them are never bypassed at any setting:
    ///
    ///   PrivateZone     someone else's ward
    ///   NoBuildZone     boss altars and similar protected ground
    ///   BlockedbyPlayer a player is standing there
    ///
    /// The first two are other people's boundaries rather than the game being fussy, and a
    /// building convenience has no business removing them. The third would let you place
    /// inside a player.
    ///
    /// Implemented as a postfix that rewrites the verdict, which is safe because vanilla's
    /// last act in the method is to apply that verdict to the ghost - so re-applying it
    /// ourselves reproduces exactly what vanilla would have done had it decided differently.
    /// </remarks>
    internal static class PlacementRules
    {
        private static readonly MethodInfo SetGhostValid =
            AccessTools.Method(typeof(Player), "SetPlacementGhostValid", new[] { typeof(bool) });

        internal static void Apply(Player player, ref Player.PlacementStatus status, GameObject ghost)
        {
            if (!ModConfig.IsEnabled || status == Player.PlacementStatus.Valid)
            {
                return;
            }

            if (ModConfig.Freedom.Value == PlacementFreedom.Vanilla)
            {
                return;
            }

            // Tied to free placement for the same reason clipping is: it is the one moment
            // the player has explicitly said they know better than the game.
            if (!FreePlacement.IsActiveNow())
            {
                return;
            }

            if (!CanBypass(status, ModConfig.Freedom.Value))
            {
                return;
            }

            HammerOfOdenPlugin.Debug($"Free placement overrode '{status}'.");

            status = Player.PlacementStatus.Valid;

            // One rule hides the ghost outright before bailing out; bring it back.
            if (ghost != null && !ghost.activeSelf)
            {
                ghost.SetActive(true);
            }

            SetGhostValid?.Invoke(player, new object[] { true });
        }

        private static bool CanBypass(Player.PlacementStatus status, PlacementFreedom freedom)
        {
            switch (status)
            {
                // Never. Other people's boundaries, and standing on someone.
                case Player.PlacementStatus.PrivateZone:
                case Player.PlacementStatus.NoBuildZone:
                case Player.PlacementStatus.BlockedbyPlayer:
                    return false;

                // Nothing was aimed at; there is no position to make valid.
                case Player.PlacementStatus.NoRayHits:
                    return false;

                // What the piece is allowed to rest on.
                case Player.PlacementStatus.Invalid:
                case Player.PlacementStatus.NeedCultivated:
                case Player.PlacementStatus.NeedDirt:
                    return freedom >= PlacementFreedom.Surfaces;

                // How much room a piece demands around itself.
                case Player.PlacementStatus.MoreSpace:
                case Player.PlacementStatus.ExtensionMissingStation:
                    return freedom >= PlacementFreedom.SurfacesAndSpacing;

                // Where in the world a piece is permitted at all.
                case Player.PlacementStatus.WrongBiome:
                case Player.PlacementStatus.NotInDungeon:
                case Player.PlacementStatus.NoTeleportArea:
                case Player.PlacementStatus.DeepSnow:
                case Player.PlacementStatus.NoSnow:
                    return freedom >= PlacementFreedom.Everything;

                default:
                    return false;
            }
        }
    }
}
