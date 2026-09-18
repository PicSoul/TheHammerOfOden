using UnityEngine;
using UnityEngine.Rendering;

namespace TheHammerOfOden
{
    /// <summary>
    /// The small orbs that ride each rotation ring at the piece's current angle.
    /// </summary>
    /// <remarks>
    /// Spheres rather than the camera-facing rings used elsewhere. A flat ring reads as a
    /// disc however it is coloured; a shaded sphere gives the eye a highlight and a
    /// terminator to work with, which is what makes it look like an object sitting on the
    /// ring rather than a mark drawn over it.
    ///
    /// That needs a lit shader, so the Standard shader is preferred and configured for
    /// transparency. Where it is unavailable the mod's flat material is used instead - the
    /// orb then reads as a solid dot, which is still clearer than an outline.
    ///
    /// The collider Unity attaches to a primitive is destroyed immediately. Vanilla's
    /// placement code runs sphere queries around the ghost every frame, and a stray collider
    /// of ours would be found by them and corrupt the placement it is meant to describe.
    /// </remarks>
    internal static class AngleBeads
    {
        private static Material _material;
        private static bool _resolved;
        private static MaterialPropertyBlock _properties;

        internal static Renderer Create(string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;

            // Must go: vanilla sphere-casts around the ghost every frame and would find it.
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = sphere.GetComponent<Renderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Object.DontDestroyOnLoad(sphere);
            return renderer;
        }

        /// <summary>Colour without instancing a material per bead.</summary>
        internal static void Tint(Renderer renderer, Color color)
        {
            if (_properties == null)
            {
                _properties = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(_properties);
            _properties.SetColor("_Color", color);
            _properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(_properties);
        }

        private static Material GetMaterial()
        {
            if (_resolved)
            {
                return _material;
            }

            _resolved = true;

            Shader standard = Shader.Find("Standard");
            if (standard != null)
            {
                _material = new Material(standard);

                // The documented recipe for putting the Standard shader into Fade mode.
                _material.SetFloat("_Mode", 2f);
                _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _material.SetInt("_ZWrite", 0);
                _material.DisableKeyword("_ALPHATEST_ON");
                _material.EnableKeyword("_ALPHABLEND_ON");
                _material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _material.renderQueue = 3000;

                _material.SetFloat("_Glossiness", 0.6f);
                _material.SetFloat("_Metallic", 0f);

                HammerOfOdenPlugin.Debug("Angle beads using the Standard shader.");
                return _material;
            }

            _material = GizmoMaterial.Get();
            HammerOfOdenPlugin.Debug("Standard shader unavailable; angle beads fall back to flat shading.");
            return _material;
        }

        internal static void Destroy()
        {
            if (_material != null && _material != GizmoMaterial.Get())
            {
                Object.Destroy(_material);
            }

            _material = null;
            _resolved = false;
            _properties = null;
        }
    }
}
