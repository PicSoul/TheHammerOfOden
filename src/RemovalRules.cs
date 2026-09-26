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
            return Allows(player, piece, out reason, true);
        }

        /// <param name="flash">Flash a ward that refuses, as vanilla does. Off when checking a group.</param>
        internal static bool Allows(Player player, Piece piece, out string reason, bool flash)
        {
            return Allows(player, piece, out reason, flash, true);
        }

        /// <param name="needStation">
        /// Whether the piece's crafting station has to be in range. Off for moving a group in
        /// place: the station rule is about what you can build, and a move builds nothing - the
        /// same pieces simply stand somewhere else. Across a large building most pieces are far
        /// from any station, so asking would refuse nearly every move of one.
        /// </param>
        internal static bool Allows(Player player, Piece piece, out string reason, bool flash, bool needStation)
        {
            string why = Refusal(player, piece, flash, needStation);
            reason = Sentence(why, flash);
            return why == null;
        }

        /// <summary>
        /// Why not, in a few words that read after a count - "12 x Chest (in use)" - or null if
        /// the piece may be taken down. For reporting on a group, where one sentence per piece
        /// would be unreadable.
        /// </summary>
        internal static string Refusal(Player player, Piece piece, bool flash, bool needStation)
        {
            if (player == null || piece == null)
            {
                return "gone";
            }

            if (!piece.m_canBeRemoved)
            {
                return "can never be taken down";
            }

            if (Location.IsInsideNoBuildLocation(piece.transform.position))
            {
                return "in a place that cannot be built in";
            }

            if (!PrivateArea.CheckAccess(piece.transform.position, 0f, flash, false))
            {
                return "inside someone else's ward";
            }

            if (needStation && StationCheck != null && !StationCheck(player, piece))
            {
                return "its crafting station is not in range";
            }

            if (!piece.CanBeRemoved())
            {
                return "in use";
            }

            return null;
        }

        private static string Sentence(string why, bool flash)
        {
            switch (why)
            {
                case null:
                    return null;
                case "can never be taken down":
                    return "That piece cannot be taken down, so it cannot be edited";
                case "in a place that cannot be built in":
                    return "Nothing can be changed here";
                case "inside someone else's ward":
                    return "That is inside someone else's ward";
                case "its crafting station is not in range":
                    // Vanilla has already put up its own message, unless asked quietly.
                    return flash ? null : "Its crafting station is not in range";
                case "in use":
                    return "That cannot be taken down right now";
                default:
                    return null;
            }
        }
    }
}
