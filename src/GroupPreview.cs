using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheHammerOfOden
{
    /// <summary>
    /// What a held group will look like where it is about to go: every piece in it but the one
    /// you are holding it by, which the game's own ghost already shows.
    /// </summary>
    /// <remarks>
    /// Drawn from merged meshes, not from a copy per piece. A group can be thousands of pieces,
    /// and a copy of each - which is what Infinity Hammer holds - is thousands of extra objects
    /// the game has to carry and draw every frame. Merging the meshes that share a material into
    /// one turns that into a few dozen draw calls, however large the group.
    ///
    /// A mesh can only be merged if the game lets a mod read it, and a few are shipped sealed.
    /// Those are drawn as they are, one renderer each, which is as many as there are sealed
    /// meshes in the group and no more.
    ///
    /// One level of detail per piece. A piece with an LOD group carries a renderer for each level
    /// and lets the game pick; drawing them all would lay every level over the others. A small
    /// group shows the full detail, a large one the next level down, which is what those pieces
    /// look like from the distance a large group is seen from anyway.
    ///
    /// Everything is baked relative to the piece the group is held by, as it stood, at its own
    /// size of one. The preview's root then follows the ghost: its position and turn, and a single
    /// uniform scale for the whole group - which scales each piece and each piece's distance from
    /// the one held together, so nothing overlaps when it shrinks and nothing parts when it grows.
    /// </remarks>
    internal sealed class GroupPreview
    {
        /// <summary>A merged mesh is split well before a single one would strain anything.</summary>
        private const int VerticesPerMesh = 500000;

        private GameObject _root;
        private readonly List<Mesh> _made = new List<Mesh>();

        internal int Merged { get; private set; }
        internal int Loose { get; private set; }
        internal long BuildMilliseconds { get; private set; }

        /// <summary>
        /// A preview drawn from a list of pieces rather than pieces standing in the world - a
        /// blueprint's. Laid out in the held piece's frame, like the one built from the world.
        /// </summary>
        internal static GroupPreview FromPlan(List<CopyOrder> plan)
        {
            GroupPreview preview = new GroupPreview();
            Stopwatch watch = Stopwatch.StartNew();
            preview._root = ModelDisplay.Build(plan, 1f, preview._made, out _, out int shown) ?? new GameObject("HoO_GroupPreview");
            preview.Merged = preview._made.Count;
            preview.BuildMilliseconds = watch.ElapsedMilliseconds;
            return preview;
        }

        internal static GroupPreview Build(Vector3 framePosition, Quaternion frameRotation, List<GameObject> pieces)
        {
            GroupPreview preview = new GroupPreview();
            preview.Construct(framePosition, frameRotation, pieces);
            return preview;
        }

        private void Construct(Vector3 framePosition, Quaternion frameRotation, List<GameObject> pieces)
        {
            Stopwatch watch = Stopwatch.StartNew();

            Matrix4x4 toFrame = Matrix4x4.TRS(framePosition, frameRotation, Vector3.one).inverse;
            bool lowDetail = pieces.Count > 400;

            Dictionary<Material, List<CombineInstance>> byMaterial = new Dictionary<Material, List<CombineInstance>>();
            List<MeshRenderer> sealedRenderers = new List<MeshRenderer>();

            foreach (GameObject piece in pieces)
            {
                if (piece == null)
                {
                    continue;
                }

                foreach (MeshRenderer renderer in Chosen(piece, lowDetail))
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    Mesh mesh = filter != null ? filter.sharedMesh : null;
                    if (mesh == null)
                    {
                        continue;
                    }

                    if (!mesh.isReadable)
                    {
                        sealedRenderers.Add(renderer);
                        continue;
                    }

                    Matrix4x4 matrix = toFrame * renderer.transform.localToWorldMatrix;
                    Material[] materials = renderer.sharedMaterials;
                    int count = Mathf.Min(mesh.subMeshCount, materials.Length);

                    for (int sub = 0; sub < count; sub++)
                    {
                        Material material = materials[sub];
                        if (material == null)
                        {
                            continue;
                        }

                        if (!byMaterial.TryGetValue(material, out List<CombineInstance> list))
                        {
                            list = new List<CombineInstance>();
                            byMaterial[material] = list;
                        }

                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                    }
                }
            }

            _root = new GameObject("HoO_GroupPreview");

            foreach (KeyValuePair<Material, List<CombineInstance>> group in byMaterial)
            {
                List<CombineInstance> chunk = new List<CombineInstance>();
                int vertices = 0;

                foreach (CombineInstance instance in group.Value)
                {
                    if (chunk.Count > 0 && vertices + instance.mesh.vertexCount > VerticesPerMesh)
                    {
                        AddMerged(group.Key, chunk);
                        chunk.Clear();
                        vertices = 0;
                    }

                    chunk.Add(instance);
                    vertices += instance.mesh.vertexCount;
                }

                if (chunk.Count > 0)
                {
                    AddMerged(group.Key, chunk);
                }
            }

            foreach (MeshRenderer renderer in sealedRenderers)
            {
                AddLoose(renderer, toFrame);
            }

            BuildMilliseconds = watch.ElapsedMilliseconds;
        }

        private void AddMerged(Material material, List<CombineInstance> chunk)
        {
            Mesh mesh = new Mesh { name = "HoO_GroupPreview", indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(chunk.ToArray(), true, true);
            _made.Add(mesh);

            GameObject part = new GameObject("merged");
            part.transform.SetParent(_root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Merged++;
        }

        private void AddLoose(MeshRenderer source, Matrix4x4 toFrame)
        {
            Matrix4x4 matrix = toFrame * source.transform.localToWorldMatrix;

            GameObject part = new GameObject("sealed");
            Transform t = part.transform;
            t.SetParent(_root.transform, false);
            t.localPosition = matrix.GetColumn(3);
            t.localRotation = matrix.rotation;
            t.localScale = matrix.lossyScale;

            part.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;

            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Loose++;
        }

        /// <summary>The renderers that make up one level of detail, drawn now.</summary>
        /// <remarks>
        /// A worn piece has an LOD group inside each of its new, worn and broken versions, and
        /// only the one in the version showing is switched on - so every switched-on group gives
        /// its level, and renderers in no group are taken as they are.
        /// </remarks>
        internal static IEnumerable<MeshRenderer> Chosen(GameObject piece, bool lowDetail)
        {
            HashSet<Renderer> grouped = new HashSet<Renderer>();
            List<MeshRenderer> chosen = new List<MeshRenderer>();

            foreach (LODGroup lod in piece.GetComponentsInChildren<LODGroup>(false))
            {
                LOD[] levels = lod.GetLODs();
                foreach (LOD level in levels)
                {
                    foreach (Renderer renderer in level.renderers)
                    {
                        if (renderer != null)
                        {
                            grouped.Add(renderer);
                        }
                    }
                }

                if (levels.Length == 0)
                {
                    continue;
                }

                int pick = lowDetail && levels.Length > 1 ? 1 : 0;
                foreach (Renderer renderer in levels[pick].renderers)
                {
                    if (renderer is MeshRenderer mesh && Drawn(mesh))
                    {
                        chosen.Add(mesh);
                    }
                }
            }

            foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!grouped.Contains(renderer) && Drawn(renderer))
                {
                    chosen.Add(renderer);
                }
            }

            return chosen;
        }

        /// <summary>Visible on the piece now: not switched off, not part of a hidden wear state.</summary>
        private static bool Drawn(MeshRenderer renderer)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        /// <summary>Follows the ghost, with the group's single uniform scale.</summary>
        internal void Place(Vector3 position, Quaternion rotation, float scale, bool visible)
        {
            if (_root == null)
            {
                return;
            }

            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
            }

            _root.transform.SetPositionAndRotation(position, rotation);
            _root.transform.localScale = Vector3.one * scale;
        }

        internal void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            // Meshes made here are not collected with the objects that drew them.
            foreach (Mesh mesh in _made)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            _made.Clear();
        }
    }
}
