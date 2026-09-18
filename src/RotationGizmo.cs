using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Draws the rotation rings around the placement ghost.
    /// </summary>
    /// <remarks>
    /// Built procedurally from LineRenderers rather than shipped as an AssetBundle. That
    /// keeps the mod a single DLL with no Unity Editor in the loop, and lets every aspect
    /// of the look be a config value instead of baked into an asset.
    ///
    /// Each ring shows the axis its increment actually turns the piece about. Because
    /// Quaternion.Euler applies Z, then X, then Y, only the yaw ring is world-aligned:
    /// pitch follows the yaw, and roll follows both yaw and pitch. See
    /// RotationState.AxisDirection. Orienting them any other way makes the rings disagree
    /// with what the scroll wheel does as soon as the piece is turned.
    /// </remarks>
    internal static class RotationGizmo
    {
        private const int Segments = 96;

        private static GameObject _root;
        private static LineRenderer _pitch;   // X
        private static LineRenderer _yaw;     // Y
        private static LineRenderer _roll;    // Z
        private static Material _material;

        internal static void Update(GameObject ghost, RotationAxis activeAxis)
        {
            if (!ModConfig.IsEnabled || !ModConfig.ShowGizmo.Value || ghost == null || !ghost.activeSelf)
            {
                Hide();
                return;
            }

            if (!EnsureBuilt())
            {
                return;
            }

            Bounds bounds = MeasureGhost(ghost);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f) * ModConfig.GizmoScale.Value;

            _root.SetActive(true);
            _root.transform.position = bounds.center;
            _root.transform.rotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * radius;

            bool onlyActive = ModConfig.GizmoActiveAxisOnly.Value;

            Orient(_pitch, RotationAxis.X);
            Orient(_yaw, RotationAxis.Y);
            Orient(_roll, RotationAxis.Z);

            Style(_pitch, RotationAxis.X, activeAxis, onlyActive, ModConfig.GizmoColorX.Value);
            Style(_yaw, RotationAxis.Y, activeAxis, onlyActive, ModConfig.GizmoColorY.Value);
            Style(_roll, RotationAxis.Z, activeAxis, onlyActive, ModConfig.GizmoColorZ.Value);
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
                _pitch = _yaw = _roll = null;
            }

            _material = null;
        }

        /// <summary>Point a ring's plane at the axis its increment really rotates about.</summary>
        private static void Orient(LineRenderer ring, RotationAxis axis)
        {
            Vector3 normal = RotationState.AxisDirection(axis);
            ring.transform.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
        }

        /// <summary>The inactive rings stay faint; the one the scroll wheel will move is brought forward.</summary>
        private static void Style(LineRenderer ring, RotationAxis axis, RotationAxis active, bool onlyActive, Color color)
        {
            bool isActive = axis == active;

            if (onlyActive && !isActive)
            {
                ring.enabled = false;
                return;
            }

            ring.enabled = true;

            float width = ModConfig.GizmoWidth.Value * (isActive ? 1.8f : 1f);
            ring.widthMultiplier = width;

            Color tinted = color;
            tinted.a = color.a * (isActive ? 1f : ModConfig.GizmoInactiveOpacity.Value);

            ring.startColor = tinted;
            ring.endColor = tinted;
        }

        /// <summary>Fit the rings to the piece rather than using one fixed size for a torch and a longhouse alike.</summary>
        private static Bounds MeasureGhost(GameObject ghost)
        {
            Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(ghost.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static bool EnsureBuilt()
        {
            if (_root != null)
            {
                return true;
            }

            _material = GizmoMaterial.Get();
            if (_material == null)
            {
                return false;
            }

            _root = new GameObject("HammerOfOden_Gizmo");
            Object.DontDestroyOnLoad(_root);

            // Unit-radius rings; the root's scale sizes them to the piece.
            _yaw = BuildRing("Yaw");
            _pitch = BuildRing("Pitch");
            _roll = BuildRing("Roll");

            return true;
        }

        private static LineRenderer BuildRing(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.material = _material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            // A looped LineRenderer still emits end caps, which show up as a blob at the
            // seam where the circle closes. Corners stay rounded; caps must be off.
            line.numCapVertices = 0;
            line.numCornerVertices = 4;

            for (int i = 0; i < Segments; i++)
            {
                float angle = (i / (float)Segments) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            return line;
        }

    }
}
