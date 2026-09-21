using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// How far the piece in hand is bent, and which way.
    /// </summary>
    /// <remarks>
    /// Held modifier plus the wheel, the way scaling uses a held modifier plus keys. A mode you
    /// toggle would have been fewer keystrokes and is how you end up bending something you meant
    /// to rotate: the wheel already means four things depending on what is held, and a fifth that
    /// persists invisibly is one too many.
    ///
    /// The piece is always bent along its longest axis, and that is measured rather than chosen.
    /// Offering x, y and z as equals was the first attempt and it was wrong in a way that only
    /// looked like a broken curve: a log pole runs along its own y and is a fifth of a metre
    /// across x, so bending it on x wrapped it round a radius of a tenth of a metre and tied it
    /// in a knot. Nobody wants to bend a pole across its thickness. There was never a real
    /// choice there.
    ///
    /// The choice that does exist is which way it curves, and there are exactly two: towards the
    /// piece's thickness, or towards its width. For a wall those are a round tower wall and an
    /// arch respectively, and both are things people build. Thinner first, because that is the
    /// one that bends furthest before the inside of the curve meets itself.
    /// </remarks>
    internal static class BendState
    {
        private static float _degrees;
        private static int _rise = -1;

        internal static float Degrees => _degrees;

        internal static bool IsBent => Mathf.Abs(_degrees) >= 0.01f;

        /// <summary>Which axis runs along the piece's length. Measured, never chosen.</summary>
        internal static int AxisFor(GameObject piece)
        {
            return BendDeformer.LongestAxis(piece);
        }

        /// <summary>
        /// Which way the piece curves: one of the two axes that are not its length.
        /// </summary>
        /// <remarks>
        /// Held as "the thinner one" or "the other one" rather than as x, y or z, so that turning
        /// a wall on its side does not silently change what the same keypress does.
        /// </remarks>
        internal static int RiseFor(GameObject piece)
        {
            int axis = AxisFor(piece);
            Vector3 size = BendDeformer.Size(piece);

            int first = (axis + 1) % 3;
            int second = (axis + 2) % 3;

            // Thinner first: it is the direction that bends furthest before folding through.
            if (size[second] < size[first])
            {
                int swap = first;
                first = second;
                second = swap;
            }

            return _rise <= 0 ? first : second;
        }

        internal static string Describe(GameObject piece)
        {
            if (piece == null)
            {
                return string.Empty;
            }

            int axis = AxisFor(piece);
            int rise = RiseFor(piece);
            Vector3 size = BendDeformer.Size(piece);

            return $"{_degrees:0}° along {Name(axis)} ({size[axis]:0.#}m), curving {Name(rise)}";
        }

        private static string Name(int axis)
        {
            return axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
        }

        /// <summary>Turns wheel movement into bend, while the modifier is held.</summary>
        /// <returns>True if the wheel was claimed, so rotation leaves it alone.</returns>
        internal static bool HandleScroll(Player player, GameObject ghost, float scroll)
        {
            if (Mathf.Approximately(scroll, 0f) || ghost == null)
            {
                return false;
            }

            if (!Bendable.Allows(ghost))
            {
                Notify.Show(player, "Cannot bend this: " + Bendable.Reason(ghost));
                return true;
            }

            float step = Mathf.Max(1f, ModConfig.BendStep.Value);

            // Two ceilings, and the tighter one wins. One is taste, set in the config; the other
            // is the point where the inside of the curve would pass through itself, which is a
            // property of this piece's own proportions and is not negotiable.
            float safe = BendDeformer.MaximumDegrees(ghost, AxisFor(ghost), RiseFor(ghost));
            float limit = Mathf.Min(Mathf.Clamp(ModConfig.BendMaximum.Value, 0f, 180f), safe);

            float wanted = Mathf.Clamp(_degrees + Mathf.Sign(scroll) * step, 0f, limit);

            if (Mathf.Approximately(wanted, _degrees))
            {
                if (scroll > 0f && _degrees >= limit - 0.01f)
                {
                    Notify.Show(player, $"Bend at its limit for this piece ({limit:0}°)");
                }

                return true;
            }

            _degrees = wanted;

            Notify.Show(player, IsBent ? "Bend " + Describe(ghost) : "Bend off");
            return true;
        }

        /// <summary>Swaps which of the two remaining axes the piece curves towards.</summary>
        internal static void CycleRise(Player player, GameObject ghost)
        {
            _rise = _rise <= 0 ? 1 : 0;

            // The other direction may not reach as far, so the current bend has to fit it.
            if (ghost != null && IsBent)
            {
                float safe = BendDeformer.MaximumDegrees(ghost, AxisFor(ghost), RiseFor(ghost));
                _degrees = Mathf.Min(_degrees, safe);
            }

            Notify.Show(player, ghost != null ? "Bend " + Describe(ghost) : "Bend direction swapped");
        }

        internal static void Reset(Player player, bool announce)
        {
            if (!IsBent && _rise <= 0)
            {
                return;
            }

            _degrees = 0f;
            _rise = -1;
            BendDeformer.Release();

            if (announce && player != null)
            {
                Notify.Show(player, "Bend reset");
            }
        }

        /// <summary>Curves the ghost, or leaves it alone when the piece may not bend.</summary>
        internal static void ApplyTo(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || ghost == null || !IsBent)
            {
                BendDeformer.Apply(ghost, 0f, 0, 1);
                return;
            }

            if (!Bendable.Allows(ghost))
            {
                return;
            }

            BendDeformer.Apply(ghost, _degrees, AxisFor(ghost), RiseFor(ghost));
        }

        /// <summary>Lets go of the ghost's meshes when the ghost goes.</summary>
        internal static void ForgetGhost()
        {
            BendDeformer.Release();
        }
    }
}
