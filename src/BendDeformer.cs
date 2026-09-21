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
        private static int _appliedRise = -1;

        private static Vector3 _min;
        private static Vector3 _max;
        private static bool _measured;

        /// <summary>The piece's own axis that it runs longest along: 0 x, 1 y, 2 z.</summary>
        internal static int LongestAxis(GameObject piece)
        {
            Prepare(piece);

            if (!_measured)
            {
                return 0;
            }

            Vector3 size = _max - _min;
            if (size.x >= size.y && size.x >= size.z) { return 0; }
            return size.y >= size.z ? 1 : 2;
        }

        /// <summary>
        /// How far this piece may bend on these axes before it folds through itself.
        /// </summary>
        /// <remarks>
        /// A vertex sitting at distance d from the neutral axis lands at radius R - d. Once R
        /// drops below the furthest d that radius goes negative, and the inside of the curve
        /// turns through itself - a wall crossing its own planks rather than curving.
        /// Since R is length divided by angle, the angle where that happens is length over d.
        ///
        /// This is exactly why bending a wall towards its own height went wrong at around a
        /// hundred degrees: measured that way the furthest vertex is a metre from the middle,
        /// which puts the ceiling at two radians. Bent towards its thickness instead, d is a
        /// tenth of that and the ceiling is far past anywhere useful.
        /// </remarks>
        internal static float MaximumDegrees(GameObject piece, int axis, int rise)
        {
            Prepare(piece);

            if (!_measured)
            {
                return 180f;
            }

            Vector3 size = _max - _min;
            float length = size[axis];
            float reach = size[rise] * 0.5f;

            if (length <= 0.001f || reach <= 0.001f)
            {
                return 0f;
            }

            return Mathf.Min(180f, length / reach * Mathf.Rad2Deg);
        }

        internal static Vector3 Size(GameObject piece)
        {
            Prepare(piece);
            return _measured ? _max - _min : Vector3.one;
        }

        private static void Prepare(GameObject piece)
        {
            if (piece == null || _appliedTo == piece)
            {
                return;
            }

            Release();
            Capture(piece);
            _appliedTo = piece;
            _appliedAngle = float.NaN;
            Measure(piece.transform);
        }

        /// <summary>
        /// Bends a piece, or restores it when the angle is zero.
        /// </summary>
        /// <param name="piece">The object to curve - a placement ghost, or a placed piece.</param>
        /// <param name="degrees">Total turn from one end to the other.</param>
        /// <param name="axis">Which of the piece's own axes runs along its length: 0 x, 1 y, 2 z.</param>
        internal static void Apply(GameObject piece, float degrees, int axis, int rise)
        {
            if (piece == null)
            {
                Release();
                return;
            }

            Prepare(piece);

            if (Mathf.Approximately(_appliedAngle, degrees)
                && _appliedAxis == axis
                && _appliedRise == rise)
            {
                return;
            }

            _appliedAngle = degrees;
            _appliedAxis = axis;
            _appliedRise = rise;

            if (Mathf.Abs(degrees) < 0.01f)
            {
                Restore();
                return;
            }

            Bend(piece, degrees * Mathf.Deg2Rad, axis, rise);
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
            _appliedRise = -1;
            _measured = false;
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
            int total = 0;
            int unreadable = 0;
            int batched = 0;
            int hidden = 0;

            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null)
                {
                    continue;
                }

                total++;

                if (!source.isReadable)
                {
                    unreadable++;
                    continue;
                }

                // Counted, not skipped. A static-batched renderer draws from a combined buffer
                // baked at build time, so replacing its filter's mesh changes the mesh and not
                // the picture - which looks exactly like the curve being wrong, and is the most
                // likely reason a wood wall bends its planks and leaves its rails straight.
                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.isPartOfStaticBatch)
                {
                    batched++;
                }

                if (!filter.gameObject.activeInHierarchy || renderer == null || !renderer.enabled)
                {
                    hidden++;
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

            // Said every time a piece is taken up, because the interesting cases are the ones
            // that look like a broken curve from the outside and are not one.
            HammerOfOdenPlugin.Debug(
                $"Bend captured {Active.Count} of {total} mesh(es) on '{Utils.GetPrefabName(piece)}'"
                + (unreadable > 0 ? $"; {unreadable} unreadable" : string.Empty)
                + (batched > 0 ? $"; {batched} STATIC-BATCHED and will not visibly change" : string.Empty)
                + (hidden > 0 ? $"; {hidden} not currently drawn" : string.Empty)
                + ".");
        }

        private static void Bend(GameObject piece, float radians, int axis, int rise)
        {
            Transform root = piece.transform;

            float min = _min[axis];
            float max = _max[axis];

            if (!_measured || max - min < 0.001f)
            {
                return;
            }

            float length = max - min;
            float radius = length / radians;

            // Both the line the piece is measured along and the one it curves towards run
            // through its middle, not through wherever the mesh happens to have its origin.
            float midAlong = (min + max) * 0.5f;
            float midRise = (_min[rise] + _max[rise]) * 0.5f;

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

                    float along = p[axis] - midAlong;
                    float offset = p[rise] - midRise;

                    float angle = along / radius;
                    float armLength = radius - offset;

                    p[axis] = midAlong + armLength * Mathf.Sin(angle);
                    p[rise] = midRise + radius - armLength * Mathf.Cos(angle);

                    vertices[i] = local.InverseTransformPoint(root.TransformPoint(p));
                }

                entry.Working.vertices = vertices;
                entry.Working.RecalculateNormals();
                entry.Working.RecalculateBounds();

                entry.Filter.sharedMesh = entry.Working;

                ReportDetail(entry, root, axis, length);
            }
        }

        /// <summary>
        /// How many places along the bend axis a mesh actually has vertices.
        /// </summary>
        /// <remarks>
        /// A deformer can only move vertices that exist. A plain box has its eight corners and
        /// nothing in between, so bending it lifts the corners onto the arc and leaves the edges
        /// running dead straight between them - which does not read as a gentle curve, it reads
        /// as not having bent at all. A mesh with loops along its length curves; a mesh with two
        /// slices cannot, however correct the arithmetic.
        ///
        /// That is the difference this measures, and it is the only remaining explanation that
        /// fits a wall whose planks curve while its rails stay straight: same maths, same piece,
        /// different subdivision.
        /// </remarks>
        private static void ReportDetail(Deformed entry, Transform root, int axis, float length)
        {
            if (!ModConfig.DebugEnabled)
            {
                return;
            }

            Transform local = entry.Filter.transform;

            // Bucketed at a centimetre: exact float equality would count a slice twice for
            // rounding alone, and a centimetre is far finer than any curve needs.
            HashSet<int> slices = new HashSet<int>();
            foreach (Vector3 vertex in entry.Source)
            {
                float v = root.InverseTransformPoint(local.TransformPoint(vertex))[axis];
                slices.Add(Mathf.RoundToInt(v * 100f));
            }

            Renderer renderer = entry.Filter.GetComponent<Renderer>();
            bool drawn = entry.Filter.gameObject.activeInHierarchy
                && renderer != null && renderer.enabled;

            HammerOfOdenPlugin.Debug(
                $"    {(drawn ? "DRAWN " : "hidden")} {entry.Filter.name}: "
                + $"{entry.Source.Length} verts in {slices.Count} slice(s) along the bend"
                + (slices.Count <= 2 ? "  <- too few to curve" : string.Empty));
        }

        /// <summary>
        /// The piece's size on all three of its own axes, measured once.
        /// </summary>
        /// <remarks>
        /// Taken across every mesh at once rather than per mesh. The arc has to be shared: one
        /// extent, one radius, one curve for the whole piece, or its planks and beams would each
        /// bend around their own centre and pull apart.
        ///
        /// All three axes, because which one is the length is a question about the piece and not
        /// something a caller can be trusted to know. A log pole runs along its own y and is a
        /// fifth of a metre across x; bending it on x wrapped it round a radius of a tenth of a
        /// metre, which is precisely the knot it turned into.
        /// </remarks>
        private static void Measure(Transform root)
        {
            _min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            _measured = false;

            foreach (Deformed entry in Active)
            {
                if (entry.Filter == null)
                {
                    continue;
                }

                Transform local = entry.Filter.transform;

                foreach (Vector3 vertex in entry.Source)
                {
                    Vector3 p = root.InverseTransformPoint(local.TransformPoint(vertex));
                    _min = Vector3.Min(_min, p);
                    _max = Vector3.Max(_max, p);
                    _measured = true;
                }
            }
        }
    }
}
