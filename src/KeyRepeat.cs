using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Hold-to-repeat for the keys that adjust a piece a step at a time.
    /// </summary>
    /// <remarks>
    /// A tap is one step, always. Holding the key past a short delay then repeats it, so a long
    /// adjustment no longer means tapping twenty times while a fine one is exactly as easy as it
    /// was. The delay is the whole trick: long enough that a quick tap is never mistaken for a
    /// hold, short enough that holding feels immediate.
    ///
    /// Nudging used to be deliberately left without repeat, on the reasoning that lining a piece
    /// up by eye is one step at a time and repeat would overshoot. The delay answers that: a
    /// player who wants one step taps, and one who wants twenty holds.
    ///
    /// Tracked per key, so two keys held together each keep their own rhythm, and a key's state
    /// is forgotten the moment it is released.
    /// </remarks>
    internal static class KeyRepeat
    {
        private static readonly Dictionary<KeyCode, float> NextStep = new Dictionary<KeyCode, float>();

        /// <summary>True on the press, and then repeatedly while it is held.</summary>
        /// <param name="slower">Stretch the repeat interval, for keys whose steps are large.</param>
        internal static bool Fires(ConfigEntry<KeyboardShortcut> entry, float slower = 1f)
        {
            return Fires(entry.Value, slower);
        }

        internal static bool Fires(KeyboardShortcut shortcut, float slower = 1f)
        {
            KeyCode key = shortcut.MainKey;
            if (key == KeyCode.None)
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier, true))
                {
                    NextStep.Remove(key);
                    return false;
                }
            }

            if (ZInput.GetKeyDown(key, true))
            {
                NextStep[key] = Time.time + Mathf.Max(0.05f, ModConfig.HoldRepeatDelay.Value);
                return true;
            }

            if (!ZInput.GetKey(key, true))
            {
                NextStep.Remove(key);
                return false;
            }

            if (!NextStep.TryGetValue(key, out float next) || Time.time < next)
            {
                return false;
            }

            NextStep[key] = Time.time + Mathf.Max(0.01f, ModConfig.HoldRepeatRate.Value) * Mathf.Max(1f, slower);
            return true;
        }
    }
}
