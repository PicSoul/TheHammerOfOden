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

        /// <summary>Everything above, plus a midpoint between the centre and each of them.</summary>
        Full = 3
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

            // Direct children, because vanilla only looks one level down for the tag.
            foreach (KeyValuePair<string, Vector3> anchor in BuildAnchors(center, extents, mode))
            {
                GameObject go = new GameObject(anchor.Key);
                go.tag = "snappoint";
                go.AddComponent<DerivedAnchorMarker>();
                go.transform.SetParent(ghost.transform, worldPositionStays: false);
                go.transform.localPosition = anchor.Value;
                go.transform.localRotation = Quaternion.identity;
                Cached.Add(go.transform);
            }

            _cachedFor = ghost;
            _cachedMode = mode;

            HammerOfOdenPlugin.Debug(
                $"Derived {Cached.Count} snap point(s) for '{ghost.name}' in mode {mode}.");
        }

        /// <summary>
        /// Anchor offsets only, into a caller-owned list.
        /// </summary>
        /// <remarks>
        /// The named version below allocates a list of string-keyed pairs, which is fine for
        /// the ghost because it is built once per piece. The target side runs over every
        /// nearby piece, so it needs a path that allocates nothing and never touches a string.
        /// </remarks>
        internal static void BuildLocalAnchors(Vector3 c, Vector3 e, DerivedSnapMode mode, List<Vector3> into)
        {
            into.Add(c);

            into.Add(c + new Vector3(0f, e.y, 0f));
            into.Add(c + new Vector3(0f, -e.y, 0f));
            into.Add(c + new Vector3(-e.x, 0f, 0f));
            into.Add(c + new Vector3(e.x, 0f, 0f));
            into.Add(c + new Vector3(0f, 0f, e.z));
            into.Add(c + new Vector3(0f, 0f, -e.z));

            if (mode >= DerivedSnapMode.CentersAndCorners)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            into.Add(c + new Vector3(sx * e.x, sy * e.y, sz * e.z));
                        }
                    }
                }
            }

            if (mode >= DerivedSnapMode.Full)
            {
                int derived = into.Count;
                for (int i = 1; i < derived; i++)
                {
                    into.Add(c + (into[i] - c) * 0.5f);
                }
            }
        }

        internal static IEnumerable<KeyValuePair<string, Vector3>> BuildAnchors(
            Vector3 c, Vector3 e, DerivedSnapMode mode)
        {
            List<KeyValuePair<string, Vector3>> list = new List<KeyValuePair<string, Vector3>>();

            void Add(string name, Vector3 offset) =>
                list.Add(new KeyValuePair<string, Vector3>(name, c + offset));

            Add("Centre", Vector3.zero);

            Add("Centre Top", new Vector3(0f, e.y, 0f));
            Add("Centre Bottom", new Vector3(0f, -e.y, 0f));
            Add("Centre Left", new Vector3(-e.x, 0f, 0f));
            Add("Centre Right", new Vector3(e.x, 0f, 0f));
            Add("Centre Front", new Vector3(0f, 0f, e.z));
            Add("Centre Back", new Vector3(0f, 0f, -e.z));

            if (mode >= DerivedSnapMode.CentersAndCorners)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            string name = $"Corner {(sy > 0 ? "Top" : "Bottom")} {(sx > 0 ? "Right" : "Left")} {(sz > 0 ? "Front" : "Back")}";
                            Add(name, new Vector3(sx * e.x, sy * e.y, sz * e.z));
                        }
                    }
                }
            }

            if (mode >= DerivedSnapMode.Full)
            {
                // Halfway between the centre and every anchor derived so far.
                int derived = list.Count;
                for (int i = 1; i < derived; i++)
                {
                    KeyValuePair<string, Vector3> source = list[i];
                    Add("Half " + source.Key, (source.Value - c) * 0.5f);
                }
            }

            return list;
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
                Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
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
