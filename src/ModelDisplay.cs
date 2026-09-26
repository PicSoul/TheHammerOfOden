using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheHammerOfOden
{
    /// <summary>
    /// A miniature of a building, standing on a table or a floor: a picture of it, not a copy.
    /// </summary>
    /// <remarks>
    /// A miniature made of real pieces - a copy shrunk down - would be every piece over again:
    /// each one saved, sent to every player, checked for support and carrying its own chest,
    /// door or fire, all for something you only look at. So a model is drawn instead. The list
    /// of its pieces is saved on the one piece it stands on, the same compact list a
    /// construction site keeps, and every player's game draws it from that list.
    ///
    /// Drawn cheaply:
    ///   - merged by material into a few dozen meshes, however many pieces
    ///   - from each piece's least detailed version, which at this size looks the same
    ///   - leaving out anything too small to see at the model's size - nails, candles
    ///   - only while someone is near it; walk away and the meshes are let go
    ///
    /// It is decoration and nothing else: no collision, nothing inside it works, and it is a
    /// snapshot - the building it was taken of can change without it.
    ///
    /// It belongs to the piece it stands on and moves with it. Taking that piece down takes the
    /// model with it.
    /// </remarks>
    internal sealed class ModelDisplay : MonoBehaviour
    {
        private const string PlanKey = "HoO_model";
        private const string PositionKey = "HoO_modelPos";
        private const string RotationKey = "HoO_modelRot";
        private const string ScaleKey = "HoO_modelScale";

        /// <summary>What the model cost, to give back when it is taken off: 1 for wood, 2 for stone, 4 for resin.</summary>
        private const string CostKey = "HoO_modelCost";

        /// <summary>A model as it stood on its table, for undo to put back or take off again.</summary>
        internal sealed class Saved
        {
            internal ZDOID Host;
            internal byte[] Plan;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float Scale;
            internal int Cost;
        }

        /// <summary>Each piece material, as a model draws it. See Calm.</summary>
        private static readonly Dictionary<Material, Material> Calmed = new Dictionary<Material, Material>();

        private static float _confirmUntil;
        private static ZDOID _confirmHost = ZDOID.None;

        /// <summary>Smaller than this at the model's size, in metres, and a piece is left out.</summary>
        private const float SmallestShown = 0.004f;

        private ZNetView _view;
        private List<CopyOrder> _plan;
        private Vector3 _position;
        private Quaternion _rotation;
        private float _scale;
        private uint _revision;
        private int _planLength;
        private float _nextLook;
        private GameObject _drawn;
        private readonly List<Mesh> _meshes = new List<Mesh>();

        // ------------------------------------------------------------------ on a piece

        /// <summary>Takes up the model a piece carries, as it comes into the world.</summary>
        internal static ModelDisplay Attach(ZNetView view)
        {
            if (view == null || !view.IsValid() || HammerOfOdenPlugin.IsHeadless)
            {
                return null;
            }

            byte[] plan = view.GetZDO().GetByteArray(PlanKey);
            if (plan == null || plan.Length == 0)
            {
                return null;
            }

            ModelDisplay display = view.GetComponent<ModelDisplay>() ?? view.gameObject.AddComponent<ModelDisplay>();
            display._view = view;
            display.Read();
            return display;
        }

        internal static bool Carries(Piece piece)
        {
            ZNetView view = piece != null ? piece.GetComponent<ZNetView>() : null;
            byte[] plan = view != null && view.IsValid() ? view.GetZDO().GetByteArray(PlanKey) : null;
            return plan != null && plan.Length > 0;
        }

        /// <summary>Puts a model on a piece: written to its record, so it is saved and everyone sees it.</summary>
        internal static void Place(Piece host, List<CopyOrder> plan, Vector3 worldPosition, Quaternion worldRotation, float scale, int cost)
        {
            ZNetView view = host.GetComponent<ZNetView>();
            if (view == null || !view.IsValid())
            {
                return;
            }

            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            Transform t = host.transform;
            ZDO zdo = view.GetZDO();
            zdo.Set(PositionKey, t.InverseTransformPoint(worldPosition));
            zdo.Set(RotationKey, Quaternion.Inverse(t.rotation) * worldRotation);
            zdo.Set(ScaleKey, scale);
            zdo.Set(CostKey, cost);
            zdo.Set(PlanKey, ConstructionSite.Pack(plan));

            Attach(view);
        }

        /// <summary>The model a piece carries, as it stands - for undo.</summary>
        internal static Saved Take(Piece host)
        {
            ZNetView view = host != null ? host.GetComponent<ZNetView>() : null;
            if (!Carries(host) || view == null)
            {
                return null;
            }

            ZDO zdo = view.GetZDO();
            return new Saved
            {
                Host = zdo.m_uid,
                Plan = zdo.GetByteArray(PlanKey),
                Position = zdo.GetVec3(PositionKey, Vector3.zero),
                Rotation = zdo.GetQuaternion(RotationKey, Quaternion.identity),
                Scale = zdo.GetFloat(ScaleKey, 0.05f),
                Cost = zdo.GetInt(CostKey, 0)
            };
        }

        /// <summary>
        /// Puts a model back on its table, paid for again. Returns 0 if its table is gone, already
        /// carries another, or it cannot be paid for - for undo, which counts what it did.
        /// </summary>
        internal static int PutBack(Player player, Saved saved)
        {
            Piece host = HostOf(saved);
            if (host == null || Carries(host) || !PrivateArea.CheckAccess(host.transform.position))
            {
                return 0;
            }

            if (!GroupHold.ChargeModel(player, saved.Cost))
            {
                return 0;
            }

            ZNetView view = host.GetComponent<ZNetView>();
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            ZDO zdo = view.GetZDO();
            zdo.Set(PositionKey, saved.Position);
            zdo.Set(RotationKey, saved.Rotation);
            zdo.Set(ScaleKey, saved.Scale);
            zdo.Set(CostKey, saved.Cost);
            zdo.Set(PlanKey, saved.Plan);
            Attach(view);
            return 1;
        }

        /// <summary>Takes a saved model off its table again, paying it back - for undo and redo.</summary>
        internal static int TakeOff(Player player, Saved saved)
        {
            Piece host = HostOf(saved);
            return host != null && Clear(player, host) ? 1 : 0;
        }

        private static Piece HostOf(Saved saved)
        {
            GameObject instance = saved != null && ZNetScene.instance != null ? ZNetScene.instance.FindInstance(saved.Host) : null;
            return instance != null ? instance.GetComponent<Piece>() : null;
        }

        /// <summary>Takes the model off a piece and gives its cost back. The piece stays.</summary>
        private static bool Clear(Player player, Piece host)
        {
            if (!Carries(host) || !PrivateArea.CheckAccess(host.transform.position))
            {
                return false;
            }

            ZNetView view = host.GetComponent<ZNetView>();
            if (!view.IsOwner())
            {
                view.ClaimOwnership();
            }

            int cost = view.GetZDO().GetInt(CostKey, 0);
            view.GetZDO().Set(PlanKey, new byte[0]);

            ModelDisplay display = host.GetComponent<ModelDisplay>();
            if (display != null)
            {
                Destroy(display);
            }

            GroupHold.RefundModel(player, cost);
            return true;
        }

        /// <summary>
        /// Takes a model off the piece it stands on, paying back what it cost; undo puts it back.
        /// The piece itself stays. Returns false if there was no model there.
        /// </summary>
        internal static bool RemoveFrom(Player player, Piece host)
        {
            if (!Carries(host))
            {
                return false;
            }

            Saved saved = Take(host);
            if (!Clear(player, host))
            {
                return true;
            }

            PlacementUndo.RecordRevertible(() => PutBack(player, saved), () => TakeOff(player, saved), "model");
            Notify.Show(player, "Model taken off - undo puts it back");
            return true;
        }

        /// <summary>
        /// The hammer's remove on a piece carrying a model takes the model off - asked twice, and
        /// only the model: the table or floor stays. Take the model off first to remove those.
        /// </summary>
        /// <returns>True if this took the click.</returns>
        internal static bool ConfirmRemove(Player player, Piece host)
        {
            if (!Carries(host))
            {
                return false;
            }

            ZDOID id = host.GetComponent<ZNetView>().GetZDO().m_uid;
            if (Time.time > _confirmUntil || _confirmHost != id)
            {
                _confirmUntil = Time.time + 4f;
                _confirmHost = id;
                Notify.Show(player, $"Middle-click again within 4 seconds to take the model off. The {host.m_name} "
                    + "under it stays; undo puts the model back.");
                return true;
            }

            _confirmUntil = 0f;
            _confirmHost = ZDOID.None;
            RemoveFrom(player, host);
            return true;
        }

        private void Read()
        {
            ZDO zdo = _view.GetZDO();
            _revision = zdo.DataRevision;
            byte[] packed = zdo.GetByteArray(PlanKey) ?? new byte[0];
            _planLength = packed.Length;
            _plan = ConstructionSite.Unpack(packed);
            _position = zdo.GetVec3(PositionKey, Vector3.zero);
            _rotation = zdo.GetQuaternion(RotationKey, Quaternion.identity);
            _scale = zdo.GetFloat(ScaleKey, 0.05f);
            Forget();
            _nextLook = 0f;
        }

        // ------------------------------------------------------------------ every frame

        private void Update()
        {
            // Kept on its table every frame - the table can be moved - but not a child of it:
            // a child takes on its parent's size, and a table stretched wider would stretch
            // the model with it.
            if (_drawn != null)
            {
                Place();
            }

            if (_view == null || !_view.IsValid() || Time.time < _nextLook)
            {
                return;
            }

            _nextLook = Time.time + 0.5f;

            ZDO zdo = _view.GetZDO();
            if (zdo.DataRevision != _revision)
            {
                byte[] plan = zdo.GetByteArray(PlanKey);
                if (plan == null || plan.Length == 0)
                {
                    Destroy(this);
                    return;
                }

                // Only the model's own values mean a new model; the record changes for other reasons too.
                if (zdo.GetFloat(ScaleKey, 0.05f) != _scale || zdo.GetVec3(PositionKey, Vector3.zero) != _position
                    || plan.Length != _planLength)
                {
                    Read();
                }

                _revision = zdo.DataRevision;
            }

            Camera camera = Utils.GetMainCamera();
            if (camera == null || _plan == null)
            {
                return;
            }

            float distance = Vector3.Distance(camera.transform.position, transform.position);
            float range = ModConfig.ModelDrawDistance.Value;

            if (_drawn == null && distance <= range)
            {
                Draw();
            }
            else if (_drawn != null && distance > range + 20f)
            {
                // Let go of the meshes; they are made again on the way back.
                Forget();
            }
        }

        private void Draw()
        {
            Stopwatch watch = Stopwatch.StartNew();

            _drawn = Build(_plan, _scale, _meshes, out int vertices, out int shown);
            if (_drawn == null)
            {
                return;
            }

            Place();

            HammerOfOdenPlugin.Debug($"Model drawn: {shown} of {_plan.Count} pieces, {vertices} vertices, "
                + $"{_drawn.GetComponentsInChildren<MeshRenderer>().Length} meshes, in {watch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Where the model stands: its spot on the table, found through the table as it is now,
        /// at the model's own size whatever size the table is.
        /// </summary>
        private void Place()
        {
            Transform t = _drawn.transform;
            t.SetPositionAndRotation(transform.TransformPoint(_position), transform.rotation * _rotation);
            t.localScale = Vector3.one * _scale;
        }

        private void Forget()
        {
            foreach (Mesh mesh in _meshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            _meshes.Clear();

            if (_drawn != null)
            {
                Object.Destroy(_drawn);
                _drawn = null;
            }
        }

        private void OnDestroy()
        {
            Forget();
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// Draws a model's pieces as merged meshes, laid out in the model's own units, for a root
        /// that will be scaled by <paramref name="scale"/>.
        /// </summary>
        /// <param name="made">Meshes made here, for the caller to destroy with the model.</param>
        internal static GameObject Build(List<CopyOrder> plan, float scale, List<Mesh> made, out int vertices, out int shown)
        {
            vertices = 0;
            shown = 0;
            if (plan == null || plan.Count == 0)
            {
                return null;
            }

            GameObject root = new GameObject("HoO_Model");
            Dictionary<Material, List<CombineInstance>> byMaterial = new Dictionary<Material, List<CombineInstance>>();
            Dictionary<Material, int> counts = new Dictionary<Material, int>();
            Dictionary<string, int> small = new Dictionary<string, int>();
            Dictionary<string, int> unseen = new Dictionary<string, int>();

            foreach (CopyOrder order in plan)
            {
                PieceLooks.Look look = PieceLooks.For(order, true);
                if (look == null || look.Parts.Count == 0)
                {
                    Tally(unseen, order.Prefab != null ? order.Prefab.name : "unknown");
                    continue;
                }

                // Only the odds and ends are left out when too small to see - never the building
                // itself, or the model has holes in it.
                float size = look.Extent * scale * Mathf.Max(order.Scale.x, Mathf.Max(order.Scale.y, order.Scale.z));
                if (size < SmallestShown && !IsStructure(order.Prefab))
                {
                    Tally(small, order.Prefab.name);
                    continue;
                }

                shown++;
                Matrix4x4 place = Matrix4x4.TRS(order.Position, order.Rotation, order.Scale);

                foreach (PieceLooks.Part part in look.Parts)
                {
                    Matrix4x4 matrix = place * part.Matrix;

                    if (!part.Mesh.isReadable)
                    {
                        Loose(root, part, matrix);
                        vertices += part.Mesh.vertexCount;
                        continue;
                    }

                    int count = Mathf.Min(part.Mesh.subMeshCount, part.Materials.Length);
                    for (int sub = 0; sub < count; sub++)
                    {
                        Material material = part.Materials[sub];
                        if (material == null)
                        {
                            continue;
                        }

                        if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                        {
                            list = new List<CombineInstance>();
                            byMaterial[material] = list;
                            counts[material] = 0;
                        }

                        if (counts[material] + part.Mesh.vertexCount > 500000 && list.Count > 0)
                        {
                            Merge(root, material, list, made);
                            list.Clear();
                            counts[material] = 0;
                        }

                        list.Add(new CombineInstance { mesh = part.Mesh, subMeshIndex = sub, transform = matrix });
                        counts[material] += part.Mesh.vertexCount;
                        vertices += part.Mesh.vertexCount;
                    }
                }
            }

            foreach (KeyValuePair<Material, List<CombineInstance>> group in byMaterial)
            {
                if (group.Value.Count > 0)
                {
                    Merge(root, group.Key, group.Value, made);
                }
            }

            if (small.Count > 0)
            {
                HammerOfOdenPlugin.Debug("Model left out as too small to see: " + Describe(small));
            }

            if (unseen.Count > 0)
            {
                HammerOfOdenPlugin.Debug("Model has nothing to draw for: " + Describe(unseen));
            }

            return root;
        }

        /// <summary>
        /// Part of the building rather than something in it: every piece outside the furniture,
        /// crafting and food menus - walls, floors, roofs, beams, stone, and any category a mod
        /// has added.
        /// </summary>
        private static bool IsStructure(Piece prefab)
        {
            switch (prefab.m_category)
            {
                case Piece.PieceCategory.Misc:
                case Piece.PieceCategory.Crafting:
                case Piece.PieceCategory.Furniture:
                case Piece.PieceCategory.Feasts:
                case Piece.PieceCategory.Food:
                case Piece.PieceCategory.Meads:
                    return false;
                default:
                    return true;
            }
        }

        private static void Tally(Dictionary<string, int> counts, string name)
        {
            counts.TryGetValue(name, out int n);
            counts[name] = n + 1;
        }

        private static string Describe(Dictionary<string, int> counts)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, int> entry in counts)
            {
                parts.Add($"{entry.Value} x {entry.Key}");
            }

            return string.Join(", ", parts);
        }

        private static void Merge(GameObject root, Material material, List<CombineInstance> list, List<Mesh> made)
        {
            Mesh mesh = new Mesh { name = "HoO_Model", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(list.ToArray(), true, true);
            made?.Add(mesh);

            GameObject part = new GameObject("merged");
            part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Calm(material);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static void Loose(GameObject root, PieceLooks.Part part, Matrix4x4 matrix)
        {
            GameObject loose = new GameObject("sealed");
            Transform t = loose.transform;
            t.SetParent(root.transform, false);
            t.localPosition = matrix.GetColumn(3);
            t.localRotation = matrix.rotation;
            t.localScale = matrix.lossyScale;

            loose.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            MeshRenderer renderer = loose.AddComponent<MeshRenderer>();
            Material[] materials = new Material[part.Materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = Calm(part.Materials[i]);
            }

            renderer.sharedMaterials = materials;
        }

        /// <summary>
        /// A piece material with the game's building wobble and world-placed patterning turned
        /// off, and its texture laid on the piece rather than on the world.
        /// </summary>
        /// <remarks>
        /// The building shader bends every piece's surface a few centimetres with a ripple taken
        /// from its place in the world, and tints it with noise the same way, so no two walls
        /// look machine-made. At full size that is character. On a model one twentieth the size
        /// the same few centimetres are a large share of each tiny wall, and the model looks
        /// melted. Its textures are also laid out by world position, sized for full-size walls.
        ///
        /// The game has the same problem with its own placement ghost and answers it by copying
        /// each material and setting exactly these: _RippleDistance, the wobble, to nothing;
        /// _ValueNoise, the tint, to nothing; _TriplanarLocalPos on, so the texture follows the
        /// piece. The model does the same. One copy per material, shared by every model.
        /// </remarks>
        private static Material Calm(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (Calmed.TryGetValue(source, out Material calm) && calm != null)
            {
                return calm;
            }

            calm = new Material(source) { name = source.name + " (model)" };
            if (calm.HasProperty("_RippleDistance"))
            {
                calm.SetFloat("_RippleDistance", 0f);
            }

            if (calm.HasProperty("_ValueNoise"))
            {
                calm.SetFloat("_ValueNoise", 0f);
            }

            if (calm.HasProperty("_TriplanarLocalPos"))
            {
                calm.SetFloat("_TriplanarLocalPos", 1f);
            }

            Calmed[source] = calm;
            return calm;
        }

        /// <summary>Lets go of the model materials, for leaving a world.</summary>
        internal static void ClearMaterials()
        {
            foreach (Material calm in Calmed.Values)
            {
                if (calm != null)
                {
                    Object.Destroy(calm);
                }
            }

            Calmed.Clear();
        }

        // ------------------------------------------------------------------ where one can stand

        /// <summary>
        /// Whether a model may stand on this piece, aimed at this way: a table, or a floor, from
        /// above. Tables are known by the comfort group the game gives them, floors by their name -
        /// the conventions modded pieces follow too.
        /// </summary>
        internal static bool CanStandOn(Piece piece, Vector3 surfaceNormal)
        {
            if (piece == null || surfaceNormal.y < 0.7f)
            {
                return false;
            }

            if (piece.m_comfortGroup == Piece.ComfortGroup.Table)
            {
                return true;
            }

            string name = Utils.GetPrefabName(piece.gameObject).ToLowerInvariant();
            return name.Contains("floor") || name.Contains("table");
        }
    }
}
