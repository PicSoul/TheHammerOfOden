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
                _title = "Build camera - " + _title;
                Row("W A S D", "Fly");
                Row(KeyNames.Of(ModConfig.CameraUpKey) + " / " + KeyNames.Of(ModConfig.CameraDownKey) + " alone", "Up / down");
                Row("Hold " + KeyNames.Of(ModConfig.CameraBoostKey), "Fly faster");
                Row(ModConfig.BuildCameraKey, "Back to your eyes");
            }
        }

        private static void Context()
        {
            Player player = Player.m_localPlayer;
            Piece hovered = player != null ? player.GetHoveringPiece() : null;

            if (Held(ModConfig.BendModifierKey))
            {
                _title = "Bending";
                Row("Scroll", "Bend the piece");
                Row(ModConfig.BendAxisKey, "Bend the other way round");
                Row(ModConfig.BendResetKey, "Straighten");
                return;
            }

            if (GroupHold.IsModel)
            {
                _title = "Holding a model";
                Row("Left mouse", "Place on a table or floor");
                Row(With(ModConfig.ScaleModifierKey, ModConfig.ScaleUpKey) + " / " + ModConfig.ScaleDownKey.Value.MainKey.ToHint(), "Bigger / smaller");
                Row(ModConfig.NudgeUpKey, ModConfig.NudgeDownKey, "Raise / lower");
                Row("Arrows", "Shift it a little");
                Row("Scroll", "Turn it");
                Row(ModConfig.ModelKey, "Back to a full-size copy");
                Row(ModConfig.EditKey, "Put it away");
                return;
            }

            if (GroupHold.IsHolding || PlacementEdit.IsEditing)
            {
                bool copy = GroupHold.IsCopying;
                bool move = GroupHold.IsMoving;
                _title = copy ? "Holding a copy" : move ? "Moving a group" : "Editing a piece";
                Row("Left mouse", copy ? "Place the copy" : "Put it down here");
                if (copy)
                {
                    Row(ModConfig.ModelKey, "Make it a model instead");
                }

                Row(ModConfig.FreezeKey, "Freeze where it is / follow your aim");
                Row(With(ModConfig.ScaleModifierKey, ModConfig.ScaleUpKey) + " / " + ModConfig.ScaleDownKey.Value.MainKey.ToHint(), "Bigger / smaller");
                Row("Scroll", "Turn it");
                Row("Arrows, " + KeyNames.Of(ModConfig.NudgeUpKey) + " / " + KeyNames.Of(ModConfig.NudgeDownKey), "Nudge");
                Row(ModConfig.ResetOffsetKey, "Clear the nudge");
                Row(ModConfig.EditKey, copy ? "Put it away" : "Cancel");
                return;
            }

            if (Held(ModConfig.ScaleModifierKey))
            {
                _title = "Holding " + KeyNames.MainOf(ModConfig.ScaleModifierKey);
                Row("Scroll", "Tilt forward and back");
                Row(ModConfig.ScaleWiderKey, ModConfig.ScaleNarrowerKey, "Wider / narrower", true);
                Row(ModConfig.ScaleTallerKey, ModConfig.ScaleShorterKey, "Taller / shorter", true);
                Row(ModConfig.ScaleDeeperKey, ModConfig.ScaleShallowerKey, "Deeper / shallower", true);
                Row(ModConfig.ScaleUpKey, ModConfig.ScaleDownKey, "Bigger / smaller all over", true);
                Row(ModConfig.ScaleResetKey, "Normal size");
                Row("Arrows", "Lay a run of copies (zoop)");
                Row(ModConfig.ZoopGapWiderKey, ModConfig.ZoopGapNarrowerKey, "Gap in the run");
                return;
            }

            if (Held(ModConfig.ZAxisKey))
            {
                _title = "Holding " + KeyNames.MainOf(ModConfig.ZAxisKey);
                Row("Scroll", "Tilt sideways");
                Row(ModConfig.SelectKey, "Select / deselect a piece");
                Row(ModConfig.SelectTypeKey, "Select every connected one of its kind");
                Row(ModConfig.SelectGrowKey, ModConfig.SelectShrinkKey, "Grow / shrink the selection");
                Row(ModConfig.EditKey, Selection.Count > 0 ? "Move the selection (on a selected piece) or edit a piece" : "Edit the piece you look at");
                return;
            }

            _title = "The Hammer of Oden";

            if (Selection.Count > 0)
            {
                _title = $"Selection - {Selection.Count:N0} pieces";
                Row(ModConfig.EditKey, "Move it (looking at a selected piece)");
                Row("Shift + Middle mouse", "Copy it (on a selected piece)");
                Row("Middle mouse", "Take it down (on a selected piece)");
                Row(ModConfig.SelectGrowKey, ModConfig.SelectShrinkKey, "Grow / shrink");
                Row(ModConfig.SelectBuildingKey, "Add a whole building");
                Row(ModConfig.BlueprintSaveKey, "Save it as a blueprint");
                Row(ModConfig.SelectClearKey, "Clear the selection");
            }
            else
            {
                // The game's own build hints, carried over.
                Row("Left mouse", "Place");
                Row("Middle mouse", "Remove");
                Row("Shift + Middle mouse", "Copy a piece");
                Row("Scroll", "Turn");
                Row("Right mouse", "Build menu");
                Row(ModConfig.SelectKey, "Select pieces");
                Row(ModConfig.SelectBuildingKey, "Select a whole building");
            }

            Row("Hold " + KeyNames.MainOf(ModConfig.ScaleModifierKey), "Resize, tilt, zoop");
            Row("Hold " + KeyNames.MainOf(ModConfig.ZAxisKey), "Tilt sideways, more selecting");
            Row(ModConfig.FreezeKey, "Freeze the piece");
            Row(ModConfig.EditKey, "Edit a placed piece");
            Row(ModConfig.UndoKey, ModConfig.RedoKey, "Undo / redo");
            Row(ModConfig.BlueprintBookKey, "Blueprint book");
            if (!BuildCamera.IsActive)
            {
                Row(ModConfig.BuildCameraKey, "Build camera");
            }

            Row(ModConfig.HelpKey, "Every key, explained");

            if (hovered != null && (ConstructionSite.Of(hovered) != null || ModelDisplay.Carries(hovered)))
            {
                Row(ModConfig.SiteCancelKey, ModelDisplay.Carries(hovered) ? "Take the model off" : "Stop this building where it stands");
            }

            if (hovered != null && ConstructionSite.IdOf(hovered) != 0L)
            {
                Row(ModConfig.SiteTakeDownKey, "Take this whole copy down");
            }
        }

        private static void Row(string keys, string action)
        {
            if (!string.IsNullOrEmpty(keys))
            {
                Rows.Add((keys, action));
            }
        }

        private static void Row(ConfigEntry<KeyboardShortcut> key, string action)
        {
            if (KeyNames.IsBound(key))
            {
                Rows.Add((KeyNames.Of(key), action));
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
            Rows.Add((keys, action));
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
