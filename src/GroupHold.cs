using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Moving a whole selection: held by one of its pieces, adjusted like one piece, and then the
    /// originals themselves moved to where it was placed.
    /// </summary>
    /// <remarks>
    /// Held by the piece you grab it by. That piece becomes the game's own placement ghost, so
    /// everything that already works on one piece works on the group for free: snapping, freezing,
    /// nudging, rotating, the stand-still rule, and the game's own check that it fits where you
    /// aim. The rest of the group is a preview that follows it.
    ///
    /// Moved in place, not rebuilt. Taking pieces down and building them again elsewhere would
    /// have to carry every chest's contents, every sign's text, every portal's name across by
    /// hand - and removing a chest spills what is in it, so any slip there duplicates items. It
    /// would also charge and refund materials, reset health and change who built what. Moving the
    /// originals avoids all of it: each piece's own saved record is updated with where it now
    /// stands, and it is the same piece, with everything it held, afterwards. The approach is the
    /// one World Edit Commands takes.
    ///
    /// The one thing moving in place does not do by itself is tell anyone else. Building pieces
    /// have no position syncing - they were never meant to move - so another player keeps seeing a
    /// moved piece where it was until the area reloads for them. That is the next step: this mod is
    /// on every client, so each can be told what moved.
    ///
    /// Copying holds the group the same way, but leaves the originals standing and builds new
    /// pieces where it is placed - handed to GroupBuilder, which puts them up from the bottom. Fresh ones, straight from the game's own prefabs, so nothing a
    /// piece holds comes with it: a copied chest is empty, a copied item stand bare. The one thing
    /// carried across is a sign's text, which is words rather than items. Every piece is paid for,
    /// the same as building it by hand, and the whole bill is checked before a single piece goes
    /// up, so a copy is never left half built for want of materials.
    ///
    /// Scaling is uniform only. A group scales about the piece it is held by, each piece's size and
    /// its distance from that piece together, so nothing overlaps when it shrinks and nothing parts
    /// when it grows. Stretching one axis cannot work: a turned piece inside a stretched group would
    /// need to be sheared, which a transform in this game cannot hold. Bending is off for the same
    /// kind of reason - a bend belongs to one piece's shape.
    /// </remarks>
    internal static class GroupHold
    {
        private struct Member
        {
            internal ZDOID Id;
            internal Vector3 Offset;
            internal Quaternion Turn;
            internal Vector3 Scale;

            // Copying only: what to build, and the little that is carried across.
            internal Piece Prefab;
            internal float BendDegrees;
            internal int BendAxis;
            internal int BendRise;
            internal int BendChoice;
            internal string SignText;
            internal string SignAuthor;
            internal string SignAuthorName;
        }

        private enum Mode
        {
            None,
            Move,
            Copy
        }

        private struct Cost
        {
            internal ItemDrop Item;
            internal string Token;
            internal int Amount;
        }

        private struct Was
        {
            internal ZDOID Id;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Scale;
        }

        private static readonly List<Member> Members = new List<Member>();
        private static readonly List<Collider> Suppressed = new List<Collider>();

        private static Mode _mode = Mode.None;
        private static ZDOID _anchor = ZDOID.None;
        private static Member _anchorData;

        /// <summary>The blueprint being held, if it is one - written onto what it builds, for re-saving.</summary>
        private static string _blueprintName;

        /// <summary>Kept on the first piece of anything built from a blueprint: which blueprint it was.</summary>
        internal const string BlueprintKey = "HoO_blueprint";

        /// <summary>The held piece's own proportions, its uniform size divided out - for a model of it.</summary>
        private static Vector3 _anchorUnitScale = Vector3.one;

        // A copy turned into a model: see ToggleModel.
        private static bool _model;
        private static float _modelScale;
        private static GameObject _modelPreview;
        private static readonly List<Mesh> ModelMeshes = new List<Mesh>();
        private static List<CopyOrder> _modelPlan;
        private static Piece _modelHost;
        private static bool _modelFits;
        private static Vector3 _modelPosition;
        private static Quaternion _modelRotation;
        private static readonly List<Renderer> HiddenGhost = new List<Renderer>();
        private static GameObject _hiddenGhostOf;
        private static string _anchorPrefab;
        private static Vector3 _anchorPosition;
        private static Quaternion _anchorRotation;
        private static float _anchorScale = 1f;
        private static GroupPreview _preview;

        private static readonly AccessTools.FieldRef<WearNTear, Collider[]> WearColliders =
            AccessTools.FieldRefAccess<WearNTear, Collider[]>("m_colliders");

        /// <summary>What copying the group costs, by item.</summary>
        private static readonly Dictionary<string, Cost> Bill = new Dictionary<string, Cost>();

        /// <summary>
        /// A stand-in piece whose cost is the whole bill, for asking the game whether it can be
        /// paid. See CanPay.
        /// </summary>
        private static Piece _billPiece;

        internal static bool IsHolding => _mode != Mode.None;
        internal static bool IsMoving => _mode == Mode.Move;
        internal static bool IsCopying => _mode == Mode.Copy;

        /// <summary>A copy turned into a model, to stand on a table or a floor.</summary>
        internal static bool IsModel => IsCopying && _model;

        /// <summary>"Move" or "Copy", for messages.</summary>
        internal static string Verb => _mode == Mode.Copy ? "Copy" : "Move";

        /// <summary>The kind of piece the group is held by - the game's ghost, while holding.</summary>
        internal static string AnchorPrefab => _anchorPrefab;

        internal static int Count => Members.Count + (IsHolding ? 1 : 0);

        // ------------------------------------------------------------------ taking it up

        internal static void BeginMove(Player player, Piece grabbed)
        {
            if (player == null || grabbed == null)
            {
                return;
            }

            List<Piece> pieces = Selection.LoadedPieces();
            if (pieces.Count < Selection.Count)
            {
                // Pieces in an unloaded area have no object to move or show. Moving their records
                // blind is possible, but not something to do to a build you cannot see.
                Notify.Show(player, $"{Selection.Count - pieces.Count} of the {Selection.Count} selected pieces are "
                    + "in an area the game has not loaded - move closer to the middle of them to move them");
                return;
            }

            // Every piece has to pass what the hammer's own remove asks - all but the station, which
            // is about what you can build, and a move builds nothing. Quietly: a ward flash per
            // piece would be a light show.
            List<Refused> refused = new List<Refused>();
            foreach (Piece piece in pieces)
            {
                string why = RemovalRules.Refusal(player, piece, false, false);
                if (why != null)
                {
                    refused.Add(RefusedPiece(piece, why));
                }
            }

            if (refused.Count > 0)
            {
                Refuse(player, refused, "moved");
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();

            if (!PlacementEdit.TakeInHand(player, grabbed))
            {
                Notify.Show(player, "You cannot build the piece you grabbed it by");
                return;
            }

            ZNetView grabbedView = grabbed.GetComponent<ZNetView>();
            _mode = Mode.Move;
            _anchor = grabbedView.GetZDO().m_uid;
            _anchorPrefab = Utils.GetPrefabName(grabbed.gameObject);
            _anchorPosition = grabbed.transform.position;
            _anchorRotation = grabbed.transform.rotation;
            _anchorScale = Mathf.Max(0.0001f, grabbed.transform.localScale.x);

            Quaternion back = Quaternion.Inverse(_anchorRotation);
            List<GameObject> others = new List<GameObject>();

            foreach (Piece piece in pieces)
            {
                // Everything is set aside, the piece it is held by included: the ghost stands in
                // for that one.
                SetAside(piece.gameObject);

                ZNetView view = piece.GetComponent<ZNetView>();
                if (piece == grabbed || view == null || !view.IsValid())
                {
                    continue;
                }

                Transform t = piece.transform;
                Members.Add(new Member
                {
                    Id = view.GetZDO().m_uid,
                    Offset = back * (t.position - _anchorPosition) / _anchorScale,
                    Turn = back * t.rotation,
                    Scale = t.localScale / _anchorScale
                });

                others.Add(piece.gameObject);
            }

            _preview = GroupPreview.Build(_anchorPosition, _anchorRotation, others);

            // Baked at the size things stand at, so the preview starts at a scale of one.
            _preview.Place(_anchorPosition, _anchorRotation, 1f, true);

            HammerOfOdenPlugin.Debug(
                $"Took up {Count} pieces to move in {watch.ElapsedMilliseconds} ms; preview built in "
                + $"{_preview.BuildMilliseconds} ms as {_preview.Merged} merged meshes and {_preview.Loose} sealed ones.");

            Notify.Show(player, $"Moving {Count} pieces - held in place. Adjust, then place. "
                + $"{KeyNames.Of(ModConfig.EditKey)} cancels.");
        }

        /// <summary>
        /// Says which pieces stopped it and why, and takes them out of the selection - so they are
        /// the ones no longer coloured, and pressing again works on the rest.
        /// </summary>
        private struct Refused
        {
            internal ZDOID Id;
            internal string Name;
            internal string Prefab;
            internal Vector3 At;
            internal string Why;
        }

        private static Refused RefusedPiece(Piece piece, string why)
        {
            ZNetView view = piece.GetComponent<ZNetView>();
            return new Refused
            {
                Id = view != null && view.IsValid() ? view.GetZDO().m_uid : ZDOID.None,
                Name = piece.m_name,
                Prefab = Utils.GetPrefabName(piece.gameObject),
                At = piece.transform.position,
                Why = why
            };
        }

        private static void Refuse(Player player, List<Refused> refused, string done)
        {
            Dictionary<(string name, string why), int> counts = new Dictionary<(string, string), int>();
            foreach (Refused entry in refused)
            {
                var key = (entry.Name, entry.Why);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;

                HammerOfOdenPlugin.Info(
                    $"Cannot be {done}: {entry.Prefab} at "
                    + $"({entry.At.x:0.0}, {entry.At.y:0.0}, {entry.At.z:0.0}) - {entry.Why}");
            }

            List<KeyValuePair<(string name, string why), int>> sorted =
                new List<KeyValuePair<(string name, string why), int>>(counts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            List<string> parts = new List<string>();
            for (int i = 0; i < sorted.Count && i < 3; i++)
            {
                parts.Add($"{sorted[i].Value} x {sorted[i].Key.name} ({sorted[i].Key.why})");
            }

            if (sorted.Count > 3)
            {
                parts.Add("more in the log");
            }

            Selection.Deselect(refused.ConvertAll(entry => entry.Id));

            Notify.Show(player, $"{refused.Count} pieces cannot be {done} and were deselected: "
                + string.Join(", ", parts) + $". Press again for the other {Selection.Count}.");
        }

        /// <summary>
        /// The selection as pieces to save, in the world's own terms: read from each piece's saved
        /// record like a copy, so loaded or not, bends and sign text included. Plants are left
        /// out, as copies leave them. So is anything that is not a buildable piece.
        /// </summary>
        /// <remarks>
        /// A construction site in the selection brings its unbuilt ghost pieces too. Saving a
        /// blueprint that is only partly built - placed to be edited - then keeps every piece of
        /// it, not just the ones you could afford.
        /// </remarks>
        /// <param name="source">The blueprint the selection was built from, if any piece of it remembers one.</param>
        internal static List<CopyOrder> SelectionForSaving(out int plants, out int ghosts, out string source)
        {
            plants = 0;
            ghosts = 0;
            source = null;
            List<CopyOrder> orders = new List<CopyOrder>();
            HashSet<long> sites = new HashSet<long>();

            foreach (ZDOID id in Selection.Ids())
            {
                ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
                GameObject prefab = zdo != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
                Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (piece == null)
                {
                    continue;
                }

                if (IsPlanted(prefab, piece))
                {
                    plants++;
                    continue;
                }

                CopyOrder order = Order(Capture(zdo, piece));
                order.Position = zdo.GetPosition();
                order.Rotation = zdo.GetRotation();
                order.Scale = ScaleOf(zdo, piece);
                orders.Add(order);

                string remembered = zdo.GetString(BlueprintKey, string.Empty);
                if (source == null && remembered.Length > 0)
                {
                    source = remembered;
                }

                long site = zdo.GetLong(ConstructionSite.IdKey, 0L);
                if (site != 0L)
                {
                    sites.Add(site);
                }
            }

            foreach (ConstructionSite site in ConstructionSite.All)
            {
                if (site == null || !sites.Contains(site.Id) || site.Plan == null || site.Done == null)
                {
                    continue;
                }

                for (int i = 0; i < site.Plan.Count; i++)
                {
                    if (!site.Done[i] && site.Plan[i]?.Prefab != null)
                    {
                        orders.Add(site.Plan[i]);
                        ghosts++;
                    }
                }
            }

            return orders;
        }

        /// <summary>
        /// Picks up a blueprint: held exactly like a copy, placed as a construction site, and
        /// turned into a model with the model key - everything a copy of the world can do.
        /// </summary>
        /// <remarks>
        /// The difference is what it is held by. A copy is held by a piece you grabbed; a
        /// blueprint has no piece standing anywhere, so it is held by the lowest of its pieces
        /// that the build menu can hand you - one you have learned, in this tool.
        /// </remarks>
        internal static bool BeginPlan(Player player, List<CopyOrder> orders, string name)
        {
            if (player == null || orders == null || orders.Count == 0)
            {
                return false;
            }

            if (IsHolding)
            {
                Cancel(player, null);
            }

            // Held by a plain piece where there is one - neither resized nor bent - lowest first. The
            // piece a group is held by is the game's own ghost, and its size is what the scale
            // keys and the size shown work from: held by a piece twice its normal size, the whole
            // blueprint would read as scaled up, although every piece in it keeps its own size.
            List<CopyOrder> sorted = new List<CopyOrder>(orders);
            sorted.Sort((a, b) =>
            {
                bool plainA = IsPlain(a);
                bool plainB = IsPlain(b);
                return plainA != plainB ? (plainA ? -1 : 1) : a.Position.y.CompareTo(b.Position.y);
            });

            CopyOrder anchor = null;
            foreach (CopyOrder order in sorted)
            {
                if (Knows(player, order.Prefab, false) && PlacementEdit.TakeInHandFromPlan(player, order))
                {
                    anchor = order;
                    break;
                }
            }

            if (anchor == null)
            {
                Notify.Show(player, $"Nothing in '{name}' is a piece you can take in hand with this tool");
                return false;
            }

            _mode = Mode.Copy;
            _blueprintName = name;
            _anchor = ZDOID.None;
            _anchorPrefab = Utils.GetPrefabName(anchor.Prefab.gameObject);
            _anchorPosition = anchor.Position;
            _anchorRotation = anchor.Rotation;
            _anchorScale = Mathf.Max(0.0001f, anchor.Scale.x);
            _anchorData = MemberOf(anchor);
            _anchorUnitScale = anchor.Scale / _anchorScale;

            Quaternion back = Quaternion.Inverse(_anchorRotation);
            List<CopyOrder> preview = new List<CopyOrder>(orders.Count);
            Bill.Clear();

            foreach (CopyOrder order in orders)
            {
                AddToBill(order.Prefab);
                if (order == anchor)
                {
                    continue;
                }

                Member member = MemberOf(order);
                member.Offset = back * (order.Position - _anchorPosition) / _anchorScale;
                member.Turn = back * order.Rotation;
                member.Scale = order.Scale / _anchorScale;
                Members.Add(member);

                // The preview is drawn at the size things stand at and scaled by the ghost's size
                // over the held piece's own - like the one built from the world - so it is laid out
                // in real metres, not in the held piece's units the members use.
                preview.Add(new CopyOrder
                {
                    Prefab = order.Prefab,
                    Position = back * (order.Position - _anchorPosition),
                    Rotation = member.Turn,
                    Scale = order.Scale,
                    BendDegrees = order.BendDegrees,
                    BendAxis = order.BendAxis,
                    BendRise = order.BendRise,
                    BendChoice = order.BendChoice
                });
            }

            Members.Sort((a, b) => a.Offset.y.CompareTo(b.Offset.y));
            _preview = GroupPreview.FromPlan(preview);

            HammerOfOdenPlugin.Debug($"Took up the blueprint '{name}': {Count} pieces, held by {_anchorPrefab}; "
                + $"preview built in {_preview.BuildMilliseconds} ms. Bill: {DescribeBill()}.");

            Notify.Show(player, $"Holding '{name}' - {Count} pieces, costs {DescribeBill()}, paid as each piece goes up; "
                + $"what you cannot pay for yet waits as a ghost. {KeyNames.Of(ModConfig.ModelKey)} makes it a model instead; "
                + $"{KeyNames.Of(ModConfig.EditKey)} puts it away.");
            return true;
        }

        private static bool IsPlain(CopyOrder order)
        {
            return order.Prefab != null && Mathf.Abs(order.BendDegrees) < 0.01f
                && (order.Scale - order.Prefab.transform.localScale).sqrMagnitude < 0.000001f;
        }

        private static Member MemberOf(CopyOrder order)
        {
            return new Member
            {
                Prefab = order.Prefab,
                BendDegrees = order.BendDegrees,
                BendAxis = order.BendAxis,
                BendRise = order.BendRise,
                BendChoice = order.BendChoice,
                SignText = order.SignText,
                SignAuthor = order.SignAuthor,
                SignAuthorName = order.SignAuthorName
            };
        }

        /// <summary>
        /// Picks up a copy of the selection, held by the piece you grabbed it by, to be built
        /// wherever you aim.
        /// </summary>
        internal static void BeginCopy(Player player, Piece grabbed)
        {
            if (player == null || grabbed == null)
            {
                return;
            }

            // Read from each piece's saved record rather than the piece standing in the world, so
            // it does not matter whether its area is loaded - a large selection reaches past what
            // the game keeps loaded around you. Everything a copy takes is in the record.
            if (IsPlanted(grabbed.gameObject, grabbed))
            {
                Notify.Show(player, "Plants are left out of a copy - pick it up by a building piece");
                return;
            }

            if (!Knows(player, grabbed, false))
            {
                Notify.Show(player, $"You have not learned to build {grabbed.m_name} - pick the copy up by a piece you know");
                return;
            }

            PieceTable tool = player.GetBuildTool();
            List<(ZDO zdo, Piece prefab)> records = new List<(ZDO, Piece)>();
            Dictionary<string, int> unlearned = new Dictionary<string, int>();
            int planted = 0;

            foreach (ZDOID id in Selection.Ids())
            {
                ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(id) : null;
                if (zdo == null)
                {
                    // Gone since it was selected.
                    continue;
                }

                GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
                Piece prefabPiece = prefab != null ? prefab.GetComponent<Piece>() : null;

                if (prefab != null && IsPlanted(prefab, prefabPiece))
                {
                    planted++;
                    continue;
                }

                // Something that is not a piece at all cannot be built; leave it out quietly.
                if (prefabPiece == null)
                {
                    continue;
                }

                // Known to this character, whichever tool builds it. The piece being in the
                // hammer's own current list is not asked: other mods move pieces between tools and
                // tabs. What is not known is left out and listed, not refused - a friend's house
                // can hold a good deal you have not learned yet, and the rest is still worth having.
                if (!Knows(player, prefabPiece, ModConfig.CopyAllowUnlearned.Value))
                {
                    unlearned.TryGetValue(prefabPiece.m_name, out int n);
                    unlearned[prefabPiece.m_name] = n + 1;

                    Vector3 at = zdo.GetPosition();
                    HammerOfOdenPlugin.Info(
                        $"Left out of a copy, not learned: {prefab.name} at ({at.x:0.0}, {at.y:0.0}, {at.z:0.0}); "
                        + $"in this tool {(tool != null && tool.m_pieces.Contains(prefab))}, category {prefabPiece.m_category}.");
                    continue;
                }

                records.Add((zdo, prefabPiece));
            }

            ZNetView grabbedView = grabbed.GetComponent<ZNetView>();
            if (grabbedView == null || !grabbedView.IsValid())
            {
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();

            if (!PlacementEdit.TakeInHand(player, grabbed))
            {
                Notify.Show(player, "You cannot build the piece you grabbed it by");
                return;
            }

            // A copy goes where you aim, not on top of the original.
            PlacementFreeze.Reset();

            _mode = Mode.Copy;
            _blueprintName = null;
            _anchor = grabbedView.GetZDO().m_uid;
            _anchorPrefab = Utils.GetPrefabName(grabbed.gameObject);
            _anchorPosition = grabbed.transform.position;
            _anchorRotation = grabbed.transform.rotation;
            _anchorScale = Mathf.Max(0.0001f, grabbed.transform.localScale.x);

            Quaternion back = Quaternion.Inverse(_anchorRotation);
            List<GameObject> others = new List<GameObject>();
            int unseen = 0;

            Bill.Clear();

            foreach (var record in records)
            {
                AddToBill(record.prefab);

                Member member = Capture(record.zdo, record.prefab);
                if (record.zdo.m_uid == _anchor)
                {
                    _anchorData = member;
                    _anchorUnitScale = ScaleOf(record.zdo, record.prefab) / _anchorScale;
                    continue;
                }

                member.Offset = back * (record.zdo.GetPosition() - _anchorPosition) / _anchorScale;
                member.Turn = back * record.zdo.GetRotation();
                member.Scale = ScaleOf(record.zdo, record.prefab) / _anchorScale;
                Members.Add(member);

                // What is loaded is shown in the preview.
                GameObject instance = ZNetScene.instance.FindInstance(record.zdo.m_uid);
                if (instance != null)
                {
                    others.Add(instance);
                }
                else
                {
                    unseen++;
                }
            }

            // Lowest first, so what stands on something goes up after what it stands on.
            Members.Sort((a, b) => a.Offset.y.CompareTo(b.Offset.y));

            _preview = GroupPreview.Build(_anchorPosition, _anchorRotation, others);
            _preview.Place(_anchorPosition, _anchorRotation, 1f, true);

            HammerOfOdenPlugin.Debug(
                $"Took up a copy of {Count} pieces ({unseen} of them not loaded) in {watch.ElapsedMilliseconds} ms; "
                + $"preview built in {_preview.BuildMilliseconds} ms as {_preview.Merged} merged meshes and "
                + $"{_preview.Loose} sealed ones. Bill: {DescribeBill()}.");

            if (planted > 0)
            {
                HammerOfOdenPlugin.Debug($"Left {planted} plants out of the copy.");
            }

            if (unlearned.Count > 0)
            {
                List<KeyValuePair<string, int>> kinds = new List<KeyValuePair<string, int>>(unlearned);
                kinds.Sort((a, b) => b.Value.CompareTo(a.Value));

                int count = 0;
                List<string> names = new List<string>();
                foreach (KeyValuePair<string, int> kind in kinds)
                {
                    count += kind.Value;
                    if (names.Count < 12)
                    {
                        names.Add($"{kind.Value} x {kind.Key}");
                    }
                }

                if (kinds.Count > names.Count)
                {
                    names.Add($"{kinds.Count - names.Count} more kinds, listed in the log");
                }

                Notify.Show(player, $"Left out {count} pieces you have not learned to build yet: "
                    + string.Join(", ", names) + ".");
            }

            Notify.Show(player, $"Copying {Count} pieces - costs {DescribeBill()}, paid as each piece goes up; "
                + "what you cannot pay for yet waits as a ghost. "
                + (unseen > 0 ? $"{unseen} are too far off to show but will be built. " : string.Empty)
                + (planted > 0 ? $"{planted} plants left out. " : string.Empty)
                + "Contents stay behind; sign text comes along. "
                + $"{KeyNames.Of(ModConfig.EditKey)} puts it away.");
        }

        /// <summary>
        /// Whether something in a selection grows rather than is built: crops, saplings, berry
        /// bushes, trees, and whatever PlantEverything lets you plant. A copy leaves these out.
        /// </summary>
        /// <remarks>
        /// Told by what makes the game treat it as a plant, not by name, so plants added by any
        /// mod are caught: something that grows (Plant), something picked (Pickable), a tree
        /// (TreeBase), or a piece that may only go on cultivated ground. Planting is the
        /// cultivator's job, with its own rules about soil and spacing, and a copy dropping crops
        /// wherever a building happens to stand would skip every one of them.
        /// </remarks>
        internal static bool IsPlantedPrefab(GameObject prefab, Piece piece) => IsPlanted(prefab, piece);

        /// <summary>Whether a copy may include this piece: known to this character, or the server allows any.</summary>
        internal static bool KnowsForCopy(Player player, Piece prefab) => Knows(player, prefab, ModConfig.CopyAllowUnlearned.Value);

        private static bool IsPlanted(GameObject prefab, Piece piece)
        {
            return prefab.GetComponent<Plant>() != null
                || prefab.GetComponent<Pickable>() != null
                || prefab.GetComponent<TreeBase>() != null
                || (piece != null && piece.m_cultivatedGroundOnly);
        }

        /// <summary>
        /// A piece standing in the world, as an order to build it again where it stands - for
        /// undo and redo, which take pieces down and put them back up. Read from its saved record,
        /// the same as a copy.
        /// </summary>
        internal static CopyOrder OrderOf(GameObject instance)
        {
            ZNetView view = instance != null ? instance.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || ZNetScene.instance == null)
            {
                return null;
            }

            ZDO zdo = view.GetZDO();
            GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            Piece prefabPiece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (prefabPiece == null)
            {
                return null;
            }

            CopyOrder order = Order(Capture(zdo, prefabPiece));
            order.Position = zdo.GetPosition();
            order.Rotation = zdo.GetRotation();
            order.Scale = ScaleOf(zdo, prefabPiece);
            return order;
        }

        /// <summary>
        /// Whether this character knows how to build a piece - the same knowledge that puts it in
        /// a build menu. A world set to unlock every piece counts as knowing them all.
        /// </summary>
        /// <param name="anyPiece">
        /// The AllowUnlearned setting: every piece counts as known. Not for the piece a copy is
        /// held by, which becomes the build menu's selection, and the menu only holds what you
        /// have learned.
        /// </param>
        private static bool Knows(Player player, Piece prefab, bool anyPiece)
        {
            return anyPiece
                || player.IsRecipeKnown(prefab.m_name)
                || player.NoCostCheat()
                || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.AllPiecesUnlocked));
        }

        /// <summary>What a copy of this piece needs: its kind, its bend, and a sign's text.</summary>
        private static Member Capture(ZDO zdo, Piece prefab)
        {
            Member member = new Member
            {
                Prefab = prefab,
                Id = zdo.m_uid,
                BendDegrees = zdo.GetFloat(BentPiece.DegreesKey, 0f),
                BendAxis = zdo.GetInt(BentPiece.AxisKey, 0),
                BendRise = zdo.GetInt(BentPiece.RiseKey, 1),
                BendChoice = zdo.GetInt(BentPiece.ChoiceKey, 1)
            };

            if (prefab.GetComponent<Sign>() != null)
            {
                member.SignText = zdo.GetString(ZDOVars.s_text, null);
                member.SignAuthor = zdo.GetString(ZDOVars.s_author, null);
                member.SignAuthorName = zdo.GetString(ZDOVars.s_authorDisplayName, null);
            }

            return member;
        }

        /// <summary>
        /// A piece's size as its record keeps it - read the way the game reads it back when the
        /// piece loads: the full size if one was saved, else a single uniform one, else the
        /// prefab's own.
        /// </summary>
        private static Vector3 ScaleOf(ZDO zdo, Piece prefab)
        {
            Vector3 scale = zdo.GetVec3(ZDOVars.s_scaleHash, Vector3.zero);
            if (scale != Vector3.zero)
            {
                return scale;
            }

            Vector3 own = prefab.transform.localScale;
            float uniform = zdo.GetFloat(ZDOVars.s_scaleScalarHash, 0f);
            return uniform > 0f ? Vector3.one * uniform : own;
        }

        private static void AddToBill(Piece prefab)
        {
            // A world setting can make some kinds of piece free to build.
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(prefab.FreeBuildKey()))
            {
                return;
            }

            foreach (Piece.Requirement requirement in prefab.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null || requirement.m_amount <= 0)
                {
                    continue;
                }

                string token = requirement.m_resItem.m_itemData.m_shared.m_name;
                Bill.TryGetValue(token, out Cost cost);
                cost.Item = requirement.m_resItem;
                cost.Token = token;
                cost.Amount += requirement.m_amount;
                Bill[token] = cost;
            }
        }

        private static string DescribeBill()
        {
            if (Bill.Count == 0)
            {
                return "nothing";
            }

            List<Cost> costs = new List<Cost>(Bill.Values);
            costs.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            return string.Join(", ", costs.ConvertAll(c => $"{c.Amount} {c.Token}"));
        }

        /// <summary>
        /// Whether a copy can go up where the ghost is: every piece paid for and nothing landing
        /// in someone else's ward. Checked in full before anything is built or spent, so a copy
        /// is never left half made. Says what is wrong if not.
        /// </summary>
        /// <remarks>
        /// No crafting station is asked for. The pieces in a selection were built once already,
        /// with whatever station they needed; a copy is paid for in full, which is what keeps it
        /// honest.
        ///
        /// Nor the whole bill. Only the piece the copy is held by has to be paid for now - the game
        /// builds that one on the spot. Everything else becomes a construction site, built as the
        /// materials turn up.
        /// </remarks>
        internal static bool CopyReady(Player player, GameObject ghost)
        {
            if (!IsCopying || player == null || ghost == null)
            {
                return false;
            }

            if (!CanAfford(player, _anchorData.Prefab))
            {
                Notify.Show(player, $"You need the materials for the {_anchorData.Prefab.m_name} you are holding "
                    + "the copy by - it goes up first, and the rest follows as materials allow");
                return false;
            }

            foreach (Member member in Members)
            {
                Target(ghost, member, out Vector3 position, out _, out _);
                if (Location.IsInsideNoBuildLocation(position))
                {
                    Notify.Show(player, "Part of the copy would land where nothing can be built");
                    return false;
                }

                if (!PrivateArea.CheckAccess(position, 0f, false, false))
                {
                    Notify.Show(player, "Part of the copy would land inside someone else's ward");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Asks the game whether these materials can be paid, as if they were the cost of one
        /// piece.
        /// </summary>
        /// <remarks>
        /// Through the game's own check rather than by counting the inventory, because that check
        /// is where mods that build from containers - AzuCraftyBoxes and its kind - add what is in
        /// the chests around you. Each piece is then charged through the game's own charge, which
        /// those mods take from the chests the same way. Without them this is the inventory alone,
        /// exactly as the game would count it.
        /// </remarks>
        private static bool CanPay(Player player, List<Cost> costs)
        {
            if (_billPiece == null)
            {
                GameObject holder = new GameObject("HoO_CopyBill");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
                _billPiece = holder.AddComponent<Piece>();
                _billPiece.m_name = "HoO_CopyBill";
                _billPiece.m_dlc = string.Empty;
                _billPiece.m_craftingStation = null;
            }

            _billPiece.m_resources = costs.ConvertAll(cost => new Piece.Requirement
            {
                m_resItem = cost.Item,
                m_amount = cost.Amount,
                m_recover = false
            }).ToArray();

            return player.HaveRequirements(_billPiece, Player.RequirementMode.CanBuild);
        }

        /// <summary>Where a member of the group goes, for the ghost where it is.</summary>
        private static void Target(GameObject ghost, Member member, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            Transform g = ghost.transform;
            float factor = GhostScale(ghost);
            position = g.position + g.rotation * (member.Offset * factor);
            rotation = g.rotation * member.Turn;
            scale = member.Scale * factor;
        }

        /// <summary>
        /// Hands every piece of the copy but the one it is held by to the builder, which puts
        /// them up over the next moments. The game places the held one itself, straight after -
        /// so that one gets the game's own effect, sound and skill, once.
        /// </summary>
        internal static void PlaceCopies(Player player, GameObject ghost)
        {
            if (!IsCopying || player == null || ghost == null)
            {
                return;
            }

            List<CopyOrder> orders = new List<CopyOrder>(Members.Count);
            foreach (Member member in Members)
            {
                Target(ghost, member, out Vector3 position, out Quaternion rotation, out Vector3 scale);
                CopyOrder order = Order(member);
                order.Position = position;
                order.Rotation = rotation;
                order.Scale = scale;
                orders.Add(order);
            }

            GroupBuilder.Start(orders, PlacementUndo.CurrentAction);
        }

        private static CopyOrder Order(Member member)
        {
            return new CopyOrder
            {
                Prefab = member.Prefab,
                BendDegrees = member.BendDegrees,
                BendAxis = member.BendAxis,
                BendRise = member.BendRise,
                BendChoice = member.BendChoice,
                SignText = member.SignText,
                SignAuthor = member.SignAuthor,
                SignAuthorName = member.SignAuthorName
            };
        }

        /// <summary>
        /// Called as the game builds the piece the copy is held by: its sign text, and the same
        /// pass on the support rule as the rest of the copy.
        /// </summary>
        internal static void DressAnchor(Piece piece)
        {
            if (IsCopying && !GroupBuilder.PlacingCopies && piece != null
                && Utils.GetPrefabName(piece.gameObject) == _anchorPrefab)
            {
                GroupBuilder.WriteSign(piece.GetComponent<ZNetView>(), Order(_anchorData));

                ZNetView view = piece.GetComponent<ZNetView>();
                if (_blueprintName != null && view != null && view.IsValid())
                {
                    view.GetZDO().Set(BlueprintKey, _blueprintName);
                }

                GroupBuilder.Found(piece, Player.m_localPlayer);
            }
        }

        /// <summary>
        /// Whether one more piece of this kind can be paid for now - asked as each piece of a copy
        /// goes up, through the same route as the whole bill.
        /// </summary>
        internal static bool CanAfford(Player player, Piece prefab)
        {
            if (player == null || prefab == null || player.NoCostCheat()
                || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(prefab.FreeBuildKey())))
            {
                return true;
            }

            List<Cost> costs = new List<Cost>();
            foreach (Piece.Requirement requirement in prefab.m_resources)
            {
                if (requirement != null && requirement.m_resItem != null && requirement.m_amount > 0)
                {
                    costs.Add(new Cost
                    {
                        Item = requirement.m_resItem,
                        Token = requirement.m_resItem.m_itemData.m_shared.m_name,
                        Amount = requirement.m_amount
                    });
                }
            }

            return costs.Count == 0 || CanPay(player, costs);
        }

        private static float _confirmUntil;
        private static int _confirmCount;

        /// <summary>
        /// The hammer's remove on a selected piece takes the whole selection down, its materials
        /// coming back the way a single removal's do.
        /// </summary>
        /// <remarks>
        /// Asked twice. One stray middle-click on a selected wall would otherwise flatten a
        /// building of thousands of pieces. Undo does bring it back, but only by building it
        /// again - paid for again, since taking it down paid you - and chests come back empty.
        ///
        /// Each piece must pass what the hammer's own remove asks - ward, in use, a piece the game
        /// will not let go of - except the crafting station, as for moving: across a building most
        /// pieces are far from any station, so asking would refuse nearly every one.
        /// </remarks>
        /// <returns>True if this took the click, so the game's own single remove must not run.</returns>
        internal static bool Demolish(Player player, Piece hovered)
        {
            if (player == null || hovered == null || Selection.Count == 0 || !Selection.Contains(hovered))
            {
                return false;
            }

            if (IsHolding)
            {
                Notify.Show(player, "Put the group down first");
                return true;
            }

            List<Piece> pieces = Selection.LoadedPieces();
            if (pieces.Count < Selection.Count)
            {
                Notify.Show(player, $"{Selection.Count - pieces.Count} of the {Selection.Count} selected pieces are "
                    + "in an area the game has not loaded - move closer to the middle of them to take them down");
                return true;
            }

            List<Refused> refused = new List<Refused>();
            foreach (Piece piece in pieces)
            {
                string why = RemovalRules.Refusal(player, piece, false, false);
                if (why != null)
                {
                    refused.Add(RefusedPiece(piece, why));
                }
            }

            if (refused.Count > 0)
            {
                Refuse(player, refused, "taken down");
                return true;
            }

            if (Time.time > _confirmUntil || _confirmCount != pieces.Count)
            {
                _confirmUntil = Time.time + 4f;
                _confirmCount = pieces.Count;
                Notify.Show(player, $"Middle-click again within 4 seconds to take down all {pieces.Count} selected pieces. "
                    + "Their materials come back to you; undo puts it all back up, paid for again.");
                return true;
            }

            _confirmUntil = 0f;

            if (PlacementUndo.Demolish(player, pieces.ConvertAll(piece => piece.gameObject)))
            {
                Selection.Clear(null);
            }

            return true;
        }

        /// <summary>Keeps the preview on the ghost. Called every frame, after the ghost has moved.</summary>
        internal static void Follow(GameObject ghost)
        {
            if (!IsHolding || _preview == null)
            {
                return;
            }

            if (_model)
            {
                _preview.Place(Vector3.zero, Quaternion.identity, 1f, false);
                FollowModel(ghost);
                return;
            }

            if (ghost == null)
            {
                _preview.Place(Vector3.zero, Quaternion.identity, 1f, false);
                return;
            }

            // The preview is baked at the size things stand at; the ghost carries the held piece's
            // own size on top, so the group's factor is the one divided by the other.
            _preview.Place(ghost.transform.position, ghost.transform.rotation, GhostScale(ghost) / _anchorScale, ghost.activeSelf);
        }

        /// <summary>The group's uniform scale, read off the ghost of the piece it is held by.</summary>
        private static float GhostScale(GameObject ghost)
        {
            return ghost.transform.localScale.x;
        }

        // ------------------------------------------------------------------ putting it down

        /// <summary>
        /// Moves every piece of the group to where the held piece is being placed. Runs instead
        /// of the game's placement: nothing is built, nothing is charged.
        /// </summary>
        /// <returns>False if it could not be moved there, so the group stays in hand.</returns>
        internal static bool CommitMove(Player player, Vector3 position, Quaternion rotation, GameObject ghost)
        {
            if (!IsMoving)
            {
                return false;
            }

            Stopwatch watch = Stopwatch.StartNew();

            float scale = ghost != null ? GhostScale(ghost) : _anchorScale;
            float factor = scale;

            // Where everything goes, worked out before anything moves.
            List<(ZDOID id, Vector3 position, Quaternion rotation, Vector3 scale)> targets =
                new List<(ZDOID, Vector3, Quaternion, Vector3)>(Members.Count + 1);

            Vector3 anchorScale = ghost != null ? ghost.transform.localScale : Vector3.one * _anchorScale;
            targets.Add((_anchor, position, rotation, anchorScale));

            foreach (Member member in Members)
            {
                targets.Add((
                    member.Id,
                    position + rotation * (member.Offset * factor),
                    rotation * member.Turn,
                    member.Scale * factor));
            }

            // Nothing may land in someone else's ward. Checked for every piece, not just the one
            // the game's own check looked at, and before a single piece has moved.
            foreach (var target in targets)
            {
                if (!PrivateArea.CheckAccess(target.position, 0f, false, false))
                {
                    Notify.Show(player, "Part of the group would land inside someone else's ward");
                    return false;
                }
            }

            PutBackAside();

            List<Was> before = new List<Was>(targets.Count);
            List<Was> after = new List<Was>(targets.Count);
            int moved = 0;

            foreach (var target in targets)
            {
                ZNetView view = View(target.id);
                if (view == null)
                {
                    continue;
                }

                Transform t = view.transform;
                before.Add(new Was { Id = target.id, Position = t.position, Rotation = t.rotation, Scale = t.localScale });
                after.Add(new Was { Id = target.id, Position = target.position, Rotation = target.rotation, Scale = target.scale });

                Apply(view, target.position, target.rotation, target.scale);
                moved++;
            }

            PlacementUndo.RecordRevertible(() => PutBack(before), () => PutBack(after), "moved piece");

            HammerOfOdenPlugin.Debug($"Moved {moved} pieces in place in {watch.ElapsedMilliseconds} ms.");
            Notify.Show(player, moved == 1 ? "Moved 1 piece" : $"Moved {moved} pieces");

            End();
            return true;
        }

        /// <summary>Undo: every moved piece back where it stood, as it stood.</summary>
        private static int PutBack(List<Was> before)
        {
            int put = 0;
            foreach (Was was in before)
            {
                ZNetView view = View(was.Id);
                if (view == null)
                {
                    continue;
                }

                Apply(view, was.Position, was.Rotation, was.Scale);
                put++;
            }

            return put;
        }

        /// <summary>
        /// Moves one piece: its saved record, and the object standing in the world for it.
        /// </summary>
        /// <remarks>
        /// The record is what persists and what other players receive; the object is what is drawn
        /// and what collides now. Both change together, or the piece would jump back on the next
        /// reload, or sit in the wrong place until then.
        ///
        /// Afterwards the piece's cached collider list is cleared, so the next support check reads
        /// the colliders where they now are rather than where they were - the same step World Edit
        /// Commands takes, and the one this mod already takes for bent pieces.
        /// </remarks>
        private static void Apply(ZNetView view, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            ZDO zdo = view.GetZDO();
            zdo.SetPosition(position);
            zdo.SetRotation(rotation);

            Transform t = view.transform;
            t.SetPositionAndRotation(position, rotation);

            if ((t.localScale - scale).sqrMagnitude > 0.000001f)
            {
                // The same route a scaled placement takes, so the size is saved and restored.
                view.m_syncInitialScale = true;
                t.localScale = scale;
                zdo.Set(ZDOVars.s_scaleHash, scale);
                ScaleState.ScaleParticles(view.gameObject, scale);
            }

            Rigidbody body = view.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            WearNTear wear = view.GetComponent<WearNTear>();
            if (wear != null)
            {
                WearColliders(wear) = null;
            }
        }

        // ------------------------------------------------------------------ setting aside

        /// <summary>
        /// Takes an original out of the way while the group is held: drawn faintly and with no
        /// collision, the same way editing one piece does, so the group can be set down over
        /// where it stood without colliding with itself.
        /// </summary>
        private static void SetAside(GameObject piece)
        {
            EditGhostMaterial.Add(piece);

            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null && collider.enabled)
                {
                    collider.enabled = false;
                    Suppressed.Add(collider);
                }
            }
        }

        private static void PutBackAside()
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
        }

        // ------------------------------------------------------------------ letting go

        internal static void Cancel(Player player, string message)
        {
            if (!IsHolding)
            {
                return;
            }

            PutBackAside();
            End();

            if (player != null && message != null)
            {
                Notify.Show(player, message);
            }
        }

        private static void End()
        {
            if (_preview != null)
            {
                _preview.Destroy();
                _preview = null;
            }

            LeaveModel();

            Members.Clear();
            Bill.Clear();
            _mode = Mode.None;
            _blueprintName = null;
            _anchor = ZDOID.None;
            _anchorPrefab = null;
            _anchorData = default;

            PlacementFreeze.Reset();
            PlacementOffset.Reset();
        }

        private static ZNetView View(ZDOID id)
        {
            GameObject instance = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
            ZNetView view = instance != null ? instance.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() ? view : null;
        }

        /// <summary>Forgets the hold, for leaving a world. The objects it touched are gone with it.</summary>
        internal static void Reset()
        {
            Suppressed.Clear();
            if (_preview != null)
            {
                _preview.Destroy();
                _preview = null;
            }

            LeaveModel();

            Members.Clear();
            Bill.Clear();
            _mode = Mode.None;
            _anchor = ZDOID.None;
            _anchorPrefab = null;
            _anchorData = default;
        }

        // ------------------------------------------------------------------ models

        /// <summary>
        /// Turns a held copy into a model of itself, or back.
        /// </summary>
        /// <remarks>
        /// A model is the copy shrunk to a miniature - one twentieth to start with, and the scale
        /// keys take it smaller or larger - and drawn rather than built: see ModelDisplay. It is
        /// placed on a table or a floor, and costs one wood if the building has any wood in it,
        /// one stone if it has stone, and always one resin - the glue that holds a model
        /// together. Nothing else - it is an ornament.
        /// </remarks>
        internal static void ToggleModel(Player player)
        {
            if (!IsCopying)
            {
                Notify.Show(player, "Pick up a copy first - Shift + middle-click on a selected piece - then press "
                    + $"{KeyNames.Of(ModConfig.ModelKey)} to turn it into a model");
                return;
            }

            if (_model)
            {
                LeaveModel();
                Notify.Show(player, "Back to a full-size copy");
                return;
            }

            _modelPlan = new List<CopyOrder>(Members.Count + 1);
            CopyOrder anchor = Order(_anchorData);
            anchor.Position = Vector3.zero;
            anchor.Rotation = Quaternion.identity;
            anchor.Scale = _anchorUnitScale;
            _modelPlan.Add(anchor);

            foreach (Member member in Members)
            {
                CopyOrder order = Order(member);
                order.Position = member.Offset;
                order.Rotation = member.Turn;
                order.Scale = member.Scale;
                _modelPlan.Add(order);
            }

            _model = true;
            _modelScale = Mathf.Clamp(ModConfig.ModelStartScale.Value, 0.005f, 0.5f);
            BuildModelPreview();

            Notify.Show(player, $"Model of {_modelPlan.Count} pieces at {Ratio()} - aim at the top of a table or a floor "
                + $"and place it. It costs {DescribeModelCost()}. The scale keys resize it; {KeyNames.Of(ModConfig.ModelKey)} "
                + "turns it back into a copy.");
        }

        internal static void ScaleModel(Player player, int sign)
        {
            _modelScale = Mathf.Clamp(_modelScale * (1f + ModConfig.ScaleStep.Value * sign), 0.005f, 0.5f);
            BuildModelPreview();
            Notify.Show(player, $"Model at {Ratio()}");
        }

        private static string Ratio()
        {
            return $"1:{Mathf.RoundToInt(1f / Mathf.Max(0.0001f, _modelScale))}";
        }

        private const float ModelNudgeFraction = 0.1f;

        /// <summary>The model's size for its root: the pieces are laid out in the held piece's units.</summary>
        private static float ModelRootScale => _modelScale * _anchorScale;

        private static void BuildModelPreview()
        {
            DestroyModelPreview();
            _modelPreview = ModelDisplay.Build(_modelPlan, ModelRootScale, ModelMeshes, out _, out _);
            if (_modelPreview != null)
            {
                _modelPreview.transform.localScale = Vector3.one * ModelRootScale;
                _modelPreview.SetActive(false);
            }
        }

        private static void DestroyModelPreview()
        {
            foreach (Mesh mesh in ModelMeshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            ModelMeshes.Clear();

            if (_modelPreview != null)
            {
                Object.Destroy(_modelPreview);
                _modelPreview = null;
            }
        }

        /// <summary>
        /// Sits the model on whatever the player aims at: its lowest point on the surface, its
        /// middle under the aim, turned the way the copy is turned.
        /// </summary>
        private static void FollowModel(GameObject ghost)
        {
            HideGhost(ghost);

            _modelHost = null;
            _modelFits = false;

            Camera camera = Utils.GetMainCamera();
            if (_modelPreview == null || camera == null || Player.m_localPlayer == null)
            {
                return;
            }

            bool hit = Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit aim, 50f,
                LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "terrain"));

            if (!hit || Vector3.Distance(aim.point, Player.m_localPlayer.transform.position) > 20f)
            {
                _modelPreview.SetActive(false);
                return;
            }

            Transform root = _modelPreview.transform;
            Quaternion turn = ghost != null ? ghost.transform.rotation : Quaternion.identity;
            root.SetPositionAndRotation(aim.point, turn);
            root.localScale = Vector3.one * ModelRootScale;
            _modelPreview.SetActive(true);

            Bounds bounds = default(Bounds);
            bool any = false;
            foreach (Renderer renderer in _modelPreview.GetComponentsInChildren<Renderer>())
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (any)
            {
                root.position += new Vector3(aim.point.x - bounds.center.x, aim.point.y - bounds.min.y, aim.point.z - bounds.center.z);
            }

            // The nudge keys - Home and End for up and down - move a model a tenth as far as a
            // piece: a centimetre a press, ten with the large step. Fine enough to sit a model's
            // base flush with a table top.
            root.position += PlacementOffset.Nudge * ModelNudgeFraction;

            Piece host = aim.collider != null ? aim.collider.GetComponentInParent<Piece>() : null;
            _modelHost = host;
            _modelFits = host != null && ModelDisplay.CanStandOn(host, aim.normal) && !ModelDisplay.Carries(host);
            _modelPosition = root.position;
            _modelRotation = root.rotation;
        }

        /// <summary>The game's own ghost of the held piece stays out of sight while a model is held.</summary>
        private static void HideGhost(GameObject ghost)
        {
            if (ghost == null || ghost == _hiddenGhostOf)
            {
                return;
            }

            ShowGhost();
            _hiddenGhostOf = ghost;
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    HiddenGhost.Add(renderer);
                }
            }
        }

        private static void ShowGhost()
        {
            foreach (Renderer renderer in HiddenGhost)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            HiddenGhost.Clear();
            _hiddenGhostOf = null;
        }

        private static void LeaveModel()
        {
            _model = false;
            _modelPlan = null;
            _modelHost = null;
            _modelFits = false;
            DestroyModelPreview();
            ShowGhost();
        }

        /// <summary>
        /// One wood if the building has wood in it, one stone if it has stone, and one resin
        /// always, for the glue. Nothing else. As bits - 1 wood, 2 stone, 4 resin - which is how
        /// the model keeps it, to be paid back.
        /// </summary>
        private static int ModelCostBits => (Bill.ContainsKey("$item_wood") ? 1 : 0) | (Bill.ContainsKey("$item_stone") ? 2 : 0) | 4;

        private static List<Cost> CostOf(int bits)
        {
            List<Cost> cost = new List<Cost>();
            if (ObjectDB.instance == null)
            {
                return cost;
            }

            foreach ((int bit, string prefab) in new[] { (1, "Wood"), (2, "Stone"), (4, "Resin") })
            {
                ItemDrop item = (bits & bit) != 0 ? ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>() : null;
                if (item != null)
                {
                    cost.Add(new Cost { Item = item, Token = item.m_itemData.m_shared.m_name, Amount = 1 });
                }
            }

            return cost;
        }

        private static string DescribeModelCost()
        {
            List<Cost> cost = CostOf(ModelCostBits);
            return cost.Count == 0 ? "nothing" : string.Join(" and ", cost.ConvertAll(c => $"1 {c.Token}"));
        }

        /// <summary>Charges for a model. False, with a message, if it cannot be paid for.</summary>
        internal static bool ChargeModel(Player player, int bits)
        {
            List<Cost> cost = CostOf(bits);
            if (cost.Count == 0 || player == null || player.NoCostCheat())
            {
                return true;
            }

            if (!CanPay(player, cost))
            {
                Notify.Show(player, "A model costs " + string.Join(" and ", cost.ConvertAll(c => $"1 {c.Token}")));
                return false;
            }

            player.ConsumeResources(cost.ConvertAll(c => new Piece.Requirement
            {
                m_resItem = c.Item,
                m_amount = c.Amount,
                m_recover = false
            }).ToArray(), 0);
            return true;
        }

        /// <summary>Gives back what a model cost.</summary>
        internal static void RefundModel(Player player, int bits)
        {
            Refunds.Bill bill = new Refunds.Bill();
            foreach (Cost cost in CostOf(bits))
            {
                bill.Add(Refunds.ItemPrefab(cost.Item), cost.Amount);
            }

            Refunds.Deliver(player, bill, 0f);
        }

        /// <summary>Puts the held model down on the table or floor it is aimed at.</summary>
        internal static void PlaceModel(Player player)
        {
            if (!IsModel || player == null)
            {
                return;
            }

            if (_modelHost != null && ModelDisplay.Carries(_modelHost))
            {
                Notify.Show(player, $"That already has a model on it - {KeyNames.Of(ModConfig.SiteCancelKey)} while looking at it takes it off");
                return;
            }

            if (!_modelFits)
            {
                Notify.Show(player, "A model goes on the top of a table or a floor");
                return;
            }

            if (!PrivateArea.CheckAccess(_modelHost.transform.position))
            {
                return;
            }

            int bits = ModelCostBits;
            if (!ChargeModel(player, bits))
            {
                return;
            }

            int pieces = _modelPlan.Count;
            Piece host = _modelHost;
            ModelDisplay.Place(host, _modelPlan, _modelPosition, _modelRotation, ModelRootScale, bits);
            host.m_placeEffect.Create(_modelPosition, _modelRotation);

            // Undo takes it off again and pays it back; redo puts it back, paid for again.
            ModelDisplay.Saved saved = ModelDisplay.Take(host);
            PlacementUndo.RecordRevertible(() => ModelDisplay.TakeOff(player, saved), () => ModelDisplay.PutBack(player, saved), "model");

            Cancel(player, null);
            Notify.Show(player, $"Model of {pieces} pieces placed. Undo takes it off; so does middle-click or "
                + $"{KeyNames.Of(ModConfig.SiteCancelKey)} on what it stands on.");
        }
    }
}
