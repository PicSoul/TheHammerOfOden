using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Stands in for the piece being edited with a translucent box.
    /// </summary>
    /// <remarks>
    /// Tinting the piece itself cannot be made to fade. Valheim's piece shader treats alpha as a
    /// cutout rather than a blend, so the piece is either fully solid or discarded outright with
    /// nothing in between - 0.25 made it vanish, 0.5 left it opaque. Colour is the only thing
    /// that mechanism can change, and a solid recoloured piece is no easier to see past than the
    /// original.
    ///
    /// So the piece is hidden and a box drawn where it stood. The box is ours, so it can use a
    /// shader that does blend, and a quarter-opacity green reads as neither the piece nor the
    /// blue the hammer paints on whatever it is pointed at.
    ///
    /// It is a child of the piece rather than a free-standing object in world space, so it turns
    /// and moves with whatever it is marking, and it is sized from the piece's own bounds rather
    /// than an axis-aligned box round them - a wall at forty-five degrees would otherwise be
    /// marked by something half again its size.
    /// </remarks>
    internal static class EditGhostBox
    {
        private const string ChildName = "HoO_EditGhost";

        private static GameObject _box;

        internal static void Show(GameObject piece)
        {
            Hide();

            if (piece == null || !ModConfig.EditHidesPiece.Value)
            {
                return;
            }

            if (!LocalBounds(piece, out Bounds bounds))
            {
                return;
            }

            Material material = GizmoMaterial.Get();
            if (material == null)
            {
                // No usable shader; the tint on its own is better than nothing at all.
                return;
            }

            _box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _box.name = ChildName;

            // A primitive arrives with collision, which would put back exactly the obstruction
            // that standing the piece down was meant to remove.
            Object.Destroy(_box.GetComponent<Collider>());

            _box.transform.SetParent(piece.transform, false);
            _box.transform.localPosition = bounds.center;
            _box.transform.localRotation = Quaternion.identity;
            _box.transform.localScale = bounds.size;

            Renderer renderer = _box.GetComponent<Renderer>();
            renderer.material = new Material(material) { color = ModConfig.EditGhostBoxColor.Value };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        internal static void Hide()
        {
            if (_box != null)
            {
                Object.Destroy(_box);
                _box = null;
            }
        }

        /// <summary>Whether a renderer belongs to the marker rather than to the piece.</summary>
        internal static bool IsMarker(Renderer renderer)
        {
            return renderer != null && _box != null && renderer.gameObject == _box;
        }

        /// <summary>
        /// The piece's extent in its own space, from the meshes rather than from world bounds.
        /// </summary>
        private static bool LocalBounds(GameObject piece, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            Transform root = piece.transform;

            foreach (MeshFilter filter in piece.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Bounds local = mesh.bounds;

                // Every corner, not just the centre and the extents: a child rotated inside the
                // piece turns its box into something whose corners no longer line up with it.
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(
                        local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

                    Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(corner));

                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return any;
        }
    }
}
