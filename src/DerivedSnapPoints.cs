using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum DerivedSnapMode
    {
        /// <summary>Only the snap points the piece ships with.</summary>
        Off = 0,

        /// <summary>Piece centre and the centre of each face.</summary>
        Centers = 1,

        /// <summary>Centres plus the eight corners.</summary>
        CentersAndCorners = 2,

        /// <summary>Also the midpoint of each of the twelve edges between corners.</summary>
        CentersCornersAndEdges = 3,

        /// <summary>Everything above, plus a midpoint between the centre and each of them.</summary>
        Full = 4
    }

    /// <summary>
    /// Adds centre, face, corner and half-way anchors to the piece being placed.
    /// </summary>
    /// <remarks>
    /// Appended to the placement ghost only, never to pieces in the world. Hooking Piece
    /// means any piece works, including ones added by other mods, with no per-mod support.
    /// Restricting it to the ghost means we never mutate the world: no accumulating child
    /// objects on pooled pieces, and nothing for another mod's snap logic to trip over.
    ///
    /// The anchors are real children of the ghost, tagged "snappoint" so vanilla's own
    /// GetSnapPoints collects them. An earlier version appended them from a Harmony postfix
    /// on that method instead, which was a mistake: vanilla calls GetSnapPoints on every
    /// piece within 10m every frame while a ghost exists, so the patch put a detour in one
    /// of the game's hottest loops purely to do nothing for all but one piece. Tagging the
    /// children needs no patch at all.
    ///
    /// Our own anchors carry a marker component so measuring can skip them. A name prefix
    /// was tried first and was a poor choice: vanilla puts the snap point's name on screen
    /// when cycling, so the marker leaked into the interface. A component identifies them
    /// without being visible, and survives the frame between Destroy being called and Unity
    /// actually removing the object - which matters, because a ghost is remeasured while its
    /// previous anchors are still attached.
    ///
    /// The box is measured from the piece's own snap points where it has them, because
    /// those describe its logical footprint. Renderer bounds include decorative overhang -
    /// a roof's carved ridge, a torch's flame - which would put "corners" in mid-air.
    /// </remarks>
    internal static class DerivedSnapPoints
    {
        /// <summary>
        /// How close a derived anchor may sit to an existing snap point before it is dropped.
        /// </summary>
        /// <remarks>
        /// The box is measured from the extremes of the piece's own snap points, so on a
        /// piece whose snap points are already at its corners - most floors and walls - every
        /// derived corner lands exactly on one. Keeping both would put two entries at the
        /// same position in the Q/E cycle, draw two markers over each other, and compare the
        /// same point twice when snapping.
        /// </remarks>
        internal const float DuplicateDistance = 0.01f;

        /// <summary>True when an anchor is close enough to an existing point to be redundant.</summary>
        internal static bool IsDuplicate(Vector3 candidate, List<Vector3> existing)
        {
            float limit = DuplicateDistance * DuplicateDistance;

            foreach (Vector3 point in existing)
            {
                if ((point - candidate).sqrMagnitude < limit)
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject _cachedFor;
        private static DerivedSnapMode _cachedMode;
        private static readonly List<Transform> Cached = new List<Transform>();

        /// <summary>Attach derived anchors to a freshly built placement ghost.</summary>
        internal static void AttachTo(GameObject ghost)
        {
            Invalidate();

            if (!ModConfig.IsEnabled || ModConfig.DerivedSnaps.Value == DerivedSnapMode.Off || ghost == null)
            {
                return;
            }

            Piece piece = ghost.GetComponent<Piece>();
            if (piece == null)
            {
                return;
            }

            Rebuild(piece, ghost);
        }

        internal static void Invalidate()
        {
            _cachedFor = null;

            foreach (Transform anchor in Cached)
            {
                if (anchor != null)
                {
                    Object.Destroy(anchor.gameObject);
                }
            }

            Cached.Clear();
        }

        private static void Rebuild(Piece piece, GameObject ghost)
        {
            DerivedSnapMode mode = ModConfig.DerivedSnaps.Value;

            if (_cachedFor == ghost && _cachedMode == mode && Cached.Count > 0)
            {
                return;
            }

            Invalidate();

            if (!TryMeasure(piece, out Vector3 center, out Vector3 extents))
            {
                return;
            }

            // The piece's own snap points, so derived anchors that land on one can be dropped.
            List<Vector3> existing = new List<Vector3>();
            for (int i = 0; i < ghost.transform.childCount; i++)
            {
                Transform child = ghost.transform.GetChild(i);
                if (child.CompareTag("snappoint") && child.GetComponent<DerivedAnchorMarker>() == null)
                {
                    existing.Add(child.localPosition);
                }
            }

            int skipped = 0;

            List<Anchor> anchors = new List<Anchor>();
            Build(center, extents, mode, anchors);

            // Direct children, because vanilla only looks one level down for the tag.
            foreach (Anchor anchor in anchors)
            {
                if (IsDuplicate(anchor.Local, existing))
                {
                    skipped++;
                    continue;
                }

                GameObject go = new GameObject(anchor.Name);
                go.tag = "snappoint";
                go.AddComponent<DerivedAnchorMarker>().Kind = anchor.Kind;
                go.transform.SetParent(ghost.transform, worldPositionStays: false);
                go.transform.localPosition = anchor.Local;
                go.transform.localRotation = Quaternion.identity;
                Cached.Add(go.transform);
            }

            _cachedFor = ghost;
            _cachedMode = mode;

            // After the anchors exist, so they take part in the ordering.
            SnapPointOrder.Apply(ghost);

            HammerOfOdenPlugin.Debug(
                $"Derived {Cached.Count} anchor(s) for '{ghost.name}' in mode {mode}"
                + (skipped > 0 ? $"; skipped {skipped} already on an existing snap point." : "."));
        }

        /// <summary>One derived anchor: where it is, what it is, and what to call it.</summary>
        internal struct Anchor
        {
            internal string Name;
            internal Vector3 Local;
            internal AnchorKind Kind;

            internal Anchor(string name, Vector3 local, AnchorKind kind)
            {
                Name = name;
                Local = local;
                Kind = kind;
            }
        }

        /// <summary>
        /// Every derived anchor for a box of the given centre and extents.
        /// </summary>
        /// <remarks>
        /// Full adds quarter points along each edge - between a corner and that edge's
        /// midpoint - rather than points between the centre and the surface. Interior points
        /// are all but unreachable while building and mostly sit inside the piece where they
        /// cannot be seen; quarter points divide an edge into four, which is something you
        /// can actually build to.
        ///
        /// The piece centre is the one interior anchor kept, because placing by the middle of
        /// a piece is genuinely useful. It is drawn as its own shape so it never reads as a
        /// face centre.
        /// </remarks>
        internal static void Build(Vector3 c, Vector3 e, DerivedSnapMode mode, List<Anchor> into)
        {
            into.Add(new Anchor("Centre", c, AnchorKind.PieceCentre));

            into.Add(new Anchor("Face Top", c + new Vector3(0f, e.y, 0f), AnchorKind.FaceCentre));
            into.Add(new Anchor("Face Bottom", c + new Vector3(0f, -e.y, 0f), AnchorKind.FaceCentre));
            into.Add(new Anchor("Face Left", c + new Vector3(-e.x, 0f, 0f), AnchorKind.FaceCentre));
            into.Add(new Anchor("Face Right", c + new Vector3(e.x, 0f, 0f), AnchorKind.FaceCentre));
            into.Add(new Anchor("Face Front", c + new Vector3(0f, 0f, e.z), AnchorKind.FaceCentre));
            into.Add(new Anchor("Face Back", c + new Vector3(0f, 0f, -e.z), AnchorKind.FaceCentre));

            if (mode >= DerivedSnapMode.CentersAndCorners)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            string name = string.Format(
                                "Corner {0} {1} {2}",
                                sy > 0 ? "Top" : "Bottom",
                                sx > 0 ? "Right" : "Left",
                                sz > 0 ? "Front" : "Back");

                            into.Add(new Anchor(name, c + new Vector3(sx * e.x, sy * e.y, sz * e.z), AnchorKind.Corner));
                        }
                    }
                }
            }

            if (mode >= DerivedSnapMode.CentersCornersAndEdges)
            {
                AddEdgeMidpoints(c, e, into);
            }

            if (mode >= DerivedSnapMode.Full)
            {
                AddEdgeQuarters(c, e, into);
            }
        }

        /// <summary>The midpoint of each of the twelve edges.</summary>
        private static void AddEdgeMidpoints(Vector3 c, Vector3 e, List<Anchor> into)
        {
            for (int a = -1; a <= 1; a += 2)
            {
                for (int b = -1; b <= 1; b += 2)
                {
                    into.Add(new Anchor(
                        string.Format("Edge {0} {1}", a > 0 ? "Top" : "Bottom", b > 0 ? "Front" : "Back"),
                        c + new Vector3(0f, a * e.y, b * e.z), AnchorKind.EdgeMidpoint));

                    into.Add(new Anchor(
                        string.Format("Edge {0} {1}", a > 0 ? "Top" : "Bottom", b > 0 ? "Right" : "Left"),
                        c + new Vector3(b * e.x, a * e.y, 0f), AnchorKind.EdgeMidpoint));

                    into.Add(new Anchor(
                        string.Format("Edge {0} {1}", a > 0 ? "Front" : "Back", b > 0 ? "Right" : "Left"),
                        c + new Vector3(b * e.x, 0f, a * e.z), AnchorKind.EdgeMidpoint));
                }
            }
        }

        /// <summary>
        /// Quarter and three-quarter points along each edge - halfway between a corner and
        /// that edge's midpoint.
        /// </summary>
        private static void AddEdgeQuarters(Vector3 c, Vector3 e, List<Anchor> into)
        {
            for (int a = -1; a <= 1; a += 2)
            {
                for (int b = -1; b <= 1; b += 2)
                {
                    for (int q = -1; q <= 1; q += 2)
                    {
                        // Each quarter point names the edge it lies on, then which end of
                        // that edge it sits nearer to. Twenty four points all called
                        // "Edge Quarter" tell you nothing when cycling past them.

                        // Edges running left to right.
                        into.Add(new Anchor(
                            string.Format("Quarter {0} {1} {2}",
                                a > 0 ? "Top" : "Bottom",
                                b > 0 ? "Front" : "Back",
                                q > 0 ? "Right" : "Left"),
                            c + new Vector3(q * e.x * 0.5f, a * e.y, b * e.z), AnchorKind.EdgeQuarter));

                        // Edges running top to bottom.
                        into.Add(new Anchor(
                            string.Format("Quarter {0} {1} {2}",
                                b > 0 ? "Right" : "Left",
                                a > 0 ? "Front" : "Back",
                                q > 0 ? "Upper" : "Lower"),
                            c + new Vector3(b * e.x, q * e.y * 0.5f, a * e.z), AnchorKind.EdgeQuarter));

                        // Edges running front to back.
                        into.Add(new Anchor(
                            string.Format("Quarter {0} {1} {2}",
                                a > 0 ? "Top" : "Bottom",
                                b > 0 ? "Right" : "Left",
                                q > 0 ? "Front" : "Back"),
                            c + new Vector3(b * e.x, a * e.y, q * e.z * 0.5f), AnchorKind.EdgeQuarter));
                    }
                }
            }
        }

        /// <summary>
        /// Measure the piece in its own local space. Snap points first, because they mark
        /// where the piece is meant to meet other pieces; renderers only as a fallback.
        /// </summary>
        internal static bool TryMeasure(Piece piece, out Vector3 center, out Vector3 extents)
        {
            center = Vector3.zero;
            extents = Vector3.zero;

            Transform root = piece.transform;
            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;

            // Read tagged children directly rather than calling GetSnapPoints, which we are
            // patching: going through it here would recurse.
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                // Skip anchors we added ourselves, or the box would grow from its own output.
                if (!child.CompareTag("snappoint") || child.GetComponent<DerivedAnchorMarker>() != null)
                {
                    continue;
                }

                Encapsulate(child.localPosition, ref any, ref min, ref max);
            }

            if (!any)
            {
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    // Solid geometry only: particle systems for smoke and fire have bounds
                    // far larger than the piece, which would scatter every derived anchor
                    // well outside it.
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    {
                        continue;
                    }

                    Bounds b = renderer.bounds;
                    Encapsulate(root.InverseTransformPoint(b.min), ref any, ref min, ref max);
                    Encapsulate(root.InverseTransformPoint(b.max), ref any, ref min, ref max);
                }
            }

            if (!any)
            {
                return false;
            }

            center = (min + max) * 0.5f;
            extents = (max - min) * 0.5f;

            // A perfectly flat piece would stack every "face" anchor on top of its centre.
            return extents.sqrMagnitude > 0.0001f;
        }

        private static void Encapsulate(Vector3 p, ref bool any, ref Vector3 min, ref Vector3 max)
        {
            if (!any)
            {
                min = max = p;
                any = true;
                return;
            }

            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
    }
}
