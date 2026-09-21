using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Curves a piece's meshes along a circular arc.
    /// </summary>
    /// <remarks>
    /// The shape is a true arc, not two halves hinged at the middle. Hinging is what produces
    /// the pointed, folded look; here every vertex moves and the curvature is the same all the
    /// way along, so a straight beam becomes an arch rather than a tent.
    ///
    /// For a piece of length L bent through angle t, the arc has radius R = L/t, and a vertex at
    /// distance x along the piece sits at angle f = x/R around that arc:
    ///
    ///     x' = (R - d) * sin(f)
    ///     y' = R - (R - d) * cos(f)
    ///
    /// where d is how far the vertex sits from the neutral axis, in the direction the piece is
    /// bending. Carrying d through is what keeps a beam the same thickness around the curve
    /// instead of pinching it flat at the crown.
    ///
    /// Angle is the control rather than height on purpose. The rise of an arc is not free to
    /// choose: it peaks at about 0.362 of the length, at roughly 267 degrees, by which point the
    /// piece has curled back through itself. A half circle - 180 degrees, ends pointing straight
    /// up, rising 0.318 of its length - is the most that is still a building piece, so that is
    /// the ceiling. Driving it by angle also means every value in range is a usable shape, where
    /// driving it by height would stop responding near the top.
    ///
    /// Vertices are read in the piece's own space, not each mesh's. A piece is a tree of child
    /// transforms - planks, beams, snow overlays, worn variants, LOD levels - each with its own
    /// rotation and offset, and bending them in their own spaces would curve each part around a
    /// different centre. Everything is brought into the piece's space, bent there, and put back.
    /// </remarks>
    internal static class BendDeformer
    {
        /// <summary>
        /// Everything needed to put one mesh back the way it was, and to bend it again from
        /// scratch rather than bending an already-bent copy.
        /// </summary>
        private sealed class Deformed
        {
            public MeshFilter Filter;
            public Mesh Original;
            public Mesh Working;
            public Vector3[] Source;
        }

        private static readonly List<Deformed> Active = new List<Deformed>();
        private static GameObject _appliedTo;
        private static float _appliedAngle = float.NaN;
        private static int _appliedAxis = -1;

        /// <summary>
        /// Bends a piece, or restores it when the angle is zero.
        /// </summary>
        /// <param name="piece">The object to curve - a placement ghost, or a placed piece.</param>
        /// <param name="degrees">Total turn from one end to the other.</param>
        /// <param name="axis">Which of the piece's own axes runs along its length: 0 x, 1 y, 2 z.</param>
        internal static void Apply(GameObject piece, float degrees, int axis)
        {
            if (piece == null)
            {
                Release();
                return;
            }

            if (_appliedTo != piece)
            {
                // A different object: let the old one go before taking hold of this one.
                Release();
                Capture(piece);
                _appliedTo = piece;
                _appliedAngle = float.NaN;
            }

            if (Mathf.Approximately(_appliedAngle, degrees) && _appliedAxis == axis)
            {
                return;
            }

            _appliedAngle = degrees;
            _appliedAxis = axis;

            if (Mathf.Abs(degrees) < 0.01f)
            {
                Restore();
                return;
            }

            Bend(piece, degrees * Mathf.Deg2Rad, axis);
        }

        /// <summary>Puts every mesh back and forgets the piece.</summary>
        internal static void Release()
        {
            Restore();

            foreach (Deformed entry in Active)
            {
                if (entry.Working != null)
                {
                    Object.Destroy(entry.Working);
                }
            }

            Active.Clear();
            _appliedTo = null;
            _appliedAngle = float.NaN;
            _appliedAxis = -1;
        }

        private static void Restore()
        {
            foreach (Deformed entry in Active)
            {
                if (entry.Filter != null)
                {
                    entry.Filter.sharedMesh = entry.Original;
                }
            }
        }

        /// <summary>
        /// Takes a private copy of every mesh on the piece.
        /// </summary>
        /// <remarks>
        /// A copy, always. The mesh on a MeshFilter is the shared asset: bending it in place
        /// would curve every wall of that kind in the world, including ones already built, and
        /// would persist until the game was restarted.
        /// </remarks>
        private static void Capture(GameObject piece)
        {
            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null || !source.isReadable)
                {
                    continue;
                }

                Mesh working = Object.Instantiate(source);
                working.name = source.name + "_bent";

                Active.Add(new Deformed
                {
                    Filter = filter,
                    Original = source,
                    Working = working,
                    Source = source.vertices
                });
            }
        }

        private static void Bend(GameObject piece, float radians, int axis)
        {
            Transform root = piece.transform;

            if (!Extent(root, axis, out float min, out float max) || max - min < 0.001f)
            {
                return;
            }

            float length = max - min;
            float radius = length / radians;

            // The piece bends towards one of its other two axes. Local up is the natural choice
            // for a beam or a wall lying along x or z; a piece measured along its own up has to
            // fall back to z, since it cannot bend towards the way it already runs.
            int rise = axis == 1 ? 2 : 1;

            foreach (Deformed entry in Active)
            {
                if (entry.Filter == null || entry.Working == null)
                {
                    continue;
                }

                Transform local = entry.Filter.transform;
                Vector3[] vertices = new Vector3[entry.Source.Length];

                for (int i = 0; i < entry.Source.Length; i++)
                {
                    // Into the piece's space, where the arc is defined.
                    Vector3 p = root.InverseTransformPoint(local.TransformPoint(entry.Source[i]));

                    float along = p[axis] - (min + max) * 0.5f;
                    float offset = p[rise];

                    float angle = along / radius;
                    float armLength = radius - offset;

                    p[axis] = armLength * Mathf.Sin(angle);
                    p[rise] = radius - armLength * Mathf.Cos(angle);

                    vertices[i] = local.InverseTransformPoint(root.TransformPoint(p));
                }

                entry.Working.vertices = vertices;
                entry.Working.RecalculateNormals();
                entry.Working.RecalculateBounds();

                entry.Filter.sharedMesh = entry.Working;
            }
        }

        /// <summary>
        /// How far the piece runs along the chosen axis, measured in the piece's own space.
        /// </summary>
        /// <remarks>
        /// Taken across every mesh at once rather than per mesh. The arc has to be shared: one
        /// radius for the whole piece, or its planks and beams would each curve around their own
        /// centre and come apart.
        /// </remarks>
        private static bool Extent(Transform root, int axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            bool any = false;

            foreach (Deformed entry in Active)
            {
                if (entry.Filter == null)
                {
                    continue;
                }

                Transform local = entry.Filter.transform;

                foreach (Vector3 vertex in entry.Source)
                {
                    float v = root.InverseTransformPoint(local.TransformPoint(vertex))[axis];
                    if (v < min) { min = v; }
                    if (v > max) { max = v; }
                    any = true;
                }
            }

            return any;
        }
    }
}
