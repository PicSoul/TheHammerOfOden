using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Renames the piece's own snap points after where they actually are.
    /// </summary>
    /// <remarks>
    /// Valheim names snap points however the asset author chose, which is usually a bare
    /// ordinal: "Top 1", "Bottom 3". The number carries no meaning - it does not say which
    /// side of the piece the point is on, and the same name is reused for points in quite
    /// different places, which is where this whole line of work started.
    ///
    /// The position is known, so the name can describe it. A point is classified against the
    /// box its piece's snap points span: clearly high is "Top", clearly forward is "Front",
    /// near the middle of an axis simply says nothing about that axis. The result reads the
    /// same way as the derived anchors, so one convention covers everything you cycle past.
    ///
    /// Only the ghost is touched. It is a temporary object rebuilt whenever the selected
    /// piece changes, so nothing in the world is renamed and nothing persists.
    /// </remarks>
    internal static class SnapPointNaming
    {
        /// <summary>
        /// How far along an axis a point must sit before that axis is worth naming, as a
        /// fraction of the half-extent.
        /// </summary>
        /// <remarks>
        /// Low enough that a point plainly on one side is named, high enough that one sitting
        /// near the middle is not called "Left" for being a centimetre off centre.
        /// </remarks>
        private const float Significant = 0.25f;

        private static readonly List<Transform> Points = new List<Transform>();
        private static readonly Dictionary<string, int> Used = new Dictionary<string, int>();

        internal static void Apply(GameObject ghost)
        {
            if (ghost == null || !ModConfig.RenameSnapPoints.Value)
            {
                return;
            }

            Transform root = ghost.transform;

            Points.Clear();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);

                // Anchors we added are already named to this convention.
                if (child.CompareTag("snappoint") && child.GetComponent<DerivedAnchorMarker>() == null)
                {
                    Points.Add(child);
                }
            }

            if (Points.Count == 0)
            {
                return;
            }

            if (!TryMeasure(out Vector3 centre, out Vector3 extents))
            {
                return;
            }

            Used.Clear();

            foreach (Transform point in Points)
            {
                string name = Classify(point.localPosition - centre, extents);

                // Two points can legitimately land in the same region - the ends of a shared
                // edge, say - so number them rather than leaving them indistinguishable.
                if (Used.TryGetValue(name, out int seen))
                {
                    Used[name] = seen + 1;
                    name = $"{name} {seen + 1}";
                }
                else
                {
                    Used[name] = 1;
                }

                point.name = name;
            }
        }

        /// <summary>The box the piece's own snap points span, in local space.</summary>
        private static bool TryMeasure(out Vector3 centre, out Vector3 extents)
        {
            Vector3 min = Points[0].localPosition;
            Vector3 max = min;

            foreach (Transform point in Points)
            {
                min = Vector3.Min(min, point.localPosition);
                max = Vector3.Max(max, point.localPosition);
            }

            centre = (min + max) * 0.5f;
            extents = (max - min) * 0.5f;

            // A single point, or several stacked in one spot, gives nothing to classify against.
            return extents.sqrMagnitude > 0.0001f;
        }

        private static string Classify(Vector3 offset, Vector3 extents)
        {
            List<string> parts = new List<string>(3);

            Describe(parts, offset.y, extents.y, "Top", "Bottom");
            Describe(parts, offset.z, extents.z, "Front", "Back");
            Describe(parts, offset.x, extents.x, "Right", "Left");

            return (parts.Count == 0) ? "Centre" : string.Join(" ", parts.ToArray());
        }

        private static void Describe(List<string> parts, float value, float extent, string positive, string negative)
        {
            // An axis the piece has no depth on says nothing useful about position.
            if (extent < 0.0001f)
            {
                return;
            }

            float ratio = value / extent;

            if (ratio > Significant)
            {
                parts.Add(positive);
            }
            else if (ratio < -Significant)
            {
                parts.Add(negative);
            }
        }
    }
}
