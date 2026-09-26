using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Where materials go when a piece is taken down: back into your inventory, your backpack, or
    /// the chests they came from, and only what fits nowhere onto the ground at your feet.
    /// </summary>
    /// <remarks>
    /// Everything is handed over in one go per kind of material, however many pieces it came
    /// from. Undoing a large copy used to pay back piece by piece: thousands of separate trips
    /// into the inventory, and once that was full thousands of little heaps on the ground, each
    /// its own object for the game to carry. Totalled first, it is one trip per material and a
    /// heap per full stack.
    ///
    /// Weight is respected as well as slots. The game only enforces slots, which is fair when
    /// you chose to pick something up; a refund arrives unasked and must not leave you
    /// staggering.
    ///
    /// The backpack is AdventureBackpacks', found through its published API by name, so this
    /// mod neither needs it installed nor breaks when it is not.
    ///
    /// Chests are only ever given back what came out of chests, and only chests that already
    /// hold that material: that is the closest thing to "the chest it came from" that can be
    /// known, without every piece remembering a particular chest that may since have gone.
    /// A chest someone has open, one you have no access to, or one inside a ward you are not
    /// on, is left alone.
    /// </remarks>
    internal static class Refunds
    {
        /// <summary>Amounts of materials by item prefab, and how many of each came from where.</summary>
        internal sealed class Bill
        {
            internal readonly Dictionary<GameObject, int> Total = new Dictionary<GameObject, int>();
            internal readonly Dictionary<GameObject, int> FromBackpack = new Dictionary<GameObject, int>();
            internal readonly Dictionary<GameObject, int> FromChests = new Dictionary<GameObject, int>();

            internal bool IsEmpty => Total.Count == 0;

            internal void Add(GameObject item, int amount)
            {
                if (item == null || amount <= 0)
                {
                    return;
                }

                Total.TryGetValue(item, out int n);
                Total[item] = n + amount;
            }

            /// <summary>Everything a piece gives back when taken down: what the game itself would drop.</summary>
            internal void AddPiece(Piece piece)
            {
                if (piece == null || piece.m_resources == null
                    || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())))
                {
                    return;
                }

                Feast feast = piece.GetComponent<Feast>();
                foreach (Piece.Requirement requirement in piece.m_resources)
                {
                    if (requirement == null || requirement.m_resItem == null || !requirement.m_recover)
                    {
                        continue;
                    }

                    int amount = requirement.m_amount;
                    if (feast != null)
                    {
                        amount = Mathf.FloorToInt(amount * feast.GetStackPercentige());
                    }

                    // The game gives back a third for pieces nobody built - dungeon pieces and
                    // the like - and so does this.
                    if (!piece.IsPlacedByPlayer())
                    {
                        amount = Mathf.Max(1, amount / 3);
                    }

                    Add(ItemPrefab(requirement.m_resItem), amount);
                }
            }
        }

        private static readonly Func<Container, long, bool> ContainerAccess = BindContainerAccess();
        private static Func<Player, Inventory> _backpackOf;
        private static bool _backpackLooked;

        private static Func<Container, long, bool> BindContainerAccess()
        {
            try
            {
                return AccessTools.MethodDelegate<Func<Container, long, bool>>(
                    AccessTools.Method(typeof(Container), "CheckAccess"));
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error("Could not bind Container.CheckAccess; refunds will not go into chests. " + ex.Message);
                return null;
            }
        }

        internal static GameObject ItemPrefab(ItemDrop item)
        {
            if (item == null)
            {
                return null;
            }

            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(Utils.GetPrefabName(item.gameObject)) : null;
            return prefab != null ? prefab : item.gameObject;
        }

        // ------------------------------------------------------------------ handing over

        /// <summary>
        /// Hands a bill over. What came from chests goes back to chests holding that material,
        /// what came from the backpack back to the backpack, and the rest into the inventory,
        /// then the backpack; whatever fits nowhere lands in full stacks at your feet.
        /// </summary>
        /// <param name="chestRange">How far to look for chests; zero for none.</param>
        /// <returns>How many items went onto the ground.</returns>
        internal static int Deliver(Player player, Bill bill, float chestRange)
        {
            if (player == null || bill == null || bill.IsEmpty)
            {
                return 0;
            }

            List<Container> chests = null;
            int dropped = 0;

            foreach (KeyValuePair<GameObject, int> entry in bill.Total)
            {
                GameObject item = entry.Key;
                int left = entry.Value;

                bill.FromChests.TryGetValue(item, out int toChests);
                toChests = Mathf.Min(toChests, left);
                if (toChests > 0 && chestRange > 0f)
                {
                    chests ??= ChestsNear(player, chestRange);
                    int put = IntoChests(player, chests, item, toChests);
                    left -= put;
                }

                bill.FromBackpack.TryGetValue(item, out int toBackpack);
                toBackpack = Mathf.Min(toBackpack, left);
                if (toBackpack > 0)
                {
                    left -= Into(player, BackpackOf(player), item, toBackpack);
                }

                left -= Into(player, player.GetInventory(), item, left);
                left -= Into(player, BackpackOf(player), item, left);

                if (left > 0)
                {
                    DropAtFeet(player, item, left);
                    dropped += left;
                }
            }

            return dropped;
        }

        /// <summary>
        /// Puts as much as fits into an inventory, by slots and by the player's carry weight,
        /// a stack at a time - the game's own add takes one stack at most per call.
        /// </summary>
        private static int Into(Player player, Inventory inventory, GameObject item, int amount)
        {
            if (inventory == null || item == null || amount <= 0)
            {
                return 0;
            }

            ItemDrop drop = item.GetComponent<ItemDrop>();
            if (drop == null)
            {
                return 0;
            }

            int fits = Mathf.Min(Fits(inventory, drop, amount),
                Carrying.WeightAllows(player, drop.m_itemData.m_shared.m_weight, amount));

            int perStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            int given = 0;

            while (given < fits)
            {
                int stack = Mathf.Min(perStack, fits - given);
                if (!inventory.AddItem(item, stack))
                {
                    break;
                }

                given += stack;
            }

            return given;
        }

        /// <summary>How many of an item a container has room for, found by halving rather than one by one.</summary>
        private static int Fits(Inventory inventory, ItemDrop drop, int wanted)
        {
            if (inventory.CanAddItem(drop.m_itemData, wanted))
            {
                return wanted;
            }

            int low = 0;
            int high = wanted;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (inventory.CanAddItem(drop.m_itemData, mid))
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        private static int IntoChests(Player player, List<Container> chests, GameObject item, int amount)
        {
            ItemDrop drop = item.GetComponent<ItemDrop>();
            if (drop == null)
            {
                return 0;
            }

            string name = drop.m_itemData.m_shared.m_name;
            int perStack = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            int given = 0;

            foreach (Container chest in chests)
            {
                if (given >= amount)
                {
                    break;
                }

                Inventory inventory = chest != null ? chest.GetInventory() : null;
                if (inventory == null || !inventory.HaveItem(name))
                {
                    continue;
                }

                ZNetView view = chest.GetComponent<ZNetView>();
                if (view == null || !view.IsValid() || chest.IsInUse())
                {
                    continue;
                }

                // A chest saves its contents only on its owner's machine.
                if (!view.IsOwner())
                {
                    view.ClaimOwnership();
                }

                int room = Fits(inventory, drop, amount - given);
                while (room > 0)
                {
                    int stack = Mathf.Min(perStack, room);
                    if (!inventory.AddItem(item, stack))
                    {
                        break;
                    }

                    room -= stack;
                    given += stack;
                }
            }

            return given;
        }

        private static List<Container> ChestsNear(Player player, float range)
        {
            List<Container> chests = new List<Container>();
            HashSet<Container> seen = new HashSet<Container>();
            long me = player.GetPlayerID();

            foreach (Collider collider in Physics.OverlapSphere(player.transform.position, range,
                LayerMask.GetMask("piece", "piece_nonsolid")))
            {
                Container chest = collider != null ? collider.GetComponentInParent<Container>() : null;
                if (chest == null || !seen.Add(chest))
                {
                    continue;
                }

                // Not carts or ships: a chest is a piece that stands still.
                if (chest.GetComponent<Piece>() == null || chest.GetComponentInParent<Rigidbody>() != null)
                {
                    continue;
                }

                if ((ContainerAccess != null && !ContainerAccess(chest, me))
                    || !PrivateArea.CheckAccess(chest.transform.position, 0f, false, false))
                {
                    continue;
                }

                chests.Add(chest);
            }

            // Nearest first.
            Vector3 at = player.transform.position;
            chests.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            return chests;
        }

        // ------------------------------------------------------------------ the backpack

        /// <summary>The equipped AdventureBackpacks backpack's inventory, if there is one.</summary>
        internal static Inventory BackpackOf(Player player)
        {
            if (!_backpackLooked)
            {
                _backpackLooked = true;
                try
                {
                    Type api = AccessTools.TypeByName("AdventureBackpacks.API.ABAPI");
                    var method = api != null ? AccessTools.Method(api, "GetEquippedBackpackInventory", new[] { typeof(Player) }) : null;
                    if (method != null)
                    {
                        _backpackOf = AccessTools.MethodDelegate<Func<Player, Inventory>>(method);
                        HammerOfOdenPlugin.Debug("AdventureBackpacks found; refunds can go into the backpack.");
                    }
                }
                catch (Exception ex)
                {
                    HammerOfOdenPlugin.Debug("AdventureBackpacks API not usable: " + ex.Message);
                }
            }

            try
            {
                return _backpackOf != null && player != null ? _backpackOf(player) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// How many of a material are in a container, counted straight from its contents. The
        /// game's own count is avoided on purpose: AdventureBackpacks adds the backpack to it
        /// while the game is paying for something, which is exactly when this is asked.
        /// </summary>
        internal static int Count(Inventory inventory, string name)
        {
            if (inventory == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item != null && item.m_shared != null && item.m_shared.m_name == name)
                {
                    count += item.m_stack;
                }
            }

            return count;
        }

        // ------------------------------------------------------------------ the ground

        /// <summary>
        /// What fits nowhere, on the ground at the player's feet, in full stacks.
        /// </summary>
        /// <remarks>
        /// Instantiated from the item prefab rather than handed to ItemDrop.DropItem: that one
        /// instantiates the ItemData's m_dropPrefab, which is filled in at runtime rather than
        /// stored on the prefab, so a template taken straight off the prefab carries a null.
        /// </remarks>
        private static void DropAtFeet(Player player, GameObject item, int amount)
        {
            ItemDrop template = item.GetComponent<ItemDrop>();
            if (template == null)
            {
                return;
            }

            int perStack = Mathf.Max(1, template.m_itemData.m_shared.m_maxStackSize);
            Vector3 feet = player.transform.position + Vector3.up * 0.4f;

            while (amount > 0)
            {
                int stack = Mathf.Min(amount, perStack);
                amount -= stack;

                Vector3 where = feet + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
                GameObject dropped = UnityEngine.Object.Instantiate(item, where, Quaternion.identity);
                ItemDrop drop = dropped.GetComponent<ItemDrop>();
                if (drop == null)
                {
                    continue;
                }

                drop.m_itemData.m_stack = stack;
                ItemDrop.OnCreateNew(drop);
            }
        }
    }
}
