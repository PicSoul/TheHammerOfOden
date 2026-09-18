using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lets the placing piece snap to derived anchors on pieces already built — the centre
    /// of a wall, the midpoint of a beam — not just the snap points they ship with.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT add anything to pieces in the world. Vanilla's snap search
    /// takes Transforms, which would mean creating, caching and cleaning up child objects
    /// on every piece within 10m, every frame. Instead this works entirely in Vector3
    /// maths: gather nearby pieces, derive their anchor positions numerically, pick the
    /// closest pair, and move the ghost. Nothing is added to the scene and there is nothing
    /// to leak.
    ///
    /// Runs as a postfix, which is safe here for a reason that did not apply to rotation:
    /// vanilla derives position from rotation, but nothing downstream derives anything from
    /// position. The one caveat is that placement validity (no-build zones, private areas,
    /// overlap) is evaluated before we move, so the adjustment is capped to a short
    /// distance to keep a stale verdict from mattering.
    /// </remarks>
    internal static class TargetSnapping
    {
        private static readonly Collider[] Colliders = new Collider[128];
        private static readonly List<Vector3> SourceAnchors = new List<Vector3>();
        private static readonly List<Vector3> TargetAnchors = new List<Vector3>();
        private static readonly List<Transform> ScratchPoints = new List<Transform>();
        private static readonly HashSet<Piece> Seen = new HashSet<Piece>();
        private static Vector3 _lastOffset = Vector3.positiveInfinity;

        private static int _pieceMask = -1;
        private static Vector3 _lastScanOrigin = Vector3.positiveInfinity;
        private static float _lastScanTime;

        internal static void Apply(Player player, GameObject ghost, int manualSnapPoint, bool freePlacementActive)
        {
            if (!ModConfig.IsEnabled
                || !ModConfig.SnapToDerivedTargets.Value
                || ghost == null
                || !ghost.activeSelf
                || freePlacementActive)
            {
                return;
            }

            Piece ghostPiece = ghost.GetComponent<Piece>();
            if (ghostPiece == null)
            {
                return;
            }

            CollectSourceAnchors(ghostPiece, manualSnapPoint);
            if (SourceAnchors.Count == 0)
            {
                return;
            }

            // Nearby geometry barely changes between frames, and every anchor in it is
            // static, so a slightly stale set costs nothing and saves the whole scan.
            Vector3 origin = ghost.transform.position;
            bool moved = (origin - _lastScanOrigin).sqrMagnitude > 0.0625f;   // 0.25m
            bool stale = Time.time - _lastScanTime > 0.25f;

            if (moved || stale)
            {
                _lastScanOrigin = origin;
                _lastScanTime = Time.time;
                CollectTargetAnchors(origin, SearchRadius(origin));
            }

            if (TargetAnchors.Count == 0)
            {
                return;
            }

            // Squared throughout: this is the inner loop over every source/target pair, and
            // the square root per comparison buys nothing when only the ordering matters.
            float best = ModConfig.DerivedSnapDistance.Value;
            best *= best;

            Vector3 bestOffset = Vector3.zero;
            bool found = false;

            foreach (Vector3 source in SourceAnchors)
            {
                foreach (Vector3 target in TargetAnchors)
                {
                    float distance = (target - source).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        bestOffset = target - source;
                        found = true;
                    }
                }
            }

            if (!found)
            {
                return;
            }

            ghost.transform.position += bestOffset;

            // This runs every frame while snapped, so only speak up when the answer changes.
            if (ModConfig.DebugEnabled && (bestOffset - _lastOffset).sqrMagnitude > 0.0001f)
            {
                _lastOffset = bestOffset;
                HammerOfOdenPlugin.Debug(
                    $"Derived snap: {bestOffset.magnitude:0.###}m "
                    + $"({SourceAnchors.Count} source, {TargetAnchors.Count} target anchors).");
            }
        }

        /// <summary>
        /// Anchors on the piece in hand. When a specific snap point is selected with Q/E we
        /// honour that choice and use only it, matching how vanilla treats a manual pick.
        /// </summary>
        private static void CollectSourceAnchors(Piece ghostPiece, int manualSnapPoint)
        {
            SourceAnchors.Clear();

            ScratchPoints.Clear();
            ghostPiece.GetSnapPoints(ScratchPoints);

            if (manualSnapPoint >= 0 && manualSnapPoint < ScratchPoints.Count)
            {
                if (ScratchPoints[manualSnapPoint] != null)
                {
                    SourceAnchors.Add(ScratchPoints[manualSnapPoint].position);
                }
                return;
            }

            foreach (Transform point in ScratchPoints)
            {
                if (point != null)
                {
                    SourceAnchors.Add(point.position);
                }
            }
        }

        /// <summary>
        /// Anchors on nearby built pieces: their own snap points plus the derived ones,
        /// computed rather than instantiated.
        /// </summary>
        /// <summary>
        /// How far to look for pieces, derived rather than guessed.
        /// </summary>
        /// <remarks>
        /// The sphere is centred on the ghost's origin, but its anchors sit out at its
        /// bounds - a 2x2 floor reaches about 1.4m - so the radius has to cover that spread
        /// plus the snapping distance, or a large piece would never find anything.
        ///
        /// It does not need to cover the target piece's size as well: OverlapSphere tests
        /// colliders, not origins, so a piece is found whenever any part of it is in range,
        /// and its anchors lie within its own volume.
        ///
        /// Typical pieces land near 2m rather than the flat 5m this used to use, and sphere
        /// cost grows with the cube of the radius.
        /// </remarks>
        private static float SearchRadius(Vector3 origin)
        {
            float spread = 0f;

            foreach (Vector3 anchor in SourceAnchors)
            {
                float distance = (anchor - origin).sqrMagnitude;
                if (distance > spread)
                {
                    spread = distance;
                }
            }

            spread = Mathf.Sqrt(spread);

            // A little slack for anchors that sit slightly proud of a piece's collider.
            float needed = spread + ModConfig.DerivedSnapDistance.Value + 0.25f;

            return Mathf.Min(needed, ModConfig.DerivedTargetRange.Value);
        }

        private static void CollectTargetAnchors(Vector3 origin, float radius)
        {
            TargetAnchors.Clear();
            Seen.Clear();

            if (_pieceMask < 0)
            {
                _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
            }

            // Without a mask this returns terrain, water, characters and dropped items too,
            // and GetComponentInParent then runs on every one of them to find no Piece at all.
            int count = Physics.OverlapSphereNonAlloc(origin, radius, Colliders, _pieceMask);

            DerivedSnapMode mode = ModConfig.DerivedSnaps.Value;

            for (int i = 0; i < count; i++)
            {
                Piece piece = Colliders[i] != null ? Colliders[i].GetComponentInParent<Piece>() : null;
                if (piece == null || !Seen.Add(piece))
                {
                    continue;
                }

                // Never snap to the thing we are holding.
                if (Player.IsPlacementGhost(piece.gameObject))
                {
                    continue;
                }

                // One cached lookup covers the piece's own snap points and the derived ones.
                foreach (Vector3 anchor in DerivedAnchorCache.Get(piece, mode))
                {
                    TargetAnchors.Add(anchor);
                }
            }
        }
    }
}
