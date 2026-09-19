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
            if (!ModConfig.IsEnabled || ghost == null)
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
            ScaledRanges.Apply(piece.gameObject, _scale);
            ParticleDiagnostics.AttachTo(piece.gameObject);

            HammerOfOdenPlugin.Debug($"Placed '{piece.name}' at scale {_scale}.");
        }

        /// <summary>
        /// Resize a piece's particle effects along with it.
        /// </summary>
        /// <remarks>
        /// An effect has three things that must scale together, and getting one without the
        /// others is what made earlier attempts look wrong in different ways:
        ///
        ///   particle size      or the effect is the wrong weight
        ///   emission shape     or it covers the wrong footprint - a half-size hearth kept a
        ///                      three metre wide box and its fire spilled outside the model
        ///   start speed        or particles travel their original distance and the effect
        ///                      spreads past a shrunken piece, or falls short of a grown one
        ///
        /// scalingMode is deliberately left Local and the values changed directly. Hierarchy
        /// would let Unity apply the transform scale on top of ours, and it also multiplies
        /// velocity by the full parent scale, which threw a three times portal's particles
        /// fourteen metres.
        /// </remarks>
        internal static void ScaleParticles(GameObject piece, Vector3 scale)
        {
            if (piece == null || !ModConfig.ScaleParticles.Value)
            {
                return;
            }

            float factor = (scale.x + scale.y + scale.z) / 3f;
            if (factor <= 0f || Mathf.Approximately(factor, 1f))
            {
                return;
            }

            foreach (ParticleSystem system in piece.GetComponentsInChildren<ParticleSystem>(true))
            {
                // Scaling an already-scaled system compounds, and a piece can pass through
                // here twice: once when placed and again when its zone reloads it.
                if (system.GetComponent<ScaledParticleMarker>() != null)
                {
                    continue;
                }

                system.gameObject.AddComponent<ScaledParticleMarker>();

                ParticleSystem.MainModule main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.startSize = Scaled(main.startSize, factor);
                main.startSpeed = Scaled(main.startSpeed, factor);
                main.gravityModifier = Scaled(main.gravityModifier, factor);

                ParticleSystem.ShapeModule shape = system.shape;
                if (shape.enabled)
                {
                    // scale only, never radius. Unity multiplies the two together, so scaling
                    // both squares the result: a three times portal took a nine times effect
                    // and a five times one took twenty five, which is spread thin enough to
                    // look like nothing at all. Box shapes hid this, because they take their
                    // dimensions from scale and ignore radius entirely - which is why the
                    // hearth looked right while every portal did not.
                    shape.scale *= factor;
                    shape.position *= factor;
                }

                ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
                if (velocity.enabled)
                {
                    velocity.x = Scaled(velocity.x, factor);
                    velocity.y = Scaled(velocity.y, factor);
                    velocity.z = Scaled(velocity.z, factor);
                }
            }
        }

        /// <summary>
        /// Multiply a particle value, whichever form it takes.
        /// </summary>
        /// <remarks>
        /// The obvious startSizeMultiplier only touches constantMax on a two-constant range,
        /// so a size of 0.4 to 0.5 halved became 0.4 to 0.25 - a range with its ends the
        /// wrong way round. Every value here has to be handled by mode.
        /// </remarks>
        private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve curve, float factor)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(curve.constant * factor);

                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(
                        curve.constantMin * factor,
                        curve.constantMax * factor);

                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(curve.curveMultiplier * factor, curve.curve);

                default:
                    return new ParticleSystem.MinMaxCurve(
                        curve.curveMultiplier * factor,
                        curve.curveMin,
                        curve.curveMax);
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
