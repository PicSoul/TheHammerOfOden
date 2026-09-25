using System;
using HarmonyLib;

namespace TheHammerOfOden
{
    /// <summary>
    /// Whether this player may take this piece down - asked the same questions, in the same
    /// order, as the hammer's own remove.
    /// </summary>
    /// <remarks>
    /// Editing a piece takes the original down and puts a new one up. It used to do that without
    /// asking any of this, so on a shared server anyone could edit - and so replace - a wall inside
    /// someone else's ward, or a piece the game says can never be removed. The rule is now simply
    /// that if the hammer would not let you remove it, nothing in this mod lets you replace it.
    ///
    /// Vanilla's checks, from Player.RemovePiece:
    ///   m_canBeRemoved                   the piece allows removal at all
    ///   Location.IsInsideNoBuildLocation boss altars, traders and similar
    ///   PrivateArea.CheckAccess          someone else's ward - flashes it, as vanilla does
    ///   CheckCanRemovePiece              a station the piece needs is in range
    ///   Piece.CanBeRemoved               not busy: a container or ship in use
    /// </remarks>
    internal static class RemovalRules
    {
        private static readonly Func<Player, Piece, bool> StationCheck = BindStationCheck();

        private static Func<Player, Piece, bool> BindStationCheck()
        {
            try
            {
                return AccessTools.MethodDelegate<Func<Player, Piece, bool>>(
                    AccessTools.Method(typeof(Player), "CheckCanRemovePiece"));
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not bind Player.CheckCanRemovePiece; the station check for editing is skipped. "
                    + ex.Message);
                return null;
            }
        }

        /// <param name="reason">
        /// Why not, for the player - or null where the game has already said so itself.
        /// </param>
        internal static bool Allows(Player player, Piece piece, out string reason)
        {
            reason = null;

            if (player == null || piece == null)
            {
                return false;
            }

            if (!piece.m_canBeRemoved)
            {
                reason = "That piece cannot be taken down, so it cannot be edited";
                return false;
            }

            if (Location.IsInsideNoBuildLocation(piece.transform.position))
            {
                reason = "Nothing can be changed here";
                return false;
            }

            if (!PrivateArea.CheckAccess(piece.transform.position))
            {
                reason = "That is inside someone else's ward";
                return false;
            }

            // Vanilla puts up its own message when a station is missing.
            if (StationCheck != null && !StationCheck(player, piece))
            {
                return false;
            }

            if (!piece.CanBeRemoved())
            {
                reason = "That cannot be taken down right now";
                return false;
            }

            return true;
        }
    }
}
