using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Whether one of the mod's windows has the mouse - the F3 guide or the blueprint book - so
    /// the game's own mouse look, camera zoom and pause menu stand aside while it does.
    /// </summary>
    internal static class Overlay
    {
        internal static bool Open => HelpPanel.IsOpen || BlueprintBook.IsOpen || BlueprintSave.IsOpen;

        internal static bool ClosedThisFrame =>
            HelpPanel.ClosedThisFrame || BlueprintBook.ClosedThisFrame || BlueprintSave.ClosedThisFrame;

        internal static void CloseAll()
        {
            HelpPanel.Close();
            BlueprintBook.Close();
            BlueprintSave.Close();
        }

        /// <summary>
        /// Whether the player is typing into one of the mod's text boxes - the save window's, or a
        /// search box in the book or the F3 guide. While they are, the game hears no keys at all.
        /// </summary>
        internal static bool Typing => BlueprintSave.IsOpen || (Open && GUIUtility.keyboardControl != 0);

        /// <summary>No text box has the keyboard - for a window just opened, or a click away from its box.</summary>
        internal static void Unfocus()
        {
            GUIUtility.keyboardControl = 0;
        }

        internal static void FreeCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
        }

        internal static void LockCursor()
        {
            if (Open)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ZCursor.LockState = CursorLockMode.Locked;
            ZCursor.Hide();
        }
    }

    /// <summary>
    /// The blueprint book: every blueprint in the shared folders - PlanBuild's, Infinity Hammer's,
    /// BuildShare's and this mod's - with a turning model of the one picked, and a button to pick
    /// it up and place it like a copy.
    /// </summary>
    /// <remarks>
    /// The list is read when the book opens, headers only, so it is quick however many files there
    /// are. The pieces of a blueprint are read when it is picked; that is also when what it costs,
    /// and anything left out of it - pieces from mods not installed here, pieces not learned,
    /// plants, sizes beyond the server's limits - is worked out and shown.
    /// </remarks>
    internal static class BlueprintBook
    {
        private static bool _open;
        private static int _closedOnFrame = -1;

        private static List<BlueprintFile> _files = new List<BlueprintFile>();
        private static BlueprintFile _picked;
        private static BlueprintPlan _plan;
        private static string _search = string.Empty;
        private static Vector2 _scroll;
        private static Vector2 _infoScroll;

        private static GUIStyle _window;
        private static GUIStyle _title;
        private static GUIStyle _category;
        private static GUIStyle _row;
        private static GUIStyle _rowPicked;
        private static GUIStyle _text;
        private static GUIStyle _dim;
        private static GUIStyle _button;
        private static GUIStyle _small;
        private static GUIStyle _field;
        private static GUIStyle _badge;
        private static GUIStyle _optionText;
        private static Texture2D _onBackdrop;
        private static Texture2D _offBackdrop;
        private static Texture2D _backdrop;
        private static Texture2D _pickedBackdrop;
        private static Texture2D _buttonBackdrop;
        private static int _styledFor;

        internal static bool IsOpen => _open;
        internal static bool ClosedThisFrame => _closedOnFrame == Time.frameCount;

        internal static void Toggle()
        {
            if (_open)
            {
                Close();
                return;
            }

            if (Player.m_localPlayer == null)
            {
                return;
            }

            HelpPanel.Close();
            _open = true;
            Overlay.Unfocus();
            _files = BlueprintFile.List();
            _scroll = Vector2.zero;
            Overlay.FreeCursor();

            HammerOfOdenPlugin.Debug($"Blueprint book opened: {_files.Count} blueprints found.");
        }

        internal static void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _closedOnFrame = Time.frameCount;
            BlueprintPreview.Clear();
            BlueprintPreview.Tick(false);
            _picked = null;
            _plan = null;
            Overlay.LockCursor();
        }

        /// <summary>Every frame: keeps the preview turning while the book is open.</summary>
        internal static void Tick()
        {
            BlueprintPreview.Tick(_open);
        }

        private static void Pick(BlueprintFile file)
        {
            _picked = file;
            _plan = BlueprintPlan.From(file, Player.m_localPlayer);
            _infoScroll = Vector2.zero;
            BlueprintPreview.Show(_plan.Orders);
        }

        private static void PickUp()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _plan == null || _picked == null)
            {
                return;
            }

            if (!BuildTool.IsBuildingTool || !player.InPlaceMode())
            {
                Notify.Show(player, "Take out your hammer to pick up a blueprint");
                return;
            }

            if (_plan.Orders.Count == 0)
            {
                Notify.Show(player, "Nothing in this blueprint can be built here");
                return;
            }

            string name = _picked.Name;
            List<CopyOrder> orders = _plan.Orders;
            Close();
            GroupHold.BeginPlan(player, orders, name);
        }

        // ------------------------------------------------------------------ drawing

        internal static void Draw()
        {
            if (!_open)
            {
                return;
            }

            if (Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            float ui = Mathf.Max(0.6f, Screen.height / 1080f);
            EnsureStyles(ui);

            // Esc closes the book even while the search box has the keyboard - the game hears no
            // keys then, so its own Esc handling never sees it.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            float width = Mathf.Min(Screen.width - 40f, 980f * ui);
            float height = Mathf.Min(Screen.height - 40f, 620f * ui);
            Rect window = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(window, GUIContent.none, _window);

            float pad = 14f * ui;
            Rect inner = new Rect(window.x + pad, window.y + pad, window.width - 2f * pad, window.height - 2f * pad);

            GUI.Label(new Rect(inner.x, inner.y, inner.width, 30f * ui),
                "<b>Blueprint book</b>  <color=#94A3B8><size=" + Mathf.RoundToInt(13 * ui) + ">"
                + $"{_files.Count} blueprints · {KeyNames.Of(ModConfig.BlueprintBookKey)} or Esc to close</size></color>", _title);

            float top = inner.y + 36f * ui;
            float listWidth = inner.width * 0.36f;
            DrawList(new Rect(inner.x, top, listWidth, inner.yMax - top));
            DrawPicked(new Rect(inner.x + listWidth + pad, top, inner.width - listWidth - pad, inner.yMax - top), ui);
        }

        private static void DrawList(Rect area)
        {
            float line = _row.fontSize * 1.7f;
            Rect searchBox = new Rect(area.x, area.y, area.width, line);
            if (Event.current.type == EventType.MouseDown && !searchBox.Contains(Event.current.mousePosition))
            {
                Overlay.Unfocus();
            }

            _search = GUI.TextField(searchBox, _search, _field);

            if (_files.Count == 0)
            {
                GUI.Label(new Rect(area.x, area.y + line + 8f, area.width, area.height - line),
                    "No blueprints yet. Put .blueprint or .vbuild files in\n" + BlueprintFile.SaveFolder
                    + "\nor in BepInEx/config/PlanBuild in your mod profile. PlanBuild and Infinity Hammer "
                    + "save theirs there too.", _dim);
                return;
            }

            List<BlueprintFile> shown = new List<BlueprintFile>();
            foreach (BlueprintFile file in _files)
            {
                if (_search.Length == 0
                    || file.Name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0
                    || (file.Category ?? string.Empty).IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    shown.Add(file);
                }
            }

            // Category headings between runs of the same category.
            int rows = shown.Count;
            string last = null;
            foreach (BlueprintFile file in shown)
            {
                if (file.Category != last)
                {
                    rows++;
                    last = file.Category;
                }
            }

            Rect view = new Rect(area.x, area.y + line + 6f, area.width, area.height - line - 6f);
            Rect content = new Rect(0f, 0f, view.width - 18f, rows * line);
            _scroll = GUI.BeginScrollView(view, _scroll, content);

            float y = 0f;
            last = null;
            foreach (BlueprintFile file in shown)
            {
                if (file.Category != last)
                {
                    GUI.Label(new Rect(0f, y, content.width, line), file.Category, _category);
                    y += line;
                    last = file.Category;
                }

                string label = file.Name + "  <color=#64748B>" + file.Source + "</color>";
                if (GUI.Button(new Rect(0f, y, content.width, line), label, file == _picked ? _rowPicked : _row))
                {
                    Pick(file);
                }

                y += line;
            }

            GUI.EndScrollView();
        }

        private static void DrawPicked(Rect area, float ui)
        {
            if (_picked == null)
            {
                GUI.Label(area, "Pick a blueprint to see it.", _dim);
                return;
            }

            float previewSize = Mathf.Min(area.width, area.height * 0.62f);
            Rect preview = new Rect(area.x, area.y, area.width, previewSize);

            if (BlueprintPreview.HasModel && BlueprintPreview.Texture != null)
            {
                float side = Mathf.Min(preview.width, preview.height);
                Rect square = new Rect(preview.x + (preview.width - side) / 2f, preview.y, side, side);
                GUI.DrawTexture(square, BlueprintPreview.Texture, ScaleMode.ScaleToFit, false);
                HandlePreviewInput(square);
                GUI.Label(new Rect(square.x + 6f, square.yMax - 22f * ui, square.width, 20f * ui),
                    "<color=#94A3B8>Drag to turn · scroll to zoom</color>", _dim);

                // Over the preview's top corner: keep this view as the blueprint's picture.
                float pictureWidth = 190f * ui;
                Rect pictureButton = new Rect(square.xMax - pictureWidth - 6f, square.y + 6f, pictureWidth, 26f * ui);
                if (GUI.Button(pictureButton, "Use this view as picture", _small))
                {
                    SaveView();
                }

                if (Time.unscaledTime < _savedViewUntil)
                {
                    GUI.Label(new Rect(pictureButton.x, pictureButton.yMax + 2f, pictureWidth, 20f * ui),
                        "<color=#4ADE80>Picture saved</color>", _dim);
                }
            }
            else
            {
                GUI.Label(preview, "Nothing in this blueprint can be shown.", _dim);
            }

            float y = preview.yMax + 8f * ui;
            float buttonHeight = 32f * ui;
            float optionHeight = 28f * ui;
            float optionsTop = area.yMax - buttonHeight - 8f * ui - optionHeight;
            Rect info = new Rect(area.x, y, area.width, optionsTop - y - 6f * ui);

            List<string> lines = new List<string>();
            lines.Add($"<b>{_picked.Name}</b>" + (string.IsNullOrEmpty(_picked.Creator) ? string.Empty : $"  <color=#94A3B8>by {_picked.Creator}</color>"));
            if (!string.IsNullOrEmpty(_picked.Description))
            {
                lines.Add($"<color=#CBD5E1>{_picked.Description}</color>");
            }

            lines.Add("<color=#94A3B8>" + Dates(_picked) + "</color>");

            if (_plan != null)
            {
                lines.Add($"{_plan.Orders.Count} pieces · made with {_picked.Source}");
                string cost = Cost(_plan.Orders);
                if (cost.Length > 0)
                {
                    lines.Add("Costs " + cost);
                }

                if (_plan.Unknown.Count > 0)
                {
                    lines.Add("<color=#F87171>Left out - from mods not installed here: " + BlueprintPlan.Describe(_plan.Unknown) + "</color>");
                }

                if (_plan.Unlearned.Count > 0)
                {
                    lines.Add("<color=#FBBF24>Left out - not learned yet: " + Localized(BlueprintPlan.Describe(_plan.Unlearned)) + "</color>");
                }

                if (_plan.Plants > 0)
                {
                    lines.Add($"<color=#94A3B8>{_plan.Plants} plants left out</color>");
                }

                if (_plan.Ground > 0 && !ModConfig.BlueprintShapeGround.Value)
                {
                    lines.Add($"<color=#94A3B8>{_plan.Ground} ground-shaping steps left out (raising, levelling, paths) - "
                        + "see the option below</color>");
                }
                else if (_plan.Ground > 0)
                {
                    lines.Add($"<color=#FBBF24>{_plan.Ground} ground-shaping steps included - the ground is reshaped first, "
                        + "and that cannot be undone</color>");
                }

                if (_plan.Clamped > 0)
                {
                    lines.Add($"<color=#94A3B8>{_plan.Clamped} pieces resized to the server's size limits</color>");
                }
            }

            string body = string.Join("\n", lines);
            float bodyHeight = _text.CalcHeight(new GUIContent(body), info.width - 18f);
            _infoScroll = GUI.BeginScrollView(info, _infoScroll, new Rect(0f, 0f, info.width - 18f, bodyHeight));
            GUI.Label(new Rect(0f, 0f, info.width - 18f, bodyHeight), body, _text);
            GUI.EndScrollView();

            DrawOptions(new Rect(area.x, optionsTop, area.width, optionHeight));

            float buttonWidth = 150f * ui;
            if (GUI.Button(new Rect(area.xMax - buttonWidth, area.yMax - buttonHeight, buttonWidth, buttonHeight), "Pick it up", _button))
            {
                PickUp();
            }

            if (GUI.Button(new Rect(area.xMax - 2f * buttonWidth - 8f * ui, area.yMax - buttonHeight, buttonWidth, buttonHeight), "Close", _button))
            {
                Close();
            }
        }

        /// <summary>
        /// How a blueprint is to be built. Each one re-reads the blueprint when changed, since it
        /// changes what goes into it.
        /// </summary>
        private static void DrawOptions(Rect area)
        {
            if (Option(area, ModConfig.BlueprintShapeGround.Value, "Shape the ground too (raising, levelling, paths)"))
            {
                ModConfig.BlueprintShapeGround.Value = !ModConfig.BlueprintShapeGround.Value;
                if (_picked != null)
                {
                    Pick(_picked);
                }
            }
        }

        /// <summary>
        /// One option: a green tick when on, a red cross when off, and its name - the whole row
        /// clickable. Returns true when clicked.
        /// </summary>
        private static bool Option(Rect row, bool on, string label)
        {
            float side = row.height;
            Rect badge = new Rect(row.x, row.y, side, side);

            GUI.DrawTexture(badge, on ? _onBackdrop : _offBackdrop);
            GUI.Label(badge, on ? "✓" : "✕", _badge);
            GUI.Label(new Rect(row.x + side + 8f, row.y, row.width - side - 8f, row.height),
                label + (on ? "  <color=#4ADE80>on</color>" : "  <color=#F87171>off</color>"), _optionText);

            return GUI.Button(row, GUIContent.none, GUIStyle.none);
        }

        private static float _savedViewUntil;

        /// <summary>
        /// Saves the preview as it is shown now as the blueprint's picture - the .png PlanBuild
        /// shows beside it - replacing whatever picture it had.
        /// </summary>
        private static void SaveView()
        {
            if (_picked == null)
            {
                return;
            }

            string png = System.IO.Path.ChangeExtension(_picked.Path, ".png");
            if (BlueprintPreview.CaptureView(png))
            {
                _savedViewUntil = Time.unscaledTime + 2.5f;
                HammerOfOdenPlugin.Info($"Saved the preview as the picture for '{_picked.Name}': {png}");
            }
            else
            {
                Notify.Show(Player.m_localPlayer, "The picture could not be saved - see the log");
            }
        }

        private static void HandlePreviewInput(Rect square)
        {
            Event e = Event.current;
            if (e == null || !square.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                BlueprintPreview.Drag(e.delta);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                BlueprintPreview.Zoom(e.delta.y);
                e.Use();
            }
        }

        /// <summary>
        /// When it was made and last changed, in the player's own time. A file from another mod has
        /// no dates of its own, so the file's is shown instead, said as such.
        /// </summary>
        private static string Dates(BlueprintFile file)
        {
            if (file.Created.HasValue)
            {
                string text = "Created " + When(file.Created.Value);
                return file.Updated.HasValue ? text + " · updated " + When(file.Updated.Value) : text;
            }

            try
            {
                return "File saved " + When(System.IO.File.GetLastWriteTimeUtc(file.Path));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string When(System.DateTime utc)
        {
            return utc.ToLocalTime().ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>What building every piece would cost, most first.</summary>
        private static string Cost(List<CopyOrder> orders)
        {
            Dictionary<string, int> totals = new Dictionary<string, int>();
            foreach (CopyOrder order in orders)
            {
                if (order.Prefab == null
                    || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(order.Prefab.FreeBuildKey())))
                {
                    continue;
                }

                foreach (Piece.Requirement requirement in order.Prefab.m_resources)
                {
                    if (requirement?.m_resItem == null || requirement.m_amount <= 0)
                    {
                        continue;
                    }

                    string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                    totals.TryGetValue(name, out int n);
                    totals[name] = n + requirement.m_amount;
                }
            }

            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(totals);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> entry in list)
            {
                parts.Add($"{entry.Value:N0} {entry.Key}");
            }

            return Localized(string.Join(", ", parts));
        }

        private static string Localized(string text)
        {
            return Localization.instance != null ? Localization.instance.Localize(text) : text;
        }

        private static void EnsureStyles(float ui)
        {
            int size = Mathf.RoundToInt(15f * ui);
            if (_window != null && _styledFor == size && _backdrop != null)
            {
                return;
            }

            _styledFor = size;
            _backdrop ??= Solid(new Color(0.065f, 0.075f, 0.095f, 0.97f));
            _pickedBackdrop ??= Solid(new Color(0.38f, 0.26f, 0.08f, 0.9f));
            _buttonBackdrop ??= Solid(new Color(0.2f, 0.23f, 0.29f, 0.95f));

            _window = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _window.normal.background = _backdrop;

            _title = new GUIStyle(GUI.skin.label) { fontSize = size + 5, richText = true };
            _title.normal.textColor = new Color(0.96f, 0.82f, 0.47f);

            _category = new GUIStyle(GUI.skin.label) { fontSize = size - 1, fontStyle = FontStyle.Bold };
            _category.normal.textColor = new Color(0.58f, 0.64f, 0.72f);

            _row = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true, padding = new RectOffset(8, 4, 2, 2) };
            _row.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
            _row.hover.textColor = new Color(0.99f, 0.88f, 0.28f);

            _rowPicked = new GUIStyle(_row);
            _rowPicked.normal.background = _pickedBackdrop;
            _rowPicked.normal.textColor = Color.white;

            _text = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true, wordWrap = true };
            _text.normal.textColor = new Color(0.88f, 0.91f, 0.95f);

            _dim = new GUIStyle(_text);
            _dim.normal.textColor = new Color(0.58f, 0.64f, 0.72f);

            _button = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold };
            _button.normal.background = _buttonBackdrop;
            _button.normal.textColor = new Color(0.96f, 0.82f, 0.47f);
            _button.hover.background = _pickedBackdrop;
            _button.hover.textColor = Color.white;

            _small = new GUIStyle(_button) { fontSize = size - 2 };

            _field = new GUIStyle(GUI.skin.textField) { fontSize = size };

            _onBackdrop ??= Solid(new Color(0.13f, 0.55f, 0.27f, 1f));
            _offBackdrop ??= Solid(new Color(0.62f, 0.16f, 0.16f, 1f));

            _badge = new GUIStyle(GUI.skin.label)
            {
                fontSize = size + 2,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _badge.normal.textColor = Color.white;

            _optionText = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                richText = true,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            _optionText.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            return tex;
        }
    }
}
