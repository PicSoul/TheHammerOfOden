using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TheHammerOfOden
{
    /// <summary>
    /// Marks the pieces that can be bent, in the build menu.
    /// </summary>
    /// <remarks>
    /// Whether a piece bends is not something to find out by trying. It depends on how the piece
    /// is built - one solid shape, nothing you can use, a main mesh the game lets a mod read -
    /// and none of that is visible from the outside. Without a mark the only way to know is to
    /// select a piece, hold the modifier, turn the wheel and read a refusal.
    ///
    /// Marked on what can bend rather than what cannot, because the bendable set is much the
    /// smaller: doors, gates, ladders, chests, workbenches, anything interactive and everything
    /// whose meshes are sealed are all out. Marking the exceptions would mean an icon on almost
    /// every piece, which is the same as marking nothing.
    ///
    /// The game already does exactly this for station upgrades - every icon carries a child it
    /// switches on for pieces with m_isUpgrade - so this follows that, in the opposite corner so
    /// the two never sit on top of each other.
    /// </remarks>
    internal static class BendMenuMarker
    {
        private const string MarkerName = "HoO_bendable";

        private static readonly FieldInfo IconsField = AccessTools.Field(typeof(Hud), "m_pieceIcons");

        private static FieldInfo _rootField;
        private static string _lastShape;
        private static Sprite _arch;

        /// <summary>
        /// Switches a mark on for every icon showing a piece that bends.
        /// </summary>
        /// <remarks>
        /// The icons and the pieces are paired by position in their lists, which is how vanilla
        /// pairs them a few lines earlier - the same index reads the same piece. Anything past
        /// the end of the piece list is an empty slot in the grid.
        /// </remarks>
        internal static void Apply(Hud hud, List<Piece> pieces)
        {
            if (!ModConfig.IsEnabled || !ModConfig.ShowBendableMarker.Value
                || hud == null || pieces == null)
            {
                return;
            }

            if (IconsField == null)
            {
                if (_lastShape != "no-field")
                {
                    _lastShape = "no-field";
                    HammerOfOdenPlugin.Error(
                        "Hud.m_pieceIcons was not found, so bendable pieces will not be marked. "
                        + "Valheim has probably changed. Everything else is unaffected.");
                }

                return;
            }

            if (!(IconsField.GetValue(hud) is IList icons))
            {
                if (_lastShape != "no-list")
                {
                    _lastShape = "no-list";
                    HammerOfOdenPlugin.Error(
                        "The build menu's icon list could not be read, so bendable pieces will not "
                        + "be marked. Everything else is unaffected.");
                }

                return;
            }

            int marked = 0;
            int roots = 0;

            for (int i = 0; i < icons.Count; i++)
            {
                GameObject root = RootOf(icons[i]);
                if (root == null)
                {
                    continue;
                }

                roots++;

                bool bendable = i < pieces.Count
                    && pieces[i] != null
                    && Bendable.Allows(pieces[i].gameObject);

                if (bendable)
                {
                    marked++;
                }

                Marker(root).SetActive(bendable);
            }

            // Said once per shape of the answer, not per redraw. Between "the postfix never
            // runs", "the icons cannot be reached" and "nothing qualifies" there is no way to
            // tell from an empty menu which one happened.
            string shape = $"{icons.Count}/{roots}/{pieces.Count}/{marked}";
            if (shape != _lastShape)
            {
                _lastShape = shape;
                HammerOfOdenPlugin.Debug(
                    $"Bend marker: {icons.Count} icon slot(s), {roots} reachable, "
                    + $"{pieces.Count} piece(s) listed, {marked} marked bendable.");
            }
        }

        /// <summary>The icon's own GameObject, read from a class the game keeps private.</summary>
        private static GameObject RootOf(object icon)
        {
            if (icon == null)
            {
                return null;
            }

            if (_rootField == null)
            {
                _rootField = AccessTools.Field(icon.GetType(), "m_go");
                if (_rootField == null)
                {
                    HammerOfOdenPlugin.Error(
                        "The build menu's icon layout has changed, so bendable pieces will not be "
                        + "marked. Everything else is unaffected.");
                    return null;
                }
            }

            return _rootField.GetValue(icon) as GameObject;
        }

        /// <summary>
        /// Finds the mark on an icon, adding it the first time.
        /// </summary>
        /// <remarks>
        /// Sized and placed from the upgrade arrow the game already draws, so it matches whatever
        /// that is at any resolution instead of guessing at pixels - and put in the opposite
        /// corner, since a piece can be both an upgrade and bendable.
        /// </remarks>
        private static GameObject Marker(GameObject iconRoot)
        {
            Transform existing = iconRoot.transform.Find(MarkerName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject marker = new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(iconRoot.transform, false);

            RectTransform rect = marker.GetComponent<RectTransform>();
            Transform upgrade = iconRoot.transform.Find("upgrade");

            if (upgrade is RectTransform model)
            {
                rect.sizeDelta = model.sizeDelta;
                rect.anchorMin = new Vector2(1f - model.anchorMax.x, model.anchorMin.y);
                rect.anchorMax = new Vector2(1f - model.anchorMin.x, model.anchorMax.y);
                rect.pivot = new Vector2(1f - model.pivot.x, model.pivot.y);
                rect.anchoredPosition = new Vector2(-model.anchoredPosition.x, model.anchoredPosition.y);
            }
            else
            {
                // No arrow to copy from; a small badge in the lower left will do.
                rect.sizeDelta = new Vector2(16f, 16f);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(2f, 2f);
            }

            Image image = marker.GetComponent<Image>();
            image.sprite = Arch();
            image.raycastTarget = false;
            image.color = ModConfig.BendableMarkerColour.Value;

            return marker;
        }

        /// <summary>
        /// A small arch, drawn rather than shipped.
        /// </summary>
        /// <remarks>
        /// Generating it keeps the mod a single file with nothing to load and nothing to lose,
        /// and an arch is simple enough to draw exactly: every pixel within half a stroke of a
        /// circle of the right radius, in the upper half, plus two legs down from its ends.
        /// </remarks>
        private static Sprite Arch()
        {
            if (_arch != null)
            {
                return _arch;
            }

            const int size = 32;
            const float stroke = 3.6f;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Vector2 centre = new Vector2(size * 0.5f, size * 0.34f);
            float radius = size * 0.34f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                    // The arch itself: near the circle, and only the half above its centre.
                    float ring = Mathf.Abs(Vector2.Distance(p, centre) - radius);
                    float on = p.y >= centre.y ? Mathf.InverseLerp(stroke * 0.5f, stroke * 0.5f - 1.4f, ring) : 0f;

                    // The legs, dropping from where the arch meets its own centre line.
                    if (p.y < centre.y)
                    {
                        float leg = Mathf.Abs(Mathf.Abs(p.x - centre.x) - radius);
                        on = Mathf.Max(on, Mathf.InverseLerp(stroke * 0.5f, stroke * 0.5f - 1.4f, leg));
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(on)));
                }
            }

            texture.Apply();

            _arch = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _arch;
        }
    }
}
