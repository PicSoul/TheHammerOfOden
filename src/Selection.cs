using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A set of placed pieces picked out to be moved or copied together.
    /// </summary>
    /// <remarks>
    /// Held by ZDOID rather than by object, because a piece's GameObject is destroyed and
    /// rebuilt whenever its zone unloads and loads, while its ZDOID is the piece itself for as
    /// long as it exists. A selection made, walked away from and come back to is still the same
    /// selection.
    ///
    /// Several ways in, because no single one fits every building: pick pieces one at a time,
    /// grow the selection a ring at a time through whatever it touches - the way a 3D modelling
    /// program grows a face selection - take the whole connected building at once, or take only
    /// the pieces of one kind in it. Each grow is remembered, so it can be taken back a ring at a
    /// time as well.
    ///
    /// "Touching" is measured from the pieces' own collision shapes rather than from which
    /// pieces the game says support which, so it works the same on pieces another mod adds and
    /// on things that support nothing, like rugs and furniture.
    ///
    /// Selection is only ever on the selecting player's screen. Nothing about it is networked:
    /// it changes nothing in the world until something is placed, and placing goes through the
    /// game's own placement like anything else.
    /// </remarks>
    internal static class Selection
    {
        private static readonly HashSet<ZDOID> Set = new HashSet<ZDOID>();

        /// <summary>Each grow's additions, newest last, so shrinking can take back exactly one.</summary>
        private static readonly List<List<ZDOID>> Rings = new List<List<ZDOID>>();

        /// <summary>Pieces currently drawn highlighted, so they can be put back as they were.</summary>
        private static readonly HashSet<GameObject> Highlighted = new HashSet<GameObject>();

        private static Collider[] _nearby = new Collider[512];
        private static int _pieceMask = -1;
        private static float _nextHighlight;
        private static bool _shown;

        /// <summary>How far apart two pieces can be and still count as touching.</summary>
        private const float Touching = 0.12f;

        /// <summary>
        /// Most pieces one search will walk through, whatever it is looking for - a bound on a
        /// runaway search, not on buildings: far above any build anyone has reported.
        /// </summary>
        private const int SearchCeiling = 250000;

        /// <summary>
        /// Milliseconds of searching per frame. Searches run a slice at a time, so selecting a
        /// building of any size never freezes the game - it fills in over a moment instead.
        /// </summary>
        private const float BudgetPerFrame = 6f;

        private static bool _searching;
        private static bool _cancelSearch;

        private const int MaximumBuffer = 8192;

        internal static int Count => Set.Count;

        internal static bool Contains(Piece piece)
        {
            ZDOID id = IdOf(piece);
            return id != ZDOID.None && Set.Contains(id);
        }

        /// <summary>Every selected piece that is loaded right now.</summary>
        internal static List<Piece> LoadedPieces()
        {
            List<Piece> pieces = new List<Piece>();
            foreach (ZDOID id in Set)
            {
                Piece piece = Resolve(id);
                if (piece != null)
                {
                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        // ------------------------------------------------------------------ picking by hand

        /// <summary>Adds the piece you are looking at, or takes it out if it is already in.</summary>
        internal static void Toggle(Player player, Piece piece)
        {
            ZDOID id = IdOf(piece);
            if (id == ZDOID.None)
            {
                Notify.Show(player, "Look at a piece to select it");
                return;
            }

            if (Set.Remove(id))
            {
                foreach (List<ZDOID> ring in Rings)
                {
                    ring.Remove(id);
                }

                Unhighlight(piece.gameObject);
                Notify.Show(player, $"Deselected - {Describe()}");
                return;
            }

            if (Set.Count >= Limit)
            {
                Notify.Show(player, $"Selection is full ({Limit} pieces)");
                return;
            }

            Set.Add(id);
            Highlight(piece.gameObject);
            Notify.Show(player, $"Selected {Name(piece)} - {Describe()}");
        }

        internal static void Clear(Player player)
        {
            _cancelSearch = _searching;

            if (Set.Count == 0)
            {
                return;
            }

            UnhighlightAll();
            Set.Clear();
            Rings.Clear();

            if (player != null)
            {
                Notify.Show(player, "Selection cleared");
            }
        }

        // ------------------------------------------------------------------ growing

        /// <summary>Adds every piece touching the selection: one ring outward.</summary>
        internal static void Grow(Player player)
        {
            if (Set.Count == 0)
            {
                Notify.Show(player, "Select a piece first");
                return;
            }

            if (!Begin(player))
            {
                return;
            }

            ZNetScene.instance.StartCoroutine(GrowOverFrames(player));
        }

        private static IEnumerator GrowOverFrames(Player player)
        {
            Stopwatch frame = Stopwatch.StartNew();
            List<ZDOID> added = new List<ZDOID>();
            List<ZDOID> frontier = new List<ZDOID>(Set);
            bool full = false;

            foreach (ZDOID id in frontier)
            {
                if (_cancelSearch)
                {
                    break;
                }

                Piece piece = Resolve(id);
                if (piece != null)
                {
                    foreach (Piece neighbour in Neighbours(piece))
                    {
                        if (Set.Count >= Limit)
                        {
                            full = true;
                            break;
                        }

                        ZDOID neighbourId = IdOf(neighbour);
                        if (neighbourId != ZDOID.None && Set.Add(neighbourId))
                        {
                            added.Add(neighbourId);
                            Highlight(neighbour.gameObject);
                        }
                    }
                }

                if (full)
                {
                    break;
                }

                if (frame.ElapsedMilliseconds >= BudgetPerFrame)
                {
                    yield return null;
                    frame.Restart();
                }
            }

            _searching = false;

            if (_cancelSearch)
            {
                _cancelSearch = false;
                yield break;
            }

            if (added.Count == 0)
            {
                Notify.Show(player, full ? $"Selection is full ({Limit} pieces)" : "Nothing more is touching the selection");
                yield break;
            }

            Rings.Add(added);
            Notify.Show(player, $"Grew by {added.Count} - {Describe()}" + (full ? $" - stopped at the limit of {Limit}" : string.Empty));
        }

        /// <summary>Takes back the most recent grow.</summary>
        internal static void Shrink(Player player)
        {
            if (Rings.Count == 0)
            {
                Notify.Show(player, Set.Count == 0 ? "Nothing is selected" : "Nothing left to shrink back");
                return;
            }

            List<ZDOID> ring = Rings[Rings.Count - 1];
            Rings.RemoveAt(Rings.Count - 1);

            foreach (ZDOID id in ring)
            {
                if (Set.Remove(id))
                {
                    Piece piece = Resolve(id);
                    if (piece != null)
                    {
                        Unhighlight(piece.gameObject);
                    }
                }
            }

            Notify.Show(player, $"Shrank by {ring.Count} - {Describe()}");
        }

        /// <summary>
        /// Everything connected to the piece you are looking at - or, with a type, only the
        /// connected pieces of that kind.
        /// </summary>
        /// <remarks>
        /// The search walks through every connected piece whatever its kind, and a type only
        /// decides what gets selected. So "every stone wall in this building" finds walls that
        /// only meet each other through a floor.
        ///
        /// Runs a few milliseconds per frame, so a building of any size fills in over a moment
        /// rather than freezing the game while it is found. Measured at roughly 20ms per thousand
        /// pieces, a ten-thousand-piece build would otherwise have been a quarter-second freeze.
        /// </remarks>
        internal static void SelectConnected(Player player, Piece start, bool sameTypeOnly)
        {
            if (start == null)
            {
                Notify.Show(player, "Look at a piece of the building first");
                return;
            }

            if (!Begin(player))
            {
                return;
            }

            ZNetScene.instance.StartCoroutine(SearchOverFrames(player, start, sameTypeOnly));
        }

        private static IEnumerator SearchOverFrames(Player player, Piece start, bool sameTypeOnly)
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch frame = Stopwatch.StartNew();
            float working = 0f;
            int frames = 1;
            float nextProgress = Time.time + 0.5f;

            string type = Utils.GetPrefabName(start.gameObject);
            string startName = Name(start);
            List<ZDOID> added = new List<ZDOID>();

            Queue<Piece> queue = new Queue<Piece>();
            HashSet<ZDOID> visited = new HashSet<ZDOID>();

            queue.Enqueue(start);
            visited.Add(IdOf(start));

            bool full = false;
            bool truncated = false;
            int connected = 0;

            while (queue.Count > 0)
            {
                if (_cancelSearch)
                {
                    break;
                }

                Piece piece = queue.Dequeue();
                if (piece == null)
                {
                    continue; // unloaded or destroyed since it was found
                }

                connected++;

                if (!full && (!sameTypeOnly || Utils.GetPrefabName(piece.gameObject) == type))
                {
                    ZDOID id = IdOf(piece);
                    if (id != ZDOID.None && !Set.Contains(id))
                    {
                        if (Set.Count >= Limit)
                        {
                            full = true;
                        }
                        else
                        {
                            Set.Add(id);
                            added.Add(id);
                            Highlight(piece.gameObject);
                        }
                    }
                }

                if (full)
                {
                    break;
                }

                if (visited.Count >= SearchCeiling)
                {
                    truncated = true;
                }
                else
                {
                    foreach (Piece neighbour in Neighbours(piece))
                    {
                        if (visited.Add(IdOf(neighbour)))
                        {
                            queue.Enqueue(neighbour);
                        }
                    }
                }

                if (frame.ElapsedMilliseconds >= BudgetPerFrame)
                {
                    working += frame.ElapsedMilliseconds;

                    if (Time.time >= nextProgress)
                    {
                        nextProgress = Time.time + 0.5f;
                        Notify.Show(player, $"Selecting... {Set.Count} pieces");
                    }

                    yield return null;
                    frames++;
                    frame.Restart();
                }
            }

            working += frame.ElapsedMilliseconds;
            _searching = false;

            if (_cancelSearch)
            {
                _cancelSearch = false;
                yield break;
            }

            if (added.Count > 0)
            {
                Rings.Add(added);
            }

            HammerOfOdenPlugin.Debug(
                $"Selection search: walked {connected} pieces, added {added.Count}{(full ? $", stopping at the limit of {Limit}" : string.Empty)}. "
                + $"{working:0} ms of work over {frames} frames, {total.ElapsedMilliseconds} ms end to end."
                + (truncated ? $" Stopped walking at the search ceiling of {SearchCeiling}." : string.Empty));

            string what = sameTypeOnly ? "connected " + startName + " pieces" : "connected pieces";
            string note = full ? $" - stopped at the limit of {Limit}" : truncated ? " - the building is too large to search whole" : string.Empty;
            Notify.Show(player, $"Added {added.Count} {what} - {Describe()}{note}");
        }

        /// <summary>One search at a time: two would race each other over the same selection.</summary>
        private static bool Begin(Player player)
        {
            if (_searching)
            {
                Notify.Show(player, "Still selecting - one moment");
                return false;
            }

            if (ZNetScene.instance == null)
            {
                return false;
            }

            _searching = true;
            _cancelSearch = false;
            return true;
        }

        // ------------------------------------------------------------------ touching

        /// <summary>Every placed piece within touching distance of this one's collision.</summary>
        private static List<Piece> Neighbours(Piece piece)
        {
            List<Piece> found = new List<Piece>();
            HashSet<Piece> seen = new HashSet<Piece>();

            foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                int count = Overlap(collider);
                for (int i = 0; i < count; i++)
                {
                    Piece other = _nearby[i] != null ? _nearby[i].GetComponentInParent<Piece>() : null;
                    // A real placed piece only: the placement ghost is a Piece too, with no ZDO behind it.
                    if (other != null && other != piece && seen.Add(other) && IdOf(other) != ZDOID.None)
                    {
                        found.Add(other);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Everything overlapping this collider grown by the touching distance. A box collider -
        /// what most pieces use - is tested as the turned box it really is; anything else by its
        /// bounds, which is looser on a rotated piece but never misses a neighbour.
        /// </summary>
        private static int Overlap(Collider collider)
        {
            while (true)
            {
                int count;
                if (collider is BoxCollider box)
                {
                    Transform t = box.transform;
                    Vector3 half = Vector3.Scale(box.size * 0.5f, Abs(t.lossyScale)) + Vector3.one * Touching;
                    count = Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), half, _nearby, t.rotation, Mask());
                }
                else
                {
                    Bounds bounds = collider.bounds;
                    count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * Touching, _nearby, Quaternion.identity, Mask());
                }

                // A full buffer is indistinguishable from exactly that many hits, so ask again with room.
                if (count < _nearby.Length || _nearby.Length >= MaximumBuffer)
                {
                    return count;
                }

                _nearby = new Collider[_nearby.Length * 2];
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static int Mask()
        {
            if (_pieceMask == -1)
            {
                _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid", "Default", "static_solid", "Default_small", "vehicle");
            }

            return _pieceMask;
        }

        // ------------------------------------------------------------------ showing it

        /// <summary>
        /// Keeps the selection highlighted while a building tool is in hand, and only then -
        /// selected pieces glowing while you walk about with an axe would only confuse.
        /// </summary>
        /// <remarks>
        /// Runs a few times a second and costs little however large the selection: a piece is
        /// tinted when it loads and then left alone. The one thing that writes over the tint is
        /// the game's own hover highlight, which sets the same two colours on the piece under the
        /// cursor and a moment later puts back the material's own - wiping the selection's with
        /// them. That is answered where it happens, in RestoreAfterHover, rather than by
        /// repainting the whole selection on a timer: at ten thousand pieces a timer took the best
        /// part of a minute to come round, and every piece the cursor had crossed looked
        /// deselected until it did. Pieces whose ZDO is gone have been destroyed and are dropped.
        /// </remarks>
        internal static void Tick(bool show)
        {
            if (!show)
            {
                if (_shown)
                {
                    UnhighlightAll();
                    _shown = false;
                }

                return;
            }

            if (Set.Count == 0 || Time.time < _nextHighlight)
            {
                return;
            }

            _nextHighlight = Time.time + 0.25f;
            _shown = true;

            Highlighted.RemoveWhere(piece => piece == null);

            List<ZDOID> gone = null;

            foreach (ZDOID id in Set)
            {
                if (ZDOMan.instance != null && ZDOMan.instance.GetZDO(id) == null)
                {
                    (gone ??= new List<ZDOID>()).Add(id);
                    continue;
                }

                Piece piece = Resolve(id);
                if (piece == null)
                {
                    continue;
                }

                if (!Highlighted.Contains(piece.gameObject))
                {
                    Highlight(piece.gameObject);
                }
            }

            if (gone != null)
            {
                foreach (ZDOID id in gone)
                {
                    Set.Remove(id);
                }
            }
        }

        /// <summary>
        /// Puts the selection's colour back on a piece the moment the game's hover highlight lets
        /// go of it. The hover itself is left to show: its red-to-green says how well a piece is
        /// supported, which matters more while building than the selection colour does.
        /// </summary>
        internal static void RestoreAfterHover(GameObject piece)
        {
            if (!_shown || piece == null || Set.Count == 0)
            {
                return;
            }

            Piece asPiece = piece.GetComponent<Piece>();
            if (asPiece != null && Contains(asPiece))
            {
                Highlight(piece);
            }
        }

        private static void Highlight(GameObject piece)
        {
            if (piece == null || MaterialMan.instance == null)
            {
                return;
            }

            MaterialMan.instance.SetValue(piece, ShaderProps._Color, ModConfig.SelectionTint.Value);
            MaterialMan.instance.SetValue(piece, ShaderProps._EmissionColor, ModConfig.SelectionGlow.Value);
            Highlighted.Add(piece);
        }

        private static void Unhighlight(GameObject piece)
        {
            if (piece != null && MaterialMan.instance != null && Highlighted.Remove(piece))
            {
                MaterialMan.instance.ResetValue(piece, ShaderProps._Color);
                MaterialMan.instance.ResetValue(piece, ShaderProps._EmissionColor);
            }
        }

        private static void UnhighlightAll()
        {
            foreach (GameObject piece in Highlighted)
            {
                if (piece != null && MaterialMan.instance != null)
                {
                    MaterialMan.instance.ResetValue(piece, ShaderProps._Color);
                    MaterialMan.instance.ResetValue(piece, ShaderProps._EmissionColor);
                }
            }

            Highlighted.Clear();
        }

        // ------------------------------------------------------------------ helpers

        internal static int Limit => Mathf.Max(1, ModConfig.SelectionLimit.Value);

        private static string Describe()
        {
            return Set.Count == 1 ? "1 piece selected" : $"{Set.Count} pieces selected";
        }

        /// <summary>The piece's name token; the message display translates it.</summary>
        private static string Name(Piece piece)
        {
            return piece.m_name;
        }

        private static ZDOID IdOf(Piece piece)
        {
            if (piece == null)
            {
                return ZDOID.None;
            }

            ZNetView view = piece.GetComponent<ZNetView>();
            return view != null && view.IsValid() ? view.GetZDO().m_uid : ZDOID.None;
        }

        private static Piece Resolve(ZDOID id)
        {
            if (ZNetScene.instance == null)
            {
                return null;
            }

            GameObject instance = ZNetScene.instance.FindInstance(id);
            return instance != null ? instance.GetComponent<Piece>() : null;
        }

        /// <summary>Forgets everything, for leaving a world.</summary>
        internal static void Reset()
        {
            _searching = false;
            _cancelSearch = false;
            Highlighted.Clear();
            Set.Clear();
            Rings.Clear();
            _shown = false;
        }
    }
}
