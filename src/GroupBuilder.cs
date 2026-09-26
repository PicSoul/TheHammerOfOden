using System.Collections.Generic;
using System.Diagnostics;
using Splatform;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>One piece of a copy, waiting to be built: what, where, and what it carries.</summary>
    internal sealed class CopyOrder
    {
        internal Piece Prefab;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal Vector3 Scale;

        internal float BendDegrees;
        internal int BendAxis;
        internal int BendRise;
        internal int BendChoice;

        internal string SignText;
        internal string SignAuthor;
        internal string SignAuthorName;

        /// <summary>A hoe step - raising, levelling, a path - rather than a piece: it reshapes the ground and is gone.</summary>
        internal bool IsGround => Prefab != null
            && (Prefab.GetComponent<TerrainOp>() != null || Prefab.GetComponent<TerrainModifier>() != null);

        /// <summary>
        /// The order a plan is built in: the ground first, so the building stands on the ground it
        /// was drawn for, then from the bottom up.
        /// </summary>
        internal static int BuildOrder(CopyOrder a, CopyOrder b)
        {
            bool groundA = a.IsGround;
            bool groundB = b.IsGround;
            return groundA != groundB ? (groundA ? -1 : 1) : a.Position.y.CompareTo(b.Position.y);
        }
    }

    /// <summary>
    /// Builds the local player's construction sites: whatever can be paid for, from the bottom up,
    /// each piece flying from the hammer to where it belongs.
    /// </summary>
    /// <remarks>
    /// Paid for as each piece lands, through the game's own charge, so a mod that builds from
    /// chests - AzuCraftyBoxes and its kind - pays from them. A piece that cannot be paid for is
    /// passed over and the next one tried, so a copy short of iron still puts up every wooden
    /// wall it can. What cannot be built yet stays on the site's ghost until it can.
    ///
    /// Kept cheap while waiting. The costly part of asking "can I afford this?" is not this mod
    /// but a chest mod searching every container in range, so it is asked as seldom as possible:
    ///   - once per kind of piece per pass, not once per piece - a building is a few dozen kinds
    ///   - when something changes - the player's inventory or any chest's contents - not every
    ///     frame
    ///   - otherwise every few seconds, for whatever changes without saying so
    ///   - only while the player who placed the site is near it
    /// A site nobody is near costs nothing, and a waiting site costs one short pass every few
    /// seconds.
    ///
    /// Spread over time, and from the bottom up, because building thousands of pieces in one
    /// frame is a freeze. The support rule that would bring a part-built building down is held
    /// off by the site itself - see ConstructionSite.
    /// </remarks>
    internal static class GroupBuilder
    {
        private sealed class Flight
        {
            internal ConstructionSite Site;
            internal int Index;
            internal GameObject Visual;
            internal Vector3 From;
            internal Quaternion Spin;
            internal float Started;
            internal float Duration;
            internal float Arc;
        }

        /// <summary>More than this in the air at once and pieces are built without flying.</summary>
        private const int MostInFlight = 300;

        /// <summary>Longest a frame may spend launching, in milliseconds.</summary>
        private const long FrameBudget = 6;

        /// <summary>How often a waiting site looks again when nothing has said anything changed.</summary>
        private const float QuietCheck = 5f;

        private static readonly List<Flight> Flights = new List<Flight>();
        private static readonly HashSet<(long site, int index)> InFlight = new HashSet<(long, int)>();
        private static readonly Dictionary<Piece, bool> Affordable = new Dictionary<Piece, bool>();

        private static ConstructionSite _working;
        private static int _cursor;
        private static int _launchedThisPass;
        private static float _carry;
        private static bool _poked = true;
        private static float _nextCheck;
        private static float _lastPass = -999f;
        private static float _nextPanel;

        /// <summary>
        /// Soonest a change can start another look. Every landing pays for a piece, and paying
        /// changes the inventory - so without this a site landing forty pieces a second would
        /// look again forty times a second.
        /// </summary>
        private const float PokeGap = 0.5f;
        private static float _nextEffect;
        private static string _lastReport;
        private static float _nextReport;

        private static List<CopyOrder> _pending;
        private static object _pendingUndo;

        /// <summary>True while a copied piece is being built, so the placing scale and bend stay off it.</summary>
        internal static bool PlacingCopies { get; private set; }

        /// <summary>Something may have changed what can be paid for; look again soon.</summary>
        internal static void Poke()
        {
            _poked = true;
        }

        internal static bool IsInFlight(ConstructionSite site, int index)
        {
            return site != null && InFlight.Contains((site.Id, index));
        }

        // ------------------------------------------------------------------ starting a site

        /// <summary>
        /// The plan of a copy just placed, waiting for the first piece - the one the game itself
        /// builds - to carry it. See Found.
        /// </summary>
        internal static void Start(List<CopyOrder> plan, object undoAction)
        {
            _pending = plan;
            _pendingUndo = undoAction;
        }

        /// <summary>The game has built the copy's first piece: the site goes onto it.</summary>
        internal static void Found(Piece first, Player player)
        {
            if (_pending == null)
            {
                return;
            }

            List<CopyOrder> plan = _pending;
            object undo = _pendingUndo;
            _pending = null;
            _pendingUndo = null;

            plan.Sort(CopyOrder.BuildOrder);
            ConstructionSite site = ConstructionSite.Create(first, plan, player, undo);
            if (site != null)
            {
                SitePanel.Update(site, false);
                _working = site;
                _cursor = 0;
                _launchedThisPass = 0;
                Affordable.Clear();
                _lastReport = null;
                Poke();
            }
        }

        // ------------------------------------------------------------------ every frame

        internal static void Tick()
        {
            Land();

            Player player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null)
            {
                return;
            }

            if (!Workable(_working, player))
            {
                _working = null;
                bool due = Time.time >= _nextCheck || (_poked && Time.time >= _lastPass + PokeGap);
                if (!due)
                {
                    return;
                }

                _poked = false;
                _lastPass = Time.time;
                _nextCheck = Time.time + QuietCheck;
                _working = Nearest(player);
                _cursor = 0;
                _launchedThisPass = 0;
                Affordable.Clear();

                if (_working == null)
                {
                    return;
                }

                _working.Reconcile();
            }

            if (Time.time >= _nextPanel && _working != null)
            {
                _nextPanel = Time.time + 2f;
                SitePanel.Update(_working, false);
            }

            Launch(player, _working);
        }

        private static bool Workable(ConstructionSite site, Player player)
        {
            return site != null && site.View != null && site.View.IsValid() && site.Plan != null
                && !site.IsFinished && !site.Halted && site.Owner == player.GetPlayerID()
                && Vector3.Distance(player.transform.position, site.View.transform.position) <= ModConfig.SiteRange.Value;
        }

        private static ConstructionSite Nearest(Player player)
        {
            ConstructionSite best = null;
            float bestDistance = float.MaxValue;

            foreach (ConstructionSite site in ConstructionSite.All)
            {
                if (!Workable(site, player))
                {
                    continue;
                }

                float distance = Vector3.Distance(player.transform.position, site.View.transform.position);
                if (distance < bestDistance)
                {
                    best = site;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>
        /// Sends up what can be paid for, lowest first, at the configured pace. At the end of a
        /// pass the site either starts another - pieces went up, so there may be more - or, if
        /// nothing could be paid for, says what it is waiting for and waits.
        /// </summary>
        private static void Launch(Player player, ConstructionSite site)
        {
            int remaining = site.Plan.Count - site.BuiltCount;
            float rate = Mathf.Max(ModConfig.BuildRate.Value, remaining / Mathf.Max(1f, ModConfig.BuildLongest.Value));

            _carry = Mathf.Min(_carry + rate * Time.deltaTime, 50f);
            Stopwatch frame = Stopwatch.StartNew();

            while (_carry >= 1f && frame.ElapsedMilliseconds < FrameBudget)
            {
                if (_cursor >= site.Plan.Count)
                {
                    EndPass(player, site);
                    return;
                }

                int index = _cursor++;
                if (site.Done[index] || InFlight.Contains((site.Id, index)))
                {
                    continue;
                }

                CopyOrder order = site.Plan[index];
                if (order.Prefab == null)
                {
                    // A kind of piece this game no longer has: counted as done so the site can finish.
                    site.MarkBuilt(index, null);
                    continue;
                }

                if (!Affordable.TryGetValue(order.Prefab, out bool affordable))
                {
                    affordable = GroupHold.CanAfford(player, order.Prefab);
                    Affordable[order.Prefab] = affordable;
                }

                if (!affordable)
                {
                    continue;
                }

                // Asked again for this very piece: what paid for the last one may be spent.
                if (!GroupHold.CanAfford(player, order.Prefab))
                {
                    Affordable[order.Prefab] = false;
                    continue;
                }

                _carry -= 1f;
                _launchedThisPass++;
                Send(player, site, index);
            }
        }

        private static void EndPass(Player player, ConstructionSite site)
        {
            bool progressed = _launchedThisPass > 0;

            _cursor = 0;
            _launchedThisPass = 0;
            _carry = 0f;
            Affordable.Clear();

            if (progressed)
            {
                // Pieces went up, so go round again: there may be more that can be paid for.
                return;
            }

            // Nothing more can be paid for right now: wait for something to change. Landings
            // pay for pieces, which counts as a change, so the last of the flights will prompt
            // the next look.
            _working = null;
            _nextCheck = Time.time + QuietCheck;

            if (Flights.Count == 0)
            {
                Report(player, site);
            }
        }

        /// <summary>What the site still needs - said when it changes, or now and then while you are near.</summary>
        private static void Report(Player player, ConstructionSite site)
        {
            SitePanel.Update(site, true);

            // The panel keeps the list in view; with it switched off, a message says it instead.
            if (ModConfig.SitePanelShown.Value)
            {
                return;
            }

            List<string> parts = new List<string>();
            foreach ((string item, int amount) in Needs(site))
            {
                ItemDrop drop = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() : null;
                parts.Add($"{amount} {(drop != null ? drop.m_itemData.m_shared.m_name : item)}");
                if (parts.Count == 10)
                {
                    break;
                }
            }

            string report = $"{site.BuiltCount + 1} of {site.Plan.Count + 1} pieces up - waiting for "
                + string.Join(", ", parts)
                + $". It carries on by itself as they turn up. {KeyNames.Of(ModConfig.SiteCancelKey)} on it stops it where it stands.";

            if (report == _lastReport && Time.time < _nextReport)
            {
                return;
            }

            _lastReport = report;
            _nextReport = Time.time + 60f;
            Notify.Show(player, report);
        }

        /// <summary>
        /// What a site's unbuilt pieces still cost, by material, most first - as item prefab names,
        /// which is what the panel looks icons up by and what its saved record keeps.
        /// </summary>
        internal static List<(string item, int amount)> Needs(ConstructionSite site)
        {
            Dictionary<string, int> needs = new Dictionary<string, int>();
            foreach (int index in Remaining(site))
            {
                Piece prefab = site.Plan[index].Prefab;
                if (prefab == null
                    || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(prefab.FreeBuildKey())))
                {
                    continue;
                }

                foreach (Piece.Requirement requirement in prefab.m_resources)
                {
                    if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0)
                    {
                        continue;
                    }

                    string item = Utils.GetPrefabName(requirement.m_resItem.gameObject);
                    needs.TryGetValue(item, out int n);
                    needs[item] = n + requirement.m_amount;
                }
            }

            List<(string item, int amount)> list = new List<(string, int)>();
            foreach (KeyValuePair<string, int> entry in needs)
            {
                list.Add((entry.Key, entry.Value));
            }

            list.Sort((a, b) => b.amount.CompareTo(a.amount));
            return list;
        }

        private static IEnumerable<int> Remaining(ConstructionSite site)
        {
            for (int i = 0; i < site.Plan.Count; i++)
            {
                if (!site.Done[i])
                {
                    yield return i;
                }
            }
        }

        // ------------------------------------------------------------------ flight

        private static void Send(Player player, ConstructionSite site, int index)
        {
            CopyOrder order = site.Plan[index];

            GameObject visual = ModConfig.BuildFlying.Value && Flights.Count < MostInFlight
                ? PieceLooks.Draw(PieceLooks.For(order), "HoO_FlyingPiece")
                : null;

            if (visual == null)
            {
                Build(player, site, index);
                return;
            }

            Vector3 from = HandOf(player);
            float distance = Vector3.Distance(from, order.Position);

            Flight flight = new Flight
            {
                Site = site,
                Index = index,
                Visual = visual,
                From = from,
                Spin = Random.rotationUniform,
                Started = Time.time,
                Duration = Mathf.Clamp(0.35f + distance / 25f, 0.4f, 1.4f),
                Arc = 1f + distance * 0.25f
            };

            Flights.Add(flight);
            InFlight.Add((site.Id, index));
            site.Redraw(index);
            Move(flight, 0f);
        }

        private static void Land()
        {
            if (Flights.Count == 0)
            {
                return;
            }

            Player player = Player.m_localPlayer;

            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                Flight flight = Flights[i];
                float t = (Time.time - flight.Started) / flight.Duration;

                if (t < 1f && flight.Site != null)
                {
                    Move(flight, t);
                    continue;
                }

                Flights.RemoveAt(i);
                if (flight.Visual != null)
                {
                    Object.Destroy(flight.Visual);
                }

                if (flight.Site == null)
                {
                    continue;
                }

                InFlight.Remove((flight.Site.Id, flight.Index));
                Build(player, flight.Site, flight.Index);
            }
        }

        /// <summary>An arc from the hammer, small and turning at first, settling as it lands.</summary>
        private static void Move(Flight flight, float t)
        {
            if (flight.Visual == null)
            {
                return;
            }

            CopyOrder order = flight.Site.Plan[flight.Index];
            float eased = 1f - (1f - t) * (1f - t);
            Vector3 position = Vector3.Lerp(flight.From, order.Position, eased)
                + Vector3.up * (flight.Arc * 4f * t * (1f - t));

            Transform visual = flight.Visual.transform;
            visual.SetPositionAndRotation(position, Quaternion.Slerp(order.Rotation * flight.Spin, order.Rotation, eased));
            visual.localScale = order.Scale * Mathf.Lerp(0.15f, 1f, eased);
        }

        private static Vector3 HandOf(Player player)
        {
            VisEquipment equipment = player.GetComponent<VisEquipment>();
            if (equipment != null && equipment.m_rightHand != null)
            {
                return equipment.m_rightHand.position;
            }

            return player.transform.position + Vector3.up * 1.4f;
        }

        // ------------------------------------------------------------------ building one piece

        /// <summary>One piece of a site, built and paid for as it lands.</summary>
        private static void Build(Player player, ConstructionSite site, int index)
        {
            if (player == null || site == null || site.Plan == null || site.Done == null || site.Done[index])
            {
                return;
            }

            CopyOrder order = site.Plan[index];

            // Paid for on landing: if it no longer can be, it goes back on the ghost.
            if (order.Prefab == null || !GroupHold.CanAfford(player, order.Prefab))
            {
                site.Redraw(index);
                return;
            }

            BuildOrder(player, order, site.UndoAction, view => site.MarkBuilt(index, view));

            if (site.IsFinished)
            {
                // One more than the plan: the site's first piece was built before it.
                Notify.Show(player, $"Finished - {site.Plan.Count + 1} pieces");
                HammerOfOdenPlugin.Debug($"Construction site {site.Id} finished.");
                Drop(site);
                site.Finish();
            }
        }

        /// <summary>
        /// One piece, built the way the game builds a placed one, recorded for undo and paid for.
        /// </summary>
        /// <param name="tag">Called as soon as the piece exists - before anything checks its support.</param>
        private static Piece BuildOrder(Player player, CopyOrder order, object undoAction, System.Action<ZNetView> tag)
        {
            Piece prefab = order.Prefab;
            Piece piece = null;

            PlacingCopies = true;
            TerrainOp groundOp = prefab.GetComponent<TerrainOp>();
            EffectList groundSound = null;
            try
            {
                // A hoe step applies itself and plays its dust the moment it exists. Hundreds of
                // them from one blueprint would ask for more sounds than the game has channels,
                // so all but a few a second go quietly.
                if (groundOp != null && Time.time < _nextEffect)
                {
                    groundSound = groundOp.m_onPlacedEffect;
                    groundOp.m_onPlacedEffect = new EffectList();
                }

                TerrainModifier.SetTriggerOnPlaced(true);
                GameObject built = Object.Instantiate(prefab.gameObject, order.Position, order.Rotation);
                TerrainModifier.SetTriggerOnPlaced(false);

                if (groundSound != null)
                {
                    groundOp.m_onPlacedEffect = groundSound;
                    groundSound = null;
                }
                else if (groundOp != null)
                {
                    _nextEffect = Time.time + 0.12f;
                }

                ZNetView view = built.GetComponent<ZNetView>();
                bool valid = view != null && view.IsValid();

                // Belongs to its site from the start, so the support rule passes it by.
                tag?.Invoke(valid ? view : null);

                Vector3 prefabScale = prefab.transform.localScale;
                if (valid && (order.Scale - prefabScale).sqrMagnitude > 0.000001f)
                {
                    view.m_syncInitialScale = true;
                    view.SetLocalScale(order.Scale);
                    ScaleState.ScaleParticles(built, order.Scale);
                    ScaledRanges.Apply(built, new Vector3(
                        order.Scale.x / Mathf.Max(0.0001f, prefabScale.x),
                        order.Scale.y / Mathf.Max(0.0001f, prefabScale.y),
                        order.Scale.z / Mathf.Max(0.0001f, prefabScale.z)));
                }

                BentPiece.Attach(built, order.BendDegrees, order.BendAxis, order.BendRise, order.BendChoice);

                CraftingStation station = built.GetComponentInChildren<CraftingStation>();
                if (station != null)
                {
                    player.AddKnownStation(station);
                }

                piece = built.GetComponent<Piece>();
                if (piece != null)
                {
                    piece.SetCreator(player.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
                    PlacementUndo.RecordInto(undoAction, piece);
                }

                built.GetComponent<PrivateArea>()?.Setup(Game.instance.GetPlayerProfile().GetName());
                built.GetComponent<WearNTear>()?.OnPlaced();
                built.GetComponent<ItemDrop>()?.MakePiece(true);

                foreach (IPlaced placed in built.GetComponents<IPlaced>())
                {
                    placed.OnPlaced();
                }

                WriteSign(view, order);

                bool cheated = (player.GetInventory().ItemCheated(prefab.m_resources) || player.NoCostCheat())
                    && !PlayerProfile.s_bypassCheatChecks;
                if (cheated && valid)
                {
                    view.GetZDO().Set(ZDOVars.s_cheated, true);
                }

                Game.instance.IncrementPlayerStat(PlayerStatType.Builds, 1f, cheated);
                Game.instance.GetPlayerProfile().IncrementStatBuildPiecePlaced(prefab.m_name, 1f, cheated);

                if (!player.NoCostCheat()
                    && (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(prefab.FreeBuildKey())))
                {
                    Pay(player, prefab, undoAction);
                }

                // The landing is heard, but not a hundred times a second.
                if (Time.time >= _nextEffect)
                {
                    _nextEffect = Time.time + 0.12f;
                    prefab.m_placeEffect.Create(order.Position, order.Rotation, built.transform);
                }
            }
            finally
            {
                PlacingCopies = false;
                if (groundSound != null)
                {
                    groundOp.m_onPlacedEffect = groundSound;
                }
            }

            return piece;
        }

        // ------------------------------------------------------------------ rebuilding

        /// <summary>
        /// Puts pieces back up where they stood - undoing a selection taken down, or redoing a
        /// placement that was undone. Built as a construction site, so it is paid for, pays from
        /// wherever the game pays from, and waits as a ghost for what cannot be paid for yet.
        /// </summary>
        /// <remarks>
        /// A site lives on its first piece, and here there is no piece the game has just built to
        /// put it on - so the lowest piece that can be paid for is built on the spot to carry it.
        /// If not one can be paid for, nothing starts and the step stays where it was.
        /// </remarks>
        /// <param name="undoAction">The undo step the rebuilt pieces go into.</param>
        internal static bool Rebuild(Player player, List<CopyOrder> orders, object undoAction, string verb)
        {
            List<CopyOrder> plan = new List<CopyOrder>();
            foreach (CopyOrder order in orders)
            {
                // Someone may have warded the ground since; their ward wins.
                if (order?.Prefab != null && PrivateArea.CheckAccess(order.Position, 0f, false, false))
                {
                    plan.Add(order);
                }
            }

            if (plan.Count == 0)
            {
                Notify.Show(player, "Nothing there can be put back - it is inside someone else's ward now");
                return false;
            }

            plan.Sort(CopyOrder.BuildOrder);

            int first = plan.FindIndex(order => GroupHold.CanAfford(player, order.Prefab));
            if (first < 0)
            {
                Notify.Show(player, $"Not enough materials to put back even one of the {plan.Count} pieces yet");
                return false;
            }

            CopyOrder cornerstone = plan[first];
            plan.RemoveAt(first);

            Piece built = BuildOrder(player, cornerstone, undoAction, null);
            if (built == null)
            {
                return false;
            }

            if (plan.Count > 0)
            {
                ConstructionSite site = ConstructionSite.Create(built, plan, player, undoAction);
                if (site != null)
                {
                    SitePanel.Update(site, false);
                    _working = site;
                    _cursor = 0;
                    _launchedThisPass = 0;
                    Affordable.Clear();
                    _lastReport = null;
                    Poke();
                }
            }

            Notify.Show(player, $"{verb}: putting back {plan.Count + 1} pieces, paid for as they go up");
            return true;
        }

        /// <summary>The unbuilt pieces of any site going into this undo step - for redo to rebuild them too.</summary>
        internal static List<CopyOrder> Unbuilt(object undoAction)
        {
            List<CopyOrder> unbuilt = new List<CopyOrder>();
            if (undoAction == null)
            {
                return unbuilt;
            }

            foreach (ConstructionSite site in ConstructionSite.All)
            {
                if (site == null || site.UndoAction != undoAction || site.Plan == null || site.Done == null)
                {
                    continue;
                }

                for (int i = 0; i < site.Plan.Count; i++)
                {
                    if (!site.Done[i] && site.Plan[i]?.Prefab != null)
                    {
                        unbuilt.Add(site.Plan[i]);
                    }
                }
            }

            return unbuilt;
        }

        /// <summary>
        /// Charges for a piece through the game's own charge, and notes how much of each material
        /// came out of the backpack and how much out of chests, so undo can send it back there.
        /// </summary>
        /// <remarks>
        /// Worked out by counting the inventory and the backpack before and after: whatever was
        /// spent and did not leave either of them came from a chest. Counted item by item rather
        /// than with the game's count, which AdventureBackpacks inflates with the backpack at
        /// exactly this moment.
        /// </remarks>
        private static void Pay(Player player, Piece prefab, object undoAction)
        {
            Inventory inventory = player.GetInventory();
            Inventory backpack = Refunds.BackpackOf(player);

            int n = prefab.m_resources.Length;
            int[] invBefore = new int[n];
            int[] packBefore = new int[n];

            for (int i = 0; i < n; i++)
            {
                Piece.Requirement requirement = prefab.m_resources[i];
                if (requirement?.m_resItem == null)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                invBefore[i] = Refunds.Count(inventory, name);
                packBefore[i] = Refunds.Count(backpack, name);
            }

            player.ConsumeResources(prefab.m_resources, 0);

            if (undoAction == null)
            {
                return;
            }

            for (int i = 0; i < n; i++)
            {
                Piece.Requirement requirement = prefab.m_resources[i];
                if (requirement?.m_resItem == null || requirement.m_amount <= 0)
                {
                    continue;
                }

                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int fromInventory = Mathf.Max(0, invBefore[i] - Refunds.Count(inventory, name));
                int fromBackpack = Mathf.Max(0, packBefore[i] - Refunds.Count(backpack, name));
                int fromChests = Mathf.Max(0, requirement.m_amount - fromInventory - fromBackpack);

                PlacementUndo.RecordPaid(undoAction, Refunds.ItemPrefab(requirement.m_resItem), fromBackpack, fromChests);
            }
        }

        /// <summary>
        /// A sign's words, and whose words they are. The author stays the original writer, so a
        /// player who has blocked them still does not see their words on the copy.
        /// </summary>
        internal static void WriteSign(ZNetView view, CopyOrder order)
        {
            if (view == null || !view.IsValid() || order == null || string.IsNullOrEmpty(order.SignText))
            {
                return;
            }

            ZDO zdo = view.GetZDO();
            zdo.Set(ZDOVars.s_text, order.SignText);
            if (order.SignAuthor != null)
            {
                zdo.Set(ZDOVars.s_author, order.SignAuthor);
            }

            if (order.SignAuthorName != null)
            {
                zdo.Set(ZDOVars.s_authorDisplayName, order.SignAuthorName);
            }
        }

        // ------------------------------------------------------------------ stopping

        /// <summary>
        /// Halts the site that undo is about to take down, so nothing more lands. It stays live -
        /// and so keeps standing what has not been taken down yet - until EndAbandon.
        /// </summary>
        internal static void Abandon(object undoAction)
        {
            if (undoAction == null)
            {
                return;
            }

            foreach (ConstructionSite site in new List<ConstructionSite>(ConstructionSite.All))
            {
                if (site != null && site.UndoAction == undoAction)
                {
                    Drop(site);
                    site.Halted = true;
                }
            }
        }

        /// <summary>Undo has taken the site's pieces down: whatever is left of it ends here.</summary>
        internal static void EndAbandon(object undoAction)
        {
            if (undoAction == null)
            {
                return;
            }

            foreach (ConstructionSite site in new List<ConstructionSite>(ConstructionSite.All))
            {
                if (site != null && site.UndoAction == undoAction)
                {
                    site.Stop();
                }
            }
        }

        private static float _takeDownUntil;
        private static long _takeDownId;

        /// <summary>
        /// Takes down everything the copy the player is looking at put up - that copy and nothing
        /// else - with its materials handed back, as a group take-down hands them back. Asked twice.
        /// </summary>
        /// <remarks>
        /// Undo is the usual way to take a copy back, but undo is forgotten when the game closes.
        /// This works any time: the copy is found by the id its every piece carries, which the
        /// world keeps. A copy still going up stops first, and its ghost goes with it. The
        /// take-down is itself an undo step, so it can be put back up while the game is running.
        ///
        /// Only the player who placed a copy can take it down this way, and only what is loaded
        /// around them - a very large copy may need taking down from its middle.
        /// </remarks>
        internal static void TakeDownLookedAt(Player player)
        {
            Piece hovered = player != null ? player.GetHoveringPiece() : null;
            long id = ConstructionSite.IdOf(hovered);
            if (hovered == null || id == 0L)
            {
                Notify.Show(player, "Look at a piece of a copy to take the whole copy down");
                return;
            }

            ConstructionSite site = ConstructionSite.Of(hovered);
            bool mine = site != null ? site.Owner == player.GetPlayerID() : hovered.GetCreator() == player.GetPlayerID();
            if (!mine)
            {
                Notify.Show(player, "Only the player who placed this copy can take it down this way");
                return;
            }

            List<GameObject> pieces = ConstructionSite.PiecesOf(id);
            if (pieces.Count == 0)
            {
                return;
            }

            if (Time.time > _takeDownUntil || _takeDownId != id)
            {
                _takeDownUntil = Time.time + 4f;
                _takeDownId = id;
                Notify.Show(player, $"{KeyNames.Of(ModConfig.SiteTakeDownKey)} again within 4 seconds to take down all "
                    + $"{pieces.Count} pieces this copy put up" + (site != null ? ", and its ghost" : string.Empty)
                    + ". Their materials come back to you; undo puts it back up.");
                return;
            }

            _takeDownUntil = 0f;

            // Still going up: nothing more lands, but it keeps standing until the last piece is down.
            if (site != null)
            {
                Drop(site);
                site.Halted = true;
            }

            if (PlacementUndo.Demolish(player, pieces))
            {
                SitePanel.Forget(id);
            }
        }

        /// <summary>
        /// Stops the site the player is looking at, where it stands. Only the player who placed
        /// it can.
        /// </summary>
        internal static void StopLookedAt(Player player)
        {
            // The same key takes a model off the table it stands on.
            if (player != null && ModelDisplay.RemoveFrom(player, player.GetHoveringPiece()))
            {
                return;
            }

            ConstructionSite site = ConstructionSite.Of(player != null ? player.GetHoveringPiece() : null);
            if (site == null)
            {
                return;
            }

            if (site.Owner != player.GetPlayerID())
            {
                Notify.Show(player, "Only the player who placed this copy can stop it");
                return;
            }

            int built = site.BuiltCount + 1;
            Drop(site);
            site.Stop();
            Notify.Show(player, $"Copy stopped with {built} pieces up. The ghost is gone, and anything "
                + "left without support will now fall.");
        }

        /// <summary>Takes a site's pieces out of the air.</summary>
        private static void Drop(ConstructionSite site)
        {
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                if (Flights[i].Site == site)
                {
                    if (Flights[i].Visual != null)
                    {
                        Object.Destroy(Flights[i].Visual);
                    }

                    InFlight.Remove((site.Id, Flights[i].Index));
                    Flights.RemoveAt(i);
                }
            }

            if (_working == site)
            {
                _working = null;
            }
        }

        /// <summary>Forgets everything, for leaving a world.</summary>
        internal static void Reset()
        {
            foreach (Flight flight in Flights)
            {
                if (flight.Visual != null)
                {
                    Object.Destroy(flight.Visual);
                }
            }

            Flights.Clear();
            InFlight.Clear();
            Affordable.Clear();
            _working = null;
            _pending = null;
            _pendingUndo = null;
            _lastReport = null;
            PlacingCopies = false;
            PieceLooks.Clear();
            ModelDisplay.ClearMaterials();
            SitePanel.Reset();
            ConstructionSite.Reset();
        }
    }
}
