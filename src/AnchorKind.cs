using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>What sort of point an anchor is, so it can be drawn recognisably.</summary>
    internal enum AnchorKind
    {
        /// <summary>A snap point the piece shipped with.</summary>
        Vanilla = 0,

        /// <summary>The very centre of the piece.</summary>
        PieceCentre = 1,

        /// <summary>The centre of one face.</summary>
        FaceCentre = 2,

        /// <summary>A corner of the bounding box.</summary>
        Corner = 3,

        /// <summary>The midpoint of an edge.</summary>
        EdgeMidpoint = 4,

        /// <summary>A quarter point along an edge.</summary>
        EdgeQuarter = 5
    }

    /// <summary>
    /// Gives each kind of anchor its own outline.
    /// </summary>
    /// <remarks>
    /// The vocabulary follows AutoCAD's object snap markers, which have had a long time to
    /// settle: endpoint is a square, midpoint a triangle, centre a circle with a cross
    /// through it, quadrant a diamond. Reusing conventions people may already know beats
    /// inventing a private language.
    ///
    /// The rule that matters is that shapes differ by silhouette and vertex count, never by
    /// rotation alone. A square and a diamond are the same four-sided outline turned 45
    /// degrees, and that difference collapses at small scale or an oblique angle - which is
    /// why the first attempt at this was hard to read.
    ///
    /// Outlines are only rewritten when a renderer's kind changes. Writing the positions
    /// array rebuilds the mesh, so reassigning the same shape every frame would undo the
    /// work done to stop doing exactly that with colour.
    /// </remarks>
    internal static class MarkerShapes
    {
        private static readonly Dictionary<int, AnchorKind> Applied = new Dictionary<int, AnchorKind>();
        private static readonly List<Vector3> Path = new List<Vector3>();

        internal static void Apply(LineRenderer line, AnchorKind kind)
        {
            int id = line.GetInstanceID();

            if (Applied.TryGetValue(id, out AnchorKind previous) && previous == kind)
            {
                return;
            }

            Path.Clear();
            bool loop = BuildPath(kind, Path);

            line.loop = loop;
            line.positionCount = Path.Count;

            for (int i = 0; i < Path.Count; i++)
            {
                line.SetPosition(i, Path[i]);
            }

            Applied[id] = kind;
        }

        internal static void Clear()
        {
            Applied.Clear();
        }

        /// <summary>Relative size, so shapes of different vertex counts read as equal weight.</summary>
        internal static float SizeFor(AnchorKind kind)
        {
            switch (kind)
            {
                case AnchorKind.PieceCentre:
                    return 1.5f;
                case AnchorKind.FaceCentre:
                    return 1.1f;
                case AnchorKind.Corner:
                    return 1.05f;
                case AnchorKind.EdgeQuarter:
                    return 0.8f;   // subordinate to the midpoint on the same edge
                default:
                    return 1f;
            }
        }

        /// <summary>Fills the outline for a kind. Returns whether the path should close.</summary>
        private static bool BuildPath(AnchorKind kind, List<Vector3> into)
        {
            switch (kind)
            {
                case AnchorKind.PieceCentre:
                    // Circle with a cross through it: AutoCAD's centre marker, and the only
                    // anchor that is genuinely inside the piece rather than on its surface.
                    AddPolygon(into, 20, 0f);
                    into.Add(Point(20, 0, 0f));      // back to the start of the circle
                    into.Add(Vector3.zero);
                    into.Add(Point(4, 2, 0f));       // across to the far side
                    into.Add(Vector3.zero);
                    into.Add(Point(4, 1, 0f));       // up
                    into.Add(Vector3.zero);
                    into.Add(Point(4, 3, 0f));       // down
                    return false;

                case AnchorKind.FaceCentre:
                    // Hexagon: a face is a region, and six sides read as "area" without being
                    // mistakable for the square used for corners.
                    AddPolygon(into, 6, Mathf.PI / 6f);
                    return true;

                case AnchorKind.Corner:
                    // Square, as AutoCAD marks an endpoint. A corner is where edges end.
                    AddPolygon(into, 4, Mathf.PI / 4f);
                    return true;

                case AnchorKind.EdgeMidpoint:
                    // Triangle, as AutoCAD marks a midpoint.
                    AddPolygon(into, 3, Mathf.PI / 2f);
                    return true;

                case AnchorKind.EdgeQuarter:
                    // Diamond, as AutoCAD marks a quadrant - a fractional position along
                    // something. Drawn smaller, since it subdivides the midpoint's edge.
                    AddPolygon(into, 4, 0f);
                    return true;

                default:
                    // Plain circle, keeping the piece's own snap points looking as they did.
                    AddPolygon(into, 16, 0f);
                    return true;
            }
        }

        private static void AddPolygon(List<Vector3> into, int sides, float turn)
        {
            for (int i = 0; i < sides; i++)
            {
                into.Add(Point(sides, i, turn));
            }
        }

        private static Vector3 Point(int sides, int index, float turn)
        {
            float angle = (index / (float)sides) * Mathf.PI * 2f + turn;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }
    }
}
