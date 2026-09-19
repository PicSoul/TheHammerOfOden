using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Scales the distances a piece uses to decide things, alongside its geometry.
    /// </summary>
    /// <remarks>
    /// Resizing a piece changes its mesh, its colliders and its effects, but any plain float
    /// holding a radius keeps its original value - and those are what decide whether the
    /// piece thinks you are near it.
    ///
    /// A portal is the clear case. Its effect is not drawn continuously; TeleportWorld gates
    /// it on a player being within m_activationRange, five metres by default, of a proximity
    /// marker on the model:
    ///
    ///     Player closest = Player.GetClosestPlayer(m_proximityRoot.position, m_activationRange);
    ///     m_target_found.SetActive(closest != null &amp;&amp; TargetFound());
    ///
    /// Scale the portal and that marker moves with the model while the five metres does not,
    /// so where you have to stand to light the effect no longer has anything to do with where
    /// the portal is. Nothing about the particles was ever wrong here.
    ///
    /// The range grows by how far the piece's surface moved outward, not by the scale factor.
    /// Multiplying is the obvious choice and is far too much: you do not stand five times
    /// further from a large portal, you stand about as close to its face as you would to a
    /// small one, so only the extra half-width has to be covered. A five times portal wants
    /// something near ten metres, not twenty five.
    /// </remarks>
    internal static class ScaledRanges
    {
        internal static void Apply(GameObject piece, Vector3 scale)
        {
            if (piece == null)
            {
                return;
            }

            float factor = (scale.x + scale.y + scale.z) / 3f;
            if (factor <= 0f || Mathf.Approximately(factor, 1f))
            {
                return;
            }

            ApplyToPortal(piece, factor);
        }

        private static void ApplyToPortal(GameObject piece, float factor)
        {
            TeleportWorld portal = piece.GetComponentInChildren<TeleportWorld>(true);
            if (portal == null)
            {
                return;
            }

            // Scaling multiplies in place, and a piece passes through here again whenever its
            // zone reloads, so the change has to be recorded rather than reapplied.
            if (piece.GetComponent<ScaledRangeMarker>() != null)
            {
                return;
            }

            piece.AddComponent<ScaledRangeMarker>();

            float before = portal.m_activationRange;
            float extent = BaseExtentOf(piece);
            float growth = extent * (factor - 1f) * ModConfig.RangeGrowth.Value;

            portal.m_activationRange = Mathf.Max(before, before + growth);

            HammerOfOdenPlugin.Debug(
                $"Portal activation range {before:0.##}m -> {portal.m_activationRange:0.##}m "
                + $"(scale {factor:0.##}, base extent {extent:0.##}m).");
        }

        /// <summary>
        /// The piece's half-size at its normal scale, in metres.
        /// </summary>
        /// <remarks>
        /// Measured in the piece's own local space, which divides the transform scale back
        /// out and so gives the prefab's size without needing to look the prefab up. Particle
        /// renderers are skipped for the same reason they are everywhere else: a smoke plume
        /// is far larger than the thing emitting it.
        /// </remarks>
        private static float BaseExtentOf(GameObject piece)
        {
            Transform root = piece.transform;
            Bounds bounds = default(Bounds);
            bool any = false;

            foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
            {
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

            return any ? Mathf.Max(bounds.extents.magnitude, 0.25f) : 1f;
        }
    }

    /// <summary>Marks a piece whose ranges this mod has already adjusted.</summary>
    internal sealed class ScaledRangeMarker : MonoBehaviour
    {
    }
}
