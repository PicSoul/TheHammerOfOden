using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The scale applied to the piece being placed.
    /// </summary>
    /// <remarks>
    /// Held as a multiplier of the prefab's own scale rather than an absolute value. Some
    /// pieces do not ship at a scale of one, and overwriting localScale outright would
    /// resize them the moment the feature was touched.
    ///
    /// Valheim can persist this: ZNetView has m_syncInitialScale, which writes localScale to
    /// the ZDO and restores it on load, so a scaled piece survives a reload and is seen by
    /// other players. Build pieces ship with the flag off, so it is enabled on the placed
    /// instance rather than on the prefab.
    /// </remarks>
    internal static class ScaleState
    {
        private static Vector3 _scale = Vector3.one;
        private static Vector3 _baseScale = Vector3.one;
        private static GameObject _baseFor;

        internal static Vector3 Multiplier => _scale;

        internal static bool IsScaled =>
            Mathf.Abs(_scale.x - 1f) > 0.0001f
            || Mathf.Abs(_scale.y - 1f) > 0.0001f
            || Mathf.Abs(_scale.z - 1f) > 0.0001f;

        /// <summary>Largest axis, for sizing things that sit around the piece.</summary>
        internal static float LargestAxis => Mathf.Max(_scale.x, Mathf.Max(_scale.y, _scale.z));

        internal static void Stretch(int axis, int sign)
        {
            float step = 1f + ModConfig.ScaleStep.Value * sign;

            switch (axis)
            {
                case 0:
                    _scale.x = Clamp(_scale.x * step);
                    break;
                case 1:
                    _scale.y = Clamp(_scale.y * step);
                    break;
                default:
                    _scale.z = Clamp(_scale.z * step);
                    break;
            }
        }

        internal static void Uniform(int sign)
        {
            float step = 1f + ModConfig.ScaleStep.Value * sign;

            _scale.x = Clamp(_scale.x * step);
            _scale.y = Clamp(_scale.y * step);
            _scale.z = Clamp(_scale.z * step);
        }

        internal static void Reset()
        {
            _scale = Vector3.one;
        }

        /// <summary>
        /// Adopt the scale of an already-placed piece.
        /// </summary>
        /// <remarks>
        /// A placed piece's localScale is the prefab's own scale multiplied by whatever was
        /// applied when it was built, and only the multiplier is meaningful to carry over -
        /// the prefab's part is reapplied automatically when the new ghost is made. So the
        /// prefab is looked up to divide it back out; taking localScale at face value would
        /// square the prefab's scale on any piece that does not ship at one.
        /// </remarks>
        internal static void MatchPiece(Piece piece)
        {
            if (piece == null || !ModConfig.CopyScaleOnPieceCopy.Value)
            {
                return;
            }

            if (!Scalable.Allows(piece.gameObject))
            {
                Reset();
                return;
            }

            Vector3 placed = piece.transform.localScale;
            Vector3 prefab = PrefabScaleOf(piece);

            _scale = new Vector3(
                Clamp(SafeDivide(placed.x, prefab.x)),
                Clamp(SafeDivide(placed.y, prefab.y)),
                Clamp(SafeDivide(placed.z, prefab.z)));

            HammerOfOdenPlugin.Debug($"Copied scale {_scale} from '{piece.name}'.");
        }

        private static Vector3 PrefabScaleOf(Piece piece)
        {
            if (ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(Utils.GetPrefabName(piece.gameObject));
                if (prefab != null)
                {
                    return prefab.transform.localScale;
                }
            }

            return Vector3.one;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return (Mathf.Abs(divisor) < 0.0001f) ? 1f : value / divisor;
        }

        /// <summary>
        /// Apply to the ghost, relative to whatever scale the prefab itself uses.
        /// </summary>
        internal static void ApplyTo(GameObject ghost)
        {
            if (ghost == null)
            {
                return;
            }

            // The prefab's own scale, captured before we have touched it.
            if (_baseFor != ghost)
            {
                _baseFor = ghost;
                _baseScale = ghost.transform.localScale;
            }

            Vector3 wanted = new Vector3(
                _baseScale.x * _scale.x,
                _baseScale.y * _scale.y,
                _baseScale.z * _scale.z);

            if (ghost.transform.localScale != wanted)
            {
                ghost.transform.localScale = wanted;
            }
        }

        /// <summary>Scale a freshly placed piece, and make the change persist.</summary>
        internal static void ApplyToPlaced(Piece piece)
        {
            if (piece == null || !IsScaled || !Scalable.Allows(piece.gameObject))
            {
                return;
            }

            Vector3 wanted = new Vector3(
                piece.transform.localScale.x * _scale.x,
                piece.transform.localScale.y * _scale.y,
                piece.transform.localScale.z * _scale.z);

            ZNetView view = piece.GetComponent<ZNetView>();

            if (view == null)
            {
                piece.transform.localScale = wanted;
                return;
            }

            // Build pieces ship with this off, so scale would neither save nor reach other
            // players. Setting it before SetLocalScale is what makes the change stick.
            view.m_syncInitialScale = true;
            view.SetLocalScale(wanted);

            ScaleParticles(piece.gameObject, wanted);
            ParticleDiagnostics.AttachTo(piece.gameObject);

            HammerOfOdenPlugin.Debug($"Placed '{piece.name}' at scale {_scale}.");
        }

        /// <summary>
        /// Bring a piece's particle effects up to size with it.
        /// </summary>
        /// <remarks>
        /// Draws the particles larger without enlarging the space they move through.
        ///
        /// The obvious approach - setting scalingMode to Hierarchy - was wrong, and the
        /// diagnostics showed why. Hierarchy multiplies velocity and emission volume as well
        /// as size, so a portal at three times scale threw its particles across fourteen
        /// metres. From a distance that still reads as an effect; up close you are standing
        /// inside a nearly empty cloud with the particles streaming past behind the camera,
        /// which looks exactly like the effect having vanished. Nothing was being culled and
        /// the bounds were correct the whole time.
        ///
        /// Leaving the simulation in local space and raising only the size multiplier keeps
        /// the effect where the artist put it, just drawn bigger.
        /// </remarks>
        internal static void ScaleParticles(GameObject piece, Vector3 scale)
        {
            if (piece == null || !ModConfig.ScaleParticles.Value)
            {
                return;
            }

            float factor = (scale.x + scale.y + scale.z) / 3f;
            if (factor <= 0f)
            {
                return;
            }

            foreach (ParticleSystem system in piece.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;

                // Explicitly local: whatever the prefab said, the simulation must not inherit
                // the piece's scale or the effect spreads instead of growing.
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.startSizeMultiplier *= factor;
            }
        }

        internal static void ForgetGhost()
        {
            _baseFor = null;
            _baseScale = Vector3.one;
        }

        private static float Clamp(float value)
        {
            return Mathf.Clamp(value, ModConfig.ScaleMin.Value, CurrentMax());
        }

        /// <summary>
        /// The largest scale the piece in hand can take.
        /// </summary>
        /// <remarks>
        /// Pieces with particle effects stop short, because their effects fail before their
        /// geometry does - a portal's flames start disappearing near the camera at about
        /// double and have gone entirely by five times. Clamping is not elegant, but it is
        /// honest: the alternative is letting people build something that looks broken.
        /// </remarks>
        private static float CurrentMax()
        {
            if (_baseFor != null && Scalable.HasParticles(_baseFor))
            {
                return Mathf.Min(ModConfig.ScaleMax.Value, ModConfig.ScaleMaxWithParticles.Value);
            }

            return ModConfig.ScaleMax.Value;
        }
    }
}
