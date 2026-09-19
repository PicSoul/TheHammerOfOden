using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Marks a particle system this mod has already resized.
    /// </summary>
    /// <remarks>
    /// Scaling multiplies values in place, so applying it twice squares the factor. A piece
    /// reaches the scaling code once when placed and again each time its zone reloads it,
    /// which without this would compound every visit.
    /// </remarks>
    internal sealed class ScaledParticleMarker : MonoBehaviour
    {
    }
}
