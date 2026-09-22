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
    ///
    /// The angle runs both ways from straight. The direction key picks which axis the piece
    /// curves towards; the sign picks which way along it. Those are different questions, and
    /// with only one of them a beam could arch upwards but never sag, and a wall could wrap one
    /// way round a tower but not the other. Together they reach all four.
    /// </remarks>
    internal static class BendState
    {
        private static float _degrees;
        private static int _rise = -1;

        private static float _pendingDegrees;
        private static int _pendingAxis;
        private static int _pendingRise;
        private static int _pendingChoice;

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

            string way = _degrees >= 0f ? "+" : "-";
            return $"{_degrees:0}° along {Name(axis)} ({size[axis]:0.#}m), curving {way}{Name(rise)}";
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

            // Both ways from straight. The fold-through ceiling is a property of the curve's
            // radius, which does not care which way it curves, so the same figure bounds both.
            float wanted = Mathf.Clamp(_degrees + Mathf.Sign(scroll) * step, -limit, limit);

            if (Mathf.Approximately(wanted, _degrees))
            {
                if (Mathf.Abs(_degrees) >= limit - 0.01f)
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
                _degrees = Mathf.Clamp(_degrees, -safe, safe);
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

        /// <summary>
        /// Carries the bend onto the piece that actually gets built.
        /// </summary>
        /// <remarks>
        /// PlacePiece builds from the prefab rather than from the ghost, so the curve on screen
        /// is not inherited and has to be applied again to the new object - the same reason the
        /// scale has to be. The axes are worked out from the ghost while it still exists, since
        /// they are a measurement of the piece and the fresh instance would give the same answer
        /// at more cost.
        /// </remarks>
        internal static void Remember(GameObject ghost)
        {
            _pendingDegrees = 0f;

            if (!IsBent || ghost == null || !Bendable.Allows(ghost))
            {
                return;
            }

            // Taken now, while the ghost is still there to measure. By the time the built piece
            // exists the ghost has been rebuilt, and the axes are a measurement of the thing
            // that was in hand rather than a setting that can be looked up afterwards.
            _pendingDegrees = _degrees;
            _pendingAxis = AxisFor(ghost);
            _pendingRise = RiseFor(ghost);
            _pendingChoice = _rise <= 0 ? 0 : 1;
        }

        internal static void ApplyToPlaced(Piece piece)
        {
            if (piece == null || Mathf.Abs(_pendingDegrees) < 0.01f)
            {
                return;
            }

            BentPiece.Attach(piece.gameObject, _pendingDegrees, _pendingAxis, _pendingRise, _pendingChoice);
        }

        /// <summary>
        /// Takes the bend from a piece already standing in the world.
        /// </summary>
        /// <remarks>
        /// Read out of the piece's record rather than measured off it. Measuring would mean
        /// handing the built piece to the ghost's deformer, which takes ownership of the meshes
        /// it is given - and those meshes belong to a piece that is standing there using them.
        ///
        /// The direction is stored as the choice the controls hold rather than as the axis it
        /// resolved to, so copying it needs no measurement at all: the same key means the same
        /// thing on the copy as it did on the original.
        /// </remarks>
        internal static void MatchPiece(Piece piece)
        {
            if (piece == null || !ModConfig.CopyBendOnPieceCopy.Value)
            {
                return;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            ZDO zdo = view.GetZDO();
            float degrees = zdo.GetFloat(BentPiece.DegreesKey, 0f);

            _degrees = Mathf.Abs(degrees) < 0.01f ? 0f : degrees;
            _rise = zdo.GetInt(BentPiece.ChoiceKey, 0);

            // The ghost in hand is a different object from the one copied, so whatever the
            // deformer is holding is no longer what should be curved.
            BendDeformer.Release();
        }

        /// <summary>Lets go of the ghost's meshes when the ghost goes.</summary>
        internal static void ForgetGhost()
        {
            BendDeformer.Release();
        }
    }
}
