using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Widens the patch of Mistlands fog a wisplight clears.
    /// </summary>
    /// <remarks>
    /// Building in the Mistlands means building inside a cloud. The wisplight clears a
    /// bubble around you, and that bubble is smaller than the thing you are usually trying
    /// to look at.
    ///
    /// The mist is a particle system and the wisplight pushes it away with a
    /// ParticleSystemForceField - so what decides the size of the clear patch is that field's
    /// endRange, and nothing else. This is worth stating because the obvious guess is wrong:
    /// the demister is a visible ball with a collider, and scaling that ball changes how big
    /// the ball looks while clearing exactly as much mist as before. A force field's range is
    /// a number on the component, not a consequence of its transform.
    ///
    /// The prefab's own value is captured the first time one is seen, so the multiplier
    /// always applies to Valheim's figure rather than compounding on whatever it was last
    /// set to.
    /// </remarks>
    internal static class MistClearing
    {
        /// <summary>
        /// Widens the clearing of one demister - the one the player is carrying.
        /// </summary>
        /// <remarks>
        /// Scoped to a single object on purpose. Mistlands fires and torches each carry a
        /// Demister of their own, with their own ranges, and an earlier version reached all
        /// of them through Demister.Awake: every fire nearby became a wide force field and
        /// the mist was pushed from several directions at once, thrashing without clearing.
        ///
        /// Each demister's own range is remembered on the object itself rather than in a
        /// single shared number, because they differ - the ones seen in one session ranged
        /// from 8 to 30 - and one global base applied to all of them is wrong for all but one.
        /// </remarks>
        internal static void Apply(GameObject ball)
        {
            if (ball == null)
            {
                return;
            }

            Demister demister = ball.GetComponentInChildren<Demister>(true);
            if (demister == null)
            {
                return;
            }

            ParticleSystemForceField field = Field(demister);
            if (field == null)
            {
                return;
            }

            DemisterBase original = ball.GetComponent<DemisterBase>();

            if (original == null)
            {
                original = ball.AddComponent<DemisterBase>();
                original.Range = field.endRange;

                HammerOfOdenPlugin.Info(
                    $"[mist] '{ball.name}' clears {original.Range:0.##}m by default.");

                HideOrb(demister);
            }

            // Zero means leave Valheim's own figure alone, and never shrink below it -
            // this is here to clear more, not to take away what a wisplight already does.
            float asked = ModConfig.MistClearRange.Value;
            float wanted = asked <= 0f ? original.Range : Mathf.Max(original.Range, asked);

            if (!Mathf.Approximately(field.endRange, wanted))
            {
                field.endRange = wanted;

                HammerOfOdenPlugin.Info(
                    $"[mist] '{ball.name}' now clears {wanted:0.##}m (was {original.Range:0.##}m).");
            }
        }

        /// <summary>
        /// Takes the visible wisp out of shot, leaving the clearing it does behind.
        /// </summary>
        /// <remarks>
        /// The bright ball circling your head is charming for an hour of exploring and in the
        /// way for an hour of building, and it is the part of a demister that does nothing:
        /// the mist is moved by the force field, not by the thing you can see. Only the
        /// renderers go, so the object, the field and its motion are all untouched.
        /// </remarks>
        internal static void HideOrb(Demister demister)
        {
            if (demister == null || !ModConfig.HideDemisterOrb.Value)
            {
                return;
            }

            foreach (Renderer renderer in demister.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            foreach (Light light in demister.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }
        }

        private static readonly AccessTools.FieldRef<Demister, ParticleSystemForceField> FieldOf =
            ResolveField();

        private static AccessTools.FieldRef<Demister, ParticleSystemForceField> ResolveField()
        {
            try
            {
                return AccessTools.FieldRefAccess<Demister, ParticleSystemForceField>("m_forceField");
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not reach Demister.m_forceField, so the mist clearing radius is left "
                    + "as Valheim has it. " + ex.Message);
                return null;
            }
        }

        private static ParticleSystemForceField Field(Demister demister)
        {
            return FieldOf != null ? FieldOf(demister) : null;
        }
    }

    /// <summary>Remembers what one demister cleared before this mod touched it.</summary>
    internal sealed class DemisterBase : MonoBehaviour
    {
        internal float Range;
    }
}
