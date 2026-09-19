using System.Text;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Reports what a scaled piece's particle effects are actually doing.
    /// </summary>
    /// <remarks>
    /// Attached only to scaled pieces that have effects, and only while DebugParticles is on.
    ///
    /// The point is to tell four candidate causes apart, because each has a different fix and
    /// guessing between them has already cost two failed attempts:
    ///
    ///   isVisible goes false while particles should be on screen
    ///       the renderer's bounds are not keeping pace with the scaled system, and it is
    ///       being frustum culled on stale bounds
    ///
    ///   isVisible stays true and particles are still being emitted
    ///       nothing is culled, so the effect is fading rather than disappearing - a soft
    ///       particle depth fade against the geometry it now intersects
    ///
    ///   particle count drops to zero
    ///       emission itself has stopped, which points at the system rather than the renderer
    ///
    ///   bounds size stays at its unscaled value
    ///       confirms the bounds are the problem without needing to catch the moment
    ///
    /// Visibility changes are logged as they happen; the rest is summarised once a second so
    /// the log stays readable.
    /// </remarks>
    internal sealed class ParticleDiagnostics : MonoBehaviour
    {
        private ParticleSystem[] _systems;
        private ParticleSystemRenderer[] _renderers;
        private bool[] _wasVisible;
        private float _nextSummary;

        private void Start()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _renderers = new ParticleSystemRenderer[_systems.Length];
            _wasVisible = new bool[_systems.Length];

            for (int i = 0; i < _systems.Length; i++)
            {
                _renderers[i] = _systems[i].GetComponent<ParticleSystemRenderer>();
                _wasVisible[i] = _renderers[i] != null && _renderers[i].isVisible;
            }

            HammerOfOdenPlugin.Info(
                $"[particles] '{name}' scale={transform.localScale} systems={_systems.Length}");

            // Both, because the instance shows what we changed it to and the prefab shows what
            // Valheim authored. Comparing the two is the point: without the original there is
            // no way to tell which values are the artist's and which are ours.
            HammerOfOdenPlugin.Info("[particles] -- as placed --");
            for (int i = 0; i < _systems.Length; i++)
            {
                DumpConfiguration(_systems[i], _renderers[i]);
            }

            DumpPrefab();
        }

        /// <summary>The untouched configuration, straight from the prefab.</summary>
        private void DumpPrefab()
        {
            if (ZNetScene.instance == null)
            {
                return;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(Utils.GetPrefabName(gameObject));
            if (prefab == null)
            {
                return;
            }

            HammerOfOdenPlugin.Info($"[particles] -- prefab '{prefab.name}' scale={prefab.transform.localScale} --");

            foreach (ParticleSystem system in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                DumpConfiguration(system, system.GetComponent<ParticleSystemRenderer>());
            }
        }

        /// <summary>
        /// Everything about a system that affects how scaling it behaves.
        /// </summary>
        /// <remarks>
        /// simulationSpace is the field that decides most of it. A World-space system emits
        /// into the world and its particles then ignore the emitter entirely, so neither the
        /// transform nor the scaling mode moves them once they exist. A Local one carries its
        /// particles with the object. The two need different treatment, and guessing which
        /// applies is what has made this take several attempts.
        ///
        /// Shape radius and start speed matter for the same reason: they set how far the
        /// effect reaches, which is what looks wrong when the geometry changes size and the
        /// effect does not follow.
        /// </remarks>
        private static void DumpConfiguration(ParticleSystem system, ParticleSystemRenderer renderer)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.ShapeModule shape = system.shape;
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;

            HammerOfOdenPlugin.Info(
                $"[particles]   {system.name}"
                + $" | space={main.simulationSpace}"
                + $" scaling={main.scalingMode}"
                + $" culling={main.cullingMode}");

            HammerOfOdenPlugin.Info(
                $"[particles]     size={Describe(main.startSize)}"
                + $" speed={Describe(main.startSpeed)}"
                + $" life={Describe(main.startLifetime)}"
                + $" gravity={main.gravityModifierMultiplier:0.###}"
                + $" simSpeed={main.simulationSpeed:0.###}");

            HammerOfOdenPlugin.Info(
                $"[particles]     shape={(shape.enabled ? shape.shapeType.ToString() : "off")}"
                + $" radius={shape.radius:0.###}"
                + $" shapeScale={shape.scale}"
                + $" shapePos={shape.position}");

            HammerOfOdenPlugin.Info(
                $"[particles]     emission={(emission.enabled ? Describe(emission.rateOverTime) : "off")}"
                + $" bursts={emission.burstCount}"
                + $" velOverLife={velocity.enabled}"
                + $" sizeOverLife={size.enabled}");

            if (renderer != null)
            {
                HammerOfOdenPlugin.Info(
                    $"[particles]     render={renderer.renderMode}"
                    + $" align={renderer.alignment}"
                    + $" minSize={renderer.minParticleSize:0.###}"
                    + $" maxSize={renderer.maxParticleSize:0.###}"
                    + $" lengthScale={renderer.lengthScale:0.###}");
            }
        }

        private static string Describe(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return $"{curve.constant:0.###}";
                case ParticleSystemCurveMode.TwoConstants:
                    return $"{curve.constantMin:0.###}..{curve.constantMax:0.###}";
                default:
                    return $"{curve.mode}(mult {curve.curveMultiplier:0.###})";
            }
        }

        private void Update()
        {
            if (_systems == null || !ModConfig.DebugParticles.Value)
            {
                return;
            }

            Camera camera = MainCamera.Get();
            float distance = (camera != null)
                ? Vector3.Distance(camera.transform.position, transform.position)
                : -1f;

            bool report = Time.time >= _nextSummary;

            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystemRenderer renderer = _renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                bool visible = renderer.isVisible;

                // A change is the moment that matters, so never let it wait for the summary.
                if (visible != _wasVisible[i])
                {
                    _wasVisible[i] = visible;
                    HammerOfOdenPlugin.Info(
                        $"[particles] {_systems[i].name} isVisible -> {visible} "
                        + $"at {distance:0.##}m; {Describe(_systems[i], renderer)}");
                    continue;
                }

                if (report)
                {
                    HammerOfOdenPlugin.Info(
                        $"[particles] {_systems[i].name} at {distance:0.##}m; {Describe(_systems[i], renderer)}");
                }
            }

            if (report)
            {
                _nextSummary = Time.time + 1f;
            }
        }

        private static string Describe(ParticleSystem system, ParticleSystemRenderer renderer)
        {
            Bounds b = renderer.bounds;

            StringBuilder text = new StringBuilder();
            text.Append("visible=").Append(renderer.isVisible);
            text.Append(", enabled=").Append(renderer.enabled);

            // Whether the game has switched the effect off, as opposed to it being hidden.
            // A portal's effects are disabled until it is connected, and something similar
            // could be happening on approach - which would be vanilla behaviour, not scaling.
            text.Append(", emitting=").Append(system.emission.enabled);
            text.Append(", playing=").Append(system.isPlaying);
            text.Append(", particles=").Append(system.particleCount);
            text.Append(", boundsSize=").Append(b.size.ToString("0.##"));
            text.Append(", boundsCentre=").Append(b.center.ToString("0.##"));

            return text.ToString();
        }

        /// <summary>Attach to a piece worth watching, if diagnostics are switched on.</summary>
        internal static void AttachTo(GameObject piece)
        {
            if (piece == null || !ModConfig.DebugParticles.Value)
            {
                return;
            }

            if (piece.GetComponent<ParticleDiagnostics>() != null)
            {
                return;
            }

            if (piece.GetComponentInChildren<ParticleSystem>(true) == null)
            {
                return;
            }

            piece.AddComponent<ParticleDiagnostics>();
        }
    }
}
