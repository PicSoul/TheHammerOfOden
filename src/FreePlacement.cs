using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum FreePlacementMode
    {
        /// <summary>Leave it to Valheim: held with AltPlace (Left Shift).</summary>
        Vanilla = 0,

        /// <summary>Hold this mod's key instead, freeing Left Shift for pitch.</summary>
        Hold = 1,

        /// <summary>Tap this mod's key to turn it on and off.</summary>
        Toggle = 2
    }

    /// <summary>
    /// Owns whether "free placement" is active.
    /// </summary>
    /// <remarks>
    /// In vanilla, holding AltPlace (Left Shift) does two things while the placement ghost
    /// is being built:
    ///
    ///   1. skips the snap-point search, so the ghost is not pulled onto nearby snap points
    ///   2. for terrain pieces that allow it, skips forcing the ghost's height to the
    ///      ground height under the player
    ///
    /// Both are useful, but Left Shift is also the natural pitch modifier, so holding it to
    /// tilt a piece silently turns snapping off at the same time. Taking ownership of the
    /// decision lets the two live on separate keys.
    ///
    /// AltPlace's other jobs (Shift + middle-click to copy a piece, Shift + E to
    /// alt-interact) are read in different methods and are deliberately left alone.
    /// </remarks>
    internal static class FreePlacement
    {
        private static bool _toggledOn;

        /// <summary>
        /// Whether free placement is active, resolving the vanilla key state itself.
        /// </summary>
        /// <remarks>
        /// For callers outside the patched method, which do not have vanilla's AltPlace
        /// result to hand. Passing a hard-coded false here would silently report "not free"
        /// whenever the mode is Vanilla.
        /// </remarks>
        internal static bool IsActiveNow()
        {
            bool altPlaceHeld = ZInput.GetButton("AltPlace");
            return IsActive(altPlaceHeld);
        }

        /// <summary>Whether free placement should apply right now.</summary>
        internal static bool IsActive(bool vanillaAltPlaceHeld)
        {
            if (!ModConfig.IsEnabled)
            {
                return vanillaAltPlaceHeld;
            }

            switch (ModConfig.FreePlacement.Value)
            {
                case FreePlacementMode.Hold:
                    return IsKeyHeld();
                case FreePlacementMode.Toggle:
                    return _toggledOn;
                default:
                    return vanillaAltPlaceHeld;
            }
        }

        /// <summary>Called once per placement update to service the toggle.</summary>
        internal static void HandleInput(Player player)
        {
            if (ModConfig.FreePlacement.Value != FreePlacementMode.Toggle)
            {
                return;
            }

            if (!IsKeyDown())
            {
                return;
            }

            _toggledOn = !_toggledOn;
            HammerOfOdenPlugin.Debug($"Free placement toggled {(_toggledOn ? "on" : "off")}.");

            if (player != null)
            {
                ((Character)player).Message(
                    MessageHud.MessageType.TopLeft,
                    "Free placement: " + (_toggledOn ? "on" : "off"));
            }
        }

        /// <summary>Drop the toggle when leaving build mode, so it never surprises you later.</summary>
        internal static void Reset()
        {
            _toggledOn = false;
        }

        private static bool IsKeyHeld()
        {
            KeyboardShortcut shortcut = ModConfig.FreePlacementKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
        }

        private static bool IsKeyDown()
        {
            KeyboardShortcut shortcut = ModConfig.FreePlacementKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }
    }
}
