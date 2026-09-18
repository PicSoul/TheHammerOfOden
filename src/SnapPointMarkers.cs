using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum SnapPointDisplay
    {
        /// <summary>Every anchor on both pieces.</summary>
        All = 0,

        /// <summary>
        /// Only what is in play: the anchor your piece is held by, and the target anchors
        /// close enough to snap to.
        /// </summary>
        Relevant = 1,

        /// <summary>Only the pair currently snapping together.</summary>
        ActivePairOnly = 2
    }

    /// <summary>
    /// Draws markers for the anchors on the piece being placed and the piece being aimed at.
    /// </summary>
    /// <remarks>
    /// Each kind of anchor gets its own outline - see MarkerShapes - so a corner, an edge
    /// point and the piece centre are distinguishable at a glance. Colour is already spoken
    /// for, showing which anchor is selected and which piece it belongs to, so shape carries
    /// the rest.
    ///
    /// Markers draw through geometry by default. The denser modes place anchors on faces
    /// that are turned away from you, and occluding those hides exactly the ones that are
    /// awkward to reach by eye; clutter is handled by Display, ShowInactiveSnapPoints, and
    /// scaling markers down as the anchor count rises.
    /// </remarks>
    internal static class SnapPointMarkers
    {
        private static GameObject _root;
        private static readonly List<LineRenderer> SeeThroughPool = new List<LineRenderer>();
        private static readonly List<LineRenderer> NormalPool = new List<LineRenderer>();

        private static int _seeThroughUsed;
        private static int _normalUsed;
        private static float _sizeScale = 1f;

        internal static void Update(
            GameObject ghost,
            int manualSnapPoint,
            List<Transform> ghostPoints,
            Piece hoveringPiece)
        {
            if (!ModConfig.IsEnabled || !ModConfig.ShowSnapPoints.Value || ghost == null || !ghost.activeSelf)
            {
                Hide();
                return;
            }

            if (!EnsureRoot())
            {
                return;
            }

            _root.SetActive(true);
            _seeThroughUsed = 0;
            _normalUsed = 0;
            _sizeScale = MarkerScale(ghost);

            Quaternion facing = MainCamera.Facing;
            SnapPointDisplay display = ModConfig.SnapDisplay.Value;

            DrawGhostAnchors(ghostPoints, manualSnapPoint, facing, display);
            DrawTargetAnchors(ghost, hoveringPiece, ghostPoints, facing, display);

            HideFrom(SeeThroughPool, _seeThroughUsed);
            HideFrom(NormalPool, _normalUsed);
        }

        private static void DrawGhostAnchors(
            List<Transform> points, int manualSnapPoint, Quaternion facing, SnapPointDisplay display)
        {
            if (points == null)
            {
                return;
            }

            bool seeThrough = ModConfig.SnapPointsSeeThrough.Value;

            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];
                if (point == null)
                {
                    continue;
                }

                // "Active" is the anchor you picked with Q/E, or the one vanilla settled on
                // when snapping automatically.
                bool isActive = (manualSnapPoint >= 0)
                    ? i == manualSnapPoint
                    : ActiveSnapPair.IsSource(point);

                if (!isActive
                    && (display == SnapPointDisplay.ActivePairOnly || !ModConfig.ShowInactiveSnapPoints.Value))
                {
                    continue;
                }

                // Anchors we added carry their kind; anything else is one the piece shipped with.
                DerivedAnchorMarker marker = point.GetComponent<DerivedAnchorMarker>();
                AnchorKind kind = (marker != null) ? marker.Kind : AnchorKind.Vanilla;

                Draw(
                    seeThrough,
                    point.position,
                    facing,
                    kind,
                    ModConfig.SnapPointSize.Value * (isActive ? 2.1f : 1f),
                    isActive ? ModConfig.SnapPointActiveColor.Value : ModConfig.SnapPointColor.Value,
                    isActive ? 1.6f : 1f);
            }
        }

        private static void DrawTargetAnchors(
            GameObject ghost,
            Piece hoveringPiece,
            List<Transform> ghostPoints,
            Quaternion facing,
            SnapPointDisplay display)
        {
            if (!ModConfig.ShowTargetSnapPoints.Value || hoveringPiece == null)
            {
                return;
            }

            // Derived anchors on a world piece exist only as numbers - nothing is added to the
            // piece itself - so they are drawn from the same cache that snaps to them.
            DerivedSnapMode mode = ModConfig.SnapToDerivedTargets.Value
                ? ModConfig.DerivedSnaps.Value
                : DerivedSnapMode.Off;

            Vector3[] anchors = DerivedAnchorCache.Get(hoveringPiece, mode);
            AnchorKind[] kinds = DerivedAnchorCache.KindsFor(hoveringPiece, mode);

            // Two distances, not one: the range an anchor can actually snap from, and the
            // wider range over which it is worth previewing. Fading between them means an
            // anchor announces itself as you approach rather than appearing fully formed.
            float snapReach = ModConfig.DerivedSnapDistance.Value;
            float previewReach = Mathf.Max(snapReach, ModConfig.TargetPreviewReach.Value);
            float range = ModConfig.TargetSnapPointRange.Value;
            Vector3 origin = ghost.transform.position;
            bool relevantOnly = display != SnapPointDisplay.All;
            bool seeThrough = ModConfig.SnapPointsSeeThrough.Value;

            Transform activeTarget = ActiveSnapPair.Target;
            Vector3 activePos = (activeTarget != null) ? activeTarget.position : Vector3.positiveInfinity;

            for (int i = 0; i < anchors.Length; i++)
            {
                Vector3 position = anchors[i];

                if (Vector3.Distance(position, origin) > range)
                {
                    continue;
                }

                bool isActive = (position - activePos).sqrMagnitude < 0.0001f;

                if (display == SnapPointDisplay.ActivePairOnly && !isActive)
                {
                    continue;
                }

                float fade = 1f;

                if (!isActive && relevantOnly)
                {
                    float nearest = NearestDistance(position, ghostPoints);
                    if (nearest > previewReach)
                    {
                        continue;
                    }

                    fade = Mathf.InverseLerp(previewReach, snapReach, nearest);
                    if (fade <= 0.01f)
                    {
                        continue;
                    }
                }

                Color color = isActive
                    ? ModConfig.SnapPointActiveColor.Value
                    : ModConfig.TargetSnapPointColor.Value;
                color.a *= fade * (isActive ? 1f : 0.85f);

                Draw(
                    seeThrough,
                    position,
                    facing,
                    (i < kinds.Length) ? kinds[i] : AnchorKind.Vanilla,
                    ModConfig.SnapPointSize.Value * (isActive ? 1.6f : 0.8f) * Mathf.Lerp(0.7f, 1f, fade),
                    color,
                    isActive ? 1.4f : 0.9f);
            }
        }

        /// <summary>Distance from a position to the nearest anchor on the piece in hand.</summary>
        private static float NearestDistance(Vector3 position, List<Transform> ghostPoints)
        {
            if (ghostPoints == null)
            {
                return float.MaxValue;
            }

            float nearest = float.MaxValue;

            foreach (Transform point in ghostPoints)
            {
                if (point == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(point.position, position);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// How large markers should be for this piece and anchor density.
        /// </summary>
        /// <remarks>
        /// A fixed size suits neither a torch nor a longhouse: on a small piece the markers
        /// swamp the thing they describe, and on a large one they vanish. Denser modes shrink
        /// them further, because the whole problem with showing fifty anchors is how much of
        /// the piece they cover.
        /// </remarks>
        private static float MarkerScale(GameObject ghost)
        {
            float scale = 1f;

            if (ModConfig.ScaleSnapPointsWithPiece.Value && ghost != null)
            {
                // A piece of roughly 1.5m radius is treated as the reference size.
                scale = Mathf.Clamp(GhostBounds.RadiusOf(ghost) / 1.5f, 0.45f, 1.6f);
            }

            switch (ModConfig.DerivedSnaps.Value)
            {
                case DerivedSnapMode.CentersAndCorners:
                    scale *= 0.88f;
                    break;
                case DerivedSnapMode.CentersCornersAndEdges:
                    scale *= 0.76f;
                    break;
                case DerivedSnapMode.Full:
                    scale *= 0.62f;
                    break;
            }

            return scale;
        }

        private static void Draw(
            bool seeThrough, Vector3 position, Quaternion facing, AnchorKind kind,
            float size, Color color, float widthScale)
        {
            List<LineRenderer> pool = seeThrough ? SeeThroughPool : NormalPool;
            int index = seeThrough ? _seeThroughUsed++ : _normalUsed++;

            while (pool.Count <= index)
            {
                pool.Add(BuildMarker(seeThrough, pool.Count));
            }

            LineRenderer ring = pool[index];
            ring.enabled = true;

            MarkerShapes.Apply(ring, kind);

            float finalSize = size * _sizeScale * MarkerShapes.SizeFor(kind);

            Transform t = ring.transform;
            t.position = position;
            t.rotation = facing;
            t.localScale = Vector3.one * finalSize;

            // Stroke proportional to the shape rather than fixed, or a marker shrunk for a
            // small piece keeps a full-width outline and the corners disappear into it.
            LineStyle.Apply(ring, color, finalSize * ModConfig.MarkerStroke.Value * widthScale);
        }

        internal static void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        internal static void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                SeeThroughPool.Clear();
                NormalPool.Clear();
                LineStyle.Clear();
                MarkerShapes.Clear();
            }
        }

        private static void HideFrom(List<LineRenderer> pool, int index)
        {
            for (int i = index; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    pool[i].enabled = false;
                }
            }
        }

        private static bool EnsureRoot()
        {
            if (_root != null)
            {
                return true;
            }

            if (GizmoMaterial.Get() == null)
            {
                return false;
            }

            _root = new GameObject("HammerOfOden_SnapPoints");
            Object.DontDestroyOnLoad(_root);
            return true;
        }

        private static LineRenderer BuildMarker(bool seeThrough, int index)
        {
            GameObject go = new GameObject($"{(seeThrough ? "Through" : "Normal")}Marker{index}");
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.material = seeThrough ? GizmoMaterial.GetSeeThrough() : GizmoMaterial.Get();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;

            // Sharp joints: rounding blunts a triangle's points into a blob, and the
            // shape is the whole reason these are drawn differently from each other.
            line.numCornerVertices = 0;

            // The outline itself is set per frame by MarkerShapes, which only rewrites it
            // when the kind of anchor in this slot changes.
            MarkerShapes.Apply(line, AnchorKind.Vanilla);

            return line;
        }
    }
}
