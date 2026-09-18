using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Restores a scaled piece's size when it comes back into the world.
    /// </summary>
    /// <remarks>
    /// Valheim stores scale in the ZDO, but only reads it back when the object's ZNetView has
    /// m_syncInitialScale set:
    ///
    ///     if (m_syncInitialScale) { transform.localScale = m_zdo.GetVec3(s_scaleHash, ...); }
    ///
    /// That flag lives on the component, and the component comes from the prefab. Setting it
    /// on a piece as it is placed is enough to write the scale, but when the zone unloads and
    /// the piece respawns the new instance reads the flag from the prefab again, where it is
    /// false - so the restore is skipped and the piece returns at its original size. The
    /// value was in the ZDO the whole time; nothing was reading it.
    ///
    /// Reading it ourselves on every spawn fixes it, and setting the flag afterwards means
    /// any later change syncs the way vanilla intends.
    ///
    /// This runs once per networked object as it spawns, not per frame. The early exit is a
    /// single ZDO lookup, which is what a zone load can afford.
    /// </remarks>
    internal static class ScalePersistence
    {
        internal static void Restore(ZNetView view)
        {
            if (view == null || view.m_syncInitialScale)
            {
                // Already handled by vanilla.
                return;
            }

            ZDO zdo = view.GetZDO();
            if (zdo == null)
            {
                return;
            }

            Vector3 scale = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
            if (scale == Vector3.zero)
            {
                return;
            }

            view.transform.localScale = scale;

            // So vanilla keeps it in step from here, including for other players.
            view.m_syncInitialScale = true;

            // Restoring the transform is not enough: particle systems keep their own scaling
            // mode, and a piece rebuilt from the prefab comes back with the original. Without
            // this a piece is correct when built and wrong again after the zone reloads.
            ScaleState.ScaleParticles(view.gameObject, scale);

            ParticleDiagnostics.AttachTo(view.gameObject);
        }
    }
}
