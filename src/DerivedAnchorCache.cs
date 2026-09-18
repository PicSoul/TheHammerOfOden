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
    /// through Piece.GetSnapPoints, on purpose: we patch that method, so calling it here
    /// would put a Harmony wrapper and a placement-ghost test in the middle of the hot loop
    /// for every nearby piece, every frame. Reading the children directly is the same five
    /// lines vanilla uses, without the detour through our own patch.
    ///
    /// Entries are validated against the piece's transform so a piece that does move is
    /// still handled, and the whole cache is dropped when build mode ends.
    /// </remarks>
    internal static class DerivedAnchorCache
    {
        private sealed class Entry
        {
            internal Vector3[] World;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal DerivedSnapMode Mode;
        }

        private static readonly Dictionary<int, Entry> Cache = new Dictionary<int, Entry>();
        private static readonly List<Vector3> Scratch = new List<Vector3>();
        private static readonly Vector3[] Empty = new Vector3[0];

        /// <summary>Every anchor on the piece: its own snap points plus the derived ones.</summary>
        internal static Vector3[] Get(Piece piece, DerivedSnapMode mode)
        {
            if (piece == null)
            {
                return Empty;
            }

            Transform t = piece.transform;
            int id = piece.GetInstanceID();

            if (Cache.TryGetValue(id, out Entry entry)
                && entry.Mode == mode
                && entry.Position == t.position
                && entry.Rotation == t.rotation)
            {
                return entry.World;
            }

            Scratch.Clear();

            // The piece's own snap points, read without going through our patched method.
            for (int i = 0; i < t.childCount; i++)
            {
                Transform child = t.GetChild(i);
                if (child.CompareTag("snappoint"))
                {
                    Scratch.Add(child.localPosition);
                }
            }

            if (mode != DerivedSnapMode.Off
                && DerivedSnapPoints.TryMeasure(piece, out Vector3 center, out Vector3 extents))
            {
                DerivedSnapPoints.BuildLocalAnchors(center, extents, mode, Scratch);
            }

            Vector3[] world = (Scratch.Count == 0) ? Empty : new Vector3[Scratch.Count];
            for (int i = 0; i < Scratch.Count; i++)
            {
                world[i] = t.TransformPoint(Scratch[i]);
            }

            Cache[id] = new Entry
            {
                World = world,
                Position = t.position,
                Rotation = t.rotation,
                Mode = mode
            };

            return world;
        }

        internal static void Clear()
        {
            Cache.Clear();
        }

        internal static int Count => Cache.Count;
    }
}
