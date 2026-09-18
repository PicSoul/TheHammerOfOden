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

            CollectTargetAnchors(ghost.transform.position);
            if (TargetAnchors.Count == 0)
            {
                return;
            }

            float best = ModConfig.DerivedSnapDistance.Value;
            Vector3 bestOffset = Vector3.zero;
            bool found = false;

            foreach (Vector3 source in SourceAnchors)
            {
                foreach (Vector3 target in TargetAnchors)
                {
                    float distance = Vector3.Distance(source, target);
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

            if (ModConfig.DebugEnabled)
            {
                HammerOfOdenPlugin.Debug(
                    $"Derived snap: moved {bestOffset.magnitude:0.###}m onto a derived anchor "
                    + $"({SourceAnchors.Count} source, {TargetAnchors.Count} target).");
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
        private static void CollectTargetAnchors(Vector3 origin)
        {
            TargetAnchors.Clear();
            Seen.Clear();

            float radius = ModConfig.DerivedTargetRange.Value;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, Colliders);

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

                ScratchPoints.Clear();
                piece.GetSnapPoints(ScratchPoints);
                foreach (Transform point in ScratchPoints)
                {
                    if (point != null)
                    {
                        TargetAnchors.Add(point.position);
                    }
                }

                if (mode == DerivedSnapMode.Off)
                {
                    continue;
                }

                if (!DerivedSnapPoints.TryMeasure(piece, out Vector3 center, out Vector3 extents))
                {
                    continue;
                }

                Transform t = piece.transform;
                foreach (KeyValuePair<string, Vector3> anchor in DerivedSnapPoints.BuildAnchors(center, extents, mode))
                {
                    TargetAnchors.Add(t.TransformPoint(anchor.Value));
                }
            }
        }
    }
}
