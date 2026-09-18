using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The size of the piece being placed, measured once per piece.
    /// </summary>
    /// <remarks>
    /// Both the gizmo rings and the snap markers need to know how big the piece is, and a
    /// ghost's size is a property of the prefab rather than of the frame. Measuring in local
    /// space means rotating and moving the piece need no remeasurement at all.
    /// </remarks>
    internal static class GhostBounds
    {
        private static GameObject _measured;
        private static Vector3 _localCenter;
        private static float _localRadius = 0.5f;

        internal static void Measure(GameObject ghost, out Vector3 center, out float radius)
        {
            if (_measured != ghost)
            {
                _measured = ghost;
                _localCenter = Vector3.zero;
                _localRadius = 0.5f;

                Transform root = ghost.transform;
                Bounds bounds = default(Bounds);
                bool any = false;

                foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
                {
                    // Solid geometry only. A piece like the charcoal kiln carries smoke and
                    // fire particle systems whose bounds dwarf the building itself, and
                    // measuring those makes the gizmo several times too large.
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                    {
                        continue;
                    }

                    Bounds b = renderer.bounds;

                    if (!any)
                    {
                        bounds = new Bounds(root.InverseTransformPoint(b.center), Vector3.zero);
                        any = true;
                    }

                    bounds.Encapsulate(root.InverseTransformPoint(b.min));
                    bounds.Encapsulate(root.InverseTransformPoint(b.max));
                }

                if (any)
                {
                    _localCenter = bounds.center;
                    _localRadius = Mathf.Max(bounds.extents.magnitude, 0.25f);
                }
            }

            center = ghost.transform.TransformPoint(_localCenter);
            radius = _localRadius;
        }

        /// <summary>Just the radius, for callers that do not need the centre.</summary>
        internal static float RadiusOf(GameObject ghost)
        {
            Measure(ghost, out _, out float radius);
            return radius;
        }

        internal static void Invalidate()
        {
            _measured = null;
        }
    }
}
