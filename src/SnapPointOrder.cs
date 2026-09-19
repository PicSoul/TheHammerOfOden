using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Puts the placement ghost's snap points into a predictable cycling order.
    /// </summary>
    /// <remarks>
    /// Vanilla collects snap points by walking the prefab's children, so the order you meet
    /// them with Q and E is whatever order the asset author happened to create them in -
    /// "Bottom 1, Bottom 2, Top 3, Top 1, Bottom 3" and so on. Nothing groups them.
    ///
    /// Reordering the children themselves fixes it at the source: vanilla reads child order,
    /// so sorting them once when the ghost is built costs nothing per frame and needs no
    /// patch on GetSnapPoints, which is called on every nearby piece every frame and which
    /// this mod deliberately stays out of.
    ///
    /// The piece's own points come first and keep their familiar names, sorted naturally so
    /// "Bottom 10" follows "Bottom 9" rather than "Bottom 1". Derived anchors follow, grouped
    /// from coarsest to finest: the centre, then faces, then corners, then edges - which is
    /// roughly the order of how often they are wanted.
    /// </remarks>
    internal static class SnapPointOrder
    {
        private static readonly List<Transform> Points = new List<Transform>();

        internal static void Apply(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || ghost == null || !ModConfig.SortSnapPoints.Value)
            {
                return;
            }

            Transform root = ghost.transform;

            Points.Clear();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.CompareTag("snappoint"))
                {
                    Points.Add(child);
                }
            }

            if (Points.Count < 2)
            {
                return;
            }

            Points.Sort(Compare);

            // Moving each to the end in order leaves them contiguous and correctly sequenced,
            // without disturbing the relative order of anything else on the piece.
            foreach (Transform point in Points)
            {
                point.SetAsLastSibling();
            }

            HammerOfOdenPlugin.Debug($"Ordered {Points.Count} snap point(s) for '{ghost.name}'.");
        }

        private static int Compare(Transform a, Transform b)
        {
            int byGroup = GroupOf(a).CompareTo(GroupOf(b));
            return (byGroup != 0) ? byGroup : NaturalCompare(a.name, b.name);
        }

        /// <summary>Vanilla points first, then derived anchors coarsest to finest.</summary>
        private static int GroupOf(Transform point)
        {
            DerivedAnchorMarker marker = point.GetComponent<DerivedAnchorMarker>();
            if (marker == null)
            {
                return 0;
            }

            switch (marker.Kind)
            {
                case AnchorKind.PieceCentre:
                    return 1;
                case AnchorKind.FaceCentre:
                    return 2;
                case AnchorKind.Corner:
                    return 3;
                case AnchorKind.EdgeMidpoint:
                    return 4;
                case AnchorKind.EdgeQuarter:
                    return 5;
                default:
                    return 6;
            }
        }

        /// <summary>
        /// Compare names so embedded numbers sort by value.
        /// </summary>
        /// <remarks>
        /// Plain string ordering puts "Bottom 10" between "Bottom 1" and "Bottom 2", which is
        /// precisely the sort of jumble this exists to remove.
        /// </remarks>
        private static int NaturalCompare(string a, string b)
        {
            int i = 0;
            int j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int na = ReadNumber(a, ref i);
                    int nb = ReadNumber(b, ref j);

                    if (na != nb)
                    {
                        return na.CompareTo(nb);
                    }

                    continue;
                }

                int byChar = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (byChar != 0)
                {
                    return byChar;
                }

                i++;
                j++;
            }

            return (a.Length - i).CompareTo(b.Length - j);
        }

        private static int ReadNumber(string text, ref int index)
        {
            StringBuilder digits = new StringBuilder();

            while (index < text.Length && char.IsDigit(text[index]))
            {
                digits.Append(text[index]);
                index++;
            }

            return int.TryParse(digits.ToString(), out int value) ? value : 0;
        }
    }
}
