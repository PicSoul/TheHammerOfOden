using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lets you open a door without putting the hammer away, and optionally on approach.
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
    /// Opening is done through Door.Open rather than Door.Interact for the automatic case,
    /// because Interact toggles. An automatic toggle on a timer would shut the door in your
    /// face as often as it opened it.
    ///
    /// Closing lives here too, rather than being left to a mod that does it well, because the
    /// two halves need to agree and two mods cannot. An opener that only knows distances and
    /// a closer that only knows distances will fight over any door you stand beside: one sees
    /// you near enough to open, the other sees you far enough to close, and the door flaps
    /// until you move. Tuning both mods until their thresholds happen not to overlap works
    /// and is a poor thing to ask of anyone.
    ///
    /// Owning both ends removes the guessing. The doors this opened are remembered, so
    /// closing them again is not an inference about intent - it is undoing something we know
    /// we did. Doors opened by hand are left alone, because propping one open is a decision.
    /// </remarks>
    internal static class DoorAccess
    {
        private delegate void OpenCall(Door door, Vector3 userDir);
        private delegate bool CanInteractCall(Door door);
        private delegate bool HaveKeyCall(Door door, Humanoid player, bool matchWorldLevel);

        private static readonly OpenCall OpenDoor =
            Bind<OpenCall>("Open", new[] { typeof(Vector3) });

        private static readonly CanInteractCall CanInteract =
            Bind<CanInteractCall>("CanInteract", new Type[0]);

        private static readonly HaveKeyCall HasKey =
            Bind<HaveKeyCall>("HaveKey", new[] { typeof(Humanoid), typeof(bool) });

        /// <summary>
        /// Binds one of Door's own methods, which are not public.
        /// </summary>
        /// <remarks>
        /// Door.Open earns the reflection on its own: it opens rather than toggles, which is
        /// the difference between a door that opens as you walk up to it and one that flaps
        /// shut in your face every time the timer comes round.
        /// </remarks>
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

        /// <summary>Doors this mod opened, and when the player was last close to them.</summary>
        private static readonly Dictionary<Door, float> Opened = new Dictionary<Door, float>();

        private static readonly List<Door> Finished = new List<Door>();
        private static readonly List<Door> Found = new List<Door>();
        private static readonly List<Door> Leaves = new List<Door>();
        private static readonly List<Door> Tracked = new List<Door>();

        private static float _nextScan;
        private static int _pieceMask = -1;
        private static readonly Collider[] Nearby = new Collider[32];
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

        /// <summary>Flips auto-open on or off, wherever you are and whatever you hold.</summary>
        internal static void HandleToggle(Player player)
        {
            KeyboardShortcut key = ModConfig.AutoOpenDoorsKey.Value;

            if (key.MainKey == KeyCode.None || !ZInput.GetKeyDown(key.MainKey, true))
            {
                return;
            }

            // Written to the setting rather than held in a field, so the choice survives a
            // restart and reads correctly in the config file.
            ModConfig.AutoOpenDoors.Value = !ModConfig.AutoOpenDoors.Value;

            Notify.Show(player, ModConfig.AutoOpenDoors.Value
                ? "Auto-open doors: on"
                : "Auto-open doors: off");
        }

        /// <summary>
        /// Opens doors as you walk up to them, whatever you are holding.
        /// </summary>
        /// <remarks>
        /// Off by default. It is a convenience some people want and others would find
        /// alarming, and it acts without being asked, which is the kind of thing that should
        /// be opted into rather than discovered.
        /// </remarks>
        internal static void AutoOpen(Player player)
        {
            if (player == null || !ModConfig.AutoOpenDoors.Value || Time.time < _nextScan)
            {
                return;
            }

            _nextScan = Time.time + Mathf.Max(0.1f, ModConfig.AutoOpenInterval.Value);

            Vector3 where = player.transform.position;
            float range = ModConfig.AutoOpenRange.Value;

            if (OpenDoor == null)
            {
                return;
            }

            Collect(where, range, Found);

            while (Found.Count > 0)
            {
                Group(Found[0], Leaves);
                OpenLeaves(player, where, Leaves);

                foreach (Door leaf in Leaves)
                {
                    Found.Remove(leaf);
                }
            }
        }

        /// <summary>Distinct doors within reach, however many colliders each one has.</summary>
        private static void Collect(Vector3 where, float range, List<Door> into)
        {
            into.Clear();

            int found = Physics.OverlapSphereNonAlloc(where, range, Nearby, Mask());

            for (int i = 0; i < found; i++)
            {
                Door door = Nearby[i] == null ? null : Nearby[i].GetComponentInParent<Door>();

                if (door != null && !into.Contains(door))
                {
                    into.Add(door);
                }
            }
        }

        /// <summary>
        /// A door and any others standing right beside it.
        /// </summary>
        /// <remarks>
        /// Double doors are two independent Door components that happen to be neighbours,
        /// and nothing in the game ties them together. Searching around the door rather than
        /// around the player also picks up a second leaf that is just outside the range that
        /// triggered the first, which is why one half used to open alone.
        /// </remarks>
        private static void Group(Door door, List<Door> into)
        {
            into.Clear();
            into.Add(door);

            float reach = ModConfig.DoorPairDistance.Value;

            int found = Physics.OverlapSphereNonAlloc(
                door.transform.position, reach, Nearby, Mask());

            for (int i = 0; i < found; i++)
            {
                Door other = Nearby[i] == null ? null : Nearby[i].GetComponentInParent<Door>();

                if (other != null && !into.Contains(other))
                {
                    into.Add(other);
                }
            }
        }

        /// <summary>
        /// Opens a door, or a set of neighbouring doors, as one.
        /// </summary>
        /// <remarks>
        /// One direction for the whole group, measured from the middle of it. Door.Open takes
        /// the dot of that direction against each door's own forward to choose a side, so two
        /// leaves given their own directions can disagree when you approach at an angle -
        /// which is exactly how a double door ends up opening inside out.
        /// </remarks>
        private static void OpenLeaves(Player player, Vector3 where, List<Door> leaves)
        {
            Vector3 centre = Vector3.zero;

            foreach (Door leaf in leaves)
            {
                centre += leaf.transform.position;
            }

            centre /= leaves.Count;

            Vector3 direction = (where - centre).normalized;

            foreach (Door leaf in leaves)
            {
                if (!Allowed(player, leaf))
                {
                    continue;
                }

                // Open or not, the timer is refreshed: standing in a doorway must never let
                // a close start, and a leaf we did not open is not ours to shut.
                if (IsOpen(leaf))
                {
                    if (Opened.ContainsKey(leaf))
                    {
                        Opened[leaf] = Time.time;
                    }

                    continue;
                }

                OpenDoor(leaf, direction);
                Opened[leaf] = Time.time;
            }
        }

        /// <summary>
        /// Closes the doors this mod opened, once you have gone.
        /// </summary>
        /// <remarks>
        /// Only doors in the register. A door you opened by hand and left open was a choice,
        /// and shutting it because you walked away would be the mod overruling you.
        ///
        /// The timer is reset while you are anywhere near, so a door does not close because
        /// you paused in the doorway - only because you actually left.
        /// </remarks>
        internal static void AutoClose(Player player)
        {
            if (player == null || Opened.Count == 0)
            {
                return;
            }

            bool enabled = ModConfig.AutoCloseDoors.Value;

            Vector3 where = player.transform.position;
            float far = ModConfig.AutoCloseDistance.Value;
            float delay = ModConfig.AutoCloseDelay.Value;

            Finished.Clear();

            // Over a snapshot of the keys, never the dictionary itself. Refreshing a timer
            // writes to a key that is already present, which does not change the count and
            // does invalidate a live enumerator under Unity's Mono.
            Tracked.Clear();
            Tracked.AddRange(Opened.Keys);

            foreach (Door door in Tracked)
            {
                float since = Opened[door];

                // Gone from the world, closed by hand, or the feature switched off: in every
                // case this is no longer ours to close.
                if (door == null || !IsOpen(door) || !enabled)
                {
                    Finished.Add(door);
                    continue;
                }

                if ((door.transform.position - where).sqrMagnitude < far * far)
                {
                    Opened[door] = Time.time;
                    continue;
                }

                if (Time.time - since < delay)
                {
                    continue;
                }

                // Interact rather than a direct state write, so the close is replicated and
                // raises the same sound and animation as closing it yourself.
                if (Allowed(player, door))
                {
                    door.Interact(player, false, false);
                }

                Finished.Add(door);
            }

            foreach (Door done in Finished)
            {
                Opened.Remove(done);
            }
        }

        /// <summary>Whether a door is standing open, read from the state it replicates.</summary>
        private static bool IsOpen(Door door)
        {
            // m_nview is private; the component sits on the same object anyway.
            ZNetView view = door.GetComponentInParent<ZNetView>();

            if (view == null || !view.IsValid())
            {
                return false;
            }

            // Zero is shut; either sign is open, since a door records which way it swung.
            return view.GetZDO().GetInt(ZDOVars.s_state, 0) != 0;
        }

        /// <summary>
        /// Whether this door is one the player could have opened by hand anyway.
        /// </summary>
        /// <remarks>
        /// Door.Open does not ask any of these questions, so they are asked here. Opening a
        /// locked door because the mod skipped the key check would be a cheat, and opening
        /// one inside someone else's ward would be worse.
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

        private static int Mask()
        {
            if (_pieceMask == -1)
            {
                _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid");
            }

            return _pieceMask;
        }
    }
}
