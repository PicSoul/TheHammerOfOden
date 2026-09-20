using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Whether the player can actually take something, by weight as well as by slots.
    /// </summary>
    /// <remarks>
    /// Valheim enforces slots and not weight. Nothing stops a pickup taking you over your
    /// carry limit, which is reasonable when you chose to pick the thing up and can accept
    /// the penalty - and unreasonable for anything this mod hands over without being asked,
    /// whether that is an undo refunding a run or a camera sweeping up what it flies past.
    ///
    /// Capacity is read each time rather than remembered. It moves with a belt, a buff or a
    /// change of gear, and a decision made minutes ago must not still be working from it.
    /// </remarks>
    internal static class Carrying
    {
        /// <summary>How many of an item fit before the carry limit, up to the number wanted.</summary>
        internal static int WeightAllows(Player player, float unitWeight, int wanted)
        {
            if (player == null)
            {
                return 0;
            }

            if (unitWeight <= 0f)
            {
                return wanted;
            }

            Inventory inventory = player.GetInventory();
            if (inventory == null)
            {
                return 0;
            }

            float spare = player.GetMaxCarryWeight() - inventory.GetTotalWeight();
            if (spare <= 0f)
            {
                return 0;
            }

            return Mathf.Min(wanted, Mathf.FloorToInt(spare / unitWeight));
        }

        /// <summary>Whether a whole stack would fit, by weight and by slots together.</summary>
        internal static bool CanTake(Player player, ItemDrop.ItemData item, int stack)
        {
            if (player == null || item == null || item.m_shared == null)
            {
                return false;
            }

            if (WeightAllows(player, item.m_shared.m_weight, stack) < stack)
            {
                return false;
            }

            Inventory inventory = player.GetInventory();
            return inventory != null && inventory.CanAddItem(item, stack);
        }
    }
}
