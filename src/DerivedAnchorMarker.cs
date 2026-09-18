using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Identifies an anchor this mod added to a placement ghost.
    /// </summary>
    /// <remarks>
    /// Carries no data and does nothing. It exists so the bounds measurement can tell our
    /// anchors from the piece's own without inspecting names - vanilla shows a snap point's
    /// name on screen when cycling, so anything encoded there is user-visible.
    /// </remarks>
    internal sealed class DerivedAnchorMarker : MonoBehaviour
    {
    }
}
