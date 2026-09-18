using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Draws markers for the snap points on the piece being placed and on the piece being
    /// aimed at, highlighting the one currently selected.
    /// </summary>
    /// <remarks>
    /// Vanilla names snap points after their prefab child objects and reuses those names,
    /// so cycling with Q/E can show "Bottom 1" for two physically different points. The
    /// label cannot disambiguate them; showing where they are can.
    ///
    /// Two separate pools, because the ghost's points and the target's points answer
    /// different questions: "which part of my piece am I holding it by" versus "what can I
    /// attach that to".
    ///
    /// Camera-facing rings rather than spheres, so a marker reads the same from any angle
    /// and never occludes the piece behind it.
    /// </remarks>
    internal static class SnapPointMarkers
    {
        private const int Segments = 20;

        private static GameObject _root;
        private static readonly List<LineRenderer> GhostPool = new List<LineRenderer>();
        private static readonly List<LineRenderer> TargetPool = new List<LineRenderer>();
        private static readonly List<Transform> TargetPoints = new List<Transform>();

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

            Quaternion facing = (Camera.main != null) ? Camera.main.transform.rotation : Quaternion.identity;

            DrawGhostPoints(ghostPoints, manualSnapPoint, facing);
            DrawTargetPoints(ghost, hoveringPiece, facing);
        }

        private static void DrawGhostPoints(List<Transform> points, int manualSnapPoint, Quaternion facing)
        {
            int drawn = 0;

            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    Transform point = points[i];
                    if (point == null)
                    {
                        continue;
                    }

                    bool isActive = i == manualSnapPoint;
                    if (!isActive && !ModConfig.ShowInactiveSnapPoints.Value)
                    {
                        continue;
                    }

                    Place(
                        Take(GhostPool, drawn++),
                        point.position,
                        facing,
                        ModConfig.SnapPointSize.Value * (isActive ? 2.1f : 1f),
                        isActive ? ModConfig.SnapPointActiveColor.Value : ModConfig.SnapPointColor.Value,
                        isActive ? 1.6f : 1f);
                }
            }

            HideFrom(GhostPool, drawn);
        }

        /// <summary>
        /// The snap points you could attach to. Only worth drawing while they are close
        /// enough to matter: vanilla's auto-snap reaches 0.5m, so anything further away is
        /// noise rather than information.
        /// </summary>
        private static void DrawTargetPoints(GameObject ghost, Piece hoveringPiece, Quaternion facing)
        {
            int drawn = 0;

            if (ModConfig.ShowTargetSnapPoints.Value && hoveringPiece != null)
            {
                TargetPoints.Clear();
                hoveringPiece.GetSnapPoints(TargetPoints);

                float range = ModConfig.TargetSnapPointRange.Value;
                Vector3 origin = ghost.transform.position;

                for (int i = 0; i < TargetPoints.Count; i++)
                {
                    Transform point = TargetPoints[i];
                    if (point == null || Vector3.Distance(point.position, origin) > range)
                    {
                        continue;
                    }

                    Place(
                        Take(TargetPool, drawn++),
                        point.position,
                        facing,
                        ModConfig.SnapPointSize.Value * 0.85f,
                        ModConfig.TargetSnapPointColor.Value,
                        1f);
                }
            }

            HideFrom(TargetPool, drawn);
        }

        private static void Place(LineRenderer ring, Vector3 position, Quaternion facing, float size, Color color, float widthScale)
        {
            ring.enabled = true;

            Transform t = ring.transform;
            t.position = position;
            t.rotation = facing;
            t.localScale = Vector3.one * size;

            ring.startColor = color;
            ring.endColor = color;
            ring.widthMultiplier = ModConfig.GizmoWidth.Value * widthScale;
        }

        internal static void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private static bool _builtSeeThrough;

        internal static void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                GhostPool.Clear();
                TargetPool.Clear();
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

        private static LineRenderer Take(List<LineRenderer> pool, int index)
        {
            while (pool.Count <= index)
            {
                pool.Add(BuildMarker(pool == GhostPool ? "Ghost" : "Target", pool.Count));
            }

            return pool[index];
        }

        private static bool EnsureRoot()
        {
            // The material is baked into each LineRenderer when built, so a change of mind
            // about see-through means rebuilding the pools.
            if (_root != null && _builtSeeThrough != ModConfig.SnapPointsSeeThrough.Value)
            {
                Destroy();
            }

            if (_root != null)
            {
                return true;
            }

            if (MarkerMaterial() == null)
            {
                return false;
            }

            _root = new GameObject("HammerOfOden_SnapPoints");
            Object.DontDestroyOnLoad(_root);
            _builtSeeThrough = ModConfig.SnapPointsSeeThrough.Value;
            return true;
        }

        /// <summary>
        /// See-through by default: a snap point on the underside of a piece is invisible
        /// exactly when you most need to know where it is.
        /// </summary>
        private static Material MarkerMaterial()
        {
            return ModConfig.SnapPointsSeeThrough.Value
                ? GizmoMaterial.GetSeeThrough()
                : GizmoMaterial.Get();
        }

        private static LineRenderer BuildMarker(string prefix, int index)
        {
            GameObject go = new GameObject($"{prefix}Snap{index}");
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.material = MarkerMaterial();
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
