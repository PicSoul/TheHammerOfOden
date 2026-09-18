using UnityEngine;
using UnityEngine.Rendering;

namespace TheHammerOfOden
{
    /// <summary>
    /// Shared unlit materials for every line the mod draws, in normal and see-through form.
    /// </summary>
    /// <remarks>
    /// A built Unity game only ships the shaders it actually uses, so Shader.Find can return
    /// null for anything the game does not reference. Probe a list rather than assume, and
    /// remember a failure so we are not searching every frame.
    ///
    /// The see-through material exists because a snap point on the underside of a piece is
    /// invisible exactly when you need it. UI/Default is preferred for it: it declares
    /// ZTest [unity_GUIZTestMode], so the depth test can be forced to Always from script,
    /// which most world shaders do not allow.
    /// </remarks>
    internal static class GizmoMaterial
    {
        private static Material _normal;
        private static Material _xray;
        private static bool _failed;

        /// <summary>Shaders that can have their depth test overridden, best first.</summary>
        private static readonly string[] XrayCandidates =
        {
            "UI/Default",
            "Sprites/Default",
            "Particles/Standard Unlit",
            "Unlit/Transparent"
        };

        private static readonly string[] NormalCandidates =
        {
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Unlit/Transparent",
            "Unlit/Color",
            "Legacy Shaders/Particles/Alpha Blended"
        };

        internal static Material Get()
        {
            if (_normal != null || _failed)
            {
                return _normal;
            }

            _normal = Build(NormalCandidates, seeThrough: false);
            if (_normal == null)
            {
                _failed = true;
                HammerOfOdenPlugin.Error(
                    "Could not find a usable shader, so the gizmo and snap point markers will not "
                    + "be drawn. Rotation and placement are unaffected.");
            }

            return _normal;
        }

        /// <summary>Material that ignores depth, so markers show through the piece in front of them.</summary>
        internal static Material GetSeeThrough()
        {
            if (_xray != null)
            {
                return _xray;
            }

            _xray = Build(XrayCandidates, seeThrough: true);
            return _xray ?? Get();
        }

        private static Material Build(string[] candidates, bool seeThrough)
        {
            foreach (string name in candidates)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    continue;
                }

                Material material = new Material(shader);

                if (!seeThrough)
                {
                    HammerOfOdenPlugin.Debug($"Line material using shader '{name}'.");
                    return material;
                }

                bool overrode = false;

                // UI shaders read the depth test from this, which is the reliable route.
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                overrode |= shader.name == "UI/Default";

                if (material.HasProperty("_ZTest"))
                {
                    material.SetInt("_ZTest", (int)CompareFunction.Always);
                    overrode = true;
                }

                if (material.HasProperty("_ZWrite"))
                {
                    material.SetInt("_ZWrite", 0);
                }

                // Draw after opaque geometry so nothing paints over us afterwards.
                material.renderQueue = 5000;

                HammerOfOdenPlugin.Debug(
                    $"See-through material using shader '{name}'; depth override "
                    + (overrode ? "applied" : "NOT available on this shader"));

                return material;
            }

            return null;
        }

        internal static void Destroy()
        {
            if (_normal != null)
            {
                Object.Destroy(_normal);
                _normal = null;
            }

            if (_xray != null)
            {
                Object.Destroy(_xray);
                _xray = null;
            }

            _failed = false;
        }
    }
}
