using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A placed piece that is curved, and stays curved.
    /// </summary>
    /// <remarks>
    /// The ghost's deformer is a single thing: one piece at a time, restored when you let go of
    /// it. A world full of arches is the opposite - many at once, each holding its own meshes
    /// for as long as it stands - so a built piece carries its own rather than borrowing that
    /// one. The arithmetic is the same arc either way; only the ownership differs.
    ///
    /// Meshes are copies, and this destroys them when the piece goes. An instantiated mesh is
    /// not garbage collected with the object that used it: left alone, every arch built or
    /// walked past would leak one copy per part for the rest of the session.
    ///
    /// The bend itself lives in the piece's ZDO, so it survives the zone unloading and the world
    /// being reloaded, and travels to other players - the same route the piece scale takes. What
    /// it does not do is reach anyone without the mod, since nothing in vanilla knows to read it
    /// back, which is why the mod is required on the server.
    /// </remarks>
    internal sealed class BentPiece : MonoBehaviour
    {
        internal const string DegreesKey = "HoO_bendDegrees";
        internal const string AxisKey = "HoO_bendAxis";
        internal const string RiseKey = "HoO_bendRise";

        /// <summary>
        /// Which of the two directions was chosen, rather than which axis that turned out to be.
        /// </summary>
        /// <remarks>
        /// The axis is what the deformer needs; the choice is what the controls hold. Copying a
        /// bend off a built piece needs the second, and deriving it from the first would mean
        /// measuring the built piece - which would hand its meshes to the ghost's deformer and
        /// take them away from the piece that is standing there using them.
        /// </remarks>
        internal const string ChoiceKey = "HoO_bendChoice";

        private readonly List<Mesh> _owned = new List<Mesh>();
        private readonly List<GameObject> _collision = new List<GameObject>();
        private readonly List<Collider> _silenced = new List<Collider>();

        private float _degrees;
        private int _axis;
        private int _rise;
        private bool _applied;

        /// <summary>Writes the bend onto a piece and curves it, at the moment it is built.</summary>
        internal static void Attach(GameObject piece, float degrees, int axis, int rise, int choice)
        {
            if (piece == null || Mathf.Abs(degrees) < 0.01f)
            {
                return;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view != null && view.IsValid())
            {
                ZDO zdo = view.GetZDO();
                zdo.Set(DegreesKey, degrees);
                zdo.Set(AxisKey, axis);
                zdo.Set(RiseKey, rise);
                zdo.Set(ChoiceKey, choice);
            }

            Apply(piece, degrees, axis, rise);
        }

        /// <summary>Curves a piece coming back into the world, if it was built bent.</summary>
        internal static void Restore(ZNetView view)
        {
            if (view == null || !view.IsValid())
            {
                return;
            }

            ZDO zdo = view.GetZDO();

            float degrees = zdo.GetFloat(DegreesKey, 0f);
            if (Mathf.Abs(degrees) < 0.01f)
            {
                return;
            }

            Apply(view.gameObject, degrees, zdo.GetInt(AxisKey, 0), zdo.GetInt(RiseKey, 1));
        }

        private static void Apply(GameObject piece, float degrees, int axis, int rise)
        {
            if (piece.GetComponent<BentPiece>() != null)
            {
                // Already curved. Applying twice would bend the bent copy.
                return;
            }

            BentPiece bent = piece.AddComponent<BentPiece>();
            bent._degrees = degrees;
            bent._axis = axis;
            bent._rise = rise;
            bent.Curve();
        }

        private void Curve()
        {
            if (_applied)
            {
                return;
            }

            _applied = true;

            Transform root = transform;
            float radians = _degrees * Mathf.Deg2Rad;

            if (!Extent(root, out Vector3 min, out Vector3 max))
            {
                return;
            }

            float length = max[_axis] - min[_axis];
            if (length < 0.001f)
            {
                return;
            }

            float radius = length / radians;
            float midAlong = (min[_axis] + max[_axis]) * 0.5f;
            float midRise = (min[_rise] + max[_rise]) * 0.5f;

            float segment = Mathf.Max(0.02f, ModConfig.BendSegment.Value);

            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                if (source == null)
                {
                    continue;
                }

                if (!source.isReadable)
                {
                    // Nothing to read, so nothing to curve; drawing it straight over a curved
                    // piece is worse than not drawing it.
                    Renderer straight = filter.GetComponent<Renderer>();
                    if (straight != null)
                    {
                        straight.enabled = false;
                    }

                    continue;
                }

                Matrix4x4 toPiece = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Mesh dense = MeshSubdivider.Subdivide(source, toPiece, _axis, segment) ?? Object.Instantiate(source);

                _owned.Add(dense);

                Vector3[] vertices = dense.vertices;
                Transform local = filter.transform;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = root.InverseTransformPoint(local.TransformPoint(vertices[i]));

                    float along = p[_axis] - midAlong;
                    float offset = p[_rise] - midRise;

                    float angle = along / radius;
                    float arm = radius - offset;

                    p[_axis] = midAlong + arm * Mathf.Sin(angle);
                    p[_rise] = midRise + radius - arm * Mathf.Cos(angle);

                    vertices[i] = local.InverseTransformPoint(root.TransformPoint(p));
                }

                dense.vertices = vertices;
                dense.RecalculateNormals();
                dense.RecalculateBounds();

                filter.sharedMesh = dense;
            }

            CurveAnchors(root, radius, midAlong, midRise);

            BendCollision.Apply(gameObject, _degrees, _axis, _rise, min, max, _collision, _silenced);

            HammerOfOdenPlugin.Debug(
                $"Restored a {_degrees:0.#} degree bend on '{Utils.GetPrefabName(gameObject)}'.");
        }

        /// <summary>The same tangent turn the ghost applies, so a built arch connects as it looked.</summary>
        private void CurveAnchors(Transform root, float radius, float midAlong, float midRise)
        {
            int axle = 3 - _axis - _rise;

            Vector3 spin = Vector3.zero;
            spin[axle] = 1f;

            Vector3 alongDir = Vector3.zero;
            alongDir[_axis] = 1f;

            Vector3 riseDir = Vector3.zero;
            riseDir[_rise] = 1f;

            float handed = Vector3.Dot(Vector3.Cross(alongDir, riseDir), spin) >= 0f ? -1f : 1f;
            Quaternion intoPiece = Quaternion.Inverse(root.rotation);

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == null || !child.CompareTag("snappoint"))
                {
                    continue;
                }

                Vector3 p = root.InverseTransformPoint(child.position);
                Quaternion facing = intoPiece * child.rotation;

                float along = p[_axis] - midAlong;
                float offset = p[_rise] - midRise;

                float angle = along / radius;
                float arm = radius - offset;

                p[_axis] = midAlong + arm * Mathf.Sin(angle);
                p[_rise] = midRise + radius - arm * Mathf.Cos(angle);

                child.position = root.TransformPoint(p);
                child.rotation = root.rotation
                    * Quaternion.AngleAxis(angle * Mathf.Rad2Deg * handed, spin)
                    * facing;
            }
        }

        /// <summary>The piece's extent in its own space, across every mesh it has.</summary>
        private bool Extent(Transform root, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            bool any = false;

            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable)
                {
                    continue;
                }

                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }

            return any;
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in _owned)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            _owned.Clear();

            // The boxes are children and go with the object; the list is only bookkeeping. The
            // colliders that were switched off are going too, so there is nothing to put back.
            _collision.Clear();
            _silenced.Clear();
        }
    }
}
