using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// World-space anchors for pieces already built: their own snap points and the derived
    /// ones, computed once and kept.
    /// </summary>
    /// <remarks>
    /// A built piece does not move, so all of this is a constant. Recomputing it per frame
    /// for every piece in range is what made derived target snapping expensive.
    ///
    /// The piece's real snap points are read straight from its tagged children rather than
    /// through Piece.GetSnapPoints, on purpose: calling that here would walk a Harmony
    /// wrapper for every nearby piece every frame, which is the mistake this cache exists to
    /// undo.
    ///
    /// Entries are validated against the piece's transform so a piece that does move is
    /// still handled, and the whole cache is dropped when build mode ends.
    /// </remarks>
    internal static class DerivedAnchorCache
    {
        private sealed class Entry
        {
            internal Vector3[] World;
            internal AnchorKind[] Kinds;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal DerivedSnapMode Mode;
        }

        private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();

        private static readonly List<Vector3> Local = new List<Vector3>();
        private static readonly List<AnchorKind> Kinds = new List<AnchorKind>();
        private static readonly List<Vector3> RealPoints = new List<Vector3>();
        private static readonly List<DerivedSnapPoints.Anchor> Derived = new List<DerivedSnapPoints.Anchor>();

        private static readonly Vector3[] NoPoints = new Vector3[0];
        private static readonly AnchorKind[] NoKinds = new AnchorKind[0];

        /// <summary>Every anchor on the piece: its own snap points plus the derived ones.</summary>
        internal static Vector3[] Get(Piece piece, DerivedSnapMode mode)
        {
            return GetEntry(piece, mode)?.World ?? NoPoints;
        }

        /// <summary>Kinds lining up one-for-one with the array returned by Get.</summary>
        internal static AnchorKind[] KindsFor(Piece piece, DerivedSnapMode mode)
        {
            return GetEntry(piece, mode)?.Kinds ?? NoKinds;
        }

        private static Entry GetEntry(Piece piece, DerivedSnapMode mode)
        {
            if (piece == null)
            {
                return null;
            }

            Transform t = piece.transform;
            int id = piece.GetInstanceID();

            if (Cache.TryGetValue(id, out Entry entry)
                && entry.Mode == mode
                && entry.Position == t.position
                && entry.Rotation == t.rotation)
            {
                return entry;
            }

            Local.Clear();
            Kinds.Clear();
            RealPoints.Clear();

            // The piece's own snap points, read without going through our patched method.
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (!child.CompareTag("snappoint"))
                {
                    continue;
                }

                Local.Add(child.localPosition);
                Kinds.Add(AnchorKind.Vanilla);
                RealPoints.Add(child.localPosition);
            }

            if (mode != DerivedSnapMode.Off
                && DerivedSnapPoints.TryMeasure(piece, out Vector3 center, out Vector3 extents))
            {
                Derived.Clear();
                DerivedSnapPoints.Build(center, extents, mode, Derived);

                foreach (DerivedSnapPoints.Anchor anchor in Derived)
                {
                    // An anchor landing on a snap point the piece already has would be the
                    // same position stored twice.
                    if (DerivedSnapPoints.IsDuplicate(anchor.Local, RealPoints))
                    {
                        continue;
                    }

                    Local.Add(anchor.Local);
                    Kinds.Add(anchor.Kind);
                }
            }

            Vector3[] world = (Local.Count == 0) ? NoPoints : new Vector3[Local.Count];
            for (int i = 0; i < Local.Count; i++)
            {
                world[i] = t.TransformPoint(Local[i]);
            }

            entry = new Entry
            {
                World = world,
                Kinds = (Kinds.Count == 0) ? NoKinds : Kinds.ToArray(),
                Position = t.position,
                Rotation = t.rotation,
                Mode = mode
            };

            Cache[id] = entry;
            return entry;
        }

        internal static void Clear()
        {
            Cache.Clear();
        }

        internal static int Count => Cache.Count;
    }
}
