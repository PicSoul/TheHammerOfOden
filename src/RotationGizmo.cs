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
        private static Renderer _pitchMark;
        private static Renderer _yawMark;
        private static Renderer _rollMark;
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

            MeasureGhost(ghost, out Vector3 center, out float localRadius);
            float radius = localRadius * ModConfig.GizmoScale.Value;

            _root.SetActive(true);
            _root.transform.position = center;
            _root.transform.rotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * radius;

            bool onlyActive = ModConfig.GizmoActiveAxisOnly.Value;

            Orient(_pitch, RotationAxis.X);
            Orient(_yaw, RotationAxis.Y);
            Orient(_roll, RotationAxis.Z);

            Style(_pitch, RotationAxis.X, activeAxis, onlyActive, ModConfig.GizmoColorX.Value);
            Style(_yaw, RotationAxis.Y, activeAxis, onlyActive, ModConfig.GizmoColorY.Value);
            Style(_roll, RotationAxis.Z, activeAxis, onlyActive, ModConfig.GizmoColorZ.Value);

            Mark(_pitchMark, _pitch, RotationAxis.X, activeAxis, ModConfig.GizmoColorX.Value);
            Mark(_yawMark, _yaw, RotationAxis.Y, activeAxis, ModConfig.GizmoColorY.Value);
            Mark(_rollMark, _roll, RotationAxis.Z, activeAxis, ModConfig.GizmoColorZ.Value);
        }

        internal static void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }

            foreach (Renderer mark in new[] { _pitchMark, _yawMark, _rollMark })
            {
                if (mark != null)
                {
                    mark.enabled = false;
                }
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

            foreach (Renderer mark in new[] { _pitchMark, _yawMark, _rollMark })
            {
                if (mark != null)
                {
                    Object.Destroy(mark.gameObject);
                }
            }

            _pitchMark = _yawMark = _rollMark = null;
            _measuredGhost = null;
            LineStyle.Clear();
            AngleBeads.Destroy();

            _material = null;
        }

        /// <summary>
        /// Point a ring's plane at the axis its increment really rotates about, with a
        /// defined zero direction.
        /// </summary>
        /// <remarks>
        /// FromToRotation would do for drawing a plain circle, but it leaves the twist about
        /// the normal arbitrary - and an arbitrary twist means the angle marker would sit in
        /// a meaningless place. Building the frame from an explicit reference fixes where
        /// zero is.
        /// </remarks>
        private static void Orient(LineRenderer ring, RotationAxis axis)
        {
            Vector3 normal = RotationState.AxisDirection(axis);
            Vector3 reference = (axis == RotationAxis.Y) ? Vector3.forward : Vector3.up;

            Vector3 projected = Vector3.ProjectOnPlane(reference, normal);
            if (projected.sqrMagnitude < 0.0001f)
            {
                projected = Vector3.ProjectOnPlane(Vector3.right, normal);
            }

            ring.transform.localRotation = Quaternion.LookRotation(projected.normalized, normal);
        }

        private static void Mark(Renderer marker, LineRenderer ring, RotationAxis axis, RotationAxis active, Color color)
        {
            if (!ring.enabled)
            {
                marker.enabled = false;
                return;
            }

            PlaceMarker(marker, ring, axis, axis == active, color);
        }

        /// <summary>Position the travelling bead that shows the current angle on an axis.</summary>
        private static void PlaceMarker(Renderer marker, LineRenderer ring, RotationAxis axis, bool isActive, Color color)
        {
            if (!ModConfig.ShowAngleMarkers.Value)
            {
                marker.enabled = false;
                return;
            }

            float angle = RotationState.AngleOf(axis) * Mathf.Deg2Rad;

            // Unity is left handed, so a positive rotation about +Y carries +Z towards +X:
            //   (0,0,1) -> (sin t, 0, cos t)
            // Laying the marker out the same way makes it travel with the piece rather than
            // against it, and puts zero on the ring's reference direction (local forward).
            Vector3 local = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));

            marker.enabled = true;
            marker.transform.position = ring.transform.TransformPoint(local);

            // A sphere needs no facing, and diameter reads more naturally than radius here.
            float diameter = ModConfig.AngleMarkerSize.Value * 2f * (isActive ? 1.5f : 1f);
            marker.transform.localScale = Vector3.one * diameter;

            AngleBeads.Tint(marker, color);
        }

        /// <summary>
        /// The active ring is the configured colour at full strength; the others are
        /// desaturated and dimmed.
        /// </summary>
        /// <remarks>
        /// Separating by saturation as well as brightness is what makes highlighting read as
        /// "this one is red" rather than "this one is a lighter version of the same thing".
        /// Dimming alone forces the base colour to be bright enough to see while faded, and a
        /// bright red is a pink - so the highlight had nowhere left to go.
        /// </remarks>
        private static void Style(LineRenderer ring, RotationAxis axis, RotationAxis active, bool onlyActive, Color color)
        {
            bool isActive = axis == active;

            if (onlyActive && !isActive)
            {
                ring.enabled = false;
                return;
            }

            ring.enabled = true;

            Color tinted = color;

            if (!isActive)
            {
                float grey = color.grayscale;
                tinted = Color.Lerp(new Color(grey, grey, grey), color, ModConfig.GizmoInactiveSaturation.Value);
                tinted.a = color.a * ModConfig.GizmoInactiveOpacity.Value;
            }

            LineStyle.Apply(ring, tinted, ModConfig.GizmoWidth.Value * (isActive ? 1.8f : 1f));
        }

        private static GameObject _measuredGhost;
        private static Vector3 _localCenter;
        private static float _localRadius;

        /// <summary>
        /// Fit the rings to the piece, measuring it only when the piece changes.
        /// </summary>
        /// <remarks>
        /// The size of a ghost is a property of the prefab, not of the frame. Walking its
        /// renderers every frame allocated an array and re-derived the same numbers a hundred
        /// times a second; measuring in local space means rotation and movement need no
        /// remeasurement at all.
        /// </remarks>
        private static void MeasureGhost(GameObject ghost, out Vector3 center, out float radius)
        {
            if (_measuredGhost != ghost)
            {
                _measuredGhost = ghost;
                _localCenter = Vector3.zero;
                _localRadius = 0.5f;

                Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Transform root = ghost.transform;
                    Bounds bounds = new Bounds(root.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);

                    foreach (Renderer renderer in renderers)
                    {
                        Bounds b = renderer.bounds;
                        bounds.Encapsulate(root.InverseTransformPoint(b.min));
                        bounds.Encapsulate(root.InverseTransformPoint(b.max));
                    }

                    _localCenter = bounds.center;
                    _localRadius = Mathf.Max(bounds.extents.magnitude, 0.5f);
                }
            }

            center = ghost.transform.TransformPoint(_localCenter);
            radius = _localRadius;
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

            // Beads live outside the ring hierarchy so ring rotation does not carry them.
            _yawMark = AngleBeads.Create("YawBead");
            _pitchMark = AngleBeads.Create("PitchBead");
            _rollMark = AngleBeads.Create("RollBead");

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
