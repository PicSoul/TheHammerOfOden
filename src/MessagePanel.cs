using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// The mod's own message window: a small stack of notes at the side of the screen, each
    /// staying up long enough to read.
    /// </summary>
    /// <remarks>
    /// The game's centre message was the only place these went, and it suits one or two words:
    /// it shows a single line in large type, cuts off anything longer, and fades in about two
    /// seconds whatever it says. A report such as which pieces a copy left out and why cannot be
    /// read like that.
    ///
    /// Each note stays for a base time plus as long as it takes to read at a steady pace, so a
    /// long one lasts longer than a short one. A few are kept at once, newest nearest the corner.
    ///
    /// A message from the same place in the code replaces its last one rather than stacking
    /// under it. Holding a key that reports a value - a scale ticking up - otherwise pushes
    /// twenty copies of the same line into the stack and everything else out of it. The place is
    /// known from the call site itself, so no caller has to name it.
    ///
    /// Drawn with the same immediate-mode GUI and colours as the F3 guide.
    /// </remarks>
    internal static class MessagePanel
    {
        private sealed class Note
        {
            internal string Key;
            internal string Text;
            internal float Until;
            internal float Lasts;
        }

        private static readonly List<Note> Notes = new List<Note>();

        private const float FadeSeconds = 0.6f;
        private const float LongestSeconds = 30f;

        private static GUIStyle _box;
        private static GUIStyle _text;
        private static Texture2D _backdrop;
        private static Texture2D _stripe;
        private static int _styledFontSize;

        internal static void Add(string key, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            float lasts = Mathf.Min(LongestSeconds,
                ModConfig.MessageSeconds.Value + Words(text) / Mathf.Max(0.5f, ModConfig.MessageReadingSpeed.Value));

            Note note = Notes.Find(n => n.Key == key);
            if (note != null)
            {
                Notes.Remove(note);
            }
            else
            {
                note = new Note { Key = key };
            }

            note.Text = text;
            note.Lasts = lasts;
            note.Until = Time.time + lasts;
            Notes.Add(note);

            while (Notes.Count > Mathf.Max(1, ModConfig.MessageMostShown.Value))
            {
                Notes.RemoveAt(0);
            }
        }

        private static int Words(string text)
        {
            int words = 0;
            bool inWord = false;
            foreach (char c in text)
            {
                bool letter = !char.IsWhiteSpace(c);
                if (letter && !inWord)
                {
                    words++;
                }

                inWord = letter;
            }

            return words;
        }

        internal static void Draw()
        {
            if (Notes.Count == 0)
            {
                return;
            }

            Notes.RemoveAll(n => Time.time >= n.Until);
            if (Notes.Count == 0 || Player.m_localPlayer == null)
            {
                return;
            }

            float ui = Mathf.Max(0.5f, Screen.height / 1080f);
            EnsureStyles(Mathf.RoundToInt(ModConfig.MessageFontSize.Value * ui));

            float width = ModConfig.MessageWidth.Value * ui;
            float gap = 6f * ui;
            float offsetX = ModConfig.MessageOffsetX.Value * ui;
            float offsetY = ModConfig.MessageOffsetY.Value * ui;

            // Heights first, so the stack can be placed as a whole.
            List<float> heights = new List<float>(Notes.Count);
            float total = 0f;
            foreach (Note note in Notes)
            {
                float h = _box.CalcHeight(new GUIContent(note.Text), width);
                heights.Add(h);
                total += h + gap;
            }

            total -= gap;

            MessagePosition where = ModConfig.MessagePosition.Value;
            float x = where == MessagePosition.TopLeft || where == MessagePosition.MiddleLeft || where == MessagePosition.BottomLeft
                ? offsetX
                : where == MessagePosition.TopCentre || where == MessagePosition.BottomCentre
                    ? (Screen.width - width) / 2f
                    : Screen.width - width - offsetX;

            bool fromTop = where == MessagePosition.TopLeft || where == MessagePosition.TopRight || where == MessagePosition.TopCentre;
            bool middle = where == MessagePosition.MiddleLeft || where == MessagePosition.MiddleRight;

            float y = fromTop ? offsetY
                : middle ? (Screen.height - total) / 2f + offsetY
                : Screen.height - offsetY - total;

            Color was = GUI.color;

            // Newest nearest the edge it grows from: at the top when hung from the top, at the
            // bottom otherwise.
            for (int n = 0; n < Notes.Count; n++)
            {
                int i = fromTop ? Notes.Count - 1 - n : n;
                Note note = Notes[i];
                float h = heights[i];

                float left = note.Until - Time.time;
                GUI.color = new Color(was.r, was.g, was.b, was.a * Mathf.Clamp01(left / FadeSeconds));

                Rect rect = new Rect(x, y, width, h);
                GUI.Box(rect, GUIContent.none, _box);
                GUI.DrawTexture(new Rect(x, y, 3f * ui, h), _stripe);
                GUI.Label(rect, note.Text, _text);

                y += h + gap;
            }

            GUI.color = was;
        }

        private static void EnsureStyles(int fontSize)
        {
            if (_box != null && _styledFontSize == fontSize && _backdrop != null)
            {
                return;
            }

            _styledFontSize = fontSize;

            if (_backdrop == null)
            {
                _backdrop = Solid(new Color(0.065f, 0.075f, 0.095f, 0.90f));
                _stripe = Solid(new Color(0.85f, 0.68f, 0.28f, 1f));
            }

            RectOffset padding = new RectOffset(
                Mathf.RoundToInt(fontSize * 0.9f), Mathf.RoundToInt(fontSize * 0.7f),
                Mathf.RoundToInt(fontSize * 0.45f), Mathf.RoundToInt(fontSize * 0.45f));

            _box = new GUIStyle(GUI.skin.box)
            {
                fontSize = fontSize,
                wordWrap = true,
                richText = true,
                padding = padding,
                border = new RectOffset(0, 0, 0, 0)
            };
            _box.normal.background = _backdrop;

            _text = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                wordWrap = true,
                richText = true,
                alignment = TextAnchor.UpperLeft,
                padding = padding
            };
            _text.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            return tex;
        }

        /// <summary>Forgets what is showing, for leaving a world.</summary>
        internal static void Clear()
        {
            Notes.Clear();
        }
    }

    internal enum MessagePosition
    {
        TopRight,
        MiddleRight,
        BottomRight,
        TopLeft,
        MiddleLeft,
        BottomLeft,
        TopCentre,
        BottomCentre
    }

    internal enum MessageStyle
    {
        Panel,
        GameCentre
    }
}
