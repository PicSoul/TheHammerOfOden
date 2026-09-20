using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Whether the player can see through the mist, and optionally clearing it ourselves.
    /// </summary>
    /// <remarks>
    /// Detection is by status effect type, not by item. Valheim represents "you can see in
    /// the Mistlands" as SE_Demister on the player, and anything that grants the ability -
    /// the wisplight, a backpack with one built in, whatever a future mod adds - grants it by
    /// applying that same effect. Asking what the player is carrying would mean a list of
    /// item names to maintain and would miss most of them.
    ///
    /// Clearing is done by putting Valheim's own demister object at the camera, rather than
    /// by giving the player the status effect. The status effect brings a whole apparatus
    /// with it - an orb that drifts under its own physics, a leash to the player, a HUD icon
    /// - none of which is wanted here, and all of which then has to be fought. The Mistwalker
    /// sword is the model: it clears mist simply by existing while held, with no effect
    /// applied to anybody. One object, parked where it is useful, removed when it is not.
    /// </remarks>
    internal static class Wisplight
    {
        private static GameObject _orb;

        private static readonly AccessTools.FieldRef<SE_Demister, GameObject> BallPrefab =
            ResolveBallPrefab();

        private static AccessTools.FieldRef<SE_Demister, GameObject> ResolveBallPrefab()
        {
            try
            {
                return AccessTools.FieldRefAccess<SE_Demister, GameObject>("m_ballPrefab");
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not reach SE_Demister.m_ballPrefab, so clearing mist without a "
                    + "wisplight is unavailable. " + ex.Message);
                return null;
            }
        }

        /// <summary>Whether the player already has mist vision from any source.</summary>
        internal static bool PlayerHas(Player player)
        {
            if (player == null)
            {
                return false;
            }

            SEMan effects = player.GetSEMan();
            List<StatusEffect> active = effects?.GetStatusEffects();

            if (active == null)
            {
                return false;
            }

            foreach (StatusEffect effect in active)
            {
                if (effect is SE_Demister)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Keeps our own demister at the camera while it is flying, or takes it away.
        /// </summary>
        /// <remarks>
        /// Tied to the camera rather than to holding a tool. A hammer can be held while
        /// walking across the Mistlands, so granting it there hands out free mist vision to
        /// anyone who equips one; the camera pins the character in place, which makes this a
        /// building convenience rather than a way to travel.
        /// </remarks>
        internal static void Ensure(Player player)
        {
            bool wanted = player != null
                && BuildCamera.IsActive
                && !ModConfig.RequiresWisplight.Value;

            if (!wanted)
            {
                Remove();
                return;
            }

            if (_orb == null)
            {
                GameObject prefab = Prefab();
                if (prefab == null)
                {
                    return;
                }

                _orb = Object.Instantiate(prefab, BuildCamera.Position, Quaternion.identity);
                HammerOfOdenPlugin.Info("[mist] placed our own demister at the camera.");

                // Only where it means something. The demister follows the camera everywhere,
                // but announcing it in the Meadows would be reporting on nothing.
                if (player.GetCurrentBiome() == Heightmap.Biome.Mistlands)
                {
                    Notify.Show(player, "Demister: on");
                }
            }

            _orb.transform.position = BuildCamera.Position;

            MistClearing.Apply(_orb);
        }

        /// <summary>Takes our own demister away. Never touches the player's wisplight.</summary>
        internal static void Remove()
        {
            if (_orb == null)
            {
                return;
            }

            Object.Destroy(_orb);
            _orb = null;

            HammerOfOdenPlugin.Info("[mist] removed our demister.");
        }

        /// <summary>
        /// The demister object Valheim itself uses, taken from the effect that spawns it.
        /// </summary>
        /// <remarks>
        /// Found by type rather than by name: the name is a localisation token that any mod
        /// is free to change, and the type is not.
        /// </remarks>
        private static GameObject Prefab()
        {
            if (BallPrefab == null || ObjectDB.instance?.m_StatusEffects == null)
            {
                return null;
            }

            foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects)
            {
                if (effect is SE_Demister demister)
                {
                    return BallPrefab(demister);
                }
            }

            HammerOfOdenPlugin.Error(
                "No demister effect found in the object database, so clearing mist without a "
                + "wisplight is unavailable.");

            return null;
        }
    }
}
