using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Decides which pieces may be bent.
    /// </summary>
    /// <remarks>
    /// Bending deforms a piece's meshes along a curve, which means its collision has to follow.
    /// A BoxCollider cannot curve, so a bent piece needs its box replaced by geometry that can -
    /// and that is only tractable while there is one box to replace. A piece built from nine of
    /// them is nine chains of collision to lay out and keep in step.
    ///
    /// The happy accident is that the collider count turns out to be a good proxy for whether a
    /// piece should bend at all. Measuring six pieces in game: a wall, a log pole, a stone wall
    /// and a roof section carry one collider each and are exactly the plain structure an arch is
    /// made of. A step ladder carries nine and a fence gate four - both things with moving or
    /// functional parts, and both things nobody wants bent. The rule that makes the work
    /// tractable and the rule that keeps doors straight are the same rule.
    ///
    /// So eligibility is read off the piece rather than from a list of names, the way Scalable
    /// and CreatureTiers do it, and a piece another mod adds is judged by the same measure. The
    /// two exception lists are there because a rule this simple will be wrong occasionally, and
    /// being wrong occasionally is fine as long as it can be corrected without a new build.
    /// </remarks>
    internal static class Bendable
    {
        private static GameObject _cachedFor;
        private static bool _cached;
        private static string _cachedReason;

        private static string _listSource;
        private static readonly HashSet<string> Never = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Always = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static bool Allows(GameObject piece)
        {
            Evaluate(piece);
            return _cached;
        }

        /// <summary>Why a piece was refused, for the message and the log.</summary>
        internal static string Reason(GameObject piece)
        {
            Evaluate(piece);
            return _cachedReason;
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
                _cachedReason = "nothing selected";
                return;
            }

            _cached = IsAllowed(piece, out _cachedReason);
        }

        private static bool IsAllowed(GameObject piece, out string reason)
        {
            RefreshLists();

            string prefab = Utils.GetPrefabName(piece);

            // The lists win over the measurement, in both directions, because the measurement is
            // a good guess rather than a promise.
            if (Never.Contains(prefab))
            {
                reason = "listed as never bendable";
                return false;
            }

            if (Always.Contains(prefab))
            {
                reason = "listed as always bendable";
                return true;
            }

            Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);

            int solid = 0;
            foreach (Collider collider in colliders)
            {
                // Triggers are not collision; they are the volumes a piece uses to notice you.
                // Counting them would exclude perfectly plain pieces for having a comfort range.
                if (collider != null && !collider.isTrigger)
                {
                    solid++;
                }
            }

            if (solid == 0)
            {
                reason = "has no collision to rebuild";
                return false;
            }

            if (solid > 1)
            {
                reason = "is built from " + solid + " colliders, so it has moving or working parts";
                return false;
            }

            if (piece.GetComponentInChildren<MeshFilter>(true) == null)
            {
                reason = "has no mesh to bend";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>Splits the exception lists once per change, since this is asked per piece.</summary>
        private static void RefreshLists()
        {
            string raw = (ModConfig.BendNever.Value ?? string.Empty)
                + "\u0000" + (ModConfig.BendAlways.Value ?? string.Empty);

            if (raw == _listSource)
            {
                return;
            }

            _listSource = raw;

            Fill(Never, ModConfig.BendNever.Value);
            Fill(Always, ModConfig.BendAlways.Value);
        }

        private static void Fill(HashSet<string> into, string raw)
        {
            into.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string entry in raw.Split(','))
            {
                string name = entry.Trim();
                if (name.Length > 0)
                {
                    into.Add(name);
                }
            }
        }
    }
}
