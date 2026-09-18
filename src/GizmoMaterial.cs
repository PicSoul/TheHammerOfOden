using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Shared unlit material for every line the mod draws.
    /// </summary>
    /// <remarks>
    /// A built Unity game only ships the shaders it actually uses, so Shader.Find can
    /// return null for anything the game does not reference. Probe a list rather than
    /// assume, and remember the failure so we are not searching every frame.
    /// </remarks>
    internal static class GizmoMaterial
    {
        private static Material _material;
        private static bool _failed;

        internal static Material Get()
        {
            if (_material != null)
            {
                return _material;
            }

            if (_failed)
            {
                return null;
            }

            string[] candidates =
            {
                "Particles/Standard Unlit",
                "Sprites/Default",
                "Unlit/Transparent",
                "Unlit/Color",
                "Legacy Shaders/Particles/Alpha Blended"
            };

            foreach (string name in candidates)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                {
                    continue;
                }

                _material = new Material(shader);
                HammerOfOdenPlugin.Debug($"Line material using shader '{name}'.");
                return _material;
            }

            _failed = true;
            HammerOfOdenPlugin.Error(
                "Could not find a usable shader, so the gizmo and snap point markers will not "
                + "be drawn. Rotation and placement are unaffected.");
            return null;
        }

        internal static void Destroy()
        {
            if (_material != null)
            {
                Object.Destroy(_material);
                _material = null;
            }

            _failed = false;
        }
    }
}
