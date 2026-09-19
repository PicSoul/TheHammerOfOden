using UnityEngine;

namespace TheHammerOfOden
{
    internal enum NudgeFrameMode
    {
        /// <summary>
        /// Steps run along the world's own axes, whichever way you happen to be facing.
        /// </summary>
        /// <remarks>
        /// Your facing still picks which axis "forward" means - the nearest one to where you
        /// are looking - but the step itself is along that axis exactly. Every nudge from
        /// anywhere therefore lands on the same lattice, which is what makes a piece line up
        /// with the one beside it.
        /// </remarks>
        World = 0,

        /// <summary>
        /// Steps run along your line of sight, at whatever angle you are standing at.
        /// </summary>
        /// <remarks>
        /// Useful for pushing a piece straight away from you when nothing needs to line up.
        /// The cost is that turning between presses changes what the next one does, so a
        /// sequence of nudges taken while moving traces a zigzag rather than a straight line.
        /// </remarks>
        Camera = 1
    }

    internal enum NudgeAxis
    {
        /// <summary>Away from you and back, along the ground.</summary>
        Forward = 0,

        /// <summary>Left and right, along the ground.</summary>
        Lateral = 1,

        /// <summary>Up and down.</summary>
        Vertical = 2
    }

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
    ///
    /// The nudge is a second, separate offset and works differently on purpose. The depth
    /// above is stored as a distance along whatever you are currently looking at, so turning
    /// on the spot swings the piece with you - right for "push it that way", wrong for
    /// "leave it exactly there". The nudge is accumulated as a world vector at the moment
    /// each key is pressed: once you have moved a piece 20cm left it stays 20cm left,
    /// whichever way you then turn. That is what makes it usable together with freezing,
    /// where you are expected to walk around and look from elsewhere.
    ///
    /// Accumulating in world space is not on its own enough, because the direction of each
    /// new step still has to come from somewhere, and taking it raw from the camera makes
    /// the feature behave differently depending on which way you happen to be standing:
    /// face near a world axis and the steps land on a tidy lattice, face at forty degrees
    /// and they march off at forty degrees. Under NudgeFrameMode.World the camera only
    /// chooses which axis is meant and the step runs along that axis exactly, so nudges from
    /// anywhere in the world agree with each other. That is the whole point of nudging - a
    /// piece that lines up with the one next to it - so it is the default.
    /// </remarks>
    internal static class PlacementOffset
    {
        private static float _depth;
        private static Vector3 _nudge;

        internal static bool IsOffset => Mathf.Abs(_depth) > 0.0001f || _nudge.sqrMagnitude > 1e-8f;

        internal static float Depth => _depth;

        internal static Vector3 Nudge => _nudge;

        internal static void Adjust(int sign)
        {
            _depth += sign * ModConfig.OffsetStep.Value;

            float limit = ModConfig.OffsetLimit.Value;
            _depth = Mathf.Clamp(_depth, -limit, limit);
        }

        /// <summary>
        /// Moves the piece one step along an axis of the frame you are facing.
        /// </summary>
        /// <remarks>
        /// Forward and right are flattened onto the horizontal plane, because looking down
        /// at the floor while nudging "forward" should move the piece away from you rather
        /// than into the ground. Up is world up for the same reason.
        /// </remarks>
        internal static void NudgeBy(NudgeAxis axis, int sign, bool large)
        {
            Camera camera = MainCamera.Get();
            if (camera == null)
            {
                return;
            }

            Vector3 forward = Flatten(camera.transform.forward);

            if (ModConfig.NudgeFrame.Value == NudgeFrameMode.World)
            {
                forward = NearestWorldAxis(forward);
            }

            if (forward.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector3 direction;

            switch (axis)
            {
                case NudgeAxis.Vertical:
                    direction = Vector3.up;
                    break;
                case NudgeAxis.Lateral:
                    // Derived from forward rather than read off the camera, so the two stay
                    // square to each other once forward has been quantised.
                    direction = Vector3.Cross(Vector3.up, forward);
                    break;
                default:
                    direction = forward;
                    break;
            }

            float step = large ? ModConfig.NudgeStepLarge.Value : ModConfig.NudgeStep.Value;
            _nudge += direction * (sign * step);

            float limit = ModConfig.OffsetLimit.Value;
            _nudge = Vector3.ClampMagnitude(_nudge, limit);
        }

        /// <summary>Straight down the horizontal plane, so "forward" never means "into the floor".</summary>
        private static Vector3 Flatten(Vector3 direction)
        {
            direction.y = 0f;
            return direction.normalized;
        }

        /// <summary>Whichever of north, south, east or west you are most nearly facing.</summary>
        private static Vector3 NearestWorldAxis(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            {
                return new Vector3(Mathf.Sign(direction.x), 0f, 0f);
            }

            return new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }

        internal static void ResetNudge()
        {
            _nudge = Vector3.zero;
        }

        internal static void Reset()
        {
            _depth = 0f;
            _nudge = Vector3.zero;
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

            ghost.transform.position += camera.transform.forward * _depth + _nudge;
        }
    }
}
