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

        /// <summary>
        /// LOD groups held at full detail while a piece is bent.
        /// </summary>
        /// <remarks>
        /// A low-detail stand-in is a box: twenty-four vertices, two slices along its length,
        /// nothing in between. A deformer can only move vertices that exist, so bending one
        /// lifts its eight corners onto the arc and leaves the edges running dead straight
        /// between them. Drawn over a curved high-detail mesh, those straight edges are exactly
        /// the two rails that would not bend on the wood wall, and the frame the thatch appeared
        /// to sit on without curving. Neither was ever a separate part; both were the silhouette
        /// of a box that could not follow.
        ///
        /// Holding the group at LOD0 costs a little distant performance on bent pieces and makes
        /// the artefact impossible. Subdividing the low meshes instead would keep the LODs, and
        /// is the better answer for genuinely low-poly high meshes - a log pole has six slices
        /// and bends visibly faceted - but it is a great deal more work for a mesh nobody looks
        /// at closely.
        /// </remarks>
        private static readonly List<LODGroup> Held = new List<LODGroup>();

        /// <summary>
        /// Renderers switched off for the duration of a bend because their mesh cannot curve.
        /// </summary>
        /// <remarks>
        /// Holding LOD groups at full detail was aimed at the right mesh and missed. A piece's
        /// coarse stand-in is not always inside an LODGroup - Valheim has its own ways of
        /// switching one in - so forcing the group proved nothing and the box kept drawing. The
        /// straight bars across a bent wall are its eight corners lifted onto the arc with flat
        /// faces still spanning between them.
        ///
        /// Counting slices settles it without having to know which system owns the mesh. Two
        /// slices means two rings of vertices and nothing in between, and no arithmetic makes
        /// that curve. Such a mesh is hidden while the piece is bent and comes back the moment
        /// it is straightened.
        /// </remarks>
        private static readonly List<Renderer> Flattened = new List<Renderer>();

        /// <summary>
        /// A snap point, with where it sat before the piece was bent.
        /// </summary>
        /// <remarks>
        /// A snap point is a bare transform, so bending the meshes leaves every one exactly
        /// where it was - a piece that looks like an arch and connects like a plank. The ends
        /// are the whole point of an arch: they are what the next piece attaches to, and until
        /// they travel round the curve with the geometry an arch is something to look at rather
        /// than to build with.
        ///
        /// Its facing travels too. A snap point carries a rotation and vanilla uses it to decide
        /// how the next piece sits, so one still pointing the way it did when the piece was
        /// straight would attach the next piece square to an end that is no longer square.
        ///
        /// Both are held in the piece's frame rather than the parent's, because a snap point is
        /// not always a direct child - some sit inside a sub-object with its own rotation, and
        /// the arc is defined in the piece's space, not theirs. The original local values are
        /// kept alongside purely to put things back.
        /// </remarks>
        private sealed class Anchor
        {
            public Transform Point;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 PositionInPiece;
            public Quaternion RotationInPiece;
        }

        private static readonly List<Anchor> Anchors = new List<Anchor>();

        private static GameObject _appliedTo;
        private static float _appliedAngle = float.NaN;
        private static int _appliedAxis = -1;
        private static int _appliedRise = -1;

        private static int _densifiedFor = -1;

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

            HoldDetail(piece);
            Densify(piece, axis);
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
            Anchors.Clear();
            _appliedTo = null;
            _appliedAngle = float.NaN;
            _appliedAxis = -1;
            _appliedRise = -1;
            _densifiedFor = -1;
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

            foreach (LODGroup group in Held)
            {
                if (group != null)
                {
                    group.ForceLOD(-1);
                }
            }

            Held.Clear();

            foreach (Renderer renderer in Flattened)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            Flattened.Clear();

            foreach (Anchor anchor in Anchors)
            {
                if (anchor.Point != null)
                {
                    anchor.Point.localPosition = anchor.LocalPosition;
                    anchor.Point.localRotation = anchor.LocalRotation;
                }
            }

        }

        /// <summary>Pins every LOD group on the piece to full detail.</summary>
        private static void HoldDetail(GameObject piece)
        {
            if (Held.Count > 0)
            {
                return;
            }

            foreach (LODGroup group in piece.GetComponentsInChildren<LODGroup>(true))
            {
                if (group == null)
                {
                    continue;
                }

                group.ForceLOD(0);
                Held.Add(group);
            }

            if (Held.Count > 0)
            {
                HammerOfOdenPlugin.Debug(
                    $"Bend held {Held.Count} LOD group(s) at full detail; their low meshes are "
                    + "boxes and cannot follow a curve.");
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

                    // It cannot be curved, so it must not be drawn: left alone it renders dead
                    // straight over the bent piece, which is the same artefact a mesh too coarse
                    // to curve produces and wants the same answer.
                    Renderer straight = filter.GetComponent<Renderer>();
                    if (straight != null && straight.enabled)
                    {
                        straight.enabled = false;
                        Flattened.Add(straight);
                    }

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

            Transform pieceRoot = piece.transform;
            Quaternion intoPiece = Quaternion.Inverse(pieceRoot.rotation);

            foreach (Transform child in piece.GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.CompareTag("snappoint"))
                {
                    continue;
                }

                Anchors.Add(new Anchor
                {
                    Point = child,
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
                    PositionInPiece = pieceRoot.InverseTransformPoint(child.position),
                    RotationInPiece = intoPiece * child.rotation
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

            // Every figure the curve is built from, each time it is rebuilt. Wild movement is
            // either the ghost being repositioned under us or the arc being computed from
            // different numbers frame to frame, and these two lines tell those apart: the
            // position says whether the piece moved, the length and radius say whether the
            // measurement did.
            HammerOfOdenPlugin.Debug(
                $"Bend {radians * Mathf.Rad2Deg:0.#}deg axis {axis} rise {rise} "
                + $"len {length:0.###} radius {radius:0.###} "
                + $"mid {(min + max) * 0.5f:0.###} "
                + $"ghost at {piece.transform.position.ToString("0.##")} "
                + $"meshes {Active.Count}");

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

                HideIfItCannotCurve(entry, root, axis);
            }

            // Not on the ghost. Vanilla positions a snapped ghost by lining its chosen snap
            // point up with one on the target, so moving those points moves the ghost - and the
            // ghost is what is being bent, so every notch of the wheel shifted the piece, which
            // moved the points again. That is the wild wandering: a loop, not a broken curve.
            //
            // The built piece still gets curved anchors, because there the geometry is settled
            // and nothing is aligning against it while it changes. The cost is that the preview
            // snaps as though the piece were straight, which is worth paying for a ghost that
            // holds still while you decide.
            if (ModConfig.BendCurvesGhostSnapPoints.Value)
            {
                BendAnchors(root, radius, axis, rise, midAlong, midRise);
            }

            // Not on the ghost, and this is why it wandered. A ghost's colliders are
            // measured by things that hold onto them - surface placement caches the array it
            // found and slides the piece until that geometry touches the surface - so replacing
            // them every notch of the wheel left it measuring against boxes that had been
            // destroyed and missing the ones that had not existed yet. The answer it got was
            // arbitrary, and the piece went wherever that answer pointed.
            //
            // The built piece is where collision has to be right anyway: it is what you walk on
            // and what the game tests the next piece against. A ghost only needs a shape to
            // clip-test, and the straight box it already has does that without being churned
            // sixty times a second.
        }

        /// <summary>
        /// Carries the snap points round the same arc, facing and all.
        /// </summary>
        /// <remarks>
        /// The third axis - neither the length nor the direction of the curve - is the axle the
        /// piece turns about, so that is what a snap point rotates around, by the arc's own
        /// angle at that point. Which way that turn goes depends on the handedness of the pair
        /// of axes in play, and getting it backwards leaves snap points facing into the piece
        /// instead of out of it; it is worked out from the axes rather than assumed.
        /// </remarks>
        private static void BendAnchors(
            Transform root, float radius, int axis, int rise, float midAlong, float midRise)
        {
            if (Anchors.Count == 0)
            {
                return;
            }

            int axle = 3 - axis - rise;

            Vector3 spin = Vector3.zero;
            spin[axle] = 1f;

            Vector3 alongDir = Vector3.zero;
            alongDir[axis] = 1f;

            Vector3 riseDir = Vector3.zero;
            riseDir[rise] = 1f;

            float handed = Vector3.Dot(Vector3.Cross(alongDir, riseDir), spin) >= 0f ? -1f : 1f;

            foreach (Anchor anchor in Anchors)
            {
                if (anchor.Point == null)
                {
                    continue;
                }

                Vector3 p = anchor.PositionInPiece;

                float along = p[axis] - midAlong;
                float offset = p[rise] - midRise;

                float angle = along / radius;
                float armLength = radius - offset;

                p[axis] = midAlong + armLength * Mathf.Sin(angle);
                p[rise] = midRise + radius - armLength * Mathf.Cos(angle);

                Quaternion turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * handed, spin);

                // Written in world space, so a snap point nested inside a rotated sub-object
                // lands where the piece's own frame says it should rather than where its
                // parent's frame would put it.
                anchor.Point.position = root.TransformPoint(p);
                anchor.Point.rotation = root.rotation * turn * anchor.RotationInPiece;
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
        /// <summary>
        /// Gives every mesh enough vertices along the bend to curve, once per axis.
        /// </summary>
        /// <remarks>
        /// Done here rather than at capture because the axis is not known until something is
        /// actually bent, and subdividing along the wrong one would be wasted work and wasted
        /// memory. Repeated for a different axis, which costs one rebuild on the rare occasion
        /// the direction is changed mid-piece.
        /// </remarks>
        private static void Densify(GameObject piece, int axis)
        {
            if (_densifiedFor == axis)
            {
                return;
            }

            _densifiedFor = axis;

            Transform root = piece.transform;
            float segment = Mathf.Max(0.02f, ModConfig.BendSegment.Value);
            int rebuilt = 0;

            foreach (Deformed entry in Active)
            {
                if (entry.Filter == null || entry.Original == null)
                {
                    continue;
                }

                Matrix4x4 toPiece = root.worldToLocalMatrix * entry.Filter.transform.localToWorldMatrix;

                Mesh dense = MeshSubdivider.Subdivide(entry.Original, toPiece, axis, segment);
                if (dense == null)
                {
                    continue;
                }

                if (entry.Working != null)
                {
                    Object.Destroy(entry.Working);
                }

                entry.Working = dense;
                entry.Source = dense.vertices;
                rebuilt++;
            }

            if (rebuilt > 0)
            {
                HammerOfOdenPlugin.Debug(
                    $"Bend subdivided {rebuilt} mesh(es) to {segment:0.##}m segments along the bend.");
            }
        }

        private static void HideIfItCannotCurve(Deformed entry, Transform root, int axis)
        {
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

            bool canCurve = slices.Count > Mathf.Max(2, ModConfig.BendMinimumSlices.Value);

            if (!canCurve && renderer != null && renderer.enabled)
            {
                renderer.enabled = false;
                Flattened.Add(renderer);
            }

            HammerOfOdenPlugin.Debug(
                $"    {entry.Filter.name}: {entry.Source.Length} verts in {slices.Count} slice(s)"
                + (canCurve ? string.Empty : " - too few to curve, hidden while bent"));
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
