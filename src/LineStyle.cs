using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Applies colour and width to a LineRenderer only when they actually change.
    /// </summary>
    /// <remarks>
    /// Moving a LineRenderer is cheap - a transform change costs nothing extra - but writing
    /// startColor, endColor or widthMultiplier marks its mesh dirty and forces a rebuild on
    /// the next render, whether or not the value differs from what is already there.
    ///
    /// The markers and rings reassign all three every frame while their values change only
    /// when the selected axis or snap point does, so nearly every one of those rebuilds was
    /// spent writing a value back over itself. Remembering the last applied values turns the
    /// common frame into a pair of comparisons.
    /// </remarks>
    internal static class LineStyle
    {
        private struct Applied
        {
            internal Color Color;
            internal float Width;
        }

        private static readonly Dictionary<int, Applied> Last = new Dictionary<int, Applied>();

        internal static void Apply(LineRenderer line, Color color, float width)
        {
            int id = line.GetInstanceID();

            if (Last.TryGetValue(id, out Applied previous)
                && previous.Width == width
                && previous.Color == color)
            {
                return;
            }

            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;

            Last[id] = new Applied { Color = color, Width = width };
        }

        /// <summary>Forget everything, for when the renderers themselves are destroyed.</summary>
        internal static void Clear()
        {
            Last.Clear();
        }
    }
}
