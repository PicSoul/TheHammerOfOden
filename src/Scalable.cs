using UnityEngine;

namespace TheHammerOfOden
{
    internal enum ScaleRestriction
    {
        /// <summary>Anything may be resized.</summary>
        Nothing = 0,

        /// <summary>Crafting and production stations are left alone; everything else may be resized.</summary>
        ProductionStations = 1,

        /// <summary>Anything you can interact with is left alone.</summary>
        AnythingInteractive = 2
    }

    /// <summary>
    /// Decides which pieces may be resized, and how far.
    /// </summary>
    /// <remarks>
    /// The default blocks crafting and production stations and nothing else. Those are pieces
    /// whose behaviour is tied to their geometry - a workbench has a build radius, a smelter
    /// has ore and output points, a cooking station has slots that food is placed on - and
    /// stretching them moves things the game expects to find where the model put them.
    ///
    /// Almost everything else is safe. Chests, doors, portals, torches, braziers, beds, item
    /// stands and station add-ons like the forge cooler are geometry with a trigger attached,
    /// and the trigger scales with the rest.
    ///
    /// Windmills and spinning wheels are covered by Smelter, which is the component Valheim
    /// uses for anything that converts one item into another over time.
    /// </remarks>
    internal static class Scalable
    {
        private static GameObject _cachedFor;
        private static bool _cached;
        private static bool _cachedParticles;

        internal static bool Allows(GameObject piece)
        {
            Evaluate(piece);
            return _cached;
        }

        /// <summary>
        /// Whether this piece carries particle effects, so a separate size ceiling can apply.
        /// </summary>
        /// <remarks>
        /// This existed because scaled effects appeared to break past roughly double. They did
        /// not: the emission volume was being squared, and a portal gates its effect on a
        /// proximity range that scaling does not touch. Both are fixed, and
        /// MaximumWithParticles matches Maximum by default, so nothing is capped.
        ///
        /// It stays because the second of those is a pattern rather than a single bug. Any
        /// piece, a modded one especially, can gate its own effects on a distance this mod
        /// knows nothing about, and a ceiling is the one lever that works without knowing
        /// which distance it is.
        /// </remarks>
        internal static bool HasParticles(GameObject piece)
        {
            Evaluate(piece);
            return _cachedParticles;
        }

        internal static void Forget()
        {
            _cachedFor = null;
        }

        private static void Evaluate(GameObject piece)
        {
            if (_cachedFor == piece)
            {
                return;
            }

            _cachedFor = piece;

            if (piece == null)
            {
                _cached = false;
                _cachedParticles = false;
                return;
            }

            _cachedParticles = piece.GetComponentInChildren<ParticleSystem>(true) != null;
            _cached = IsAllowed(piece);
        }

        private static bool IsAllowed(GameObject piece)
        {
            switch (ModConfig.ScaleRestrictions.Value)
            {
                case ScaleRestriction.Nothing:
                    return true;

                case ScaleRestriction.AnythingInteractive:
                    return !IsProduction(piece)
                        && piece.GetComponentInChildren<Interactable>(true) == null;

                default:
                    return !IsProduction(piece);
            }
        }

        /// <summary>Stations that turn one thing into another, or that craft.</summary>
        private static bool IsProduction(GameObject piece)
        {
            // Workbench, forge, artisan table, stonecutter and the like.
            if (piece.GetComponentInChildren<CraftingStation>(true) != null)
            {
                return true;
            }

            // Smelter covers kilns, furnaces, windmills and spinning wheels too: Valheim uses
            // one component for everything that converts an item over time.
            if (piece.GetComponentInChildren<Smelter>(true) != null)
            {
                return true;
            }

            if (piece.GetComponentInChildren<CookingStation>(true) != null)
            {
                return true;
            }

            if (piece.GetComponentInChildren<Fermenter>(true) != null)
            {
                return true;
            }

            if (piece.GetComponentInChildren<Beehive>(true) != null)
            {
                return true;
            }

            return false;
        }
    }
}
