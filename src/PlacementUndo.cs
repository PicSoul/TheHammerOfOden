using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Takes back the last thing you built.
    /// </summary>
    /// <remarks>
    /// Zooping made this necessary. Laying one piece wrong costs one swing to correct;
    /// laying twenty wrong costs twenty, and you will not notice the mistake until the run
    /// is finished and you can see it. A single key that takes the whole run back is what
    /// makes committing to a run comfortable in the first place.
    ///
    /// It undoes a placement *action*, not a piece. That is a run when you zooped and a
    /// single piece when you did not, which is the unit you were thinking in either way.
    ///
    /// Nothing is free: a piece gives back what it cost and no more, the same as taking it
    /// down by hand. An undo that refunded more than that would be a way of manufacturing
    /// resources.
    ///
    /// Where it goes is a different question from how much. Vanilla scatters the materials
    /// at the piece, which is fine for one piece and useless for a run - undo a wall forty
    /// long and the refund is spread over its whole length, in forty little piles, and you
    /// walk the wall picking them up. So the materials are handed straight to you, and only
    /// what will not fit is dropped, in one heap at your feet where you can see it.
    ///
    /// Pieces are held as ZDOIDs rather than references, because the objects themselves are
    /// destroyed and recreated whenever their zone unloads, and a reference would go stale
    /// the first time you walked away. The prefab name is kept alongside and checked on the
    /// way back, so an id that has come to mean something else is skipped rather than
    /// removing a stranger's building.
    /// </remarks>
    internal static class PlacementUndo
    {
        private struct Placed
        {
            internal ZDOID Id;
            internal string Prefab;
        }

        /// <summary>
        /// One thing the player did. Usually pieces placed, which undo takes down; or, for a move,
        /// a way to put what was moved back where it stood - moving builds nothing, so there is
        /// nothing to take down.
        /// </summary>
        private sealed class Action
        {
            internal readonly List<Placed> Placed = new List<Placed>();
            internal System.Func<int> Revert;

            /// <summary>For a move: does it again. Revert's opposite, for redo.</summary>
            internal System.Func<int> Reapply;
            internal string Noun;

            /// <summary>
            /// Pieces to build rather than pieces to take down - what an undone placement, or a
            /// selection taken down, turns into. Performing it builds them as a construction site,
            /// paid for again: taking them down gave the materials back.
            /// </summary>
            internal List<CopyOrder> Orders;

            /// <summary>
            /// For a copy: how much of what it cost came out of the backpack and out of chests,
            /// so undo can put it back there. The rest came from the inventory.
            /// </summary>
            internal readonly Refunds.Bill Paid = new Refunds.Bill();
        }

        /// <summary>Longest a frame may spend taking pieces down, in milliseconds.</summary>
        private const long FrameBudget = 6;

        private static bool _undoing;

        private static readonly List<Action> History = new List<Action>();

        /// <summary>
        /// What has been undone, most recent last, for redo. Anything new the player does clears
        /// it - redoing across something done since would put things back into a world that has
        /// moved on.
        /// </summary>
        private static readonly List<Action> Future = new List<Action>();

        /// <summary>Starts a new group, for a placement the player has just made themselves.</summary>
        internal static void BeginAction()
        {
            Future.Clear();
            History.Add(new Action());
            Trim();
        }

        /// <summary>
        /// Records something undone by reversing it rather than by taking pieces down.
        /// </summary>
        /// <param name="revert">Reverses it, returning how many pieces it put back.</param>
        /// <param name="reapply">Does it again, for redo.</param>
        /// <param name="noun">What to call them in the message: "moved piece", say.</param>
        internal static void RecordRevertible(System.Func<int> revert, System.Func<int> reapply, string noun)
        {
            Future.Clear();
            History.Add(new Action { Revert = revert, Reapply = reapply, Noun = noun });
            Trim();
        }

        private static void Trim()
        {
            // Depth is about what you can still remember doing, not about memory: a few
            // thousand ids would cost nothing. Undoing something from twenty minutes ago
            // would simply be a surprise.
            int depth = Mathf.Max(1, ModConfig.UndoDepth.Value);
            while (History.Count > depth)
            {
                History.RemoveAt(0);
            }

            while (Future.Count > depth)
            {
                Future.RemoveAt(0);
            }
        }

        /// <summary>
        /// The action most recently begun, for something that goes on adding to it after the
        /// player may have done something else - a copy going up piece by piece.
        /// </summary>
        internal static object CurrentAction => History.Count > 0 ? History[History.Count - 1] : null;

        /// <summary>Adds a piece to that action, wherever it now is in the history.</summary>
        internal static void RecordInto(object action, Piece piece)
        {
            if (!(action is Action target) || piece == null)
            {
                return;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            target.Placed.Add(new Placed
            {
                Id = view.GetZDO().m_uid,
                Prefab = Utils.GetPrefabName(piece.gameObject)
            });
        }

        /// <summary>Notes where a copied piece's materials came from, for undo to send them back.</summary>
        internal static void RecordPaid(object action, GameObject item, int fromBackpack, int fromChests)
        {
            if (!(action is Action target) || item == null)
            {
                return;
            }

            if (fromBackpack > 0)
            {
                target.Paid.FromBackpack.TryGetValue(item, out int n);
                target.Paid.FromBackpack[item] = n + fromBackpack;
            }

            if (fromChests > 0)
            {
                target.Paid.FromChests.TryGetValue(item, out int n);
                target.Paid.FromChests[item] = n + fromChests;
            }
        }

        /// <summary>Adds a freshly placed piece to the group in progress.</summary>
        internal static void Record(Piece piece)
        {
            if (piece == null || History.Count == 0)
            {
                return;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            History[History.Count - 1].Placed.Add(new Placed
            {
                Id = view.GetZDO().m_uid,
                Prefab = Utils.GetPrefabName(piece.gameObject)
            });
        }

        /// <summary>Takes back the most recent thing done.</summary>
        internal static bool UndoLast(Player player)
        {
            return Step(player, History, Future, "Undid", "Nothing to undo");
        }

        /// <summary>Does again the most recent thing undone.</summary>
        internal static bool RedoLast(Player player)
        {
            return Step(player, Future, History, "Redid", "Nothing to redo");
        }

        /// <summary>
        /// Performs the latest step of one list, and files its opposite on the other.
        /// </summary>
        /// <remarks>
        /// Undo and redo are the same operation run in opposite directions, because every step
        /// has an opposite:
        ///   pieces placed      are taken down and paid back   - the opposite rebuilds them
        ///   pieces to rebuild  are built as a site, paid for   - the opposite takes them down
        ///   a move             is put back                     - the opposite moves it again
        /// A step whose pieces are all gone - torn down by hand, or by a raid - is passed over for
        /// the one before, rather than doing nothing visible.
        /// </remarks>
        private static bool Step(Player player, List<Action> from, List<Action> to, string verb, string nothing)
        {
            if (ZNetScene.instance == null || player == null)
            {
                return false;
            }

            if (_undoing)
            {
                Notify.Show(player, "Still taking the last lot down");
                return true;
            }

            while (from.Count > 0)
            {
                Action action = from[from.Count - 1];
                from.RemoveAt(from.Count - 1);

                if (action.Revert != null)
                {
                    int reverted = action.Revert();
                    if (reverted > 0)
                    {
                        to.Add(new Action { Revert = action.Reapply, Reapply = action.Revert, Noun = action.Noun });
                        Trim();
                        Notify.Show(player, $"{verb}: {reverted} {action.Noun}{(reverted == 1 ? string.Empty : "s")}");
                        return true;
                    }

                    continue;
                }

                if (action.Orders != null)
                {
                    Action built = new Action();
                    if (GroupBuilder.Rebuild(player, action.Orders, built, verb))
                    {
                        to.Add(built);
                        Trim();
                        return true;
                    }

                    // Could not start - nothing affordable yet. Kept, so it can be tried again.
                    from.Add(action);
                    return true;
                }

                // A copy still going up stops first, so nothing lands after it is taken down.
                List<CopyOrder> unbuilt = GroupBuilder.Unbuilt(action);
                GroupBuilder.Abandon(action);

                List<GameObject> standing = Standing(action.Placed);

                if (standing.Count > 0 || unbuilt.Count > 0)
                {
                    List<CopyOrder> orders = OrdersOf(standing);
                    orders.AddRange(unbuilt);

                    to.Add(new Action { Orders = orders });
                    Trim();

                    if (standing.Count > 0)
                    {
                        ZNetScene.instance.StartCoroutine(TakeDown(player, action, standing,
                            ModConfig.UndoRefundsToInventory.Value, ModConfig.UndoChestRange.Value, verb));
                    }
                    else
                    {
                        GroupBuilder.EndAbandon(action);
                    }

                    return true;
                }

                // A copy none of whose pieces are left: it still must not go on building.
                GroupBuilder.EndAbandon(action);
            }

            Notify.Show(player, nothing);
            return false;
        }

        private static List<CopyOrder> OrdersOf(List<GameObject> pieces)
        {
            List<CopyOrder> orders = new List<CopyOrder>(pieces.Count);
            foreach (GameObject piece in pieces)
            {
                CopyOrder order = GroupHold.OrderOf(piece);
                if (order != null)
                {
                    orders.Add(order);
                }
            }

            return orders;
        }

        /// <summary>The pieces of an action still standing here, checked to still be what was placed.</summary>
        private static List<GameObject> Standing(List<Placed> group)
        {
            List<GameObject> standing = new List<GameObject>();

            foreach (Placed placed in group)
            {
                GameObject instance = ZNetScene.instance.FindInstance(placed.Id);

                // Missing means the piece is either gone already or in an unloaded zone,
                // and neither is something to complain about.
                if (instance == null)
                {
                    continue;
                }

                if (Utils.GetPrefabName(instance) != placed.Prefab)
                {
                    HammerOfOdenPlugin.Debug(
                        $"Undo skipped an id that is now '{Utils.GetPrefabName(instance)}', "
                        + $"not the '{placed.Prefab}' that was placed.");
                    continue;
                }

                standing.Add(instance);
            }

            return standing;
        }

        /// <summary>
        /// Takes an action's pieces down over as many frames as it needs, top first, quietly,
        /// and pays back once at the end.
        /// </summary>
        /// <remarks>
        /// Undoing a large copy in one frame was a long freeze with a break sound and a burst of
        /// splinters per piece - more sounds than the game has channels for - and a refund per
        /// piece. Now:
        ///   - a few milliseconds of it per frame, so the game keeps running
        ///   - from the top down, so nothing is left hanging with its support gone first
        ///   - a copy's first piece last: it holds the construction site, and the site is what
        ///     keeps the rest standing until it is all down
        ///   - the break effect now and then, not for every piece
        ///   - everything totalled and handed over in one go per material - see Refunds
        /// </remarks>
        /// <param name="action">The undo step being taken back, or null for a selection taken down.</param>
        /// <param name="refund">Hand the materials over, rather than let each piece drop its own.</param>
        /// <param name="chestRange">How far to look for chests to send chest materials back to.</param>
        /// <param name="done">"Undid", or "Took down", for the message.</param>
        private static IEnumerator TakeDown(Player player, Action action, List<GameObject> pieces,
            bool refund, float chestRange, string done)
        {
            _undoing = true;
            Refunds.Bill bill = new Refunds.Bill();
            if (action != null)
            {
                foreach (KeyValuePair<GameObject, int> entry in action.Paid.FromBackpack)
                {
                    bill.FromBackpack[entry.Key] = entry.Value;
                }

                foreach (KeyValuePair<GameObject, int> entry in action.Paid.FromChests)
                {
                    bill.FromChests[entry.Key] = entry.Value;
                }
            }

            int removed = 0;
            float nextEffect = 0f;

            pieces.Sort((a, b) =>
            {
                bool siteA = a != null && a.GetComponent<ConstructionSite>() != null;
                bool siteB = b != null && b.GetComponent<ConstructionSite>() != null;
                if (siteA != siteB)
                {
                    return siteA ? 1 : -1;
                }

                float ya = a != null ? a.transform.position.y : 0f;
                float yb = b != null ? b.transform.position.y : 0f;
                return yb.CompareTo(ya);
            });

            try
            {
                Stopwatch frame = Stopwatch.StartNew();

                foreach (GameObject instance in pieces)
                {
                    if (frame.ElapsedMilliseconds >= FrameBudget)
                    {
                        yield return null;
                        frame.Restart();
                    }

                    // Gone since the undo began - fallen, or taken down by hand.
                    if (instance == null)
                    {
                        continue;
                    }

                    bool effect = Time.time >= nextEffect;
                    if (effect)
                    {
                        nextEffect = Time.time + 0.1f;
                    }

                    if (Remove(instance, refund ? bill : null, effect))
                    {
                        removed++;
                    }
                }
            }
            finally
            {
                _undoing = false;
            }

            if (action != null)
            {
                GroupBuilder.EndAbandon(action);
            }

            int dropped = 0;
            if (refund && player != null)
            {
                dropped = Refunds.Deliver(player, bill, chestRange);
            }

            if (player != null && removed > 0)
            {
                Notify.Show(player, $"{done} {removed} piece{(removed == 1 ? string.Empty : "s")}"
                    + (dropped > 0 ? $" - {dropped} items would not fit anywhere and are at your feet" : string.Empty));
            }
        }

        /// <summary>
        /// Takes a selection down: the same paced, quiet takedown as undo, with the materials
        /// handed over the way a single hammer removal hands them over.
        /// </summary>
        internal static bool Demolish(Player player, List<GameObject> pieces)
        {
            if (_undoing)
            {
                Notify.Show(player, "Still taking the last lot down");
                return false;
            }

            if (ZNetScene.instance == null || pieces == null || pieces.Count == 0)
            {
                return false;
            }

            // Taking a building down is undoable: its pieces are written down first, and undo
            // puts them back up - paid for again, since this hands the materials back.
            Future.Clear();
            History.Add(new Action { Orders = OrdersOf(pieces) });
            Trim();

            ZNetScene.instance.StartCoroutine(TakeDown(player, null, pieces,
                ModConfig.RemovalRefundsToInventory.Value, 0f, "Took down"));
            return true;
        }

        /// <summary>
        /// Takes one piece down the way the game does, less the break effect unless asked for,
        /// and with its materials added to the bill rather than dropped.
        /// </summary>
        /// <param name="bill">Where the refund goes; null to let the game drop it at the piece.</param>
        private static bool Remove(GameObject instance, Refunds.Bill bill, bool effect)
        {
            ZNetView view = instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return false;
            }

            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            Piece piece = instance.GetComponent<Piece>();
            piece?.GetComponent<IRemoved>()?.OnRemoved();

            // A piece built with cheated materials gives back cheated materials, which only the
            // game's own drop marks - so it gets the game's own drop.
            bool cheated = view.GetZDO().GetBool(ZDOVars.s_cheated);
            if (piece != null)
            {
                if (bill != null && !cheated)
                {
                    bill.AddPiece(piece);
                }
                else
                {
                    piece.DropResources();
                }
            }

            WearNTear wear = instance.GetComponent<WearNTear>();
            if (wear != null)
            {
                Bed bed = instance.GetComponent<Bed>();
                if (bed != null && Game.instance != null)
                {
                    Game.instance.RemoveCustomSpawnPoint(bed.GetSpawnPoint());
                }

                // What the piece itself does as it goes - a chest spilling what is in it.
                wear.m_onDestroyed?.Invoke();

                if (effect)
                {
                    wear.m_destroyedEffect.Create(instance.transform.position, instance.transform.rotation, instance.transform);
                }
            }

            ZNetScene.instance.Destroy(instance);
            return true;
        }

        /// <summary>
        /// Gives back what one piece cost, for an edit replacing it. Into the inventory, then the
        /// backpack, then at your feet.
        /// </summary>
        /// <returns>True if the materials were handed over, so the game must not drop them too.</returns>
        internal static bool Refund(Piece piece)
        {
            Player player = Player.m_localPlayer;
            if (piece == null || player == null || !ModConfig.UndoRefundsToInventory.Value)
            {
                return false;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && view.GetZDO().GetBool(ZDOVars.s_cheated))
            {
                return false;
            }

            Refunds.Bill bill = new Refunds.Bill();
            bill.AddPiece(piece);
            Refunds.Deliver(player, bill, 0f);
            return true;
        }

        internal static void Clear()
        {
            History.Clear();
        }
    }
}
