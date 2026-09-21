using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Takes a piece that is already built and puts it back in your hands to change.
    /// </summary>
    /// <remarks>
    /// The piece is not altered where it stands. It is rebuilt: the prefab goes back on the
    /// placement ghost with the original's rotation and size loaded into it, you change whatever
    /// you came to change, and when you place, the original comes down.
    ///
    /// That is deliberate, and the alternative is worse than it looks. A built piece's transform
    /// is read out of its ZDO once, when the object spawns, and never consulted again. Moving a
    /// placed piece by editing its ZDO would therefore look right to you and leave the piece
    /// exactly where it was for every other player, until their zone happened to reload. That is
    /// the same shape as the station range bug, except there the value could be re-read and here
    /// it cannot, because it is applied at instantiation. Placing a new piece is an ordinary
    /// spawn, which every client already knows how to draw.
    ///
    /// What it costs: the piece comes back at full health, so damage and wear are wiped, and
    /// anything holding state of its own has to be refused rather than silently emptied.
    /// </remarks>
    internal static class PlacementEdit
    {
        private delegate bool SetSelectedPieceCall(Player player, Piece piece);

        /// <summary>
        /// Valheim's own "put this piece in my hands", which CopyPiece uses for the middle-click
        /// copy. Private, so it is bound rather than called, and bound defensively: a failure
        /// here costs this one feature instead of the mod.
        /// </summary>
        private static readonly SetSelectedPieceCall SelectPiece = Resolve();

        private static SetSelectedPieceCall Resolve()
        {
            try
            {
                System.Reflection.MethodInfo method =
                    AccessTools.Method(typeof(Player), "SetSelectedPiece", new[] { typeof(Piece) });

                if (method == null)
                {
                    HammerOfOdenPlugin.Error(
                        "Player.SetSelectedPiece was not found, so editing placed pieces is "
                        + "disabled. Valheim has probably changed. Everything else is unaffected.");
                    return null;
                }

                return AccessTools.MethodDelegate<SetSelectedPieceCall>(method);
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not bind Player.SetSelectedPiece, so editing placed pieces is "
                    + "disabled: " + ex.Message);
                return null;
            }
        }

        private static ZDOID _original = ZDOID.None;
        private static string _originalPrefab;

        /// <summary>
        /// Colliders switched off for the duration of the edit, so they can be switched back on
        /// if the edit is abandoned.
        /// </summary>
        /// <remarks>
        /// Remembered rather than re-derived, because only the ones that were on get turned back
        /// on. A piece may ship with a collider already disabled and re-enabling it would be a
        /// change the player never asked for and would never connect to having edited something.
        /// </remarks>
        private static readonly List<Collider> Suppressed = new List<Collider>();

        private static bool _tinted;

        internal static bool IsEditing => _original != ZDOID.None;

        /// <summary>The piece being edited, if it is still loaded.</summary>
        private static GameObject Original =>
            IsEditing && ZNetScene.instance != null
                ? ZNetScene.instance.FindInstance(_original)
                : null;

        /// <summary>Starts an edit, or ends the one under way.</summary>
        internal static void Toggle(Player player)
        {
            if (!ModConfig.IsEnabled || !ModConfig.EditPlacedPieces.Value || player == null)
            {
                return;
            }

            if (IsEditing)
            {
                Cancel(player, "Edit cancelled - the piece is untouched");
                return;
            }

            Begin(player);
        }

        private static void Begin(Player player)
        {
            if (SelectPiece == null)
            {
                return;
            }

            Piece piece = player.GetHoveringPiece();
            if (piece == null)
            {
                Notify.Show(player, "Look at a piece to edit it");
                return;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                Notify.Show(player, "That cannot be edited");
                return;
            }

            if (Holds(piece, out string holding))
            {
                // Editing rebuilds the piece from its prefab, and a prefab has no idea what was
                // inside the thing it is replacing. Emptying a chest to move it is the player's
                // decision to make, not ours to make quietly on their behalf.
                Notify.Show(player, "Empty the " + holding + " first");
                return;
            }

            if (!SelectPiece(player, piece))
            {
                // Vanilla's own answer when the recipe is not known or the station is missing.
                Notify.Show(player, "You cannot build that piece");
                return;
            }

            _original = view.GetZDO().m_uid;
            _originalPrefab = Utils.GetPrefabName(piece.gameObject);

            RotationState.MatchPiece(piece);
            ScaleState.MatchPiece(piece);

            Ghost(piece.gameObject);

            HammerOfOdenPlugin.Debug(
                $"Editing '{_originalPrefab}' ({_original}); rotation and scale copied.");
            Notify.Show(player, "Editing " + piece.m_name + " - place to apply");
        }

        /// <summary>
        /// Whether a piece is carrying state that rebuilding it would throw away.
        /// </summary>
        /// <remarks>
        /// Checked by component rather than by a list of prefab names, so a chest another mod
        /// adds is covered by the same rule. A sign's text and an item stand's contents are as
        /// much worth keeping as a chest's inventory, and none of the three survive the piece
        /// being replaced.
        /// </remarks>
        private static bool Holds(Piece piece, out string what)
        {
            Container container = piece.GetComponent<Container>();
            if (container != null && container.GetInventory() != null
                && container.GetInventory().NrOfItems() > 0)
            {
                what = "container";
                return true;
            }

            ItemStand stand = piece.GetComponent<ItemStand>();
            if (stand != null && stand.HaveAttachment())
            {
                what = "item stand";
                return true;
            }

            Sign sign = piece.GetComponent<Sign>();
            if (sign != null && !string.IsNullOrEmpty(sign.GetText()))
            {
                what = "sign";
                return true;
            }

            what = null;
            return false;
        }

        /// <summary>
        /// Takes the original down, now that its replacement stands.
        /// </summary>
        /// <remarks>
        /// Ordering matters: the new piece is already placed by the time this runs, so anything
        /// that was leaning on the old one has something to lean on before it goes. Removing
        /// first would drop whatever it was holding up.
        ///
        /// The materials go back rather than being dropped, because the replacement has just
        /// charged for them: an edit that costs a full set of stone to nudge a wall is not an
        /// edit. That leaves it net zero rather than free, which is the honest answer - undo
        /// already refunds the same way, and reusing it means both obey the same setting and
        /// the same carry-weight rules.
        /// </remarks>
        internal static void CommitAfterPlacement(Player player)
        {
            if (!IsEditing)
            {
                return;
            }

            GameObject instance = Original;
            ZDOID was = _original;
            string prefab = _originalPrefab;

            Clear();

            if (instance == null)
            {
                HammerOfOdenPlugin.Debug($"Edited piece {was} was already gone; nothing removed.");
                return;
            }

            if (Utils.GetPrefabName(instance) != prefab)
            {
                HammerOfOdenPlugin.Debug(
                    $"Edit skipped an id that is now '{Utils.GetPrefabName(instance)}', not '{prefab}'.");
                return;
            }

            ZNetView view = instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            Unghost(instance);

            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            bool refunded = PlacementUndo.Refund(instance.GetComponent<Piece>());

            WearNTear wear = instance.GetComponent<WearNTear>();
            if (wear != null)
            {
                wear.Remove(refunded);
            }
            else
            {
                ZNetScene.instance.Destroy(instance);
            }

            HammerOfOdenPlugin.Debug($"Edit replaced '{prefab}' and removed the original.");
            Notify.Show(player, "Edited");
        }

        /// <summary>Ends an edit without touching the original.</summary>
        internal static void Cancel(Player player, string message)
        {
            if (!IsEditing)
            {
                return;
            }

            HammerOfOdenPlugin.Debug($"Edit of {_original} cancelled.");

            Unghost(Original);
            Clear();

            if (player != null && message != null)
            {
                Notify.Show(player, message);
            }
        }

        internal static void Clear()
        {
            // Anything still on the list belongs to a piece that has gone out of the world
            // under us. The colliders went with it, so this is only tidying the bookkeeping.
            Suppressed.Clear();
            _tinted = false;

            _original = ZDOID.None;
            _originalPrefab = null;
        }

        /// <summary>
        /// Takes the piece being edited out of the way: no collision, and faint enough to see
        /// past while still showing where it stands.
        /// </summary>
        /// <remarks>
        /// Collision is the point. A piece cannot be nudged a few centimetres or scaled slightly
        /// while its own former self is still solid in the same space - the placement check sees
        /// the original and refuses, and the change you came to make is the one change you
        /// cannot make. Since the piece is about to be replaced anyway, nothing is lost by
        /// standing it down early.
        ///
        /// It is worth knowing what that costs, because structural support is decided by
        /// Physics.OverlapBox against the colliders actually present: for as long as the edit
        /// lasts, the piece holds nothing up. Editing a wall a roof is resting on can therefore
        /// drop the roof if the game recalculates support in that window. The window is short
        /// and the colliders come back the moment the edit ends by any route, but the setting is
        /// there for anyone who would rather not take that chance on a load-bearing piece.
        ///
        /// The fade goes through Valheim's own per-object material system, the one that turns a
        /// ghost red when it cannot be placed, so no shared material is touched and no piece
        /// elsewhere in the world changes colour. Whether the alpha reads as see-through depends
        /// on the shader the piece uses; the darkening does not, so even where alpha is ignored
        /// the piece still reads as stood down rather than solid.
        /// </remarks>
        private static void Ghost(GameObject piece)
        {
            if (piece == null)
            {
                return;
            }

            if (ModConfig.EditRemovesCollision.Value)
            {
                foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
                {
                    if (collider == null || !collider.enabled)
                    {
                        continue;
                    }

                    collider.enabled = false;
                    Suppressed.Add(collider);
                }
            }

            if (MaterialMan.instance != null)
            {
                MaterialMan.instance.SetValue(piece, ShaderProps._Color, ModConfig.EditGhostTint.Value);
                _tinted = true;
            }
        }

        private static void Unghost(GameObject piece)
        {
            foreach (Collider collider in Suppressed)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }

            Suppressed.Clear();

            if (_tinted && piece != null && MaterialMan.instance != null)
            {
                MaterialMan.instance.ResetValue(piece, ShaderProps._Color);
            }

            _tinted = false;
        }
    }
}
