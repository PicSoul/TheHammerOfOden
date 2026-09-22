using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Rebuilds a bent piece's collision as a short chain of boxes following the arc.
    /// </summary>
    /// <remarks>
    /// A BoxCollider cannot curve, so a bent piece kept the straight box it started as - which
    /// is why an arch could be walked through, and why the game refused to place one whose feet
    /// reached the ground: it was judging a shape that was no longer on screen.
    ///
    /// The chain is short. What decides how many boxes are needed is not the piece's size but
    /// how far a straight chord is allowed to stray from the curve it is standing in for, and
    /// that falls off quickly: a two metre piece bent into a semicircle needs four boxes to stay
    /// within five centimetres, and three at ten. Ends and middle alone - the obvious minimum -
    /// leaves a nine centimetre gap at the quarter points, which is enough to catch a foot in,
    /// so the count is worked out from the tolerance rather than fixed.
    ///
    /// Each box is turned by the arc's tangent where it sits, so the chain follows the curve
    /// rather than chording across it, and every one is a child of the piece: they move with it,
    /// they are destroyed with it, and nothing in the prefab is modified.
    /// </remarks>
    internal static class BendCollision
    {
        private const string ChildName = "HoO_bendCollider";

        /// <summary>Boxes beyond this stop buying accuracy worth the physics cost.</summary>
        private const int MaximumSegments = 12;

        /// <summary>
        /// Replaces a piece's collision with a chain along the arc.
        /// </summary>
        /// <param name="created">Filled with what was made, so a caller can undo it.</param>
        /// <param name="silenced">Filled with the colliders switched off, for the same reason.</param>
        internal static void Apply(
            GameObject piece,
            float degrees,
            int axis,
            int rise,
            Vector3 min,
            Vector3 max,
            List<GameObject> created,
            List<Collider> silenced)
        {
            Restore(created, silenced);

            if (piece == null || Mathf.Abs(degrees) < 0.01f || !ModConfig.BendRebuildsCollision.Value)
            {
                return;
            }

            float radians = degrees * Mathf.Deg2Rad;
            float length = max[axis] - min[axis];

            if (length < 0.001f)
            {
                return;
            }

            float radius = length / radians;

            // The shape being replaced, taken from the colliders rather than the meshes: it is
            // collision that is being rebuilt, and on most pieces the two do not match exactly.
            if (!SolidExtent(piece, out Bounds solid, out int layer, silenced))
            {
                return;
            }

            int axle = 3 - axis - rise;
            int segments = SegmentsFor(radians, radius);

            Vector3 spin = Vector3.zero;
            spin[axle] = 1f;

            Vector3 alongDir = Vector3.zero;
            alongDir[axis] = 1f;

            Vector3 riseDir = Vector3.zero;
            riseDir[rise] = 1f;

            float handed = Vector3.Dot(Vector3.Cross(alongDir, riseDir), spin) >= 0f ? -1f : 1f;

            float midAlong = (min[axis] + max[axis]) * 0.5f;
            float midRise = (min[rise] + max[rise]) * 0.5f;

            // The thickness the chain has to carry, measured off the solid it replaces.
            Vector3 size = Vector3.zero;
            size[axis] = length / segments * 1.06f; // a little overlap, so no seam opens up
            size[rise] = solid.size[rise];
            size[axle] = solid.size[axle];

            // Where the solid sits across the piece, which is not always its middle - a wall's
            // collision can be offset to one face.
            float solidRise = solid.center[rise] - midRise;
            float solidAxle = solid.center[axle];

            for (int i = 0; i < segments; i++)
            {
                float along = length * ((i + 0.5f) / segments - 0.5f);
                float angle = along / radius;
                float arm = radius - solidRise;

                Vector3 centre = Vector3.zero;
                centre[axis] = midAlong + arm * Mathf.Sin(angle);
                centre[rise] = midRise + radius - arm * Mathf.Cos(angle);
                centre[axle] = solidAxle;

                GameObject box = new GameObject(ChildName);
                box.layer = layer;
                box.transform.SetParent(piece.transform, false);
                box.transform.localPosition = centre;
                box.transform.localRotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * handed, spin);

                BoxCollider collider = box.AddComponent<BoxCollider>();
                collider.size = size;

                created.Add(box);
            }

            HammerOfOdenPlugin.Debug(
                $"Bend rebuilt collision as {segments} box(es) for {degrees:0.#} degrees "
                + $"(radius {radius:0.##}m).");
        }

        internal static void Restore(List<GameObject> created, List<Collider> silenced)
        {
            foreach (GameObject box in created)
            {
                if (box != null)
                {
                    Object.Destroy(box);
                }
            }

            created.Clear();

            foreach (Collider collider in silenced)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            silenced.Clear();
        }

        /// <summary>
        /// How many boxes keep the chain within the configured distance of the true arc.
        /// </summary>
        /// <remarks>
        /// A chord spanning angle t/n on radius R strays from the arc by R(1 - cos(t/2n)) at its
        /// middle. Solving that for n is the whole calculation, and it is worth doing rather
        /// than picking a number: the answer depends on the bend as much as the piece, so a
        /// gentle curve costs two boxes where a semicircle costs four.
        /// </remarks>
        private static int SegmentsFor(float radians, float radius)
        {
            float tolerance = Mathf.Max(0.01f, ModConfig.BendCollisionTolerance.Value);
            float sweep = Mathf.Abs(radians);
            float reach = Mathf.Abs(radius);

            if (tolerance >= reach)
            {
                return 1;
            }

            float half = Mathf.Acos(Mathf.Clamp(1f - tolerance / reach, -1f, 1f));
            if (half <= 0.0001f)
            {
                return MaximumSegments;
            }

            return Mathf.Clamp(Mathf.CeilToInt(sweep / (2f * half)), 1, MaximumSegments);
        }

        /// <summary>
        /// The solid the piece actually collides with, and the layer it lives on.
        /// </summary>
        /// <remarks>
        /// Damage variants are skipped for the same reason they are when deciding whether a
        /// piece may bend at all: a worn or broken state's collision belongs to a piece that is
        /// not standing here. Triggers are skipped because they are how a piece notices you,
        /// not what it is made of, and rebuilding one as collision would turn a comfort range
        /// into a wall.
        /// </remarks>
        private static bool SolidExtent(
            GameObject piece, out Bounds solid, out int layer, List<Collider> silenced)
        {
            solid = new Bounds();
            layer = piece.layer;
            bool any = false;

            Transform root = piece.transform;
            WearNTear wear = piece.GetComponent<WearNTear>();

            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider.isTrigger || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || IsDamagedVariant(wear, piece, collider.transform))
                {
                    continue;
                }

                Bounds world = collider.bounds;
                Vector3 centre = root.InverseTransformPoint(world.center);

                // Extent in the piece's own frame, which is where the arc lives. Taken from the
                // world box rather than the collider's own size so that a collider on a rotated
                // or scaled child still measures correctly.
                Vector3 extents = root.InverseTransformVector(world.extents);
                Bounds local = new Bounds(centre, new Vector3(
                    Mathf.Abs(extents.x) * 2f, Mathf.Abs(extents.y) * 2f, Mathf.Abs(extents.z) * 2f));

                if (!any)
                {
                    solid = local;
                    layer = collider.gameObject.layer;
                    any = true;
                }
                else
                {
                    solid.Encapsulate(local);
                }

                collider.enabled = false;
                silenced.Add(collider);
            }

            return any;
        }

        private static bool IsDamagedVariant(WearNTear wear, GameObject piece, Transform child)
        {
            if (wear == null)
            {
                return false;
            }

            for (Transform t = child; t != null && t.gameObject != piece; t = t.parent)
            {
                if ((wear.m_worn != null && t.gameObject == wear.m_worn)
                    || (wear.m_broken != null && t.gameObject == wear.m_broken))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
