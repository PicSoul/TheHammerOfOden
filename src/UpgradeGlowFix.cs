using System;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Stops an upgraded item's glow from churning the Mistlands mist.
    /// </summary>
    /// <remarks>
    /// Valheim's UpgraderGlow.prefab - the sparkle on an item you have upgraded at a forge -
    /// carries a ParticleSystemForceField: a sphere reaching five metres with a weak outward
    /// push. It is attached to your hand, and the Mistlands mist is a particle system, so the
    /// field shoves the mist about wherever you go.
    ///
    /// The effect is unmistakable once you know to look for it. Hold an upgraded axe in the
    /// Mistlands and the fog boils around you; hold a torch, which has no upgrade glow, and
    /// it settles. It is not the biome's intended drift, and it is not a mod - it took an
    /// in-game dump of every force field near the player to find, because every reasonable
    /// guess pointed somewhere else.
    ///
    /// The field is presumably meant to shape the glow's own sparkles and catches the mist by
    /// accident, so only the field is switched off. The glow itself is left alone: the point
    /// is to stop it moving the weather, not to take away the shine on your axe.
    /// </remarks>
    internal static class UpgradeGlowFix
    {
        private const string GlowName = "UpgraderGlow";

        private static readonly GameObject[] Seen = new GameObject[4];

        /// <summary>
        /// Both hands and both shoulders.
        /// </summary>
        /// <remarks>
        /// Sheathing a weapon does not put the glow away - it moves the object to the back,
        /// where it carries on pushing the mist from over your shoulder. Covering only what
        /// is in hand would have looked fixed right up until you put the axe away.
        /// </remarks>
        private static readonly AccessTools.FieldRef<VisEquipment, GameObject>[] Slots =
        {
            Resolve("m_rightItemInstance"),
            Resolve("m_leftItemInstance"),
            Resolve("m_rightBackItemInstance"),
            Resolve("m_leftBackItemInstance")
        };

        private static AccessTools.FieldRef<VisEquipment, GameObject> Resolve(string field)
        {
            try
            {
                return AccessTools.FieldRefAccess<VisEquipment, GameObject>(field);
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    $"Could not reach VisEquipment.{field}, so the upgrade glow fix is "
                    + "unavailable. Everything else is unaffected. " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Checks whatever is in the player's hands, when it changes.
        /// </summary>
        /// <remarks>
        /// Only when the instance changes. Valheim rebuilds the held object on every
        /// equipment change, so comparing the reference is enough to know whether there is
        /// anything new to look at, and holding one axe for an hour costs two comparisons a
        /// frame and nothing else.
        /// </remarks>
        internal static void Apply(Player player)
        {
            if (player == null || !ModConfig.QuietUpgradeGlow.Value)
            {
                return;
            }

            VisEquipment visuals = player.GetComponent<VisEquipment>();
            if (visuals == null)
            {
                return;
            }

            for (int i = 0; i < Slots.Length; i++)
            {
                Check(i, Slots[i]?.Invoke(visuals));
            }
        }

        private static void Check(int slot, GameObject held)
        {
            if (Seen[slot] == held)
            {
                return;
            }

            Seen[slot] = held;

            if (held == null)
            {
                return;
            }

            foreach (ParticleSystemForceField field
                in held.GetComponentsInChildren<ParticleSystemForceField>(true))
            {
                if (!BelongsToGlow(field.transform, held.transform))
                {
                    continue;
                }

                field.enabled = false;

                HammerOfOdenPlugin.Debug(
                    $"Silenced the upgrade glow's force field on '{held.name}' "
                    + $"(reached {field.endRange:0.#}m).");
            }
        }

        /// <summary>
        /// Whether this field belongs to the upgrade glow rather than to the item itself.
        /// </summary>
        /// <remarks>
        /// Matched by name up the chain, not by taking every force field on the object. An
        /// item is entitled to a force field of its own - a torch's flame, a magic weapon's
        /// effect - and switching those off would be breaking something to fix something else.
        /// </remarks>
        private static bool BelongsToGlow(Transform field, Transform item)
        {
            for (Transform up = field; up != null && up != item.parent; up = up.parent)
            {
                if (up.name.IndexOf(GlowName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
