using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The in-game reference: what every key does, read from the keys themselves.
    /// </summary>
    /// <remarks>
    /// Every binding shown here is read out of the live config entry at the moment it is drawn,
    /// never from a list written alongside it. A printed sheet of shortcuts is wrong the first
    /// time anybody rebinds anything, and on a server it is wrong for everyone the moment an
    /// admin changes a setting - the values a joined player is using are the server's, not the
    /// ones in their own file, and this shows what they are actually holding.
    ///
    /// Drawn with IMGUI rather than built out of Valheim's own UI. A panel of this kind is worth
    /// about two hundred lines either way; the difference is that Unity's immediate mode needs
    /// no font asset, no prefab, no canvas and no teardown, where reusing the game's UI means
    /// sourcing a TMP_FontAsset from a game that does not hand them out and keeping a hierarchy
    /// alive across scene loads. The cost is that it draws only while it is open, which for a
    /// help page is not a cost at all.
    /// </remarks>
    internal static class HelpPanel
    {
        private const int WindowId = 0x484F4F; // "HOO"

        private static bool _open;
        private static Vector2 _scroll;
        private static Rect _window = new Rect(0f, 0f, 760f, 640f);
        private static bool _placed;

        private static GUIStyle _heading;
        private static GUIStyle _section;
        private static GUIStyle _key;
        private static GUIStyle _text;
        private static GUIStyle _note;
        private static Texture2D _backdrop;

        internal static bool IsOpen => _open;

        internal static void Toggle()
        {
            _open = !_open;

            // Valheim keeps the cursor hidden and the camera on the mouse while playing, and a
            // panel you cannot point at is not much of a panel.
            if (_open)
            {
                _scroll = Vector2.zero;
            }
        }

        internal static void Close()
        {
            _open = false;
        }

        internal static void Draw()
        {
            if (!_open)
            {
                return;
            }

            EnsureStyles();

            if (!_placed)
            {
                _placed = true;
                _window.x = (Screen.width - _window.width) * 0.5f;
                _window.y = Mathf.Max(20f, (Screen.height - _window.height) * 0.5f);
            }

            GUI.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.96f);
            _window = GUI.Window(WindowId, _window, DrawWindow, string.Empty);
        }

        private static void DrawWindow(int id)
        {
            GUILayout.Space(6f);
            GUILayout.Label("The Hammer of Oden", _heading);
            GUILayout.Label(
                ServerEnforced
                    ? "Showing the server's settings, which are the ones you are playing with."
                    : "Showing your settings, read from the config as they are right now.",
                _note);

            GUILayout.Space(8f);
            _scroll = GUILayout.BeginScrollView(_scroll);

            Master();
            Rotating();
            Scaling();
            Bending();
            Placing();
            Snapping();
            Runs();
            Camera();
            Doors();
            Stations();

            GUILayout.Space(10f);
            GUILayout.Label(
                "Everything above is a config setting. Change a key there and this page changes "
                + "with it - nothing here is written down twice.",
                _note);
            GUILayout.Space(8f);

            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            GUILayout.Label("Press " + Key(ModConfig.HelpKey) + " again to close.", _note);

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 32f));
        }

        // ------------------------------------------------------------------ sections

        private static void Master()
        {
            Section("The master switch");
            Row(ModConfig.MasterToggleKey, "Turn the whole mod on or off");
            Note("The hammer glows and throws a few slow motes while the mod is on, so the state "
                + "is readable at a glance. Everything but the build camera applies to the hammer "
                + "alone; the camera also works with the hoe and cultivator.");
        }

        private static void Rotating()
        {
            Section("Rotating");
            Plain("Scroll wheel", "Turn the piece on the flat");
            Plain("Hold " + Key(ModConfig.XAxisKey) + " + scroll", "Tip it forward and back");
            Plain("Hold " + Key(ModConfig.ZAxisKey) + " + scroll", "Roll it left and right");
            Plain("Hold both + scroll", "Push it along your aim");
            Row(ModConfig.ResetAxisKey, "Reset only the axis you are holding");
            Row(ModConfig.ResetAllKey, "Reset every axis at once");
            Row(ModConfig.SnapIncreaseKey, "Finer rotation steps");
            Row(ModConfig.SnapDecreaseKey, "Coarser rotation steps");
            Note("Currently " + ModConfig.SnapDivisions.Value + " steps to a full turn, or "
                + (360f / Mathf.Max(1, ModConfig.SnapDivisions.Value)).ToString("0.#") + " degrees each.");
        }

        private static void Scaling()
        {
            Section("Scaling");
            Plain("Hold " + Key(ModConfig.ScaleModifierKey), "While using the keys below");
            Row(ModConfig.ScaleWiderKey, "Wider", ModConfig.ScaleNarrowerKey, "Narrower");
            Row(ModConfig.ScaleTallerKey, "Taller", ModConfig.ScaleShorterKey, "Shorter");
            Row(ModConfig.ScaleDeeperKey, "Deeper", ModConfig.ScaleShallowerKey, "Shallower");
            Row(ModConfig.ScaleUpKey, "Bigger all round", ModConfig.ScaleDownKey, "Smaller all round");
            Row(ModConfig.ScaleResetKey, "Back to its proper size");
            Note("Between " + ModConfig.ScaleMin.Value.ToString("0.##") + "x and "
                + ModConfig.ScaleMax.Value.ToString("0.##") + "x, in steps of "
                + ModConfig.ScaleStep.Value.ToString("0.##") + ". A scaled piece keeps its size "
                + "through a world reload, and everyone with the mod sees it - which is why the "
                + "mod is required on the server.");
        }

        private static void Bending()
        {
            Section("Bending");
            Plain("Hold " + Key(ModConfig.BendModifierKey) + " + scroll", "Curve the piece, both ways from straight");
            Row(ModConfig.BendAxisKey, "Swap which way it curves");
            Row(ModConfig.BendResetKey, "Straighten it");
            Note("A piece bends along its longest side, up to " + ModConfig.BendMaximum.Value.ToString("0")
                + " degrees - half a circle turns a straight beam into an arch. Each piece also has "
                + "its own lower limit, past which the inside of the curve would pass through itself.");
            Note("Only plain structure bends: one solid shape, nothing you can use, and a main mesh "
                + "the game lets a mod read. Doors, gates and ladders are out by that rule rather "
                + "than by a list of names, so pieces from other mods are judged the same way.");
        }

        private static void Placing()
        {
            Section("Placing freely");
            Row(ModConfig.FreePlacementKey, "Ignore where the game would rather put it");
            Row(ModConfig.SurfacePlacementKey, "Lay it flat against whatever you point at");
            Row(ModConfig.FreezeKey, "Pin it in the air and walk around it");
            Row(ModConfig.GridKey, "Snap to a grid of " + ModConfig.GridSize.Value.ToString("0.##") + "m");
            Row(ModConfig.ClippingToggleKey, "Allow pieces to overlap");
            Plain(Key(ModConfig.NudgeForwardKey) + " / " + Key(ModConfig.NudgeBackwardKey), "Nudge away and towards you");
            Plain(Key(ModConfig.NudgeLeftKey) + " / " + Key(ModConfig.NudgeRightKey), "Nudge left and right");
            Plain(Key(ModConfig.NudgeUpKey) + " / " + Key(ModConfig.NudgeDownKey), "Nudge up and down");
            Plain("Hold " + Key(ModConfig.NudgeLargeModifierKey), "Nudge in bigger steps");
            Row(ModConfig.ResetOffsetKey, "Undo all nudging");
            Note("Steps of " + ModConfig.NudgeStep.Value.ToString("0.###") + "m, or "
                + ModConfig.NudgeStepLarge.Value.ToString("0.###") + "m held.");
        }

        private static void Snapping()
        {
            Section("Snap points and editing");
            Row(ModConfig.CycleDerivedSnapPointsKey, "Cycle the extra anchors added to a piece");
            Row(ModConfig.EditKey, "Take a built piece back into your hands to change");
            Note("Editing keeps the piece's rotation and size, lets every control above act on it, "
                + "and swaps it for the result - the materials move across rather than being "
                + "charged twice. The piece is drawn see-through while you work, and snapping goes "
                + "automatic, since the piece already stands where it stands.");
            Note("A chest, sign or item stand with something in it is refused rather than quietly "
                + "emptied, and an edited piece comes back at full health.");
        }

        private static void Runs()
        {
            Section("Laying a run, and taking it back");
            Plain("Hold " + Key(ModConfig.ZoopModifierKey) + " + scroll", "Extend the run before placing");
            Row(ModConfig.UndoKey, "Take back the whole of the last run");
            Note("Up to " + ModConfig.ZoopLimit.Value + " extra copies in one go, spaced by the "
                + "piece's own size. Undo remembers the last " + ModConfig.UndoDepth.Value
                + " runs, not the last " + ModConfig.UndoDepth.Value + " pieces"
                + (ModConfig.UndoRefundsToInventory.Value
                    ? ", and hands the materials back into your inventory - anything that will not "
                      + "fit, by slots or by weight, is dropped at your feet."
                    : ", and drops the materials where the pieces stood."));
        }

        private static void Camera()
        {
            Section("The build camera");
            Row(ModConfig.BuildCameraKey, "Detach the camera and fly it");
            Plain("Movement keys", "Fly");
            Plain(Key(ModConfig.CameraUpKey) + " / " + Key(ModConfig.CameraDownKey), "Up and down");
            Plain("Hold " + Key(ModConfig.CameraBoostKey), "Faster");
            Note("Your character stays put and placement follows the camera, so you can put a piece "
                + "where you could never have stood to aim at it. It reaches "
                + ModConfig.CameraRange.Value.ToString("0") + "m from you"
                + (ModConfig.CameraPickup.Value ? ", and picks up loose items it passes over." : ".")
                + " The tether is not arbitrary: the game keeps the world alive around your body, "
                + "and a camera beyond that either sees a half-built world or forces a second one "
                + "to be loaded, which is what makes other build cameras expensive.");
        }

        private static void Doors()
        {
            Section("Doors");
            Plain("Your use key", "Open the door you are looking at, while building");
            Row(ModConfig.AutoOpenDoorsKey, "Auto-open on approach, on or off");
            Note(ModConfig.AutoOpenDoors.Value
                ? "Auto-open is on: doors open within " + ModConfig.AutoOpenRange.Value.ToString("0.#")
                  + "m and close again once you are " + ModConfig.AutoCloseDistance.Value.ToString("0.#")
                  + "m away. Doors you opened by hand are never touched."
                : "Auto-open is off. Turned on, doors open as you approach and close behind you; "
                  + "a door you opened by hand is never touched.");
            Note("If another mod also closes doors for you, turn one of them off - two openers "
                + "that each know only distances will fight over any door you stand beside.");
        }

        private static void Stations()
        {
            Section("Workbench range and reach");
            Plain("Hold " + Key(ModConfig.StationRangeKey) + " + scroll", "Widen or narrow a workbench's circle");
            Note("Between " + ModConfig.StationRangeMin.Value.ToString("0.#") + "m and "
                + ModConfig.StationRangeMax.Value.ToString("0.#") + "m. The range belongs to that "
                + "bench, survives a reload, and other players see it too."
                + (ModConfig.ExtendReachToStation.Value
                    ? " You can build anywhere inside the circle you are standing in, up to "
                      + ModConfig.ReachLimit.Value.ToString("0") + "m."
                    : string.Empty));
        }

        // ------------------------------------------------------------------ drawing

        private static void Section(string title)
        {
            GUILayout.Space(12f);
            GUILayout.Label(title, _section);
        }

        private static void Row(ConfigEntry<KeyboardShortcut> shortcut, string what)
        {
            Plain(Key(shortcut), what);
        }

        private static void Row(
            ConfigEntry<KeyboardShortcut> first, string firstWhat,
            ConfigEntry<KeyboardShortcut> second, string secondWhat)
        {
            Plain(Key(first) + " / " + Key(second), firstWhat + " / " + secondWhat);
        }

        private static void Plain(string keys, string what)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(keys, _key, GUILayout.Width(210f));
            GUILayout.Label(what, _text);
            GUILayout.EndHorizontal();
        }

        private static void Note(string text)
        {
            GUILayout.Space(2f);
            GUILayout.Label(text, _note);
        }

        /// <summary>A binding as the player would say it, read from the entry itself.</summary>
        private static string Key(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null)
            {
                return "unbound";
            }

            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None)
            {
                return "unbound";
            }

            List<string> parts = new List<string>();
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                parts.Add(Pretty(modifier));
            }

            parts.Add(Pretty(shortcut.MainKey));
            return string.Join(" + ", parts.ToArray());
        }

        /// <summary>KeyCode names are written for code, not for people.</summary>
        private static string Pretty(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.KeypadPlus: return "Keypad +";
                case KeyCode.KeypadMinus: return "Keypad -";
                case KeyCode.KeypadPeriod: return "Keypad .";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                default:
                    string name = key.ToString();
                    return name.StartsWith("Keypad", StringComparison.Ordinal)
                        ? "Keypad " + name.Substring(6)
                        : name;
            }
        }

        /// <summary>Whether the values on show came from a server rather than this machine.</summary>
        private static bool ServerEnforced
        {
            get
            {
                try
                {
                    return ZNet.instance != null && !ZNet.instance.IsServer()
                        && ModConfig.LockServerSettings != null
                        && ModConfig.LockServerSettings.Value;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void EnsureStyles()
        {
            if (_heading != null)
            {
                return;
            }

            _backdrop = new Texture2D(1, 1);
            _backdrop.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.98f));
            _backdrop.Apply();

            _heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _heading.normal.textColor = new Color(0.95f, 0.82f, 0.55f);

            _section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            _section.normal.textColor = new Color(0.55f, 0.78f, 1f);

            _key = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            _key.normal.textColor = new Color(0.98f, 0.94f, 0.80f);

            _text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
            _text.normal.textColor = new Color(0.88f, 0.90f, 0.93f);

            _note = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                fontStyle = FontStyle.Italic,
                padding = new RectOffset(8, 8, 2, 2)
            };
            _note.normal.textColor = new Color(0.62f, 0.68f, 0.75f);

            GUI.skin.window.normal.background = _backdrop;
            GUI.skin.window.onNormal.background = _backdrop;
        }
    }
}
