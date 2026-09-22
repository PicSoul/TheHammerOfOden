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
        private static bool _diagLogged;
        private static GameObject _firstMarked;
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
            if (hud == null || pieces == null)
            {
                return;
            }

            bool show = ModConfig.IsEnabled && ModConfig.ShowBendableMarker.Value;

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

                if (!show)
                {
                    Transform existing = root.transform.Find(MarkerName);
                    if (existing != null && existing.gameObject.activeSelf)
                    {
                        existing.gameObject.SetActive(false);
                    }
                    continue;
                }

                bool bendable = i < pieces.Count
                    && pieces[i] != null
                    && Bendable.Allows(pieces[i].gameObject);

                if (bendable)
                {
                    marked++;
                }

                GameObject marker = Marker(root);
                if (bendable)
                {
                    marker.transform.SetAsLastSibling();
                    Image image = marker.GetComponent<Image>();
                    if (image != null)
                    {
                        Color c = ModConfig.BendableMarkerColour.Value;
                        if (c.a < 0.1f) c.a = 0.85f;
                        image.color = c;
                    }
                }
                marker.SetActive(bendable);

                if (bendable && _firstMarked == null)
                {
                    _firstMarked = marker;
                }
            }

            if (!show)
            {
                return;
            }

            if (!_diagLogged && _firstMarked != null)
            {
                _diagLogged = true;
                Describe(_firstMarked);
                _firstMarked = null;
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

        /// <summary>
        /// Everything about a marker that is switched on, since one that is switched off says
        /// nothing about whether the rest works.
        /// </summary>
        /// <remarks>
        /// The first attempt at this reported slot zero, which is whatever piece happens to sit
        /// at the top left and is usually not bendable - so it reported a marker correctly
        /// switched off and looked like a fault. What is wanted is one that should be visible
        /// and is not.
        /// </remarks>
        private static void Describe(GameObject marker)
        {
            RectTransform rect = marker.GetComponent<RectTransform>();
            Image image = marker.GetComponent<Image>();
            CanvasRenderer canvas = marker.GetComponent<CanvasRenderer>();

            System.Text.StringBuilder parts = new System.Text.StringBuilder();
            foreach (Component c in marker.GetComponents<Component>())
            {
                parts.Append(c.GetType().Name).Append(' ');
            }

            Transform clip = marker.transform;
            string masks = string.Empty;
            while (clip != null)
            {
                if (clip.GetComponent<Mask>() != null) { masks += "Mask(" + clip.name + ") "; }
                if (clip.GetComponent<RectMask2D>() != null) { masks += "RectMask2D(" + clip.name + ") "; }
                if (clip.GetComponent<CanvasGroup>() is CanvasGroup g) { masks += $"CanvasGroup({clip.name} a={g.alpha}) "; }
                clip = clip.parent;
            }

            HammerOfOdenPlugin.Info(
                $"[BendMarker] an active marker on '{marker.transform.parent?.name}': "
                + $"activeInHierarchy={marker.activeInHierarchy}, children={marker.transform.childCount}, "
                + $"components=[{parts.ToString().Trim()}], "
                + $"image={(image == null ? "none" : $"enabled={image.enabled} sprite={image.sprite?.name} rect={image.sprite?.rect} colour={image.color} alpha={image.color.a}")}, "
                + $"canvasAlpha={(canvas == null ? -1f : canvas.GetAlpha())}, "
                + $"worldRect={(rect == null ? "none" : rect.rect.ToString())}, scale={marker.transform.lossyScale}, "
                + $"ancestors=[{masks.Trim()}]");
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
        /// Clones Valheim's own upgrade badge to inherit its exact canvas renderer, material, and
        /// layer configuration, then mirrors it to the top-right corner with the arch sprite.
        /// </remarks>
        private static GameObject Marker(GameObject iconRoot)
        {
            Transform existing = iconRoot.transform.Find(MarkerName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            Transform upgrade = iconRoot.transform.Find("upgrade");
            GameObject marker;

            if (upgrade != null)
            {
                marker = Object.Instantiate(upgrade.gameObject, iconRoot.transform, false);
                marker.name = MarkerName;
                marker.layer = iconRoot.layer;

                RectTransform rect = marker.GetComponent<RectTransform>();
                if (upgrade is RectTransform model)
                {
                    rect.anchorMin = new Vector2(1f - model.anchorMax.x, model.anchorMin.y);
                    rect.anchorMax = new Vector2(1f - model.anchorMin.x, model.anchorMax.y);
                    rect.pivot = new Vector2(1f - model.pivot.x, model.pivot.y);
                    rect.anchoredPosition = new Vector2(-3f, -3f);
                    rect.sizeDelta = new Vector2(22f, 22f);
                }
            }
            else
            {
                marker = new GameObject(MarkerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                marker.layer = iconRoot.layer;
                marker.transform.SetParent(iconRoot.transform, false);

                RectTransform rect = marker.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-3f, -3f);
                rect.sizeDelta = new Vector2(22f, 22f);
            }

            marker.transform.localScale = Vector3.one;
            marker.transform.SetAsLastSibling();

            Image image = marker.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = Arch();
                image.type = Image.Type.Simple;
                image.raycastTarget = false;
                Color c = ModConfig.BendableMarkerColour.Value;
                if (c.a < 0.1f) c.a = 0.85f;
                image.color = c;
                image.maskable = true;
                image.RecalculateMasking();
                image.RecalculateClipping();
            }

            return marker;
        }

        private const string MedallionPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAGm0lEQVR4nO1bbWxTVRh+ejfmNvbFNDAIYWyLgUTA5BJtMAtbiYFN"
            + "FBGIOAkDwgKGYNAYNf7wl//0h0Z0iQYTxShggBhAN42hgxBJIa0CM8GYjW4hfKn7cGPUjW3mWc815ba359yuK+26J2nSj3Pf933e"
            + "855z3nPOW2AKU0hrOBKpTHe6ZgLgawaA6QCmiZ+GANwG0A3gls/jvjUpHKA7XWUAFgFYAIDv54Y4IM/kgH7DAQCuArgC4HcArT6P"
            + "m+9TwwG600VylQCcAJYCWAwgK0ZxgwAuAfAC8AA44/O46aTkc4AeDO8aACsALBc9HE8wQk4DOAmgOV7DxDFeAbrTRRnrADwDoDaS"
            + "zM6OtphkzyutiPT1KIAmAMcBHPV53Px8fxygO10c3y8A2ACgOB6kbTijC8BhAAd9Hndrwh2gO13s8XoAVRNJXMERpwDs93ncjIjE"
            + "OEB3urYDaABQkSjiEkdQ+T6fx/3ZhDpAD473lwHsCg35RJO3cAKHRCOAvXbmBc2mTpJ/JRnIR9BdLGyjjfGPAD0Y9q8b5O8ncUk0"
            + "MBLeUx0Omo0JryFZyZtsoo0NwubxO0APLnWc7SMuykkK2lovbI99COjBSe8dADuM78bb+2XlFYMls2ZmFBYWaNnZ2WP6A4HAaG/v"
            + "PyM3bt4avtLeFmvaHGli/BTA29EmxUyJvHUiyYmZfO70/DvOx5Y+8OiSxVkLFz48bX5pacHsklkZRUVF9zigp6dn5PqNm8P+jo6h"
            + "y5f/GLpw8dKg57z334HbfTl29NHGECfQ9l8AHLEdAXowt38XwFOGYDsofvChwMonV+RUV1VmL3M+nj1nzmyZs+/BtWvX7571nAu0"
            + "nDoT+PGnk3e6/v4r287zIU74HsAbVnuHzCgyakRubxs1NatG165ZPaN21crc3Nwcu0vtGOiw9c89m0cZ1VWVA98e+26gufmHWBK3"
            + "WrGB2h/pR0eULe37dnufvV6/qS6vbuP6vPLyMmOvHxe0t18ZOnDoSP/+rw70q0aDKQpejbSVzrR4tlJsaW1NbjsbthZu27I5X9Mc"
            + "cT9noEPfevO1opKSmRmf7Pu8z+ZkuVxwCtsvaBYPOI39vErvk/ye3S8VbN9WXzAR5A1QNnVQF3XK2ofYnic4hctE5GMsnuQoh/3O"
            + "hq35m+qez0eCQF3USd02HlsquEkjYJE4xlICxzzDHgkGdVK3jUfIaZGKAxYYZ3iy8OdszwlvIsPeCtRJ3bQhWrsQDlmCm3QSLFMx"
            + "gOHHpc7ubP/zWU/g4qXfBv0dnXd7enqH+V1RUWHG/NJ5mUsWP5L1xDKn8npP3WvXrM49d97brbgylEV1gB5Mfnh0LQWTHK7Rdogf"
            + "O9E04G45fac9fAanI4bLyyv6XNXLc9Y8XZur6gjawGTp4KFvVM4A5pJjaFKUaWpgXFxI09vqqspi1STnwKHD/V98+XWf1+ulPsvl"
            + "i45pb28b/vXCxe4tm1/Mr9u4QTrGaQOzzWMnmroU0maDn6UDZohXVDC3Z3oLRfIffNjYG6HXLUFHdXf39PK9ihNoC21yt7TImobx"
            + "00wNeF0lVciNjUpuz7Bnz9shb4DP8FnKkLWlLbRJQWye4GjpgGkh11WW4K5OQRk45kXYxwQ+SxkqbRVtCuOn2TWKGdj80lKpMvYc"
            + "JzyME5ShEgW0SSU7NEMzfR4SL0vwMIP7eZlgLnWxhL4ZlEFZsna0ibZJmoXx00wNbos7OEvwJIeHGTKDuM4jTlCRRZtom6RZv+D4"
            + "PzRTA24Xo96+8hTHOMmJBiPJiQdUZCnaFcZPMzXg+hj11tXhcIyloTKDRkZHEC+oyKJNtE2CMH5a6AeRIbE4YbLiqvloTIvQaMKq"
            + "MZIAYdy0CI1YljIY5X4+pRDCYVBwkzqgVZSlTDaQU6vUAb5gQRJrciYbvJGKrTSLxh4jH0jlYRBie7/gBFUHnBEFSZMFpwUnNQf4"
            + "gufnvEwYTdUoCLGZHE5alddpUWQ0i2qsVEeT4AJbDvAFE4bjouAgpaLAVCxxPFpNoSaRdVSUopkFJy1MNh4WHBCTA3zBe/WDohQt"
            + "1XBK1BBGPSzVZFJEESJvVpOvLsYabaJ2UFpAqalIE0WI+4z5IMnRJWoGlQonNVWpouqqUXZgcp9B2xrtFExm2lSw1+/vHNmxa89u"
            + "c22wGX7/2ClOzAeiZlk7du35U9Ksy+/v/AjAx3ZkO2IxKG1LZUOR1sXSBtK6XN5AWv9hIhRp+5cZM9L2T1ORkJZ/m0u1P05OYQpI"
            + "b/wHXwi864ivXqIAAAAASUVORK5CYII=";

        /// <summary>
        /// A small arch medallion badge, decoded from PNG.
        /// </summary>
        /// <remarks>
        /// Features a dark slate circular backing with a clean rim and a bright white arch.
        /// When tinted by the Image component, the white arch glows in the configured color
        /// while the dark medallion background guarantees high contrast against all piece icons.
        /// </remarks>
        private static Sprite Arch()
        {
            if (_arch != null)
            {
                return _arch;
            }

            byte[] bytes = System.Convert.FromBase64String(MedallionPngBase64);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            texture.LoadImage(bytes);
            _arch = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            _arch.name = "HoO_BendMedallion";
            return _arch;
        }
    }
}
