using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheHammerOfOden
{
    /// <summary>
    /// A copy still going up: the plan of every piece in it, which are built, and the ghost of the
    /// rest. It lives on the first piece of the copy, the one the game built, and is saved with
    /// the world.
    /// </summary>
    /// <remarks>
    /// A copy you cannot pay for in full is not refused. What your materials cover goes up, the
    /// rest waits as a ghost, and it carries on by itself as materials turn up - on you, or in the
    /// chests around you with a mod such as AzuCraftyBoxes. See GroupBuilder for the building.
    ///
    /// Why it is saved on a piece. A half-built copy that existed only in memory would be lost on
    /// logging out, and the part already standing would then fall, since what keeps it up is the
    /// promise that the rest is coming. So the plan is written onto the first piece's saved
    /// record: it survives the zone unloading and the world restarting, and it reaches every
    /// player near it - which is what lets them see the ghost too.
    ///
    /// Why a site has its own id. The game renumbers its objects every time a world loads, so a
    /// piece cannot point at its site by the game's own id. A site makes up a random one, and
    /// every piece it builds carries it. A piece carrying the id of a site that is still going up
    /// is excused from the support rule - which is how the standing part stays up while it waits.
    /// When the site finishes, or is stopped, the id is no longer live and the rule applies again.
    ///
    /// Only the player who placed it builds it, and only while near it. Other players see the
    /// ghost; they do not spend their materials on someone else's building.
    ///
    /// What is saved as built is written every few seconds rather than per piece: the whole
    /// record is sent to every player nearby each time it changes, and a large plan is a large
    /// record. If the game stops between two saves, the pieces built in between are found again
    /// on the way back in - each one carries its site and its place in the plan.
    /// </remarks>
    internal sealed class ConstructionSite : MonoBehaviour
    {
        internal const string IdKey = "HoO_site";
        internal const string IndexKey = "HoO_siteIndex";
        private const string PlanKey = "HoO_sitePlan";
        private const string DoneKey = "HoO_siteDone";
        private const string OwnerKey = "HoO_siteOwner";

        private const int PlanVersion = 1;
        private const int ChunkSize = 256;
        private const float SaveEvery = 8f;

        /// <summary>Sites going up whose first piece is loaded here, by site id.</summary>
        private static readonly Dictionary<long, ConstructionSite> Live = new Dictionary<long, ConstructionSite>();

        private static readonly AccessTools.FieldRef<WearNTear, ZNetView> WearView =
            AccessTools.FieldRefAccess<WearNTear, ZNetView>("m_nview");

        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

        private static Material _ghostMaterial;

        internal long Id { get; private set; }
        internal long Owner { get; private set; }
        internal List<CopyOrder> Plan { get; private set; }
        internal bool[] Done { get; private set; }
        internal int BuiltCount { get; private set; }
        internal ZNetView View { get; private set; }

        /// <summary>The undo step the pieces go into, while the player who placed it is still on.</summary>
        internal object UndoAction;

        /// <summary>
        /// Being taken down by undo: nothing more is built, but it stays live - and so keeps
        /// what is still standing up - until the last piece is gone.
        /// </summary>
        internal bool Halted;

        private uint _revision;
        private byte[] _doneBytes = new byte[0];
        private bool _doneDirty;
        private float _nextSave;
        private bool _reconciled;

        private GameObject _ghostRoot;
        private readonly List<GameObject> _ghostChunks = new List<GameObject>();
        private readonly List<Mesh> _ghostMeshes = new List<Mesh>();
        private readonly HashSet<int> _dirtyChunks = new HashSet<int>();

        internal static IEnumerable<ConstructionSite> All => Live.Values;

        /// <summary>
        /// Every piece built. A server never reads the plan - it has no use for it - so there a
        /// site is simply live until its first piece's record says otherwise.
        /// </summary>
        internal bool IsFinished => Plan != null && BuiltCount >= Plan.Count;

        // ------------------------------------------------------------------ starting one

        /// <summary>Writes a new site onto the first piece of a copy, the one the game built.</summary>
        internal static ConstructionSite Create(Piece first, List<CopyOrder> plan, Player owner, object undoAction)
        {
            ZNetView view = first != null ? first.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid() || plan == null || plan.Count == 0 || owner == null)
            {
                return null;
            }

            long id = NewId();
            ZDO zdo = view.GetZDO();
            zdo.Set(IdKey, id);
            zdo.Set(OwnerKey, owner.GetPlayerID());
            zdo.Set(PlanKey, Pack(plan));
            zdo.Set(DoneKey, new byte[(plan.Count + 7) / 8]);

            ConstructionSite site = Attach(view);
            if (site != null)
            {
                site.UndoAction = undoAction;
            }

            return site;
        }

        private static long NewId()
        {
            long id;
            do
            {
                id = ((long)Random.Range(int.MinValue, int.MaxValue) << 32) | (uint)Random.Range(int.MinValue, int.MaxValue);
            }
            while (id == 0 || Live.ContainsKey(id));

            return id;
        }

        /// <summary>
        /// Called as any object comes into the world: takes up the site it carries, if any.
        /// </summary>
        internal static ConstructionSite Attach(ZNetView view)
        {
            if (view == null || !view.IsValid())
            {
                return null;
            }

            ZDO zdo = view.GetZDO();
            byte[] plan = zdo.GetByteArray(PlanKey);
            if (plan == null || plan.Length == 0)
            {
                return null;
            }

            ConstructionSite site = view.GetComponent<ConstructionSite>() ?? view.gameObject.AddComponent<ConstructionSite>();
            site.View = view;
            site.Id = zdo.GetLong(IdKey, 0L);
            site.Owner = zdo.GetLong(OwnerKey, 0L);

            if (site.Id == 0L)
            {
                Destroy(site);
                return null;
            }

            // A server only needs to know the site is live, for the support rule; the plan is
            // for building and drawing, neither of which happens there.
            if (!HammerOfOdenPlugin.IsHeadless)
            {
                site.Plan = Unpack(plan);
                if (site.Plan == null)
                {
                    HammerOfOdenPlugin.Error("A construction site's plan could not be read; the site is ignored.");
                    Destroy(site);
                    return null;
                }

                site.ReadDone();
                site.MarkAllChunksDirty();
            }

            site._revision = zdo.DataRevision;
            Live[site.Id] = site;
            GroupBuilder.Poke();
            return site;
        }

        // ------------------------------------------------------------------ the support rule

        /// <summary>Whether this record carries a site's plan - checked as updates arrive from other players.</summary>
        internal static bool HasPlan(ZDO zdo)
        {
            byte[] plan = zdo?.GetByteArray(PlanKey);
            return plan != null && plan.Length > 0;
        }

        /// <summary>Whether this piece belongs to a site still going up here.</summary>
        internal static bool IsExempt(WearNTear wear)
        {
            if (Live.Count == 0 || wear == null)
            {
                return false;
            }

            ZNetView view = WearView(wear);
            if (view == null || !view.IsValid())
            {
                return false;
            }

            long id = view.GetZDO().GetLong(IdKey, 0L);
            return id != 0L && Live.TryGetValue(id, out ConstructionSite site) && site != null && !site.IsFinished;
        }

        // ------------------------------------------------------------------ building progress

        /// <summary>Marks a piece of the plan built, and tags the piece with the site.</summary>
        internal void MarkBuilt(int index, ZNetView built)
        {
            if (Done == null || index < 0 || index >= Done.Length || Done[index])
            {
                return;
            }

            Done[index] = true;
            BuiltCount++;
            _doneDirty = true;
            _dirtyChunks.Add(index / ChunkSize);

            if (built != null && built.IsValid())
            {
                built.GetZDO().Set(IdKey, Id);
                built.GetZDO().Set(IndexKey, index);
            }
        }

        /// <summary>
        /// Finds pieces this site built that were not yet saved as built - the game stopped
        /// between two saves. Each carries its site and its place in the plan.
        /// </summary>
        internal void Reconcile()
        {
            if (_reconciled || Done == null || ZNetScene.instance == null)
            {
                return;
            }

            _reconciled = true;
            int found = 0;

            foreach (KeyValuePair<ZDO, ZNetView> pair in Instances(ZNetScene.instance))
            {
                ZDO zdo = pair.Key;
                if (zdo == null || zdo.GetLong(IdKey, 0L) != Id || pair.Value == View)
                {
                    continue;
                }

                int index = zdo.GetInt(IndexKey, -1);
                if (index >= 0 && index < Done.Length && !Done[index])
                {
                    Done[index] = true;
                    BuiltCount++;
                    _dirtyChunks.Add(index / ChunkSize);
                    found++;
                }
            }

            if (found > 0)
            {
                _doneDirty = true;
                HammerOfOdenPlugin.Debug($"Construction site found {found} built pieces that were not yet saved as built.");
            }
        }

        /// <summary>The whole site is up: it stops being a site, and the support rule applies again.</summary>
        internal void Finish()
        {
            if (View != null && View.IsValid())
            {
                if (!View.IsOwner())
                {
                    View.ClaimOwnership();
                }

                View.GetZDO().Set(PlanKey, new byte[0]);
                View.GetZDO().Set(DoneKey, new byte[0]);
            }

            Retire();
        }

        /// <summary>
        /// Stops the site where it stands: nothing more is built and the ghost goes. What is
        /// standing stays, and the support rule applies to it again.
        /// </summary>
        internal void Stop()
        {
            Finish();
        }

        private void Retire()
        {
            _doneDirty = false;
            SitePanel.Forget(Id);
            if (Live.TryGetValue(Id, out ConstructionSite live) && live == this)
            {
                Live.Remove(Id);
            }

            DestroyGhost();
            Plan = null;
            Done = null;
            Destroy(this);
        }

        /// <summary>
        /// Takes up the saved progress, redrawing only the slices of the ghost whose pieces
        /// changed. Returns false if nothing had.
        /// </summary>
        private bool ReadDone()
        {
            byte[] bits = View.GetZDO().GetByteArray(DoneKey) ?? new byte[0];
            if (Done != null && Same(bits, _doneBytes))
            {
                return false;
            }

            bool first = Done == null;
            bool[] was = Done;
            Done = new bool[Plan.Count];
            BuiltCount = 0;

            for (int i = 0; i < Done.Length; i++)
            {
                int b = i >> 3;
                if (b < bits.Length && (bits[b] & (1 << (i & 7))) != 0)
                {
                    Done[i] = true;
                    BuiltCount++;
                }

                if (!first && was[i] != Done[i])
                {
                    _dirtyChunks.Add(i / ChunkSize);
                }
            }

            _doneBytes = bits;
            return true;
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void SaveDone()
        {
            if (View == null || !View.IsValid() || Done == null)
            {
                return;
            }

            byte[] bits = new byte[(Done.Length + 7) / 8];
            for (int i = 0; i < Done.Length; i++)
            {
                if (Done[i])
                {
                    bits[i >> 3] |= (byte)(1 << (i & 7));
                }
            }

            if (!View.IsOwner())
            {
                View.ClaimOwnership();
            }

            View.GetZDO().Set(DoneKey, bits);
            _doneBytes = bits;
            _revision = View.GetZDO().DataRevision;
            _doneDirty = false;
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            if (View == null || !View.IsValid())
            {
                return;
            }

            ZDO zdo = View.GetZDO();

            // Someone else - the player building it - saved progress, or stopped it. The record
            // changes for plenty of other reasons too, its health and support among them, so
            // only the site's own values are compared.
            if (zdo.DataRevision != _revision && !_doneDirty)
            {
                _revision = zdo.DataRevision;
                byte[] plan = zdo.GetByteArray(PlanKey);
                if (plan == null || plan.Length == 0)
                {
                    Retire();
                    return;
                }

                if (Plan != null)
                {
                    ReadDone();
                }
            }

            if (_doneDirty && Time.time >= _nextSave)
            {
                _nextSave = Time.time + SaveEvery;
                SaveDone();
            }

            // A few chunks a frame, so a large ghost is redrawn without a hitch.
            int redrawn = 0;
            while (_dirtyChunks.Count > 0 && redrawn < 2)
            {
                int chunk = -1;
                foreach (int c in _dirtyChunks)
                {
                    chunk = c;
                    break;
                }

                _dirtyChunks.Remove(chunk);
                DrawChunk(chunk);
                redrawn++;
            }
        }

        private void OnDestroy()
        {
            // Leaving the world, or the zone unloading: keep what was built since the last save,
            // if the record can still take it.
            if (_doneDirty && View != null && View.IsValid() && ZNet.instance != null)
            {
                SaveDone();
            }

            if (Live.TryGetValue(Id, out ConstructionSite live) && live == this)
            {
                Live.Remove(Id);
            }

            DestroyGhost();
        }

        // ------------------------------------------------------------------ the ghost

        private void MarkAllChunksDirty()
        {
            if (Plan == null)
            {
                return;
            }

            for (int c = 0; c * ChunkSize < Plan.Count; c++)
            {
                _dirtyChunks.Add(c);
            }
        }

        /// <summary>
        /// Draws one slice of the plan's unbuilt pieces as a single see-through mesh.
        /// </summary>
        /// <remarks>
        /// In slices so building one piece only redraws the few hundred around it in the plan,
        /// and in one see-through material so a slice is one mesh however many kinds of piece are
        /// in it. The plan is sorted bottom up, so a slice is roughly a layer of the building.
        /// Meshes the game will not let a mod read are drawn one by one instead.
        /// </remarks>
        private void DrawChunk(int chunk)
        {
            if (Plan == null || Done == null || HammerOfOdenPlugin.IsHeadless || !ModConfig.SiteGhosts.Value)
            {
                return;
            }

            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("HoO_SiteGhost");
            }

            while (_ghostChunks.Count <= chunk)
            {
                _ghostChunks.Add(null);
            }

            if (_ghostChunks[chunk] != null)
            {
                foreach (MeshFilter filter in _ghostChunks[chunk].GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh != null && _ghostMeshes.Remove(filter.sharedMesh))
                    {
                        Object.Destroy(filter.sharedMesh);
                    }
                }

                Object.Destroy(_ghostChunks[chunk]);
                _ghostChunks[chunk] = null;
            }

            Material material = GhostMaterial();
            if (material == null)
            {
                return;
            }

            GameObject holder = new GameObject("chunk " + chunk);
            holder.transform.SetParent(_ghostRoot.transform, false);
            _ghostChunks[chunk] = holder;

            List<CombineInstance> merged = new List<CombineInstance>();
            int vertices = 0;

            int end = Mathf.Min(Plan.Count, (chunk + 1) * ChunkSize);
            for (int i = chunk * ChunkSize; i < end; i++)
            {
                if (Done[i] || GroupBuilder.IsInFlight(this, i))
                {
                    continue;
                }

                CopyOrder order = Plan[i];
                PieceLooks.Look look = PieceLooks.For(order);
                if (look == null)
                {
                    continue;
                }

                Matrix4x4 place = Matrix4x4.TRS(order.Position, order.Rotation, order.Scale);
                foreach (PieceLooks.Part part in look.Parts)
                {
                    Matrix4x4 matrix = place * part.Matrix;

                    if (!part.Mesh.isReadable)
                    {
                        Loose(holder, part, matrix, material);
                        continue;
                    }

                    for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                    {
                        if (vertices + part.Mesh.vertexCount > 500000 && merged.Count > 0)
                        {
                            Merge(holder, merged, material);
                            merged.Clear();
                            vertices = 0;
                        }

                        merged.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = sub, transform = matrix });
                        vertices += part.Mesh.vertexCount;
                    }
                }
            }

            if (merged.Count > 0)
            {
                Merge(holder, merged, material);
            }
        }

        private void Merge(GameObject holder, List<CombineInstance> merged, Material material)
        {
            Mesh mesh = new Mesh { name = "HoO_SiteGhost", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(merged.ToArray(), true, true);
            _ghostMeshes.Add(mesh);

            GameObject part = new GameObject("merged");
            part.transform.SetParent(holder.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void Loose(GameObject holder, PieceLooks.Part part, Matrix4x4 matrix, Material material)
        {
            GameObject loose = new GameObject("sealed");
            Transform t = loose.transform;
            t.SetParent(holder.transform, false);
            t.localPosition = matrix.GetColumn(3);
            t.localRotation = matrix.rotation;
            t.localScale = matrix.lossyScale;

            loose.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            MeshRenderer renderer = loose.AddComponent<MeshRenderer>();
            Material[] materials = new Material[Mathf.Max(1, part.Mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void DestroyGhost()
        {
            foreach (Mesh mesh in _ghostMeshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            _ghostMeshes.Clear();
            _ghostChunks.Clear();
            _dirtyChunks.Clear();

            if (_ghostRoot != null)
            {
                Object.Destroy(_ghostRoot);
                _ghostRoot = null;
            }
        }

        /// <summary>Redraws the slice a piece is in - it has just taken off, or failed to land.</summary>
        internal void Redraw(int index)
        {
            _dirtyChunks.Add(index / ChunkSize);
        }

        private static Material GhostMaterial()
        {
            if (_ghostMaterial == null)
            {
                Material source = GizmoMaterial.GetSeeThrough();
                if (source == null)
                {
                    return null;
                }

                _ghostMaterial = new Material(source) { renderQueue = 3000 };
                if (_ghostMaterial.HasProperty("_ZWrite"))
                {
                    _ghostMaterial.SetInt("_ZWrite", 0);
                }
            }

            _ghostMaterial.color = ModConfig.SiteGhostColor.Value;
            return _ghostMaterial;
        }

        // ------------------------------------------------------------------ the saved plan

        /// <summary>The plan, compressed: kind, place, turn, and size, bend and sign text where there are any.</summary>
        internal static byte[] Pack(List<CopyOrder> plan)
        {
            ZPackage package = new ZPackage();
            package.Write(PlanVersion);
            package.Write(plan.Count);

            foreach (CopyOrder order in plan)
            {
                package.Write(order.Prefab.gameObject.name.GetStableHashCode());
                package.Write(order.Position);
                package.Write(order.Rotation);

                bool scaled = (order.Scale - order.Prefab.transform.localScale).sqrMagnitude > 0.000001f;
                bool bent = Mathf.Abs(order.BendDegrees) >= 0.01f;
                bool sign = !string.IsNullOrEmpty(order.SignText);

                package.Write((byte)((scaled ? 1 : 0) | (bent ? 2 : 0) | (sign ? 4 : 0)));

                if (scaled)
                {
                    package.Write(order.Scale);
                }

                if (bent)
                {
                    package.Write(order.BendDegrees);
                    package.Write(order.BendAxis);
                    package.Write(order.BendRise);
                    package.Write(order.BendChoice);
                }

                if (sign)
                {
                    package.Write(order.SignText);
                    package.Write(order.SignAuthor ?? string.Empty);
                    package.Write(order.SignAuthorName ?? string.Empty);
                }
            }

            byte[] raw = package.GetArray();
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream zip = new GZipStream(output, CompressionMode.Compress))
                {
                    zip.Write(raw, 0, raw.Length);
                }

                return output.ToArray();
            }
        }

        internal static List<CopyOrder> Unpack(byte[] packed)
        {
            try
            {
                byte[] raw;
                using (MemoryStream input = new MemoryStream(packed))
                using (GZipStream zip = new GZipStream(input, CompressionMode.Decompress))
                using (MemoryStream output = new MemoryStream())
                {
                    zip.CopyTo(output);
                    raw = output.ToArray();
                }

                ZPackage package = new ZPackage(raw);
                if (package.ReadInt() != PlanVersion)
                {
                    return null;
                }

                int count = package.ReadInt();
                List<CopyOrder> plan = new List<CopyOrder>(count);

                for (int i = 0; i < count; i++)
                {
                    int hash = package.ReadInt();
                    GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
                    Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;

                    CopyOrder order = new CopyOrder
                    {
                        Prefab = piece,
                        Position = package.ReadVector3(),
                        Rotation = package.ReadQuaternion()
                    };

                    byte flags = package.ReadByte();
                    order.Scale = (flags & 1) != 0 ? package.ReadVector3()
                        : piece != null ? piece.transform.localScale : Vector3.one;

                    if ((flags & 2) != 0)
                    {
                        order.BendDegrees = package.ReadSingle();
                        order.BendAxis = package.ReadInt();
                        order.BendRise = package.ReadInt();
                        order.BendChoice = package.ReadInt();
                    }

                    if ((flags & 4) != 0)
                    {
                        order.SignText = package.ReadString();
                        order.SignAuthor = NullIfEmpty(package.ReadString());
                        order.SignAuthorName = NullIfEmpty(package.ReadString());
                    }

                    // A kind of piece this game does not have - a mod since removed - stays in the
                    // plan so the indices still line up, and is simply never built.
                    plan.Add(order);
                }

                return plan;
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error("Reading a construction site's plan failed: " + ex.Message);
                return null;
            }
        }

        private static string NullIfEmpty(string text)
        {
            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>The id of the copy a piece was built by, or zero - still going up or long finished.</summary>
        internal static long IdOf(Piece piece)
        {
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            return view != null && view.IsValid() ? view.GetZDO().GetLong(IdKey, 0L) : 0L;
        }

        /// <summary>
        /// Every piece standing here that the copy with this id built - and nothing else. Found by
        /// the id every piece of a copy carries, which, unlike the game's own ids, survives the
        /// world loading again.
        /// </summary>
        internal static List<GameObject> PiecesOf(long id)
        {
            List<GameObject> pieces = new List<GameObject>();
            if (id == 0L || ZNetScene.instance == null)
            {
                return pieces;
            }

            foreach (KeyValuePair<ZDO, ZNetView> pair in Instances(ZNetScene.instance))
            {
                if (pair.Key != null && pair.Value != null && pair.Key.GetLong(IdKey, 0L) == id
                    && pair.Value.GetComponent<Piece>() != null)
                {
                    pieces.Add(pair.Value.gameObject);
                }
            }

            return pieces;
        }

        /// <summary>The site this piece belongs to, if it is one going up here.</summary>
        internal static ConstructionSite Of(Piece piece)
        {
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
            {
                return null;
            }

            long id = view.GetZDO().GetLong(IdKey, 0L);
            return id != 0L && Live.TryGetValue(id, out ConstructionSite site) ? site : null;
        }

        /// <summary>Forgets everything, for leaving a world.</summary>
        internal static void Reset()
        {
            Live.Clear();
        }
    }
}
