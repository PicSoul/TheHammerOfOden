using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// How far the piece in hand is bent, and which way.
    /// </summary>
    /// <remarks>
    /// Held modifier plus the wheel, the way scaling uses a held modifier plus keys. A mode you
    /// toggle would have been fewer keystrokes and is how you end up bending something you meant
    /// to rotate: the wheel already means four different things depending on what is held, and a
    /// fifth that persists invisibly is one too many.
    ///
    /// The ceiling is half a circle. Past that an arc curls back through itself, and the rise
    /// stops being worth the turn well before - a half circle already lifts a beam by 0.318 of
    /// its own length, with its ends pointing straight up, which is an arch by any reading.
    /// </remarks>
    internal static class BendState
    {
        private static float _degrees;
        private static int _axis = 0;

        internal static float Degrees => _degrees;

        internal static int Axis => _axis;

        internal static bool IsBent => Mathf.Abs(_degrees) >= 0.01f;

        internal static string AxisName =>
            _axis == 0 ? "X (length)" : _axis == 1 ? "Y (height)" : "Z (depth)";

        /// <summary>Turns wheel movement into bend, while the modifier is held.</summary>
        /// <returns>True if anything changed, so the caller can swallow the wheel.</returns>
        internal static bool HandleScroll(Player player, float scroll)
        {
            if (Mathf.Approximately(scroll, 0f))
            {
                return false;
            }

            float step = Mathf.Max(1f, ModConfig.BendStep.Value);
            float limit = Mathf.Clamp(ModConfig.BendMaximum.Value, 0f, 180f);

            float wanted = Mathf.Clamp(_degrees + Mathf.Sign(scroll) * step, 0f, limit);
            if (Mathf.Approximately(wanted, _degrees))
            {
                return true;
            }

            _degrees = wanted;

            Notify.Show(player, IsBent
                ? $"Bend {_degrees:0}° on {AxisName}"
                : "Bend off");

            return true;
        }

        internal static void CycleAxis(Player player)
        {
            _axis = (_axis + 1) % 3;
            Notify.Show(player, "Bend axis: " + AxisName);
        }

        internal static void Reset(Player player, bool announce)
        {
            if (!IsBent && _axis == 0)
            {
                return;
            }

            _degrees = 0f;
            _axis = 0;
            BendDeformer.Release();

            if (announce && player != null)
            {
                Notify.Show(player, "Bend reset");
            }
        }

        /// <summary>
        /// Curves the ghost, or leaves it alone when the piece is not one that may bend.
        /// </summary>
        internal static void ApplyTo(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || ghost == null || !IsBent)
            {
                BendDeformer.Apply(ghost, 0f, _axis);
                return;
            }

            if (!Bendable.Allows(ghost))
            {
                return;
            }

            BendDeformer.Apply(ghost, _degrees, _axis);
        }

        /// <summary>Lets go of the ghost's meshes when the ghost goes.</summary>
        internal static void ForgetGhost()
        {
            BendDeformer.Release();
        }
    }
}
