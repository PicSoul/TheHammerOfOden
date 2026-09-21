using System.Collections.Generic;
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

        private static readonly List<List<Placed>> History = new List<List<Placed>>();

        /// <summary>Starts a new group, for a placement the player has just made themselves.</summary>
        internal static void BeginAction()
        {
            History.Add(new List<Placed>());

            // Depth is about what you can still remember doing, not about memory: a few
            // thousand ids would cost nothing. Undoing something from twenty minutes ago
            // would simply be a surprise.
            while (History.Count > Mathf.Max(1, ModConfig.UndoDepth.Value))
            {
                History.RemoveAt(0);
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

            History[History.Count - 1].Add(new Placed
            {
                Id = view.GetZDO().m_uid,
                Prefab = Utils.GetPrefabName(piece.gameObject)
            });
        }

        /// <summary>Removes everything placed by the most recent action.</summary>
        internal static bool UndoLast(Player player)
        {
            if (ZNetScene.instance == null)
            {
                return false;
            }

            while (History.Count > 0)
            {
                List<Placed> group = History[History.Count - 1];
                History.RemoveAt(History.Count - 1);

                int removed = Remove(group);

                if (removed > 0)
                {
                    Notify.Show(player, removed == 1 ? "Undid 1 piece" : $"Undid {removed} pieces");
                    return true;
                }

                // Everything in that group is already gone - torn down by hand, or by a
                // raid. Fall through to the one before rather than doing nothing visible.
            }

            Notify.Show(player, "Nothing to undo");
            return false;
        }

        private static int Remove(List<Placed> group)
        {
            int removed = 0;

            // Backwards, so a run comes down in the reverse of the order it went up and
            // whatever was supporting it is taken last.
            for (int i = group.Count - 1; i >= 0; i--)
            {
                GameObject instance = ZNetScene.instance.FindInstance(group[i].Id);

                // Missing means the piece is either gone already or in an unloaded zone,
                // and neither is something to complain about.
                if (instance == null)
                {
                    continue;
                }

                if (Utils.GetPrefabName(instance) != group[i].Prefab)
                {
                    HammerOfOdenPlugin.Debug(
                        $"Undo skipped an id that is now '{Utils.GetPrefabName(instance)}', "
                        + $"not the '{group[i].Prefab}' that was placed.");
                    continue;
                }

                ZNetView view = instance.GetComponent<ZNetView>();
                if (view == null || !view.IsValid())
                {
                    continue;
                }

                if (!view.IsOwner())
                {
                    view.ClaimOwnership();
                }

                bool refunded = Refund(instance.GetComponent<Piece>());

                WearNTear wear = instance.GetComponent<WearNTear>();

                if (wear != null)
                {
                    // Vanilla's drop is blocked only when we have handed the materials over
                    // ourselves. Blocking it unconditionally would pay nothing at all with
                    // RefundToInventory off, and not blocking it would pay twice with it on.
                    wear.Remove(refunded);
                }
                else
                {
                    ZNetScene.instance.Destroy(instance);
                }

                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Gives back what a piece cost, into the inventory where possible.
        /// </summary>
        /// <remarks>
        /// Only requirements marked m_recover are returned, which is the same rule vanilla
        /// applies when it drops them - a few pieces deliberately consume something you do
        /// not get back, and undo is not the place to change that.
        ///
        /// Build pieces have no quality or level, so the flat m_amount is the whole cost.
        /// The per-level amounts on a Requirement belong to crafting recipes.
        /// </remarks>
        /// <returns>True if the materials were handed over, so vanilla must not drop them too.</returns>
        internal static bool Refund(Piece piece)
        {
            Player player = Player.m_localPlayer;

            if (piece == null || player == null || piece.m_resources == null
                || !ModConfig.UndoRefundsToInventory.Value)
            {
                return false;
            }

            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null
                    || !requirement.m_recover || requirement.m_amount <= 0)
                {
                    continue;
                }

                Give(player, requirement.m_resItem, requirement.m_amount);
            }

            return true;
        }

        /// <summary>
        /// Into the pack, and whatever will not go there onto the ground at your feet.
        /// </summary>
        /// <remarks>
        /// Two separate limits, and Valheim only enforces one of them. CanAddItem answers
        /// about slots; nothing stops an item taking you over your carry weight, because in
        /// normal play you pick things up deliberately and can accept the penalty. A refund
        /// arrives without being asked for, so it must not be able to leave you staggering
        /// after an undo you expected to cost nothing.
        ///
        /// Capacity is read each time rather than cached. It moves with a belt, a buff or a
        /// change of gear, and an undo minutes later must not be working from a number that
        /// was true when the piece was built.
        ///
        /// The whole amount is offered first and then one at a time, because a stack that
        /// does not fit entirely may still fit partly - refusing the lot because the last
        /// two are over the limit would drop far more than it needed to.
        /// </remarks>
        private static void Give(Player player, ItemDrop item, int amount)
        {
            Inventory inventory = player.GetInventory();
            int remaining = amount;

            if (inventory != null)
            {
                float unitWeight = item.m_itemData.m_shared.m_weight;

                int allowed = Carrying.WeightAllows(player, unitWeight, remaining);

                if (allowed >= remaining
                    && inventory.CanAddItem(item.gameObject, remaining)
                    && inventory.AddItem(item.gameObject, remaining))
                {
                    return;
                }

                while (remaining > 0
                    && Carrying.WeightAllows(player, unitWeight, 1) >= 1
                    && inventory.CanAddItem(item.gameObject, 1)
                    && inventory.AddItem(item.gameObject, 1))
                {
                    remaining--;
                }
            }

            if (remaining <= 0)
            {
                return;
            }

            DropAtFeet(player, item, remaining);

            HammerOfOdenPlugin.Debug(
                $"Undo dropped {remaining} x {item.name} at the player's feet; "
                + "no room by slots or by weight.");
        }

        /// <summary>
        /// Puts what would not fit on the ground where the player is standing.
        /// </summary>
        /// <remarks>
        /// Instantiated from the item prefab rather than handed to ItemDrop.DropItem, which
        /// looks the obvious way and does not work here: DropItem instantiates the ItemData's
        /// m_dropPrefab, and that field is filled in at runtime rather than stored on the
        /// prefab asset, so a template copied straight off the prefab carries a null and
        /// Instantiate throws on it.
        ///
        /// Split into stacks the item actually allows, because one heap of two hundred wood
        /// is not a thing the game can represent.
        /// </remarks>
        private static void DropAtFeet(Player player, ItemDrop item, int amount)
        {
            int perStack = Mathf.Max(1, item.m_itemData.m_shared.m_maxStackSize);
            Vector3 feet = player.transform.position + Vector3.up * 0.4f;

            while (amount > 0)
            {
                int stack = Mathf.Min(amount, perStack);
                amount -= stack;

                // A hand's width apart, so several stacks are visibly a pile rather than
                // one item sitting on top of another.
                Vector3 where = feet + new Vector3(
                    Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));

                GameObject dropped = Object.Instantiate(item.gameObject, where, Quaternion.identity);

                ItemDrop drop = dropped.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    continue;
                }

                drop.m_itemData.m_stack = stack;
                ItemDrop.OnCreateNew(drop);
            }
        }


        internal static void Clear()
        {
            History.Clear();
        }
    }
}
