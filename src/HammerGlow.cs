using System;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lights the hammer while the mod is doing anything.
    /// </summary>
    /// <remarks>
    /// A master switch you cannot see the state of is a switch you press twice. Everything
    /// this mod does is a change to how placement already behaves, so with nothing selected
    /// there is nothing on screen to say whether it is on - you find out by scrolling and
    /// watching what the piece does.
    ///
    /// The hammer itself is the obvious place to say so, because it is already in your hand
    /// and already where you are looking. Valheim lights a few weapons this way, so a glowing
    /// tool reads as "this one is doing something" without needing to be explained.
    ///
    /// A light rather than an emissive material. Turning on emission means taking an instance
    /// of the renderer's material, which has to be given back exactly, and getting that wrong
    /// leaves a permanently glowing hammer for the rest of the session. A child object with a
    /// Light on it is removed by deleting the child, and disappears by itself when the item
    /// it hangs from is destroyed on unequip.
    ///
    /// The light alone reads well at night and washes out at noon, so a few slow motes are
    /// drifting off the head as well - those are visible in any light. They are built here
    /// rather than borrowed from one of Valheim's own glowing weapons, so that nothing
    /// depends on an asset name that a game update is free to change.
    /// </remarks>
    internal static class HammerGlow
    {
        private const string ChildName = "HoO_Glow";

        private static readonly AccessTools.FieldRef<VisEquipment, GameObject> RightItem =
            ResolveRightItem();

        private static AccessTools.FieldRef<VisEquipment, GameObject> ResolveRightItem()
        {
            try
            {
                return AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_rightItemInstance");
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not reach VisEquipment.m_rightItemInstance, so the hammer glow is "
                    + "disabled. Everything else is unaffected. " + ex.Message);
                return null;
            }
        }

        private static Light _light;
        private static Material _sparkMaterial;
        private static Texture2D _sparkTexture;

        /// <summary>Called every frame the build tool is out, whether the mod is on or off.</summary>
        internal static void Apply(Player player, bool on)
        {
            if (RightItem == null || player == null)
            {
                return;
            }

            bool glow = on && ModConfig.ShowHammerGlow.Value;
            bool sparks = on && ModConfig.ShowHammerSparks.Value;
            bool wanted = glow || sparks;

            VisEquipment equipment = player.GetComponent<VisEquipment>();
            GameObject item = equipment != null ? RightItem(equipment) : null;

            if (item == null)
            {
                // Nothing in hand to light. The glow child, if there was one, went with it.
                _light = null;
                return;
            }

            // The instance is rebuilt on every equipment change, so a remembered light can
            // belong to a hammer that no longer exists.
            if (_light == null || _light.transform.parent == null
                || _light.transform.parent.parent != item.transform)
            {
                _light = Find(item);
            }

            if (!wanted)
            {
                if (_light != null)
                {
                    UnityEngine.Object.Destroy(_light.gameObject);
                    _light = null;
                }

                return;
            }

            if (_light == null)
            {
                _light = Create(item);
            }

            if (_light == null)
            {
                return;
            }

            // Read every frame so the config sliders take effect as you drag them.
            _light.enabled = glow;
            _light.color = ModConfig.GlowColor.Value;
            _light.intensity = ModConfig.GlowIntensity.Value;
            _light.range = ModConfig.GlowRange.Value;

            ParticleSystem system = _light.GetComponentInChildren<ParticleSystem>(true);
            if (system == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = sparks;
            emission.rateOverTime = ModConfig.SparkRate.Value;

            ParticleSystem.MainModule main = system.main;
            main.startColor = ModConfig.GlowColor.Value;
        }

        private static Light Find(GameObject item)
        {
            Transform existing = item.transform.Find(ChildName);
            return existing != null ? existing.GetComponentInChildren<Light>() : null;
        }

        /// <summary>
        /// Hangs a light on the head of the tool rather than its grip.
        /// </summary>
        /// <remarks>
        /// Valheim's item pivots sit at the hand, so a light at the origin sits inside the
        /// player's fist. The centre of the geometry is no better: a hammer is mostly handle,
        /// so its middle is halfway down the shaft and still reads as coming from the hand.
        ///
        /// What is wanted is the far end. The tool is measured in its own space, the axis it
        /// is longest along is taken to be its length, and the light goes near whichever end
        /// of that axis is further from the grip - the head of a hammer, the tip of anything
        /// else, without needing to know which tool it is.
        /// </remarks>
        private static Light Create(GameObject item)
        {
            GameObject holder = new GameObject(ChildName);
            holder.transform.SetParent(item.transform, false);
            holder.transform.localPosition = HeadOf(item);

            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;

            AddSparks(holder);

            HammerOfOdenPlugin.Debug($"Hammer glow attached to '{item.name}'.");
            return light;
        }

        /// <summary>
        /// A handful of slow motes drifting off the head.
        /// </summary>
        /// <remarks>
        /// Emitted into world space rather than carried with the tool, so they hang where the
        /// hammer was and trail behind as you walk - embers coming off it rather than a cloud
        /// stuck to it. Local space would make them swing with every hammer blow, which reads
        /// as an attached effect instead of something the tool is giving off.
        ///
        /// Deliberately small and slow. This is a status light, not a weapon enchantment, and
        /// anything livelier would compete with the piece you are trying to look at.
        /// </remarks>
        private static void AddSparks(GameObject holder)
        {
            Material material = SparkMaterial();
            if (material == null)
            {
                return;
            }

            GameObject child = new GameObject("Sparks");
            child.transform.SetParent(holder.transform, false);

            ParticleSystem system = child.AddComponent<ParticleSystem>();

            // Stopped first: a system configures itself on Awake and will emit a burst with
            // default settings before any of the below is applied.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.gravityModifier = -0.04f;
            // Headroom for the highest rate the setting allows, times the longest life.
            // A system that hits its ceiling stops emitting rather than thinning out, which
            // looks like the effect breaking.
            main.maxParticles = 120;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = ModConfig.SparkRate.Value;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.07f;

            // Fade in and out rather than blinking into existence at full brightness.
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(Fade());

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
        }

        private static Gradient Fade()
        {
            Gradient gradient = new Gradient();

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        /// <summary>
        /// A soft round mote, drawn rather than loaded.
        /// </summary>
        /// <remarks>
        /// Without a texture a particle is a hard-edged square, which at this size reads as
        /// grit rather than a glow. A radial alpha falloff is a few lines to generate and
        /// avoids depending on any asset the game happens to ship.
        /// </remarks>
        private static Material SparkMaterial()
        {
            if (_sparkMaterial != null)
            {
                return _sparkMaterial;
            }

            Material template = GizmoMaterial.Get();
            if (template == null)
            {
                return null;
            }

            if (_sparkTexture == null)
            {
                const int size = 32;
                _sparkTexture = new Texture2D(size, size, TextureFormat.ARGB32, false);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f;
                        float dy = (y + 0.5f) / size - 0.5f;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy) * 2f;

                        float alpha = Mathf.Clamp01(1f - distance);
                        _sparkTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                    }
                }

                _sparkTexture.Apply();
            }

            _sparkMaterial = new Material(template);
            _sparkMaterial.mainTexture = _sparkTexture;

            return _sparkMaterial;
        }

        private static Vector3 HeadOf(GameObject item)
        {
            Transform root = item.transform;
            Bounds bounds = default(Bounds);
            bool any = false;

            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                Bounds b = renderer.bounds;

                if (!any)
                {
                    bounds = new Bounds(root.InverseTransformPoint(b.center), Vector3.zero);
                    any = true;
                }

                // The corners, not just the centre. Encapsulating centres measures where the
                // parts are, not how far the tool reaches, which on a one-piece model leaves
                // a box of no size at all.
                bounds.Encapsulate(root.InverseTransformPoint(b.min));
                bounds.Encapsulate(root.InverseTransformPoint(b.max));
            }

            if (!any)
            {
                return Vector3.zero;
            }

            Vector3 extents = bounds.extents;

            int along = 0;
            if (extents.y > extents.x) along = 1;
            if (extents.z > extents[along]) along = 2;

            // Whichever end of that axis is further from the grip at the origin.
            float far = Mathf.Abs(bounds.max[along]) > Mathf.Abs(bounds.min[along])
                ? bounds.max[along]
                : bounds.min[along];

            Vector3 head = bounds.center;
            head[along] = far * Mathf.Clamp01(ModConfig.GlowHeadOffset.Value);

            return head;
        }

        internal static void Forget()
        {
            _light = null;

            if (_sparkMaterial != null)
            {
                UnityEngine.Object.Destroy(_sparkMaterial);
                _sparkMaterial = null;
            }

            if (_sparkTexture != null)
            {
                UnityEngine.Object.Destroy(_sparkTexture);
                _sparkTexture = null;
            }
        }
    }
}
