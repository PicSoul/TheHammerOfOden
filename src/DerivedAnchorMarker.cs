using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Identifies an anchor this mod added to a placement ghost, and what sort it is.
    /// </summary>
    /// <remarks>
    /// The kind is held here rather than encoded in the name, because vanilla puts a snap
    /// point's name on screen when cycling - anything stored there is user-visible.
    /// </remarks>
    internal sealed class DerivedAnchorMarker : MonoBehaviour
    {
        internal AnchorKind Kind = AnchorKind.Vanilla;
    }
}
