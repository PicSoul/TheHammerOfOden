using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Restricts the ghost to whole steps of a world grid.
    /// </summary>
    /// <remarks>
    /// Snap points solve alignment between pieces that were built to meet each other. They
    /// do nothing for pieces that were not: torches spaced evenly along a wall, fence posts
    /// across open ground, chests in a row, anything standing free. Vanilla leaves those to
    /// the eye, and the eye is reliably about ten centimetres out.
    ///
    /// The grid is fixed to the world rather than to where you started building, so it is
    /// the same grid tomorrow, in another session, on the other side of the base - and it is
    /// the same grid for anyone else in the world, since nothing about it is stored.
    ///
    /// Height is left alone by default. Ground is rarely flat, and quantising height there
    /// either buries a piece or floats it; the depth and nudge offsets are the better tool
    /// for height, and GridHeight is there for when you are building on something level.
    /// </remarks>
    internal static class PlacementGrid
    {
        private static bool _on;

        internal static bool IsOn => _on;

        /// <summary>Called once per placement update to service the toggle.</summary>
        internal static void HandleInput(Player player)
        {
            if (!IsKeyDown())
            {
                return;
            }

            _on = !_on;

            HammerOfOdenPlugin.Debug($"Grid snapping {(_on ? "on" : "off")}.");
            Notify.Show(player, _on
                ? $"Grid: {ModConfig.GridSize.Value}m"
                : "Grid: off");
        }

        internal static void Reset()
        {
            _on = false;
        }

        /// <summary>
        /// Quantises the ghost's position to the grid.
        /// </summary>
        /// <remarks>
        /// Applied to the aim-derived position only. A frozen piece is already sitting
        /// exactly where it was put, and one laid against a surface is answering to that
        /// surface; rounding either to a grid would drag it off the thing it was lined up
        /// with, which is the opposite of the point.
        /// </remarks>
        internal static void Apply(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || !_on || ghost == null || !ghost.activeSelf)
            {
                return;
            }

            float size = ModConfig.GridSize.Value;
            if (size <= 0f)
            {
                return;
            }

            Vector3 position = ghost.transform.position;

            position.x = Mathf.Round(position.x / size) * size;
            position.z = Mathf.Round(position.z / size) * size;

            if (ModConfig.GridHeight.Value)
            {
                position.y = Mathf.Round(position.y / size) * size;
            }

            ghost.transform.position = position;
        }

        private static bool IsKeyDown()
        {
            KeyboardShortcut shortcut = ModConfig.GridKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }
    }
}
