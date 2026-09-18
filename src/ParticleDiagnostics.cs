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

            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem.MainModule main = _systems[i].main;
                HammerOfOdenPlugin.Info(
                    $"[particles]   {_systems[i].name}: scalingMode={main.scalingMode}, "
                    + $"cullingMode={main.cullingMode}, startSize={main.startSize.constant:0.###}, "
                    + $"maxParticleSize={(_renderers[i] != null ? _renderers[i].maxParticleSize : -1f):0.###}");
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
