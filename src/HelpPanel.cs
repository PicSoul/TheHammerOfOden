using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The in-game reference: what every key does, read from the keys themselves.
    /// Styled with authentic Norse aesthetics, interactive category tabs, search filter, and badge-styled keys.
    /// </summary>
    internal static class HelpPanel
    {
        private const int WindowId = 0x484F4F; // "HOO"

        private static bool _open;
        private static Vector2 _scroll;
        private static Rect _window = new Rect(0f, 0f, 820f, 680f);
        private static bool _placed;
        private static int _closedOnFrame = -1;

        internal static bool ClosedThisFrame => _closedOnFrame == Time.frameCount;
        internal static bool IsOpen => _open;

        public enum CategoryTab
        {
            All,
            Master,
            Rotating,
            Scaling,
            Bending,
            Placing,
            Snapping,
            Runs,
            Camera,
            Doors,
            Stations
        }

        private static CategoryTab _currentTab = CategoryTab.All;
        private static string _searchFilter = "";
        private static int _rowIndex = 0;

        // Custom GUIStyles
        private static GUIStyle _windowStyle;
        private static GUIStyle _heading;
        private static GUIStyle _subHeading;
        private static GUIStyle _section;
        private static GUIStyle _key;
        private static GUIStyle _text;
        private static GUIStyle _note;
        private static GUIStyle _noteBox;
        private static GUIStyle _rowEven;
        private static GUIStyle _rowOdd;
        private static GUIStyle _tabNormal;
        private static GUIStyle _tabActive;
        private static GUIStyle _searchField;
        private static GUIStyle _searchLabel;
        private static GUIStyle _closeButton;
        private static GUIStyle _toolbarBox;
        private static GUIStyle _footerBox;
        private static GUIStyle _footerText;
        private static GUIStyle _divider;

        // Procedural Textures
        private static Texture2D _windowBackdrop;
        private static Texture2D _cardBackdrop;
        private static Texture2D _rowEvenBackdrop;
        private static Texture2D _rowOddBackdrop;
        private static Texture2D _noteBackdrop;
        private static Texture2D _tabNormalBackdrop;
        private static Texture2D _tabActiveBackdrop;
        private static Texture2D _tabHoverBackdrop;
        private static Texture2D _searchBackdrop;
        private static Texture2D _closeBtnBackdrop;
        private static Texture2D _closeBtnHoverBackdrop;
        private static Texture2D _dividerTex;
        private static Texture2D _scrollTrackTex;
        private static Texture2D _scrollThumbTex;

        internal static void Toggle()
        {
            if (_open)
            {
                Close();
            }
            else
            {
                _open = true;
                _scroll = Vector2.zero;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ZCursor.LockState = CursorLockMode.None;
                ZCursor.Show();
            }
        }

        internal static void Close()
        {
            if (_open)
            {
                _open = false;
                _closedOnFrame = Time.frameCount;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                ZCursor.LockState = CursorLockMode.Locked;
                ZCursor.Hide();
            }
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
                _window.x = Mathf.Max(10f, (Screen.width - _window.width) * 0.5f);
                _window.y = Mathf.Max(15f, (Screen.height - _window.height) * 0.5f);
            }

            // Keep window on screen
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.white;

            _window = GUI.Window(WindowId, _window, DrawWindow, string.Empty, _windowStyle);

            GUI.backgroundColor = prevBg;
        }

        private static void DrawWindow(int id)
        {
            _rowIndex = 0;

            // 1. Header Bar
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginVertical();
            GUILayout.Label("<b><color=#F5D278>ᚦ   THE HAMMER OF ODEN   ᚦ</color></b>", _heading);
            GUILayout.Label("<color=#94A3B8>Viking Precision Architecture & Building Reference</color>", _subHeading);
            GUILayout.EndVertical();

            // Close button top right
            if (GUILayout.Button("✕", _closeButton, GUILayout.Width(30f), GUILayout.Height(26f)))
            {
                Close();
            }
            GUILayout.Space(6f);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            // 2. Toolbar (Search + Drag hint)
            GUILayout.BeginHorizontal(_toolbarBox);
            GUILayout.Space(6f);
            GUILayout.Label("<color=#FCD34D><b>Search:</b></color>", _searchLabel, GUILayout.Width(54f));
            _searchFilter = GUILayout.TextField(_searchFilter, _searchField, GUILayout.Width(220f));
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (GUILayout.Button("Clear", _tabNormal, GUILayout.Width(50f), GUILayout.Height(22f)))
                {
                    _searchFilter = "";
                }
            }

            GUILayout.FlexibleSpace();

            if (ServerEnforced)
            {
                GUILayout.Label("<color=#60A5FA><b>ℹ Server Enforced</b></color>", _footerText);
                GUILayout.Space(12f);
            }

            GUILayout.Label("<color=#64748B>Drag header to reposition</color>", _footerText);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);

            // 3. Category Filter Tabs
            DrawCategoryTabs();

            GUILayout.Space(6f);

            // 4. Scrollable Content Area
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Master) Master();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Rotating) Rotating();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Scaling) Scaling();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Bending) Bending();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Placing) Placing();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Snapping) Snapping();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Runs) Runs();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Camera) Camera();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Doors) Doors();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Stations) Stations();

            GUILayout.Space(12f);
            GUILayout.EndScrollView();

            // 5. Footer Info Bar
            GUILayout.BeginHorizontal(_footerBox);
            GUILayout.Space(8f);
            GUILayout.Label("Press <color=#FCD34D><b>" + Key(ModConfig.HelpKey) + "</b></color> or <color=#FCD34D><b>Esc</b></color> to close", _footerText);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#64748B>Controls dynamically reflect your active config</color>", _footerText);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            // Allow dragging window from title header
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 45f));
        }

        private static void DrawCategoryTabs()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);

            TabBtn(CategoryTab.All, "All");
            TabBtn(CategoryTab.Rotating, "Rotate");
            TabBtn(CategoryTab.Scaling, "Scale");
            TabBtn(CategoryTab.Bending, "Bend");
            TabBtn(CategoryTab.Placing, "Place");
            TabBtn(CategoryTab.Snapping, "Snap/Edit");
            TabBtn(CategoryTab.Runs, "Runs");
            TabBtn(CategoryTab.Camera, "Camera");
            TabBtn(CategoryTab.Doors, "Doors");
            TabBtn(CategoryTab.Stations, "Stations");
            TabBtn(CategoryTab.Master, "Master");

            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
        }

        private static void TabBtn(CategoryTab tab, string label)
        {
            bool isActive = (_currentTab == tab);
            GUIStyle style = isActive ? _tabActive : _tabNormal;
            string text = isActive ? $"<b><color=#FDE047>{label}</color></b>" : $"<color=#CBD5E1>{label}</color>";
            if (GUILayout.Button(text, style, GUILayout.Height(24f)))
            {
                _currentTab = tab;
            }
        }

        // ------------------------------------------------------------------ sections

        private static void Master()
        {
            Section("The Master Switch", "ᛗ");
            Row(ModConfig.MasterToggleKey, "Turn the whole mod on or off");
            Note("The hammer glows and emits subtle embers while active. Everything except the build "
                + "camera is limited to tools that build, so the hoe, the cultivator and modded terrain "
                + "tools get the camera and nothing else. Turning the mod off hands back its keys and "
                + "leaves the game alone - pieces you have already bent or scaled keep their shape."
                + (ModConfig.LockWhileMoving.Value
                    ? " Rotating, scaling, bending, nudging and zooping only work while you stand still."
                    : string.Empty));
        }

        private static void Rotating()
        {
            Section("Rotating Pieces", "ᚱ");
            Plain("Scroll wheel", "Turn the piece on the flat horizon");
            Plain("Hold " + Key(ModConfig.XAxisKey) + " + scroll", "Pitch: tip it forward and backward");
            Plain("Hold " + Key(ModConfig.ZAxisKey) + " + scroll", "Roll: tilt it left and right");
            Plain("Hold both + scroll", "Yaw along aim: push along placement line");
            Row(ModConfig.ResetAxisKey, "Reset only the axis you are currently holding");
            Row(ModConfig.ResetAllKey, "Reset every rotation axis at once");
            Row(ModConfig.SnapIncreaseKey, "Finer rotation steps (more divisions)");
            Row(ModConfig.SnapDecreaseKey, "Coarser rotation steps (fewer divisions)");
            Note("Currently configured to " + ModConfig.SnapDivisions.Value + " steps per full turn ("
                + (360f / Mathf.Max(1, ModConfig.SnapDivisions.Value)).ToString("0.#") + "° per click).");
        }

        private static void Scaling()
        {
            Section("Precision Scaling", "ᛊ");
            Plain("Hold " + Key(ModConfig.ScaleModifierKey), "Hold this modifier while using scaling keys below");
            Row(ModConfig.ScaleWiderKey, "Wider", ModConfig.ScaleNarrowerKey, "Narrower");
            Row(ModConfig.ScaleTallerKey, "Taller", ModConfig.ScaleShorterKey, "Shorter");
            Row(ModConfig.ScaleDeeperKey, "Deeper", ModConfig.ScaleShallowerKey, "Shallower");
            Row(ModConfig.ScaleUpKey, "Bigger (uniform)", ModConfig.ScaleDownKey, "Smaller (uniform)");
            Row(ModConfig.ScaleResetKey, "Reset piece back to default 1.0x size");
            Note((ModConfig.ScaleRestrictions.Value == ScaleRestriction.Nothing
                    ? "Every piece can be resized, stations and ships included. "
                    : ModConfig.ScaleRestrictions.Value == ScaleRestriction.ProductionStations
                        ? "Crafting and production stations keep their normal size. "
                        : "Anything you can use keeps its normal size. ")
                + "Scaling range: " + ModConfig.ScaleMin.Value.ToString("0.##") + "x to "
                + ModConfig.ScaleMax.Value.ToString("0.##") + "x in steps of "
                + ModConfig.ScaleStep.Value.ToString("0.##") + ". Scaled pieces persist through world reloads and sync to all players.");
        }

        private static void Bending()
        {
            Section("Bending & Curving Beams", "ᛒ");
            Plain("Hold " + Key(ModConfig.BendModifierKey) + " + scroll", "Curve the piece continuously in both directions");
            Row(ModConfig.BendAxisKey, "Swap curve orientation axis");
            Row(ModConfig.BendResetKey, "Straighten piece back to flat");
            Note("While " + Key(ModConfig.BendModifierKey) + " is held the gizmo lights up the ring the bend turns "
                + "around, and a message says which way the ends will curve - up, down, left, right, towards or "
                + "away from you. Pieces bend along their longest side, up to " + ModConfig.BendMaximum.Value.ToString("0")
                + "° (180° turns a straight beam into a semicircular arch). Only solid structural pieces bend; "
                + "the build menu marks them with a small arch.");
        }

        private static void Placing()
        {
            Section("Free Placement & Nudging", "ᛈ");
            Row(ModConfig.FreePlacementKey, "Free placement: disable collision / surface lock");
            Row(ModConfig.SurfacePlacementKey, "Surface placement: align flush against whatever surface you target");
            Row(ModConfig.FreezeKey, "Freeze placement ghost: pin in mid-air to inspect from all angles");
            Row(ModConfig.GridKey, "Toggle world grid snapping (" + ModConfig.GridSize.Value.ToString("0.##") + "m)");
            Row(ModConfig.ClippingToggleKey, "Allow overlapping pieces inside each other");
            Plain(Key(ModConfig.NudgeForwardKey) + " / " + Key(ModConfig.NudgeBackwardKey), "Nudge depth: forward and backward");
            Plain(Key(ModConfig.NudgeLeftKey) + " / " + Key(ModConfig.NudgeRightKey), "Nudge horizontal: left and right");
            Plain(Key(ModConfig.NudgeUpKey) + " / " + Key(ModConfig.NudgeDownKey), "Nudge vertical: upward and downward");
            Plain("Hold " + Key(ModConfig.NudgeLargeModifierKey), "Hold for larger nudge increments");
            Row(ModConfig.ResetOffsetKey, "Reset all nudging offsets back to zero");
            Note("Nudge step size: " + ModConfig.NudgeStep.Value.ToString("0.###") + "m (fine), or "
                + ModConfig.NudgeStepLarge.Value.ToString("0.###") + "m (large held).");
            Note("Free placement sets aside: " + DescribeFreedom(ModConfig.Freedom.Value)
                + (ModConfig.BuildWithoutWorkbench.Value ? " No workbench is needed while it is on." : string.Empty)
                + " Someone else's ward is always respected.");
        }

        private static string DescribeFreedom(PlacementFreedom freedom)
        {
            switch (freedom)
            {
                case PlacementFreedom.Vanilla: return "no rules - it only changes snapping.";
                case PlacementFreedom.Surfaces: return "what a piece may rest on.";
                case PlacementFreedom.SurfacesAndSpacing: return "what a piece may rest on, and the room it needs.";
                case PlacementFreedom.Everything: return "surface, spacing, biome, dungeon and weather rules.";
                default: return "every rule, no-build zones and characters in the way included.";
            }
        }

        private static void Snapping()
        {
            Section("Snap Points & In-Place Editing", "ᛋ");
            Row(ModConfig.CycleDerivedSnapPointsKey, "Cycle derived center, face, and edge snap anchors");
            Row(ModConfig.EditKey, "In-place edit: pick up already-built piece to resize/rotate/reposition");
            Note("An edited piece starts frozen exactly where it stands, so small adjustments stay small; press "
                + Key(ModConfig.FreezeKey) + " to let it follow your aim for a bigger move. Placing or cancelling "
                + "releases it. Editing keeps the piece's health and materials at no extra cost. Containers, signs "
                + "and item stands with something in them cannot be edited.");
        }

        private static void Runs()
        {
            Section("Zooping Runs & Instant Undo", "ᛉ");
            Plain("Hold " + Key(ModConfig.ZoopModifierKey) + " + arrows / " + Key(ModConfig.NudgeUpKey) + " / "
                + Key(ModConfig.NudgeDownKey), "Extend a line or grid of copies (zooping) before placing");
            Row(ModConfig.ZoopGapWiderKey, "Wider gap", ModConfig.ZoopGapNarrowerKey, "Narrower gap / overlap");
            Row(ModConfig.UndoKey, "Undo: instantly dismantle and refund entire last run");
            Note("Gap between copies: " + (Mathf.Approximately(Zooping.Gap, 0f)
                    ? "none, they touch"
                    : Zooping.Gap > 0f ? Zooping.Gap.ToString("0.##") + "m" : (-Zooping.Gap).ToString("0.##") + "m overlap")
                + ", in " + ModConfig.ZoopGapStep.Value.ToString("0.##") + "m steps. "
                + Key(ModConfig.ResetOffsetKey) + " clears it with the zoop. "
                + "Builds up to " + ModConfig.ZoopLimit.Value + " continuous copies in a single click. Undo remembers the last "
                + ModConfig.UndoDepth.Value + " placement actions"
                + (ModConfig.UndoRefundsToInventory.Value
                    ? ", refunding resources directly into your inventory."
                    : ", dropping materials at piece locations."));
        }

        private static void Camera()
        {
            Section("Detached Build Camera", "ᚲ");
            Row(ModConfig.BuildCameraKey, "Toggle detached free-flight build camera");
            Plain("Movement keys", "Fly around build site (W / A / S / D)");
            Plain(Key(ModConfig.CameraUpKey) + " / " + Key(ModConfig.CameraDownKey), "Fly vertically up and down");
            Plain("Hold " + Key(ModConfig.CameraBoostKey), "High-speed camera boost flight");
            Note("Character remains anchored while placement aims from the flying camera. Tether range: "
                + ModConfig.CameraRange.Value.ToString("0") + "m from character"
                + (ModConfig.CameraPickup.Value ? " (camera picks up loose items as it flies)." : "."));
        }

        private static void Doors()
        {
            Section("Doors", "ᛞ");
            Plain("Your use key", "Open targeted door while holding building tool");
            Note("Reach: " + ModConfig.DoorReach.Value.ToString("0.#")
                + "m. Doors only - chests, stations and everything else stay shut off while a "
                + "build tool is in hand, so nothing opens by accident as you line up a piece.");
        }

        private static void Stations()
        {
            Section("Workbench Range & Extended Reach", "ᚹ");
            Plain("Hold " + Key(ModConfig.StationRangeKey) + " + scroll", "Expand or contract active workbench radius");
            Note("Configurable range: " + ModConfig.StationRangeMin.Value.ToString("0.#") + "m to "
                + ModConfig.StationRangeMax.Value.ToString("0.#") + "m. Radius persists per station."
                + (ModConfig.ExtendReachToStation.Value
                    ? " Build reach extends anywhere inside the workbench aura (up to "
                      + ModConfig.ReachLimit.Value.ToString("0") + "m)."
                    : string.Empty));
        }

        // ------------------------------------------------------------------ drawing helpers

        private static void Section(string title, string rune)
        {
            // If filtering by search, skip header if no rows match
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(4f);
            GUILayout.Label($"<color=#F59E0B>{rune}</color>  <b><color=#FCD34D>{title.ToUpperInvariant()}</color></b>", _section);
            GUILayout.EndHorizontal();

            // Decorative gold/bronze divider line
            GUILayout.Space(2f);
            GUILayout.Label(GUIContent.none, _divider, GUILayout.Height(2f));
            GUILayout.Space(4f);
        }

        private static void Row(ConfigEntry<KeyboardShortcut> shortcut, string what)
        {
            Plain(Key(shortcut), what);
        }

        private static void Row(
            ConfigEntry<KeyboardShortcut> first, string firstWhat,
            ConfigEntry<KeyboardShortcut> second, string secondWhat)
        {
            Plain(Key(first) + " <color=#64748B>/</color> " + Key(second), firstWhat + " <color=#64748B>/</color> " + secondWhat);
        }

        private static void Plain(string keys, string what)
        {
            // Search filter check
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                string cleanKeys = StripTags(keys);
                string cleanWhat = StripTags(what);
                if (cleanKeys.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                    cleanWhat.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }
            }

            _rowIndex++;
            GUIStyle rowBg = (_rowIndex % 2 == 0) ? _rowEven : _rowOdd;

            GUILayout.BeginHorizontal(rowBg);
            GUILayout.Space(8f);

            string formattedKeys = HighlightKeys(keys);
            GUILayout.Label(formattedKeys, _key, GUILayout.Width(270f));

            GUILayout.Label(what, _text);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
            GUILayout.Space(1f);
        }

        private static void Note(string text)
        {
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (text.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }
            }

            GUILayout.Space(3f);
            GUILayout.BeginHorizontal(_noteBox);
            GUILayout.Space(8f);
            GUILayout.Label($"<color=#F59E0B><b>✦ Tip:</b></color>  <color=#CBD5E1>{text}</color>", _note);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        private static string HighlightKeys(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains("<color=")) return text; // already rich-text formatted

            string s = text;
            s = s.Replace("Scroll wheel", "<color=#FDE047><b>[ Scroll Wheel ]</b></color>");
            s = s.Replace("scroll", "<color=#FDE047><b>[ Scroll ]</b></color>");
            s = s.Replace("Movement keys", "<color=#FDE047><b>[ W / A / S / D ]</b></color>");
            s = s.Replace("Your use key", "<color=#FDE047><b>[ E ]</b></color>");
            s = s.Replace(" + ", " <color=#64748B>+</color> ");
            s = s.Replace(" / ", " <color=#64748B>/</color> ");
            s = s.Replace("Hold ", "<color=#94A3B8>Hold</color> ");
            s = s.Replace("While ", "<color=#94A3B8>While</color> ");
            return s;
        }

        private static string StripTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        /// <summary>A binding formatted as a golden badge.</summary>
        private static string Key(ConfigEntry<KeyboardShortcut> entry)
        {
            if (entry == null)
            {
                return "<color=#64748B><i>[ unbound ]</i></color>";
            }

            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None)
            {
                return "<color=#64748B><i>[ unbound ]</i></color>";
            }

            List<string> parts = new List<string>();
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                parts.Add("<color=#FDE047><b>[ " + Pretty(modifier) + " ]</b></color>");
            }

            parts.Add("<color=#FDE047><b>[ " + Pretty(shortcut.MainKey) + " ]</b></color>");
            return string.Join(" <color=#64748B>+</color> ", parts.ToArray());
        }

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

            // Procedural Textures
            _windowBackdrop = MakeWindowFrame(64, 64);
            _cardBackdrop = MakeCardFrame(32, 32);
            _rowEvenBackdrop = MakeSolidTex(16, 16, new Color(0.12f, 0.14f, 0.18f, 0.40f));
            _rowOddBackdrop = MakeSolidTex(16, 16, new Color(0.06f, 0.07f, 0.09f, 0.20f));
            _noteBackdrop = MakeNoteFrame(24, 24);
            _tabNormalBackdrop = MakeButtonFrame(24, 24, new Color(0.13f, 0.15f, 0.19f, 0.85f), new Color(0.28f, 0.32f, 0.38f, 0.65f));
            _tabActiveBackdrop = MakeButtonFrame(24, 24, new Color(0.38f, 0.26f, 0.08f, 0.95f), new Color(0.85f, 0.68f, 0.28f, 1.0f));
            _tabHoverBackdrop = MakeButtonFrame(24, 24, new Color(0.22f, 0.25f, 0.32f, 0.90f), new Color(0.45f, 0.50f, 0.60f, 0.80f));
            _searchBackdrop = MakeButtonFrame(20, 20, new Color(0.08f, 0.09f, 0.12f, 0.95f), new Color(0.35f, 0.40f, 0.48f, 0.70f));
            _closeBtnBackdrop = MakeButtonFrame(24, 24, new Color(0.32f, 0.12f, 0.12f, 0.85f), new Color(0.65f, 0.25f, 0.25f, 0.90f));
            _closeBtnHoverBackdrop = MakeButtonFrame(24, 24, new Color(0.55f, 0.15f, 0.15f, 0.95f), new Color(0.95f, 0.40f, 0.40f, 1.0f));
            _dividerTex = MakeDividerTex(32, 2);
            _scrollTrackTex = MakeSolidTex(16, 16, new Color(0.05f, 0.06f, 0.08f, 0.85f));
            _scrollThumbTex = MakeButtonFrame(16, 16, new Color(0.45f, 0.35f, 0.18f, 0.90f), new Color(0.70f, 0.55f, 0.28f, 1.0f));

            // Window Style
            _windowStyle = new GUIStyle();
            _windowStyle.normal.background = _windowBackdrop;
            _windowStyle.border = new RectOffset(10, 10, 10, 10);
            _windowStyle.padding = new RectOffset(14, 14, 10, 10);

            // Headers
            _heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            _subHeading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            _section = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            // Rows & text
            _key = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                richText = true
            };

            _text = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                richText = true
            };
            _text.normal.textColor = new Color(0.88f, 0.91f, 0.95f);

            // Notes
            _note = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true
            };

            _noteBox = new GUIStyle();
            _noteBox.normal.background = _noteBackdrop;
            _noteBox.border = new RectOffset(5, 5, 5, 5);
            _noteBox.padding = new RectOffset(6, 6, 6, 6);

            _rowEven = new GUIStyle();
            _rowEven.normal.background = _rowEvenBackdrop;
            _rowEven.padding = new RectOffset(2, 2, 4, 4);

            _rowOdd = new GUIStyle();
            _rowOdd.normal.background = _rowOddBackdrop;
            _rowOdd.padding = new RectOffset(2, 2, 4, 4);

            // Tabs
            _tabNormal = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            _tabNormal.normal.background = _tabNormalBackdrop;
            _tabNormal.hover.background = _tabHoverBackdrop;
            _tabNormal.border = new RectOffset(5, 5, 5, 5);
            _tabNormal.padding = new RectOffset(8, 8, 2, 2);

            _tabActive = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            _tabActive.normal.background = _tabActiveBackdrop;
            _tabActive.border = new RectOffset(5, 5, 5, 5);
            _tabActive.padding = new RectOffset(8, 8, 2, 2);

            // Search
            _searchField = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            _searchField.normal.background = _searchBackdrop;
            _searchField.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
            _searchField.border = new RectOffset(4, 4, 4, 4);
            _searchField.padding = new RectOffset(6, 6, 3, 3);

            _searchLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };

            // Close button
            _closeButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _closeButton.normal.background = _closeBtnBackdrop;
            _closeButton.normal.textColor = new Color(0.95f, 0.85f, 0.85f);
            _closeButton.hover.background = _closeBtnHoverBackdrop;
            _closeButton.hover.textColor = Color.white;
            _closeButton.border = new RectOffset(4, 4, 4, 4);

            // Toolbars
            _toolbarBox = new GUIStyle();
            _toolbarBox.normal.background = _cardBackdrop;
            _toolbarBox.border = new RectOffset(6, 6, 6, 6);
            _toolbarBox.padding = new RectOffset(4, 4, 4, 4);

            _footerBox = new GUIStyle();
            _footerBox.normal.background = _cardBackdrop;
            _footerBox.border = new RectOffset(6, 6, 6, 6);
            _footerBox.padding = new RectOffset(4, 4, 4, 4);

            _footerText = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                richText = true
            };
            _footerText.normal.textColor = new Color(0.65f, 0.70f, 0.78f);

            _divider = new GUIStyle();
            _divider.normal.background = _dividerTex;
            _divider.margin = new RectOffset(4, 4, 2, 4);

            // Customize Scrollbars
            GUI.skin.verticalScrollbar.normal.background = _scrollTrackTex;
            GUI.skin.verticalScrollbarThumb.normal.background = _scrollThumbTex;
            GUI.skin.verticalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);
        }

        // Procedural Texture Generators
        private static Texture2D MakeSolidTex(int w, int h, Color color)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = color;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeWindowFrame(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = new Color(0.065f, 0.075f, 0.095f, 0.97f);
            Color outerIron = new Color(0.16f, 0.18f, 0.22f, 1f);
            Color highlightIron = new Color(0.26f, 0.29f, 0.35f, 1f);
            Color bronzeOuter = new Color(0.72f, 0.54f, 0.25f, 0.90f);
            Color bronzeInner = new Color(0.48f, 0.34f, 0.15f, 0.70f);
            Color rivetGold = new Color(0.95f, 0.82f, 0.45f, 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        tex.SetPixel(x, y, outerIron);
                    else if (x == 1 || x == w - 2 || y == 1 || y == h - 2)
                        tex.SetPixel(x, y, highlightIron);
                    else if ((x == 4 || x == w - 5 || y == 4 || y == h - 5) && (x >= 4 && x <= w - 5 && y >= 4 && y <= h - 5))
                        tex.SetPixel(x, y, bronzeOuter);
                    else if ((x == 5 || x == w - 6 || y == 5 || y == h - 6) && (x >= 5 && x <= w - 6 && y >= 5 && y <= h - 6))
                        tex.SetPixel(x, y, bronzeInner);
                    else
                        tex.SetPixel(x, y, bg);
                }
            }

            // Rivets at 4 corners
            int[] rx = { 5, w - 6 };
            int[] ry = { 5, h - 6 };
            foreach (int cx in rx)
            {
                foreach (int cy in ry)
                {
                    tex.SetPixel(cx, cy, rivetGold);
                    tex.SetPixel(cx + 1, cy, rivetGold);
                    tex.SetPixel(cx - 1, cy, rivetGold);
                    tex.SetPixel(cx, cy + 1, rivetGold);
                    tex.SetPixel(cx, cy - 1, rivetGold);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeCardFrame(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = new Color(0.095f, 0.105f, 0.13f, 0.75f);
            Color border = new Color(0.24f, 0.28f, 0.34f, 0.55f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        tex.SetPixel(x, y, border);
                    else
                        tex.SetPixel(x, y, bg);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeButtonFrame(int w, int h, Color bg, Color border)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color topHighlight = Color.Lerp(border, Color.white, 0.3f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x == 0 || x == w - 1 || y == 0 || y == h - 1)
                        tex.SetPixel(x, y, border);
                    else if (y == h - 2 && x > 0 && x < w - 1)
                        tex.SetPixel(x, y, topHighlight);
                    else
                        tex.SetPixel(x, y, bg);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeNoteFrame(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bg = new Color(0.09f, 0.10f, 0.13f, 0.85f);
            Color border = new Color(0.35f, 0.28f, 0.14f, 0.70f);
            Color leftBar = new Color(0.92f, 0.70f, 0.24f, 0.95f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x <= 2)
                        tex.SetPixel(x, y, leftBar); // Amber accent bar on left edge
                    else if (x == w - 1 || y == 0 || y == h - 1)
                        tex.SetPixel(x, y, border);
                    else
                        tex.SetPixel(x, y, bg);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D MakeDividerTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color bronze = new Color(0.75f, 0.58f, 0.28f, 0.80f);
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                float alpha = Mathf.Sin(t * Mathf.PI); // fade at ends, bright in center
                Color c = new Color(bronze.r, bronze.g, bronze.b, bronze.a * alpha);
                for (int y = 0; y < h; y++)
                {
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
