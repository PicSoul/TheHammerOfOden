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
            Selecting,
            Copying,
            Models,
            Blueprints,
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
                BlueprintBook.Close();
                _open = true;
                Overlay.Unfocus();
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
            GUILayout.Label("<b><color=#F5D278>ᚦ   " + Words.T("$hoo_help_title") + "   ᚦ</color></b>", _heading);
            GUILayout.Label("<color=#94A3B8>" + Words.T("$hoo_help_subtitle") + "</color>", _subHeading);
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
            GUILayout.Label("<color=#FCD34D><b>" + Words.T("$hoo_help_search") + "</b></color>", _searchLabel, GUILayout.ExpandWidth(false));
            _searchFilter = GUILayout.TextField(_searchFilter, _searchField, GUILayout.Width(220f));
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                if (GUILayout.Button(Words.T("$hoo_help_clear"), _tabNormal, GUILayout.ExpandWidth(false), GUILayout.Height(22f)))
                {
                    _searchFilter = "";
                }
            }

            GUILayout.FlexibleSpace();

            if (ServerEnforced)
            {
                GUILayout.Label("<color=#60A5FA><b>ℹ " + Words.T("$hoo_help_server_enforced") + "</b></color>", _footerText);
                GUILayout.Space(12f);
            }

            GUILayout.Label("<color=#64748B>" + Words.T("$hoo_help_drag_hint") + "</color>", _footerText);
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
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Selecting) Selecting();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Selecting) Groups();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Copying) Copying();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Models) Models();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Blueprints) Blueprints();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Runs) Runs();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Camera) Camera();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Doors) Doors();
            if (_currentTab == CategoryTab.All || _currentTab == CategoryTab.Stations) Stations();

            GUILayout.Space(12f);
            GUILayout.EndScrollView();

            // 5. Footer Info Bar
            GUILayout.BeginHorizontal(_footerBox);
            GUILayout.Space(8f);
            GUILayout.Label(Words.T("$hoo_help_footer_close", "<color=#FCD34D><b>" + Key(ModConfig.HelpKey) + "</b></color>", "<color=#FCD34D><b>Esc</b></color>"), _footerText);
            GUILayout.FlexibleSpace();
            GUILayout.Label("<color=#64748B>" + Words.T("$hoo_help_footer_config") + "</color>", _footerText);
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            // Allow dragging window from title header
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 45f));
        }

        private static void DrawCategoryTabs()
        {
            // Two rows: one piece at a time, then whole buildings and the rest.
            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            TabBtn(CategoryTab.All, "$hoo_help_tab_all");
            TabBtn(CategoryTab.Rotating, "$hoo_help_tab_rotate");
            TabBtn(CategoryTab.Scaling, "$hoo_help_tab_scale");
            TabBtn(CategoryTab.Bending, "$hoo_help_tab_bend");
            TabBtn(CategoryTab.Placing, "$hoo_help_tab_place");
            TabBtn(CategoryTab.Snapping, "$hoo_help_tab_snap");
            TabBtn(CategoryTab.Runs, "$hoo_help_tab_runs");
            TabBtn(CategoryTab.Master, "$hoo_help_tab_master");
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            TabBtn(CategoryTab.Selecting, "$hoo_help_tab_select");
            TabBtn(CategoryTab.Copying, "$hoo_help_tab_copy");
            TabBtn(CategoryTab.Models, "$hoo_help_tab_models");
            TabBtn(CategoryTab.Blueprints, "$hoo_help_tab_blueprints");
            TabBtn(CategoryTab.Camera, "$hoo_help_tab_camera");
            TabBtn(CategoryTab.Doors, "$hoo_help_tab_doors");
            TabBtn(CategoryTab.Stations, "$hoo_help_tab_stations");
            GUILayout.Space(8f);
            GUILayout.EndHorizontal();
        }

        private static void TabBtn(CategoryTab tab, string label)
        {
            label = L(label);
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
            Section("$hoo_help_master", "ᛗ");
            Row(ModConfig.MasterToggleKey, "$hoo_help_master_toggle");
            Note(Words.T("$hoo_help_master_note1")
                + (ModConfig.LockWhileMoving.Value ? " " + Words.T("$hoo_help_master_note1_still") : string.Empty));
            Note(Words.T("$hoo_help_master_note2"));
        }

        private static void Rotating()
        {
            Section("$hoo_help_rotating", "ᚱ");
            Plain("$hoo_help_key_scroll_wheel", "$hoo_help_rot_turn");
            Plain(Words.T("$hoo_help_key_hold_scroll", Key(ModConfig.XAxisKey)), "$hoo_help_rot_pitch");
            Plain(Words.T("$hoo_help_key_hold_scroll", Key(ModConfig.ZAxisKey)), "$hoo_help_rot_roll");
            Plain("$hoo_help_key_hold_both_scroll", "$hoo_help_rot_yaw_aim");
            Row(ModConfig.ResetAxisKey, "$hoo_help_rot_reset_axis");
            Row(ModConfig.ResetAllKey, "$hoo_help_rot_reset_all");
            Row(ModConfig.SnapIncreaseKey, "$hoo_help_rot_finer");
            Row(ModConfig.SnapDecreaseKey, "$hoo_help_rot_coarser");
            Note(Words.T("$hoo_help_rot_note", ModConfig.SnapDivisions.Value.ToString(),
                (360f / Mathf.Max(1, ModConfig.SnapDivisions.Value)).ToString("0.#")));
        }

        private static void Scaling()
        {
            Section("$hoo_help_scaling", "ᛊ");
            Plain(Words.T("$hoo_help_key_hold", Key(ModConfig.ScaleModifierKey)), "$hoo_help_scale_hold");
            Row(ModConfig.ScaleWiderKey, "$hoo_help_scale_wider", ModConfig.ScaleNarrowerKey, "$hoo_help_scale_narrower");
            Row(ModConfig.ScaleTallerKey, "$hoo_help_scale_taller", ModConfig.ScaleShorterKey, "$hoo_help_scale_shorter");
            Row(ModConfig.ScaleDeeperKey, "$hoo_help_scale_deeper", ModConfig.ScaleShallowerKey, "$hoo_help_scale_shallower");
            Row(ModConfig.ScaleUpKey, "$hoo_help_scale_bigger", ModConfig.ScaleDownKey, "$hoo_help_scale_smaller");
            Row(ModConfig.ScaleResetKey, "$hoo_help_scale_reset");
            Note(Words.T(ModConfig.ScaleRestrictions.Value == ScaleRestriction.Nothing
                    ? "$hoo_help_scale_note_all"
                    : ModConfig.ScaleRestrictions.Value == ScaleRestriction.ProductionStations
                        ? "$hoo_help_scale_note_stations"
                        : "$hoo_help_scale_note_usable")
                + " " + Words.T("$hoo_help_scale_note_range", ModConfig.ScaleMin.Value.ToString("0.##"),
                    ModConfig.ScaleMax.Value.ToString("0.##"), ModConfig.ScaleStep.Value.ToString("0.##")));
        }

        private static void Bending()
        {
            Section("$hoo_help_bending", "ᛒ");
            Plain(Words.T("$hoo_help_key_hold_scroll", Key(ModConfig.BendModifierKey)), "$hoo_help_bend_curve");
            Row(ModConfig.BendAxisKey, "$hoo_help_bend_swap");
            Row(ModConfig.BendResetKey, "$hoo_help_bend_straighten");
            Note(Words.T("$hoo_help_bend_note", Key(ModConfig.BendModifierKey), ModConfig.BendMaximum.Value.ToString("0")));
        }

        private static void Placing()
        {
            Section("$hoo_help_placing", "ᛈ");
            Row(ModConfig.FreePlacementKey, "$hoo_help_place_free");
            Row(ModConfig.SurfacePlacementKey, "$hoo_help_place_surface");
            Row(ModConfig.FreezeKey, "$hoo_help_place_freeze");
            Row(ModConfig.GridKey, Words.T("$hoo_help_place_grid", ModConfig.GridSize.Value.ToString("0.##")));
            Row(ModConfig.ClippingToggleKey, "$hoo_help_place_clipping");
            Plain(Key(ModConfig.NudgeForwardKey) + " / " + Key(ModConfig.NudgeBackwardKey), "$hoo_help_nudge_depth");
            Plain(Key(ModConfig.NudgeLeftKey) + " / " + Key(ModConfig.NudgeRightKey), "$hoo_help_nudge_horizontal");
            Plain(Key(ModConfig.NudgeUpKey) + " / " + Key(ModConfig.NudgeDownKey), "$hoo_help_nudge_vertical");
            Plain(Words.T("$hoo_help_key_hold", Key(ModConfig.NudgeLargeModifierKey)), "$hoo_help_nudge_large");
            Row(ModConfig.ResetOffsetKey, "$hoo_help_nudge_reset");
            Note(Words.T("$hoo_help_nudge_note", ModConfig.NudgeStep.Value.ToString("0.###"), ModConfig.NudgeStepLarge.Value.ToString("0.###")));
            Note(Words.T("$hoo_help_free_note", DescribeFreedom(ModConfig.Freedom.Value))
                + (ModConfig.BuildWithoutWorkbench.Value ? " " + Words.T("$hoo_help_free_note_workbench") : string.Empty)
                + " " + Words.T("$hoo_help_free_note_ward"));
        }

        private static string DescribeFreedom(PlacementFreedom freedom)
        {
            switch (freedom)
            {
                case PlacementFreedom.Vanilla: return Words.T("$hoo_help_freedom_vanilla");
                case PlacementFreedom.Surfaces: return Words.T("$hoo_help_freedom_surfaces");
                case PlacementFreedom.SurfacesAndSpacing: return Words.T("$hoo_help_freedom_spacing");
                case PlacementFreedom.Everything: return Words.T("$hoo_help_freedom_everything");
                default: return Words.T("$hoo_help_freedom_unrestricted");
            }
        }

        private static void Snapping()
        {
            Section("$hoo_help_snapping", "ᛋ");
            Row(ModConfig.CycleDerivedSnapPointsKey, "$hoo_help_snap_cycle");
            Row(ModConfig.EditKey, "$hoo_help_snap_edit");
            Note(Words.T("$hoo_help_snap_note", Key(ModConfig.FreezeKey)));
        }

        private static void Selecting()
        {
            Section("$hoo_help_selecting", "ᛗ");
            Row(ModConfig.SelectKey, "$hoo_help_sel_toggle");
            Row(ModConfig.SelectGrowKey, "$hoo_help_sel_grow", ModConfig.SelectShrinkKey, "$hoo_help_sel_shrink");
            Row(ModConfig.SelectBuildingKey, "$hoo_help_sel_building");
            Row(ModConfig.SelectTypeKey, "$hoo_help_sel_type");
            Row(ModConfig.SelectClearKey, "$hoo_help_sel_clear");
            Note(Words.T("$hoo_help_sel_note", ModConfig.SelectionLimit.Value.ToString()) + " "
                + (Selection.Count > 0 ? Words.T("$hoo_help_sel_note_count", Selection.Count.ToString()) : Words.T("$hoo_help_sel_note_none")));
        }

        private static void Groups()
        {
            Section("$hoo_help_groups", "ᛟ");
            Plain(Words.T("$hoo_help_key_on_selected", Key(ModConfig.EditKey)), "$hoo_help_groups_move");
            Plain("$hoo_help_key_shift_mmb_selected", "$hoo_help_groups_copy");
            Plain("$hoo_help_key_mmb_twice_selected", "$hoo_help_groups_takedown");
            Row(ModConfig.BlueprintSaveKey, "$hoo_help_groups_save");
            Note(Words.T("$hoo_help_groups_note1") + " "
                + Words.T(ModConfig.CopyAllowUnlearned.Value ? "$hoo_help_groups_note_unlearned_in" : "$hoo_help_groups_note_unlearned_out")
                + " " + Words.T("$hoo_help_groups_note2"));
        }

        private static void Copying()
        {
            Section("$hoo_help_copying", "ᚺ");
            Plain("$hoo_help_key_lmb_holding_copy", "$hoo_help_copy_place");
            Row(ModConfig.SiteCancelKey, "$hoo_help_copy_stop");
            Row(ModConfig.SiteTakeDownKey, "$hoo_help_copy_takedown");
            Note(Words.T("$hoo_help_copy_note1", ModConfig.SiteRange.Value.ToString("0")));
            Note(Words.T("$hoo_help_copy_note2"));
        }

        private static void Models()
        {
            Section("$hoo_help_models", "ᛗ");
            Row(ModConfig.ModelKey, "$hoo_help_model_toggle");
            Plain(Words.T("$hoo_help_key_hold", Key(ModConfig.ScaleModifierKey)) + " + " + Key(ModConfig.ScaleUpKey) + " / " + Key(ModConfig.ScaleDownKey),
                "$hoo_help_model_size");
            Plain(Key(ModConfig.NudgeUpKey) + " / " + Key(ModConfig.NudgeDownKey), "$hoo_help_model_raise");
            Plain("$hoo_help_key_lmb", "$hoo_help_model_place");
            Plain(Words.T("$hoo_help_key_mmb_twice_or", Key(ModConfig.SiteCancelKey)), "$hoo_help_model_off");
            Note(Words.T("$hoo_help_model_note", ModConfig.ModelDrawDistance.Value.ToString("0")));
        }

        private static void Blueprints()
        {
            Section("$hoo_help_blueprints", "ᛒ");
            Row(ModConfig.BlueprintBookKey, "$hoo_help_bp_book");
            Row(ModConfig.BlueprintSaveKey, "$hoo_help_bp_save");
            Plain("$hoo_help_key_drag_scroll_preview", "$hoo_help_bp_preview");
            Note(Words.T("$hoo_help_bp_note1"));
            Note(Words.T("$hoo_help_bp_note2"));
        }

        private static void Runs()
        {
            Section("$hoo_help_runs", "ᛉ");
            Plain(Words.T("$hoo_help_key_hold_arrows", Key(ModConfig.ZoopModifierKey)) + " / " + Key(ModConfig.NudgeUpKey) + " / "
                + Key(ModConfig.NudgeDownKey), "$hoo_help_runs_zoop");
            Row(ModConfig.ZoopGapWiderKey, "$hoo_help_runs_gap_wider", ModConfig.ZoopGapNarrowerKey, "$hoo_help_runs_gap_narrower");
            Row(ModConfig.UndoKey, "$hoo_help_runs_undo");
            Row(ModConfig.RedoKey, "$hoo_help_runs_redo");
            string gap = Mathf.Approximately(Zooping.Gap, 0f)
                ? Words.T("$hoo_help_runs_gap_none")
                : Zooping.Gap > 0f ? Words.T("$hoo_help_runs_gap_metres", Zooping.Gap.ToString("0.##"))
                : Words.T("$hoo_help_runs_gap_overlap", (-Zooping.Gap).ToString("0.##"));
            Note(Words.T("$hoo_help_runs_note", gap, ModConfig.ZoopGapStep.Value.ToString("0.##"), Key(ModConfig.ResetOffsetKey),
                    ModConfig.ZoopLimit.Value.ToString(), ModConfig.UndoDepth.Value.ToString())
                + " " + Words.T(ModConfig.UndoRefundsToInventory.Value ? "$hoo_help_runs_note_refund" : "$hoo_help_runs_note_drop"));
        }

        private static void Camera()
        {
            Section("$hoo_help_camera", "ᚲ");
            Row(ModConfig.BuildCameraKey, "$hoo_help_cam_toggle");
            Plain("$hoo_help_key_movement", "$hoo_help_cam_fly");
            Plain(Key(ModConfig.CameraUpKey) + " / " + Key(ModConfig.CameraDownKey), "$hoo_help_cam_vertical");
            Plain(Words.T("$hoo_help_key_hold", Key(ModConfig.CameraBoostKey)), "$hoo_help_cam_boost");
            Note(Words.T("$hoo_help_cam_note", ModConfig.CameraRange.Value.ToString("0"))
                + (ModConfig.CameraPickup.Value ? " " + Words.T("$hoo_help_cam_note_pickup") : string.Empty));
        }

        private static void Doors()
        {
            Section("$hoo_help_doors", "ᛞ");
            Plain("$hoo_help_key_use", "$hoo_help_doors_open");
            Note(Words.T("$hoo_help_doors_note", ModConfig.DoorReach.Value.ToString("0.#")));
        }

        private static void Stations()
        {
            Section("$hoo_help_stations", "ᚹ");
            Plain(Words.T("$hoo_help_key_hold_scroll", Key(ModConfig.StationRangeKey)), "$hoo_help_stations_radius");
            Note(Words.T("$hoo_help_stations_note", ModConfig.StationRangeMin.Value.ToString("0.#"), ModConfig.StationRangeMax.Value.ToString("0.#"))
                + (ModConfig.ExtendReachToStation.Value
                    ? " " + Words.T("$hoo_help_stations_note_reach", ModConfig.ReachLimit.Value.ToString("0"))
                    : string.Empty));
        }

        // ------------------------------------------------------------------ drawing helpers

        private static void Section(string title, string rune)
        {
            title = L(title);

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
            // In the player's language before the search looks at it, so a search finds what is shown.
            keys = L(keys);
            what = L(what);

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
            text = L(text);
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
            GUILayout.Label($"<color=#F59E0B><b>✦ {Words.T("$hoo_help_tip")}</b></color>  <color=#CBD5E1>{text}</color>", _note);
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

        /// <summary>Text in the player's language: a key, or text with keys inside it.</summary>
        private static string L(string text)
        {
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
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
                return "<color=#64748B><i>[ " + Words.T("$hoo_help_unbound") + " ]</i></color>";
            }

            KeyboardShortcut shortcut = entry.Value;
            if (shortcut.MainKey == KeyCode.None)
            {
                return "<color=#64748B><i>[ " + Words.T("$hoo_help_unbound") + " ]</i></color>";
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
            return KeyNames.Name(key);
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
