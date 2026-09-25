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

        /// <summary>
        /// Direct access to the chosen snap anchor.
        /// </summary>
        /// <remarks>
        /// Setting it through the placement ghost's setup was not enough, because that setup does
        /// not always run. SetSelectedPiece rebuilds the ghost only when the selection actually
        /// changes - and editing a wall while a wall is already in hand changes nothing, which is
        /// the common case rather than the odd one. The anchor is therefore set here, where the
        /// edit begins, regardless of whether anything else happens.
        /// </remarks>
        private static readonly AccessTools.FieldRef<Player, int> ManualSnapPoint =
            AccessTools.FieldRefAccess<Player, int>("m_manualSnapPoint");

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
                // Looking at something else means "edit that instead", not "stop". Making the
                // key mean only cancel forced two presses to move to another piece, and the
                // first of them looked like nothing had happened.
                Piece wanted = player.GetHoveringPiece();
                ZNetView view = wanted == null ? null : wanted.GetComponent<ZNetView>();

                if (view != null && view.IsValid() && view.GetZDO().m_uid != _original)
                {
                    Cancel(player, null);
                    Begin(player);
                    return;
                }

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

            // Editing takes the original down, so it is held to exactly what the hammer's own
            // remove asks - wards, protected ground, pieces that cannot be removed at all.
            if (!RemovalRules.Allows(player, piece, out string refused))
            {
                if (refused != null)
                {
                    Notify.Show(player, refused);
                }

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

            // Recorded before the piece is selected, not after. Selecting rebuilds the
            // placement ghost, and the ghost's own setup is where a remembered snap anchor gets
            // restored - so an edit that announces itself afterwards has already missed the
            // moment it needed to say "not this time, snap automatically".
            _original = view.GetZDO().m_uid;
            _originalPrefab = Utils.GetPrefabName(piece.gameObject);

            if (!SelectPiece(player, piece))
            {
                // Vanilla's own answer when the recipe is not known or the station is missing.
                Clear();
                Notify.Show(player, "You cannot build that piece");
                return;
            }

            // Automatic, not the anchor last used for this kind of piece. Recalling one is for
            // laying a run of something; the piece being edited already stands where it stands.
            try
            {
                int was = ManualSnapPoint(player);
                ManualSnapPoint(player) = -1;
                HammerOfOdenPlugin.Debug($"Edit set the snap anchor to automatic (was {was}).");
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Debug("Could not reset the snap anchor: " + ex.Message);
            }

            RotationState.MatchPiece(piece);
            ScaleState.MatchPiece(piece);
            BendState.MatchPiece(piece);

            // Pinned exactly where the original stands, rather than jumping to wherever the cursor
            // happens to point. Fine adjustment is the point of editing, and a piece that starts
            // anywhere but its own place makes every adjustment start with finding it again.
            // Unfreezing lets it follow the cursor for a bigger move; placing or cancelling ends
            // the freeze, since the ghost is rebuilt after every placement.
            PlacementOffset.Reset();
            PlacementFreeze.FreezeAt(piece.transform.position, piece.transform.rotation);
            _frozeForEdit = true;

            Ghost(piece.gameObject);
            Tint(piece.gameObject);
            EditGhostMaterial.Apply(piece.gameObject);

            HammerOfOdenPlugin.Debug(
                $"Editing '{_originalPrefab}' ({_original}); rotation and scale copied.");
            Notify.Show(player, "Editing " + piece.m_name + " - held in place. Adjust it, or unfreeze ("
                + ModConfig.FreezeKey.Value.MainKey + ") to move it freely. Place to apply");
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

            ReleaseFreeze();
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
            ReleaseFreeze();
            Clear();

            if (player != null && message != null)
            {
                Notify.Show(player, message);
            }
        }

        private static bool _frozeForEdit;

        /// <summary>Lets the ghost follow the cursor again, if it was this edit that pinned it.</summary>
        private static void ReleaseFreeze()
        {
            if (!_frozeForEdit)
            {
                return;
            }

            _frozeForEdit = false;
            PlacementFreeze.Reset();
            PlacementOffset.Reset();
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
        /// elsewhere in the world changes colour.
        ///
        /// It darkens rather than fades. Custom/Piece is an opaque shader, so alpha on _Color is
        /// simply ignored - tried first, and it did nothing at all in game. What does work is
        /// pulling the colour down and giving it a little emission of its own, which reads as set
        /// aside instead of merely unlit.
        ///
        /// Darkening once was not enough either, and the reason is worth keeping. The piece being
        /// edited is also the piece the hammer is aimed at, so WearNTear.Highlight runs on it
        /// every frame - setting the same two properties to its own support colour and then
        /// scheduling ResetHighlight to clear them a fifth of a second later. Two systems writing
        /// one property, which is the door openers all over again. The answer is the same: own
        /// both ends rather than tune around them. HighlightPatch stands the vanilla highlight
        /// down for this one piece, and the colour is reasserted every frame so nothing that was
        /// already scheduled can undo it.
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

        }

        /// <summary>Asserts the edit colour. Cheap, and called every frame on purpose.</summary>
        private static void Tint(GameObject piece)
        {
            if (piece == null || MaterialMan.instance == null)
            {
                return;
            }

            // Not on top of the ghost material. MaterialMan sets a property block on the
            // renderers, and a property block beats the colour the material itself carries -
            // so tinting as well left the see-through material drawing in the tint's blue
            // rather than its own green. They are alternatives, not layers.
            if (ModConfig.EditHidesPiece.Value)
            {
                return;
            }

            MaterialMan.instance.SetValue(piece, ShaderProps._Color, ModConfig.EditGhostTint.Value);
            MaterialMan.instance.SetValue(piece, ShaderProps._EmissionColor, ModConfig.EditGhostGlow.Value);
            _tinted = true;
        }

        /// <summary>
        /// Keeps the edited piece looking edited, against anything else writing the same
        /// properties. Driven from the placement path, which already runs every frame.
        /// </summary>
        internal static void Tick()
        {
            if (IsEditing)
            {
                Tint(Original);
            }
        }

        /// <summary>Whether this is the piece currently being edited.</summary>
        internal static bool IsBeingEdited(GameObject candidate)
        {
            return IsEditing && candidate != null && Original == candidate;
        }

        private static void Unghost(GameObject piece)
        {
            EditGhostMaterial.Restore();

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
                MaterialMan.instance.ResetValue(piece, ShaderProps._EmissionColor);
            }

            _tinted = false;
        }
    }
}
