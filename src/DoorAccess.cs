using System;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lets you open a door without putting the hammer away.
    /// </summary>
    /// <remarks>
    /// Vanilla switches interaction off entirely while a build tool is out, which is sensible
    /// for most things - you do not want to be opening chests every time you aim at one - and
    /// maddening for doors. Running out of wood halfway through a wall means swapping tools,
    /// opening the door, swapping back, and finding the piece you had selected.
    ///
    /// Only doors. Not chests, not crafting stations, not anything else vanilla hides while
    /// building: those are all things you would trigger by accident while lining up a piece,
    /// and a door is the one that is genuinely in the way.
    ///
    /// This once also opened doors as you walked up to them, and closed them behind you. That
    /// is gone. It was a proximity feature in a mod about placing pieces, it never worked well
    /// enough to defend, and nothing else here depended on it - opening the door you are
    /// deliberately aiming at is a different thing entirely, and it stays.
    /// </remarks>
    internal static class DoorAccess
    {
        private delegate bool CanInteractCall(Door door);
        private delegate bool HaveKeyCall(Door door, Humanoid player, bool matchWorldLevel);

        private static readonly CanInteractCall CanInteract =
            Bind<CanInteractCall>("CanInteract", new Type[0]);

        private static readonly HaveKeyCall HasKey =
            Bind<HaveKeyCall>("HaveKey", new[] { typeof(Humanoid), typeof(bool) });

        /// <summary>Binds one of Door's own methods, which are not public.</summary>
        private static T Bind<T>(string name, Type[] args) where T : Delegate
        {
            try
            {
                System.Reflection.MethodInfo method = AccessTools.Method(typeof(Door), name, args);

                if (method == null)
                {
                    HammerOfOdenPlugin.Error(
                        $"Door.{name} was not found, so door opening is limited. "
                        + "Valheim has probably changed.");
                    return null;
                }

                return AccessTools.MethodDelegate<T>(method);
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error($"Could not bind Door.{name}. " + ex.Message);
                return null;
            }
        }

        private static int _pieceMask = -1;
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// Opens the door you are looking at, on the ordinary use key, while building.
        /// </summary>
        internal static void HandleUse(Player player)
        {
            if (player == null
                || !ModConfig.OpenDoorsWhileBuilding.Value
                || !ZInput.GetButtonDown("Use"))
            {
                return;
            }

            Transform eye = player.m_eye;
            if (eye == null)
            {
                return;
            }

            Door door = Looking(eye, ModConfig.DoorReach.Value);
            if (door == null)
            {
                return;
            }

            // Interact, not Open: aiming at a door deliberately should close it as well.
            if (Allowed(player, door))
            {
                door.Interact(player, false, false);
            }
        }

        /// <summary>Nearest door along the line of sight, ignoring everything else.</summary>
        private static Door Looking(Transform eye, float reach)
        {
            int found = Physics.RaycastNonAlloc(
                eye.position, eye.forward, Hits, reach, Mask(), QueryTriggerInteraction.Ignore);

            Door best = null;
            float nearest = float.MaxValue;

            for (int i = 0; i < found; i++)
            {
                Door door = Hits[i].collider.GetComponentInParent<Door>();

                if (door != null && Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    best = door;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether this player may open this door at all.
        /// </summary>
        /// <remarks>
        /// Opening a locked door because the mod skipped the key check would be a cheat, and
        /// opening one inside someone else's ward would be worse.
        /// </remarks>
        private static bool Allowed(Player player, Door door)
        {
            if (CanInteract == null || !CanInteract(door))
            {
                return false;
            }

            if (door.m_keyItem != null && (HasKey == null || !HasKey(door, player, true)))
            {
                return false;
            }

            if (door.m_checkGuardStone
                && !PrivateArea.CheckAccess(door.transform.position, 0f, false, false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Layers a door can be on, and no more.
        /// </summary>
        /// <remarks>
        /// Default and static_solid were in here and should not have been: they carry terrain,
        /// rocks and most of the world, so every query came back full of things that could
        /// never be a door and crowded out the one that was.
        /// </remarks>
        private static int Mask()
        {
            if (_pieceMask == -1)
            {
                _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
            }

            return _pieceMask;
        }
    }
}
