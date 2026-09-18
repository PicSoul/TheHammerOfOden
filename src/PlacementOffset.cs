using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A manual nudge applied to the ghost after vanilla has positioned it.
    /// </summary>
    /// <remarks>
    /// Vanilla places the ghost exactly where the aiming ray strikes a surface, so a piece
    /// can be put against something but never inside it. No amount of relaxing the placement
    /// rules changes that: the position was never inside the other object to begin with.
    ///
    /// Pushing along the camera's forward axis is what "clip this into that" actually needs,
    /// and it is the same axis the crosshair already implies, so the control reads the way
    /// the movement looks.
    ///
    /// Held on both axis modifiers at once, which was a free combination - pitch and roll
    /// are each a single key, and holding both meant nothing until now.
    /// </remarks>
    internal static class PlacementOffset
    {
        private static float _depth;

        internal static bool IsOffset => Mathf.Abs(_depth) > 0.0001f;

        internal static float Depth => _depth;

        internal static void Adjust(int sign)
        {
            _depth += sign * ModConfig.OffsetStep.Value;

            float limit = ModConfig.OffsetLimit.Value;
            _depth = Mathf.Clamp(_depth, -limit, limit);
        }

        internal static void Reset()
        {
            _depth = 0f;
        }

        internal static void Apply(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || !IsOffset || ghost == null || !ghost.activeSelf)
            {
                return;
            }

            Camera camera = MainCamera.Get();
            if (camera == null)
            {
                return;
            }

            ghost.transform.position += camera.transform.forward * _depth;
        }
    }
}
