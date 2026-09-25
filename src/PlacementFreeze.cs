using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Pins the placement ghost in the world so you can walk around it.
    /// </summary>
    /// <remarks>
    /// Vanilla ties the ghost to wherever your aim meets a surface, which means a piece can
    /// only ever go somewhere you can both see and stand to aim at. That rules out a good
    /// deal of building: under a roof you cannot back away from, over a cliff edge, deep
    /// inside a structure, or anywhere the piece you are placing is itself blocking the view
    /// of where it should go.
    ///
    /// Freezing records where the ghost is and holds it there. You are then free to move,
    /// look elsewhere, and judge the placement from a position you could never have aimed
    /// from - and the nudge keys in PlacementOffset become the way to adjust it, since aiming
    /// no longer does anything.
    ///
    /// Only the base position is frozen. The nudge is deliberately left live on top, so
    /// freezing and then adjusting is one continuous action rather than a thing you have to
    /// unfreeze to correct.
    ///
    /// Rotation stays live too, which takes one trick. Freezing the rotation outright would
    /// be the obvious thing and is wrong: a pinned piece you cannot turn is half a feature,
    /// and turning it is exactly what you want once you can finally see it properly. What is
    /// recorded instead is the difference between the ghost's rotation and your own, which
    /// is identity in the ordinary case and the surface's frame when you froze a piece lying
    /// against something. Re-applying that difference each frame keeps the alignment while
    /// leaving the rotation keys connected.
    /// </remarks>
    internal static class PlacementFreeze
    {
        private static bool _frozen;
        private static bool _captureWanted;
        private static Vector3 _position;

        /// <summary>The ghost's rotation at freeze time, less whatever you had rotated it by.</summary>
        private static Quaternion _alignment = Quaternion.identity;

        internal static bool IsFrozen => _frozen;

        /// <summary>Called once per placement update to service the toggle.</summary>
        internal static void HandleInput(Player player)
        {
            if (!IsKeyDown())
            {
                return;
            }

            _frozen = !_frozen;

            if (_frozen)
            {
                // The ghost has not been positioned yet this frame; capture once it has.
                _captureWanted = true;
            }
            else if (ModConfig.ResetOffsetOnUnfreeze.Value)
            {
                PlacementOffset.ResetNudge();
            }

            HammerOfOdenPlugin.Debug($"Placement {(_frozen ? "frozen" : "unfrozen")}.");
            Notify.Show(player, _frozen ? "Placement frozen" : "Placement unfrozen");
        }

        /// <summary>
        /// Holds the ghost where it was when you froze it.
        /// </summary>
        /// <remarks>
        /// Called after everything else that decides a base position, so that whatever you
        /// were looking at when you pressed the key is what gets pinned - including a piece
        /// laid against a surface.
        /// </remarks>
        internal static bool Apply(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || !_frozen || ghost == null)
            {
                return false;
            }

            if (_captureWanted)
            {
                _captureWanted = false;
                _position = ghost.transform.position;
                _alignment = ghost.transform.rotation * Quaternion.Inverse(RotationState.Current);

                HammerOfOdenPlugin.Debug($"Frozen at {_position}.");
                return true;
            }

            if (!ghost.activeSelf)
            {
                ghost.SetActive(true);
            }

            ghost.transform.position = _position;
            ghost.transform.rotation = _alignment * RotationState.Current;
            return true;
        }

        /// <summary>
        /// Pins the ghost at a known place and turn, rather than wherever it happens to be this
        /// frame. Editing uses it to hold the piece exactly where the original stands.
        /// </summary>
        internal static void FreezeAt(Vector3 position, Quaternion rotation)
        {
            _frozen = true;
            _captureWanted = false;
            _position = position;
            _alignment = rotation * Quaternion.Inverse(RotationState.Current);
        }

        /// <summary>Drop the freeze when leaving build mode or changing piece.</summary>
        internal static void Reset()
        {
            _frozen = false;
            _captureWanted = false;
            _alignment = Quaternion.identity;
        }

        private static bool IsKeyDown()
        {
            KeyboardShortcut shortcut = ModConfig.FreezeKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }
    }
}
