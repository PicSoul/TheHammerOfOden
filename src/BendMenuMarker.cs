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
    /// and none of that shows from the outside. Without a mark the only way to know is to select
    /// a piece, hold the modifier, turn the wheel and read a refusal.
    ///
    /// Marked on what can bend rather than what cannot, because the bendable set is much the
    /// smaller: doors, gates, ladders, chests, workbenches, anything interactive and everything
    /// whose meshes are sealed are all out. Marking the exceptions would put a badge on almost
    /// every piece, which says the same as marking nothing.
    ///
    /// Hung off BuildUiPieceButton, which is the menu the game actually draws.
    /// Hud.UpdatePieceList looks like the right place and is not: it still fills a grid of icons
    /// under a window called SelectionWindow, and that window is switched off in Hud.Awake and
    /// never switched back on. Everything about marking it worked - the icons were found, the
    /// right pieces chosen, the badges built and made active - and none of it could be seen,
    /// because the window has been dead since the build menu was rewritten. Nothing in the logs
    /// said so, because nothing had gone wrong.
    ///
    /// The new button is a better place in every way: each one is configured on its own rather
    /// than by position in a list, its Piece is public, and it carries the upgrade arrow that a
    /// badge can be copied from.
    /// </remarks>
    internal static class BendMenuMarker
    {
        private const string MarkerName = "HoO_bendable";

        private static readonly FieldInfo UpgradeArrowField =
            AccessTools.Field(typeof(BuildUiPieceButton), "m_upgradeArrow");

        private static Sprite _medallion;

        /// <summary>Puts the mark on a button, or takes it off, as that button is set up.</summary>
        internal static void Apply(BuildUiPieceButton button)
        {
            if (button == null)
            {
                return;
            }

            bool show = ModConfig.IsEnabled
                && ModConfig.ShowBendableMarker.Value
                && button.Piece != null
                && Bendable.Allows(button.Piece.gameObject);

            Transform existing = button.transform.Find(MarkerName);

            if (!show)
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }

                return;
            }

            GameObject marker = existing != null ? existing.gameObject : Build(button);
            if (marker != null)
            {
                marker.SetActive(true);
            }
        }

        /// <summary>
        /// Builds the badge, copied from the upgrade arrow the button already carries.
        /// </summary>
        /// <remarks>
        /// Cloned rather than built from nothing, so it inherits the canvas renderer, material
        /// and layer the game uses for that arrow instead of those being guessed at - then
        /// mirrored to the opposite corner, since a piece can be both an upgrade and bendable.
        /// </remarks>
        private static GameObject Build(BuildUiPieceButton button)
        {
            GameObject arrow = UpgradeArrowField?.GetValue(button) is Image image
                ? image.gameObject
                : null;

            GameObject marker;

            if (arrow != null)
            {
                marker = Object.Instantiate(arrow, button.transform, false);
                marker.name = MarkerName;

                if (arrow.transform is RectTransform model && marker.transform is RectTransform rect)
                {
                    rect.anchorMin = new Vector2(1f - model.anchorMax.x, model.anchorMin.y);
                    rect.anchorMax = new Vector2(1f - model.anchorMin.x, model.anchorMax.y);
                    rect.pivot = new Vector2(1f - model.pivot.x, model.pivot.y);
                    rect.anchoredPosition = new Vector2(-model.anchoredPosition.x, model.anchoredPosition.y);
                    rect.sizeDelta = model.sizeDelta;
                    rect.localScale = model.localScale;
                }
            }
            else
            {
                // Nothing to copy from, so a plain badge in the top right.
                marker = new GameObject(MarkerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                marker.layer = button.gameObject.layer;
                marker.transform.SetParent(button.transform, false);

                RectTransform rect = (RectTransform)marker.transform;
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-3f, -3f);
                rect.sizeDelta = new Vector2(20f, 20f);
            }

            marker.transform.SetAsLastSibling();

            if (marker.GetComponent<Image>() is Image badge)
            {
                badge.sprite = Medallion();
                badge.type = Image.Type.Simple;
                badge.preserveAspect = true;
                badge.raycastTarget = false;
                badge.color = ModConfig.BendableMarkerColour.Value;
            }

            return marker;
        }

        private static Sprite Medallion()
        {
            if (_medallion != null)
            {
                return _medallion;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            texture.LoadImage(System.Convert.FromBase64String(MedallionPngBase64));

            _medallion = Sprite.Create(
                texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            _medallion.name = "HoO_BendMedallion";

            return _medallion;
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
    }
}
