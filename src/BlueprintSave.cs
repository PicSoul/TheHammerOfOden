using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Saving the selection as a blueprint: a name, a description and a category, and a question
    /// before anything is overwritten.
    /// </summary>
    /// <remarks>
    /// The name is offered from what the selection remembers: the blueprint it was last saved as,
    /// or the one it was built from - that is kept on the first piece of anything built from a
    /// blueprint. Saving under a name that exists asks first, keeps the old file beside the new
    /// one as a .bak, and keeps its creator, description and category unless they are changed.
    ///
    /// The selection is read when the dialog opens, so what is saved is what was selected when
    /// the key was pressed. A construction site in the selection brings its unbuilt pieces along.
    /// </remarks>
    internal static class BlueprintSave
    {
        private enum Step
        {
            Naming,
            Replacing
        }

        private static bool _open;
        private static int _closedOnFrame = -1;
        private static float _openedAt;
        private static Step _step;

        private static List<CopyOrder> _orders;
        private static int _plants;
        private static int _ghosts;
        private static string _name = string.Empty;
        private static string _description = string.Empty;
        private static string _category = string.Empty;
        private static string _creator;
        private static System.DateTime? _created;
        private static readonly List<string> Categories = new List<string>();
        private static bool _categoriesShown;
        private static Vector2 _categoryScroll;
        private static int _replacingCount;
        private static string _problem;
        private static bool _focus;

        /// <summary>The name this selection was last saved under, offered next time.</summary>
        private static string _savedAs;

        private static GUIStyle _window;
        private static GUIStyle _title;
        private static GUIStyle _label;
        private static GUIStyle _text;
        private static GUIStyle _warn;
        private static GUIStyle _field;
        private static GUIStyle _button;
        private static GUIStyle _option;
        private static Texture2D _backdrop;
        private static Texture2D _buttonBackdrop;
        private static Texture2D _hoverBackdrop;
        private static int _styledFor;

        internal static bool IsOpen => _open;
        internal static bool ClosedThisFrame => _closedOnFrame == Time.frameCount;

        /// <summary>The selection was cleared or replaced: the name it was saved as no longer applies.</summary>
        internal static void ForgetName()
        {
            _savedAs = null;
        }

        internal static void Open(Player player)
        {
            if (player == null || _open)
            {
                return;
            }

            if (Selection.Count == 0)
            {
                Notify.Show(player, "Select what to save first");
                return;
            }

            _orders = GroupHold.SelectionForSaving(out _plants, out _ghosts, out string source);
            if (_orders.Count == 0)
            {
                Notify.Show(player, "Nothing in the selection can be saved as a blueprint");
                return;
            }

            BlueprintBook.Close();
            HelpPanel.Close();

            _name = _savedAs ?? source ?? string.Empty;
            _description = string.Empty;
            _category = "Hammer of Oden";
            TakeUpExisting();

            // Every category already in use, to pick from rather than retype.
            Categories.Clear();
            foreach (BlueprintFile file in BlueprintFile.List())
            {
                if (!string.IsNullOrEmpty(file.Category) && !Categories.Contains(file.Category))
                {
                    Categories.Add(file.Category);
                }
            }

            Categories.Sort(System.StringComparer.OrdinalIgnoreCase);
            _categoriesShown = false;

            _step = Step.Naming;
            _problem = null;
            _focus = true;
            _open = true;
            _openedAt = Time.unscaledTime;
            Overlay.FreeCursor();
        }

        internal static void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            _closedOnFrame = Time.frameCount;
            _orders = null;
            Overlay.LockCursor();
        }

        /// <summary>If a blueprint of this name exists, its description and category are offered to keep.</summary>
        private static void TakeUpExisting()
        {
            string path = BlueprintWriter.PathFor(_name);
            if (_name.Length == 0 || !File.Exists(path))
            {
                return;
            }

            BlueprintFile existing = BlueprintFile.ReadHeaders(path);
            if (existing == null)
            {
                return;
            }

            _description = existing.Description ?? string.Empty;
            _category = string.IsNullOrEmpty(existing.Category) ? _category : existing.Category;
        }

        private static void TrySave()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _orders == null)
            {
                Close();
                return;
            }

            if (_name.Trim().Length == 0)
            {
                _problem = "Give it a name first.";
                return;
            }

            string path = BlueprintWriter.PathFor(_name);
            string playerName = Game.instance != null ? Game.instance.GetPlayerProfile().GetName() : string.Empty;
            _creator = playerName;
            _created = null;
            bool replacing = File.Exists(path);

            if (replacing)
            {
                BlueprintFile existing = BlueprintFile.ReadHeaders(path);
                if (existing != null && !string.IsNullOrEmpty(existing.Creator))
                {
                    _creator = existing.Creator;
                }

                // Created stays what it was; a file from another mod has no date, so its own
                // file date stands in.
                _created = existing?.Created ?? File.GetCreationTimeUtc(path);

                if (_step == Step.Naming)
                {
                    BlueprintFile full = BlueprintFile.ReadHeaders(path);
                    _replacingCount = full != null && full.Load() ? full.Entries.Count : 0;
                    _step = Step.Replacing;
                    return;
                }
            }

            System.DateTime now = System.DateTime.UtcNow;
            if (!BlueprintWriter.Save(path, _name.Trim(), _creator, _description, _category,
                _created ?? now, replacing ? now : (System.DateTime?)null, _orders, out string error))
            {
                _problem = "Could not save it: " + error;
                _step = Step.Naming;
                return;
            }

            if (ModConfig.BlueprintThumbnails.Value)
            {
                BlueprintPreview.Capture(_orders, Path.ChangeExtension(path, ".png"));
            }

            _savedAs = _name.Trim();
            int count = _orders.Count;
            int ghosts = _ghosts;
            int plants = _plants;
            Close();

            Notify.Show(player, $"Saved the blueprint '{_savedAs}' - {count} pieces"
                + (ghosts > 0 ? $", {ghosts} of them still unbuilt ghosts" : string.Empty)
                + (plants > 0 ? $"; {plants} plants left out" : string.Empty)
                + ". PlanBuild and Infinity Hammer can load it too.");
            HammerOfOdenPlugin.Info($"Saved blueprint '{_savedAs}' ({count} pieces) to {path}");
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

            // Enter saves - but not the Enter that opened it.
            Event e = Event.current;
            if (e.type == EventType.KeyDown && Time.unscaledTime - _openedAt > 0.25f
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.Use();
                TrySave();
                return;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                if (_step == Step.Replacing)
                {
                    _step = Step.Naming;
                }
                else
                {
                    Close();
                }

                return;
            }

            float width = 560f * ui;
            float height = (_step == Step.Naming ? 330f : 220f) * ui;
            Rect window = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.Box(window, GUIContent.none, _window);

            float pad = 16f * ui;
            float x = window.x + pad;
            float y = window.y + pad;
            float inner = width - 2f * pad;
            float line = _field.fontSize * 1.9f;

            GUI.Label(new Rect(x, y, inner, line), "<b>Save as a blueprint</b>", _title);
            y += line + 4f * ui;

            string counts = $"{_orders.Count} pieces"
                + (_ghosts > 0 ? $" ({_ghosts} unbuilt ghosts included)" : string.Empty)
                + (_plants > 0 ? $" - {_plants} plants left out" : string.Empty);

            if (_step == Step.Replacing)
            {
                GUI.Label(new Rect(x, y, inner, line * 3f),
                    $"Replace '{_name.Trim()}' ({_replacingCount} pieces) with this selection ({_orders.Count} pieces)?\n"
                    + "The old one is kept beside it as a .bak file.", _text);

                float bw = 150f * ui;
                if (GUI.Button(new Rect(window.xMax - pad - bw, window.yMax - pad - line, bw, line), "Replace", _button))
                {
                    TrySave();
                }

                if (GUI.Button(new Rect(window.xMax - pad - 2f * bw - 8f * ui, window.yMax - pad - line, bw, line), "Back", _button))
                {
                    _step = Step.Naming;
                }

                return;
            }

            GUI.Label(new Rect(x, y, inner, line), counts, _label);
            y += line;

            float labelWidth = 110f * ui;
            GUI.Label(new Rect(x, y, labelWidth, line), "Name", _label);
            GUI.SetNextControlName("HoO_BlueprintName");
            string name = GUI.TextField(new Rect(x + labelWidth, y, inner - labelWidth, line), _name, 80, _field);
            if (name != _name)
            {
                _name = name;
                _problem = null;
            }

            y += line + 6f * ui;

            GUI.Label(new Rect(x, y, labelWidth, line), "Description", _label);
            _description = GUI.TextField(new Rect(x + labelWidth, y, inner - labelWidth, line), _description, 300, _field);
            y += line + 6f * ui;

            GUI.Label(new Rect(x, y, labelWidth, line), "Category", _label);
            float pick = Categories.Count > 0 ? line : 0f;
            _category = GUI.TextField(new Rect(x + labelWidth, y, inner - labelWidth - pick, line), _category, 60, _field);
            Rect categoryRow = new Rect(x + labelWidth, y, inner - labelWidth, line);
            if (pick > 0f && GUI.Button(new Rect(x + inner - pick, y, pick, line), _categoriesShown ? "▲" : "▼", _button))
            {
                _categoriesShown = !_categoriesShown;
            }

            y += line + 6f * ui;

            if (_problem != null)
            {
                GUI.Label(new Rect(x, y, inner, line), _problem, _warn);
            }
            else if (_name.Trim().Length > 0 && File.Exists(BlueprintWriter.PathFor(_name)))
            {
                GUI.Label(new Rect(x, y, inner, line), "A blueprint of this name exists - saving asks before replacing it.", _warn);
            }

            // Not while the category list is open over them: the window's controls take a click in
            // the order they are drawn, so a button beneath the list would take the click meant
            // for a category.
            float buttonWidth = 150f * ui;
            if (!_categoriesShown)
            {
                if (GUI.Button(new Rect(window.xMax - pad - buttonWidth, window.yMax - pad - line, buttonWidth, line), "Save", _button))
                {
                    TrySave();
                }

                if (GUI.Button(new Rect(window.xMax - pad - 2f * buttonWidth - 8f * ui, window.yMax - pad - line, buttonWidth, line), "Cancel", _button))
                {
                    Close();
                }
            }

            if (_focus)
            {
                _focus = false;
                GUI.FocusControl("HoO_BlueprintName");
            }

            // Drawn last, so it lies over the buttons below it while open.
            if (_categoriesShown)
            {
                DrawCategories(categoryRow, line, ui);
            }
        }

        /// <summary>The categories already in use, dropped down under the category box; clicking one takes it.</summary>
        private static void DrawCategories(Rect under, float line, float ui)
        {
            float listHeight = Mathf.Min(Categories.Count, 6) * line;
            Rect list = new Rect(under.x, under.yMax + 2f, under.width, listHeight);
            GUI.Box(list, GUIContent.none, _window);

            Rect content = new Rect(0f, 0f, list.width - 18f, Categories.Count * line);
            _categoryScroll = GUI.BeginScrollView(list, _categoryScroll, content);
            for (int i = 0; i < Categories.Count; i++)
            {
                if (GUI.Button(new Rect(0f, i * line, content.width, line), Categories[i], _option))
                {
                    _category = Categories[i];
                    _categoriesShown = false;
                }
            }

            GUI.EndScrollView();
        }

        private static void EnsureStyles(float ui)
        {
            int size = Mathf.RoundToInt(15f * ui);
            if (_window != null && _styledFor == size && _backdrop != null)
            {
                return;
            }

            _styledFor = size;
            _backdrop ??= Solid(new Color(0.065f, 0.075f, 0.095f, 0.98f));
            _buttonBackdrop ??= Solid(new Color(0.2f, 0.23f, 0.29f, 0.95f));
            _hoverBackdrop ??= Solid(new Color(0.38f, 0.26f, 0.08f, 0.9f));

            _window = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _window.normal.background = _backdrop;

            _title = new GUIStyle(GUI.skin.label) { fontSize = size + 4, richText = true };
            _title.normal.textColor = new Color(0.96f, 0.82f, 0.47f);

            _label = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = new Color(0.58f, 0.64f, 0.72f);

            _text = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true };
            _text.normal.textColor = new Color(0.88f, 0.91f, 0.95f);

            _warn = new GUIStyle(_text);
            _warn.normal.textColor = new Color(0.98f, 0.75f, 0.14f);

            _field = new GUIStyle(GUI.skin.textField) { fontSize = size, alignment = TextAnchor.MiddleLeft };

            _button = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold };
            _button.normal.background = _buttonBackdrop;
            _button.normal.textColor = new Color(0.96f, 0.82f, 0.47f);
            _button.hover.background = _hoverBackdrop;
            _button.hover.textColor = Color.white;

            _option = new GUIStyle(GUI.skin.label) { fontSize = size, padding = new RectOffset(8, 4, 2, 2) };
            _option.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
            _option.hover.background = _hoverBackdrop;
            _option.hover.textColor = Color.white;
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
