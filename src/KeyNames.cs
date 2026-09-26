using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Keys as a player would say them: "Ctrl + Z", "Numpad Enter", "Middle mouse" - not the
    /// config's "Z + LeftControl", "KeypadEnter", "Mouse2".
    /// </summary>
    /// <remarks>
    /// One place for it, used by the key hint strip, the messages and the F3 guide, so a key reads
    /// the same everywhere - and always as it is actually bound, since every one of them asks the
    /// player's own settings rather than repeating the defaults.
    ///
    /// Modifiers come first, the way a key is pressed and the way people write them. Left and
    /// right are left off Shift, Ctrl and Alt: a player reading a hint presses whichever is under
    /// their hand, and the mod's defaults are all the left ones anyway.
    /// </remarks>
    internal static class KeyNames
    {
        internal static string Of(ConfigEntry<KeyboardShortcut> entry)
        {
            return entry == null ? string.Empty : Of(entry.Value);
        }

        internal static string Of(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None)
            {
                return string.Empty;
            }

            StringBuilder text = new StringBuilder();
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                text.Append(Name(modifier)).Append(" + ");
            }

            return text.Append(Name(shortcut.MainKey)).ToString();
        }

        /// <summary>Just the key a shortcut is held by, for "hold Shift" hints.</summary>
        internal static string MainOf(ConfigEntry<KeyboardShortcut> entry)
        {
            return entry == null || entry.Value.MainKey == KeyCode.None ? string.Empty : Name(entry.Value.MainKey);
        }

        internal static bool IsBound(ConfigEntry<KeyboardShortcut> entry)
        {
            return entry != null && entry.Value.MainKey != KeyCode.None;
        }

        private static readonly Dictionary<KeyCode, string> Names = new Dictionary<KeyCode, string>
        {
            { KeyCode.LeftShift, "Shift" }, { KeyCode.RightShift, "Shift" },
            { KeyCode.LeftControl, "Ctrl" }, { KeyCode.RightControl, "Ctrl" },
            { KeyCode.LeftAlt, "Alt" }, { KeyCode.RightAlt, "Alt" },
            { KeyCode.Mouse0, "Left mouse" }, { KeyCode.Mouse1, "Right mouse" }, { KeyCode.Mouse2, "Middle mouse" },
            { KeyCode.Mouse3, "Mouse 4" }, { KeyCode.Mouse4, "Mouse 5" },
            { KeyCode.KeypadPlus, "Numpad +" }, { KeyCode.KeypadMinus, "Numpad -" },
            { KeyCode.KeypadMultiply, "Numpad *" }, { KeyCode.KeypadDivide, "Numpad /" },
            { KeyCode.KeypadPeriod, "Numpad ." }, { KeyCode.KeypadEnter, "Numpad Enter" },
            { KeyCode.UpArrow, "Up" }, { KeyCode.DownArrow, "Down" },
            { KeyCode.LeftArrow, "Left" }, { KeyCode.RightArrow, "Right" },
            { KeyCode.PageUp, "Page Up" }, { KeyCode.PageDown, "Page Down" },
            { KeyCode.Return, "Enter" }, { KeyCode.Escape, "Esc" },
            { KeyCode.BackQuote, "`" }, { KeyCode.Minus, "-" }, { KeyCode.Equals, "=" },
            { KeyCode.LeftBracket, "[" }, { KeyCode.RightBracket, "]" },
            { KeyCode.Semicolon, ";" }, { KeyCode.Quote, "'" }, { KeyCode.Comma, "," },
            { KeyCode.Period, "." }, { KeyCode.Slash, "/" }, { KeyCode.Backslash, "\\" }
        };

        internal static string Name(KeyCode key)
        {
            if (Names.TryGetValue(key, out string name))
            {
                return name;
            }

            string raw = key.ToString();

            if (raw.StartsWith("Keypad"))
            {
                return "Numpad " + raw.Substring(6);
            }

            if (raw.StartsWith("Alpha"))
            {
                return raw.Substring(5);
            }

            // "CapsLock" to "Caps Lock", "ScrollLock" to "Scroll Lock".
            StringBuilder spaced = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                {
                    spaced.Append(' ');
                }

                spaced.Append(raw[i]);
            }

            return spaced.ToString();
        }
    }
}
