using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Makes the piece being edited see-through, without losing its shape.
    /// </summary>
    /// <remarks>
    /// Three attempts, and the first two each failed for their own reason. Tinting the piece
    /// cannot fade it: Valheim's piece shader treats alpha as a cutout, so the piece is either
    /// fully solid or discarded outright with nothing in between. Hiding it and drawing a box
    /// where it stood did fade, and threw away the thing the box was there to help with - you
    /// cannot line a piece up to the nearest few centimetres against a rectangle that is only
    /// roughly the right size.
    ///
    /// What was wrong both times was the shader, not the geometry. So the geometry stays exactly
    /// as it is and the materials are swapped for one that does blend. Every plank, every bevel,
    /// every snap-worthy edge is still there to line up against, and all of it is see-through.
    ///
    /// The originals are put back per renderer. Sharing one replacement material across all of
    /// them would be cheaper and would leave every piece of that kind green the moment anything
    /// touched the shared asset, which is the same trap the mesh copies avoid.
    /// </remarks>
    internal static class EditGhostMaterial
    {
        private static readonly Dictionary<Renderer, Material[]> Original =
            new Dictionary<Renderer, Material[]>();

        private static Material _ghost;

        internal static void Apply(GameObject piece)
        {
            Restore();

            if (piece == null || !ModConfig.EditHidesPiece.Value)
            {
                return;
            }

            Material source = GizmoMaterial.GetSeeThrough();
            if (source == null)
            {
                // No shader that blends; the tint on its own is better than nothing.
                return;
            }

            if (_ghost == null)
            {
                _ghost = new Material(source) { renderQueue = 3000 };

                // Depth writing off, so the far side of the piece shows through the near side
                // rather than the first surface drawn hiding everything behind it.
                if (_ghost.HasProperty("_ZWrite"))
                {
                    _ghost.SetInt("_ZWrite", 0);
                }
            }

            _ghost.color = ModConfig.EditGhostBoxColor.Value;

            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || Original.ContainsKey(renderer))
                {
                    continue;
                }

                Original[renderer] = renderer.sharedMaterials;

                Material[] swapped = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < swapped.Length; i++)
                {
                    swapped[i] = _ghost;
                }

                renderer.sharedMaterials = swapped;
            }
        }

        internal static void Restore()
        {
            foreach (KeyValuePair<Renderer, Material[]> entry in Original)
            {
                if (entry.Key != null)
                {
                    entry.Key.sharedMaterials = entry.Value;
                }
            }

            Original.Clear();
        }
    }
}
