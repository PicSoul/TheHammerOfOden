using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum SnapPointDisplay
    {
        /// <summary>Every snap point on both pieces, all drawn through geometry.</summary>
        All = 0,

        /// <summary>
        /// Only what is actually in play: the point your piece is snapping by, and the
        /// points on the target that are close enough to snap to.
        /// </summary>
        Relevant = 1,

        /// <summary>Only the pair currently snapping together.</summary>
        ActivePairOnly = 2
    }

    /// <summary>
    /// Draws markers for snap points on the piece being placed and the piece being aimed at.
    /// </summary>
    /// <remarks>
    /// Drawing everything through geometry turns a 2x2 floor into a couple of dozen
    /// overlapping rings, which is less readable than drawing nothing. Relevant mode instead
    /// shows the one point your piece is held by - the manual pick, or whichever one vanilla
    /// chose under automatic snapping - and, on the target, only the points near enough to
    /// actually reach. A snap point you cannot currently snap to is not information.
    ///
    /// Only the points that matter are drawn through geometry. The rest use the normal
    /// material, so they are still there for context when nothing is in the way but never
    /// pile up on top of the piece.
    /// </remarks>
    internal static class SnapPointMarkers
    {
        private const int Segments = 20;

        private static GameObject _root;
        private static readonly List<LineRenderer> SeeThroughPool = new List<LineRenderer>();
        private static readonly List<LineRenderer> NormalPool = new List<LineRenderer>();
        private static readonly List<Transform> TargetPoints = new List<Transform>();
        private static readonly List<Vector3> DerivedTargets = new List<Vector3>();

        private static int _seeThroughUsed;
        private static int _normalUsed;

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

            Quaternion facing = MainCamera.Facing;
            SnapPointDisplay display = ModConfig.SnapDisplay.Value;

            DrawGhostPoints(ghostPoints, manualSnapPoint, facing, display);
            DrawTargetPoints(ghost, hoveringPiece, ghostPoints, facing, display);

            HideFrom(SeeThroughPool, _seeThroughUsed);
            HideFrom(NormalPool, _normalUsed);
        }

        private static void DrawGhostPoints(
            List<Transform> points, int manualSnapPoint, Quaternion facing, SnapPointDisplay display)
        {
            if (points == null)
            {
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                Transform point = points[i];
                if (point == null)
                {
                    continue;
                }

                // "Active" is the point you picked with Q/E, or the one vanilla settled on
                // when snapping automatically.
                bool isActive = (manualSnapPoint >= 0)
                    ? i == manualSnapPoint
                    : ActiveSnapPair.IsSource(point);

                if (!isActive)
                {
                    if (display == SnapPointDisplay.ActivePairOnly
                        || !ModConfig.ShowInactiveSnapPoints.Value)
                    {
                        continue;
                    }
                }

                // Only the point in play is worth punching through the piece for.
                bool seeThrough = isActive && ModConfig.SnapPointsSeeThrough.Value;

                Draw(
                    seeThrough,
                    point.position,
                    facing,
                    ModConfig.SnapPointSize.Value * (isActive ? 2.1f : 1f),
                    isActive ? ModConfig.SnapPointActiveColor.Value : ModConfig.SnapPointColor.Value,
                    isActive ? 1.6f : 1f);
            }
        }

        private static void DrawTargetPoints(
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

            // One cached array covers the piece's own snap points and any derived ones.
            // Going through Piece.GetSnapPoints here would run our own patch every frame.
            TargetPoints.Clear();

            // Derived anchors on a world piece exist only as numbers - nothing is added to
            // the piece itself - so they have to be drawn from the same maths that snaps to
            // them, or they are invisible.
            DerivedTargets.Clear();
            DerivedTargets.AddRange(DerivedAnchorCache.Get(
                hoveringPiece,
                ModConfig.SnapToDerivedTargets.Value ? ModConfig.DerivedSnaps.Value : DerivedSnapMode.Off));

            // Two distances, not one: the range a point can actually snap from, and the
            // wider range over which it is worth previewing. Fading between them means a
            // point announces itself as you approach rather than appearing fully formed.
            float snapReach = ModConfig.DerivedSnapDistance.Value;
            float previewReach = Mathf.Max(snapReach, ModConfig.TargetPreviewReach.Value);
            float range = ModConfig.TargetSnapPointRange.Value;
            Vector3 origin = ghost.transform.position;
            bool relevantOnly = display != SnapPointDisplay.All;

            Transform activeTarget = ActiveSnapPair.Target;
            Vector3 activeTargetPos = (activeTarget != null) ? activeTarget.position : Vector3.positiveInfinity;

            foreach (Transform point in TargetPoints)
            {
                if (point == null || Vector3.Distance(point.position, origin) > range)
                {
                    continue;
                }

                bool isActive = ActiveSnapPair.IsTarget(point);

                if (display == SnapPointDisplay.ActivePairOnly && !isActive)
                {
                    continue;
                }

                float fade = 1f;

                if (!isActive && relevantOnly)
                {
                    float nearest = NearestDistance(point.position, ghostPoints);
                    if (nearest > previewReach)
                    {
                        continue;
                    }

                    // Full strength once within snapping range, fading to nothing at the
                    // edge of the preview range.
                    fade = Mathf.InverseLerp(previewReach, snapReach, nearest);
                    if (fade <= 0.01f)
                    {
                        continue;
                    }
                }

                Color color = isActive
                    ? ModConfig.SnapPointActiveColor.Value
                    : ModConfig.TargetSnapPointColor.Value;
                color.a *= fade;

                Draw(
                    ModConfig.SnapPointsSeeThrough.Value,
                    point.position,
                    facing,
                    ModConfig.SnapPointSize.Value * (isActive ? 1.6f : 0.85f) * Mathf.Lerp(0.7f, 1f, fade),
                    color,
                    isActive ? 1.4f : 1f);
            }

            foreach (Vector3 position in DerivedTargets)
            {
                if (Vector3.Distance(position, origin) > range)
                {
                    continue;
                }

                bool isActive = (position - activeTargetPos).sqrMagnitude < 0.0001f;

                float fade = 1f;

                if (relevantOnly)
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

                // Same colour family as the piece's own target points, drawn a little smaller
                // so a derived anchor is not mistaken for one the piece actually ships with.
                Color derived = isActive
                    ? ModConfig.SnapPointActiveColor.Value
                    : ModConfig.TargetSnapPointColor.Value;
                derived.a *= fade * (isActive ? 1f : 0.85f);

                Draw(
                    ModConfig.SnapPointsSeeThrough.Value,
                    position,
                    facing,
                    ModConfig.SnapPointSize.Value * (isActive ? 1.6f : 0.7f) * Mathf.Lerp(0.7f, 1f, fade),
                    derived,
                    isActive ? 1.4f : 0.9f);
            }
        }

        /// <summary>Distance from a target point to the nearest anchor on the piece in hand.</summary>
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

        private static void Draw(bool seeThrough, Vector3 position, Quaternion facing, float size, Color color, float widthScale)
        {
            List<LineRenderer> pool = seeThrough ? SeeThroughPool : NormalPool;
            int index = seeThrough ? _seeThroughUsed++ : _normalUsed++;

            while (pool.Count <= index)
            {
                pool.Add(BuildMarker(seeThrough, pool.Count));
            }

            LineRenderer ring = pool[index];
            ring.enabled = true;

            Transform t = ring.transform;
            t.position = position;
            t.rotation = facing;
            t.localScale = Vector3.one * size;

            LineStyle.Apply(ring, color, ModConfig.GizmoWidth.Value * widthScale);
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
            GameObject go = new GameObject($"{(seeThrough ? "Through" : "Normal")}Snap{index}");
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.material = seeThrough ? GizmoMaterial.GetSeeThrough() : GizmoMaterial.Get();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.numCornerVertices = 2;

            for (int i = 0; i < Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            return line;
        }
    }
}
