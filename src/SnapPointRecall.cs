using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Works out which anchor a placed piece was snapped by, and reapplies it when that
    /// piece is copied.
    /// </summary>
    /// <remarks>
    /// Nothing records the snap point used to place a piece - it is a choice made during
    /// placement that leaves no trace, unlike rotation, which the piece carries in its
    /// transform. It can however be inferred: a snapped piece has one of its anchors sitting
    /// exactly on a neighbour's anchor, so searching for a coincident pair usually finds it.
    ///
    /// Only attempted for pieces with pitch or roll, and for two reasons. It is where the
    /// answer is useful, since a flat piece is easy enough to re-place by eye. More
    /// importantly it is where the inference is trustworthy: a flat piece in a grid has
    /// several anchors touching its neighbours and the match is ambiguous, while a tilted
    /// piece meets its surroundings at an angle where usually only one pair coincides.
    ///
    /// The result is carried as a local position rather than an index. Indices depend on
    /// child ordering and on the DerivedSnapPoints setting, so one recorded today would mean
    /// something else after a config change; a local position identifies the same corner of
    /// the same piece regardless.
    /// </remarks>
    internal static class SnapPointRecall
    {
        private const float Coincident = 0.01f;

        private static readonly Collider[] Colliders = new Collider[64];
        private static readonly HashSet<Piece> Seen = new HashSet<Piece>();
        private static readonly List<Transform> Scratch = new List<Transform>();

        private static bool _pending;
        private static Vector3 _pendingLocal;
        private static int _pieceMask = -1;

        /// <summary>Called when a piece is copied, before the new ghost exists.</summary>
        internal static void Record(Piece piece)
        {
            _pending = false;

            if (!ModConfig.IsEnabled || !ModConfig.RecallSnapPoint.Value || piece == null)
            {
                return;
            }

            if (!IsTilted(piece))
            {
                return;
            }

            if (!TryFindSnappedAnchor(piece, out Vector3 world))
            {
                HammerOfOdenPlugin.Debug("Copy: piece is tilted but no anchor meets a neighbour; snap point left as is.");
                return;
            }

            _pendingLocal = piece.transform.InverseTransformPoint(world);
            _pending = true;

            HammerOfOdenPlugin.Debug($"Copy: inferred placement anchor at local {_pendingLocal}.");
        }

        /// <summary>Called once the ghost for the copied piece has been built.</summary>
        internal static void ApplyTo(GameObject ghost, ref int manualSnapPoint)
        {
            if (!_pending || ghost == null)
            {
                return;
            }

            _pending = false;

            Piece piece = ghost.GetComponent<Piece>();
            if (piece == null)
            {
                return;
            }

            Scratch.Clear();
            piece.GetSnapPoints(Scratch);

            float best = Coincident * Coincident;
            int bestIndex = -1;

            for (int i = 0; i < Scratch.Count; i++)
            {
                if (Scratch[i] == null)
                {
                    continue;
                }

                float distance = (Scratch[i].localPosition - _pendingLocal).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            manualSnapPoint = bestIndex;
            HammerOfOdenPlugin.Debug($"Copy: snap point set to '{Scratch[bestIndex].name}'.");
        }

        /// <summary>Pitch or roll beyond a rounding error - rotation vanilla cannot produce.</summary>
        private static bool IsTilted(Piece piece)
        {
            Vector3 euler = piece.transform.rotation.eulerAngles;
            return Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 1f
                || Mathf.Abs(Mathf.DeltaAngle(0f, euler.z)) > 1f;
        }

        /// <summary>
        /// The piece's anchor that sits on a neighbour's anchor. Ambiguity is treated as
        /// failure: guessing between two candidates would be worse than leaving the current
        /// selection alone.
        /// </summary>
        private static bool TryFindSnappedAnchor(Piece piece, out Vector3 world)
        {
            world = Vector3.zero;

            Vector3[] own = DerivedAnchorCache.Get(piece, ModConfig.DerivedSnaps.Value);
            if (own.Length == 0)
            {
                return false;
            }

            if (_pieceMask < 0)
            {
                _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
            }

            Seen.Clear();

            float radius = 0f;
            Vector3 origin = piece.transform.position;
            foreach (Vector3 anchor in own)
            {
                radius = Mathf.Max(radius, (anchor - origin).magnitude);
            }

            int count = Physics.OverlapSphereNonAlloc(origin, radius + 0.5f, Colliders, _pieceMask);

            int matches = 0;
            Vector3 found = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Piece other = Colliders[i] != null ? Colliders[i].GetComponentInParent<Piece>() : null;
                if (other == null || other == piece || !Seen.Add(other))
                {
                    continue;
                }

                foreach (Vector3 neighbour in DerivedAnchorCache.Get(other, ModConfig.DerivedSnaps.Value))
                {
                    foreach (Vector3 anchor in own)
                    {
                        if ((anchor - neighbour).sqrMagnitude > Coincident * Coincident)
                        {
                            continue;
                        }

                        // A second distinct match means we cannot tell which was used.
                        if (matches > 0 && (anchor - found).sqrMagnitude > Coincident * Coincident)
                        {
                            return false;
                        }

                        found = anchor;
                        matches++;
                    }
                }
            }

            if (matches == 0)
            {
                return false;
            }

            world = found;
            return true;
        }
    }
}
