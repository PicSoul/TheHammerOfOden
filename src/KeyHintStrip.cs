using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The keys that do something right now, beside the screen's edge - in place of the game's
    /// own build hints, which it carries over.
    /// </summary>
    /// <remarks>
    /// The mod has well over fifty keys, and nobody remembers fifty keys. But only a handful
    /// matter at any moment, and seeing those at the moment they would be used is how keys get
    /// learned. So the strip follows what you are doing:
    ///   - holding a modifier: what that modifier does - Shift resizes and tilts, Alt selects,
    ///     the bend key bends
    ///   - holding a copy, a move, a model or an edit: how to adjust it, put it down or let go
    ///   - with a selection made: what can be done with it
    ///   - otherwise: the game's own build keys, and the mod's most used ones
    ///   - looking at a building still going up, or a model: how to stop it or take it off
    ///
    /// It takes the place of the game's build hints rather than sitting beside them - both would
    /// be too much - and carries their keys over: place, remove, copy, rotate, the build menu.
    /// With a controller the game's own hints stay, since controller buttons are another story;
    /// they stay too with the build menu open, and whenever the mod is switched off.
    ///
    /// Every key is read from the player's own settings as it is drawn, so rebinding a key
    /// changes the hint with it.
    /// </remarks>
    internal static class KeyHintStrip
    {
        private static readonly List<(string keys, string action)> Rows = new List<(string, string)>();
        private static string _title;
        private static int _builtFrame = -1;

        private static GUIStyle _title_;
        private static GUIStyle _key;
        private static GUIStyle _action;
        private static GUIStyle _box;
        private static Texture2D _backdrop;
        private static int _styledFontSize;

        /// <summary>Whether the strip is standing in for the game's build hints right now.</summary>
        internal static bool Replacing
        {
            get
            {
                Player player = Player.m_localPlayer;
                return ModConfig.KeyHintsShown.Value && ModConfig.IsEnabled && player != null
                    && BuildTool.IsBuildingTool && player.InPlaceMode()
                    && !ZInput.IsGamepadActive() && !Hud.IsPieceSelectionVisible();
            }
        }

        // ------------------------------------------------------------------ what to show

        private static void Build()
        {
            if (_builtFrame == Time.frameCount)
            {
                return;
            }

            _builtFrame = Time.frameCount;
            Rows.Clear();

            Context();

            // The camera's own keys come on top of whatever else applies - flying round a
            // building is when every other key is wanted too.
            if (BuildCamera.IsActive)
            {
                _title = Words.T("$hoo_hint_title_camera", _title);
                Row("W A S D", "$hoo_hint_fly");
                Row(Words.T("$hoo_key_alone", KeyNames.Of(ModConfig.CameraUpKey) + " / " + KeyNames.Of(ModConfig.CameraDownKey)), "$hoo_hint_up_down");
                Row(Words.T("$hoo_key_hold", KeyNames.Of(ModConfig.CameraBoostKey)), "$hoo_hint_fly_faster");
                Row(ModConfig.BuildCameraKey, "$hoo_hint_back_to_eyes");
            }
        }

        private static void Context()
        {
            Player player = Player.m_localPlayer;
            Piece hovered = player != null ? player.GetHoveringPiece() : null;

            if (Held(ModConfig.BendModifierKey))
            {
                _title = Words.T("$hoo_hint_title_bending");
                Row("$hoo_key_scroll", "$hoo_hint_bend");
                Row(ModConfig.BendAxisKey, "$hoo_hint_bend_other_way");
                Row(ModConfig.BendResetKey, "$hoo_hint_straighten");
                return;
            }

            if (GroupHold.IsModel)
            {
                _title = Words.T("$hoo_hint_title_model");
                Row("$hoo_key_lmb", "$hoo_hint_place_on_table");
                Row(With(ModConfig.ScaleModifierKey, ModConfig.ScaleUpKey) + " / " + ModConfig.ScaleDownKey.Value.MainKey.ToHint(), "$hoo_hint_bigger_smaller");
                Row(ModConfig.NudgeUpKey, ModConfig.NudgeDownKey, "$hoo_hint_raise_lower");
                Row("$hoo_key_arrows", "$hoo_hint_shift_little");
                Row("$hoo_key_scroll", "$hoo_hint_turn_it");
                Row(ModConfig.ModelKey, "$hoo_hint_back_to_copy");
                Row(ModConfig.EditKey, "$hoo_hint_put_away");
                return;
            }

            if (GroupHold.IsHolding || PlacementEdit.IsEditing)
            {
                bool copy = GroupHold.IsCopying;
                bool move = GroupHold.IsMoving;
                _title = Words.T(copy ? "$hoo_hint_title_copy" : move ? "$hoo_hint_title_move" : "$hoo_hint_title_edit");
                Row("$hoo_key_lmb", copy ? "$hoo_hint_place_copy" : "$hoo_hint_put_down");
                if (copy)
                {
                    Row(ModConfig.ModelKey, "$hoo_hint_make_model");
                }

                Row(ModConfig.FreezeKey, "$hoo_hint_freeze_follow");
                Row(With(ModConfig.ScaleModifierKey, ModConfig.ScaleUpKey) + " / " + ModConfig.ScaleDownKey.Value.MainKey.ToHint(), "$hoo_hint_bigger_smaller");
                Row("$hoo_key_scroll", "$hoo_hint_turn_it");
                Row("$hoo_key_arrows, " + KeyNames.Of(ModConfig.NudgeUpKey) + " / " + KeyNames.Of(ModConfig.NudgeDownKey), "$hoo_hint_nudge");
                Row(ModConfig.ResetOffsetKey, "$hoo_hint_clear_nudge");
                Row(ModConfig.EditKey, copy ? "$hoo_hint_put_away" : "$hoo_hint_cancel");
                return;
            }

            if (Held(ModConfig.ScaleModifierKey))
            {
                _title = Words.T("$hoo_hint_title_holding", KeyNames.MainOf(ModConfig.ScaleModifierKey));
                Row("$hoo_key_scroll", "$hoo_hint_tilt_forward");
                Row(ModConfig.ScaleWiderKey, ModConfig.ScaleNarrowerKey, "$hoo_hint_wider", true);
                Row(ModConfig.ScaleTallerKey, ModConfig.ScaleShorterKey, "$hoo_hint_taller", true);
                Row(ModConfig.ScaleDeeperKey, ModConfig.ScaleShallowerKey, "$hoo_hint_deeper", true);
                Row(ModConfig.ScaleUpKey, ModConfig.ScaleDownKey, "$hoo_hint_bigger_all", true);
                Row(ModConfig.ScaleResetKey, "$hoo_hint_normal_size");
                Row("$hoo_key_arrows", "$hoo_hint_zoop");
                Row(ModConfig.ZoopGapWiderKey, ModConfig.ZoopGapNarrowerKey, "$hoo_hint_gap");
                return;
            }

            if (Held(ModConfig.ZAxisKey))
            {
                _title = Words.T("$hoo_hint_title_holding", KeyNames.MainOf(ModConfig.ZAxisKey));
                Row("$hoo_key_scroll", "$hoo_hint_tilt_sideways");
                Row(ModConfig.SelectKey, "$hoo_hint_select");
                Row(ModConfig.SelectTypeKey, "$hoo_hint_select_type");
                Row(ModConfig.SelectGrowKey, ModConfig.SelectShrinkKey, "$hoo_hint_grow_shrink_selection");
                Row(ModConfig.EditKey, Selection.Count > 0 ? "$hoo_hint_move_or_edit" : "$hoo_hint_edit_looked_at");
                return;
            }

            _title = Words.T("$hoo_hint_title_mod");

            if (Selection.Count > 0)
            {
                _title = Words.T("$hoo_hint_title_selection", Selection.Count.ToString("N0"));
                Row(ModConfig.EditKey, "$hoo_hint_move_selection");
                Row("$hoo_key_shift_mmb", "$hoo_hint_copy_selection");
                Row("$hoo_key_mmb", "$hoo_hint_takedown_selection");
                Row(ModConfig.SelectGrowKey, ModConfig.SelectShrinkKey, "$hoo_hint_grow_shrink");
                Row(ModConfig.SelectBuildingKey, "$hoo_hint_add_building");
                Row(ModConfig.BlueprintSaveKey, "$hoo_hint_save_blueprint");
                Row(ModConfig.SelectClearKey, "$hoo_hint_clear_selection");
            }
            else
            {
                // The game's own build hints, carried over.
                Row("$hoo_key_lmb", "$hoo_hint_place");
                Row("$hoo_key_mmb", "$hoo_hint_remove");
                Row("$hoo_key_shift_mmb", "$hoo_hint_copy_piece");
                Row("$hoo_key_scroll", "$hoo_hint_turn");
                Row("$hoo_key_rmb", "$hoo_hint_build_menu");
                Row(ModConfig.SelectKey, "$hoo_hint_select_pieces");
                Row(ModConfig.SelectBuildingKey, "$hoo_hint_select_building");
            }

            Row(Words.T("$hoo_key_hold", KeyNames.MainOf(ModConfig.ScaleModifierKey)), "$hoo_hint_resize_tilt_zoop");
            Row(Words.T("$hoo_key_hold", KeyNames.MainOf(ModConfig.ZAxisKey)), "$hoo_hint_tilt_more_selecting");
            Row(ModConfig.FreezeKey, "$hoo_hint_freeze");
            Row(ModConfig.EditKey, "$hoo_hint_edit_placed");
            Row(ModConfig.UndoKey, ModConfig.RedoKey, "$hoo_hint_undo_redo");
            Row(ModConfig.BlueprintBookKey, "$hoo_hint_blueprint_book");
            if (!BuildCamera.IsActive)
            {
                Row(ModConfig.BuildCameraKey, "$hoo_hint_build_camera");
            }

            Row(ModConfig.HelpKey, "$hoo_hint_help");

            if (hovered != null && (ConstructionSite.Of(hovered) != null || ModelDisplay.Carries(hovered)))
            {
                Row(ModConfig.SiteCancelKey, ModelDisplay.Carries(hovered) ? "$hoo_hint_model_off" : "$hoo_hint_stop_site");
            }

            if (hovered != null && ConstructionSite.IdOf(hovered) != 0L)
            {
                Row(ModConfig.SiteTakeDownKey, "$hoo_hint_takedown_copy");
            }
        }

        private static void Row(string keys, string action)
        {
            if (!string.IsNullOrEmpty(keys))
            {
                Rows.Add((L(keys), L(action)));
            }
        }

        private static void Row(ConfigEntry<KeyboardShortcut> key, string action)
        {
            if (KeyNames.IsBound(key))
            {
                Rows.Add((KeyNames.Of(key), L(action)));
            }
        }

        /// <param name="shortSecond">Show the second key without the first's modifiers, when they share them.</param>
        private static void Row(ConfigEntry<KeyboardShortcut> first, ConfigEntry<KeyboardShortcut> second, string action, bool shortSecond = false)
        {
            if (!KeyNames.IsBound(first) && !KeyNames.IsBound(second))
            {
                return;
            }

            string b = shortSecond ? KeyNames.MainOf(second) : KeyNames.Of(second);
            string keys = KeyNames.IsBound(first) && KeyNames.IsBound(second) ? KeyNames.Of(first) + " / " + b
                : KeyNames.IsBound(first) ? KeyNames.Of(first) : KeyNames.Of(second);
            Rows.Add((keys, L(action)));
        }

        /// <summary>
        /// A row's text in the player's language. Rows are given translation keys - whole, or
        /// inside a longer label like "$hoo_key_arrows, Q / E" - and the translator swaps each.
        /// </summary>
        private static string L(string text)
        {
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
        }

        /// <summary>A key with a modifier added in front: "Shift + Numpad +".</summary>
        private static string With(ConfigEntry<KeyboardShortcut> modifier, ConfigEntry<KeyboardShortcut> key)
        {
            return KeyNames.MainOf(modifier) + " + " + KeyNames.Of(key);
        }

        private static bool Held(ConfigEntry<KeyboardShortcut> entry)
        {
            if (!KeyNames.IsBound(entry) || ZInput.instance == null)
            {
                return false;
            }

            if (!ZInput.GetKey(entry.Value.MainKey, true))
            {
                return false;
            }

            foreach (KeyCode modifier in entry.Value.Modifiers)
            {
                if (!ZInput.GetKey(modifier, true))
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------ drawing

        internal static void Draw()
        {
            if (!Replacing || !SitePanel.GameDisplaysShowing() || InventoryGui.IsVisible()
                || (Chat.instance != null && Chat.instance.IsChatDialogWindowVisible()))
            {
                return;
            }

            Build();
            if (Rows.Count == 0)
            {
                return;
            }

            float ui = Mathf.Max(0.5f, Screen.height / 1080f);
            EnsureStyles(Mathf.RoundToInt(ModConfig.KeyHintsFontSize.Value * ui));

            float pad = _key.fontSize * 0.6f;
            float rowHeight = _key.fontSize * 1.45f;
            float keyWidth = 0f;
            float actionWidth = 0f;
            foreach ((string keys, string action) in Rows)
            {
                keyWidth = Mathf.Max(keyWidth, _key.CalcSize(new GUIContent(keys)).x);
                actionWidth = Mathf.Max(actionWidth, _action.CalcSize(new GUIContent(action)).x);
            }

            float titleHeight = _title_.fontSize * 1.6f;
            float gap = _key.fontSize * 0.8f;
            float width = pad * 2f + keyWidth + gap + actionWidth;
            float height = pad * 2f + titleHeight + Rows.Count * rowHeight;

            MessagePosition where = ModConfig.KeyHintsPosition.Value;
            float offsetX = ModConfig.KeyHintsOffsetX.Value * ui;
            float offsetY = ModConfig.KeyHintsOffsetY.Value * ui;

            float x = where == MessagePosition.TopLeft || where == MessagePosition.MiddleLeft || where == MessagePosition.BottomLeft
                ? offsetX
                : where == MessagePosition.TopCentre || where == MessagePosition.BottomCentre
                    ? (Screen.width - width) / 2f
                    : Screen.width - width - offsetX;
            float y = where == MessagePosition.TopLeft || where == MessagePosition.TopRight || where == MessagePosition.TopCentre
                ? offsetY
                : where == MessagePosition.MiddleLeft || where == MessagePosition.MiddleRight
                    ? (Screen.height - height) / 2f + offsetY
                    : Screen.height - offsetY - height;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, _box);
            GUI.Label(new Rect(x + pad, y + pad, width - 2f * pad, titleHeight), _title, _title_);

            float cy = y + pad + titleHeight;
            foreach ((string keys, string action) in Rows)
            {
                GUI.Label(new Rect(x + pad, cy, keyWidth, rowHeight), keys, _key);
                GUI.Label(new Rect(x + pad + keyWidth + gap, cy, actionWidth, rowHeight), action, _action);
                cy += rowHeight;
            }
        }

        private static void EnsureStyles(int fontSize)
        {
            if (_key != null && _styledFontSize == fontSize && _backdrop != null)
            {
                return;
            }

            _styledFontSize = fontSize;
            if (_backdrop == null)
            {
                _backdrop = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color color = new Color(0.065f, 0.075f, 0.095f, 0.72f);
                _backdrop.SetPixels(new[] { color, color, color, color });
                _backdrop.Apply();
            }

            _box = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _box.normal.background = _backdrop;

            _title_ = new GUIStyle(GUI.skin.label) { fontSize = fontSize, fontStyle = FontStyle.Bold, wordWrap = false };
            _title_.normal.textColor = new Color(0.96f, 0.82f, 0.47f);

            _key = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false
            };
            _key.normal.textColor = new Color(0.99f, 0.88f, 0.28f);

            _action = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            _action.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
        }
    }

    internal static class KeyHintExtensions
    {
        internal static string ToHint(this KeyCode key)
        {
            return KeyNames.Name(key);
        }
    }
}
