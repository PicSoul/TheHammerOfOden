using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Remembers which pair of snap points vanilla last chose to snap together.
    /// </summary>
    /// <remarks>
    /// With automatic snapping nothing is "selected", so there is no obvious point to
    /// highlight - yet the pair the game just decided on is the single most useful thing to
    /// show. Vanilla works it out in FindClosestSnapPoints and then throws it away, so we
    /// note it as it goes past.
    ///
    /// Cleared each frame before the placement update, so a stale pair is never drawn after
    /// snapping stops.
    /// </remarks>
    internal static class ActiveSnapPair
    {
        internal static Transform Source { get; private set; }
        internal static Transform Target { get; private set; }

        internal static bool HasPair => Source != null && Target != null;

        internal static void Record(Transform source, Transform target)
        {
            Source = source;
            Target = target;
        }

        internal static void Clear()
        {
            Source = null;
            Target = null;
        }

        internal static bool IsSource(Transform t) => t != null && t == Source;

        internal static bool IsTarget(Transform t) => t != null && t == Target;
    }
}
