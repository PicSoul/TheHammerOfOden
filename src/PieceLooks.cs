using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// What a kind of piece looks like, read once from the game's own prefab: the meshes that are
    /// drawn, their materials, and where each sits relative to the piece.
    /// </summary>
    /// <remarks>
    /// Read from the prefab rather than from a piece standing in the world, so it is there for
    /// any kind of piece - including kinds nobody near you has built, which is what a
    /// construction site's ghost of an unbuilt building needs, on every player's screen.
    ///
    /// A prefab is not in the scene, so "is this part drawn" cannot be asked of the scene: every
    /// part of it counts as switched off there. It is asked of the prefab itself instead - the
    /// renderer enabled and each object from it up to the piece switched on.
    ///
    /// Except for a piece's wear. A piece with health carries three versions of itself - new,
    /// worn and broken - plus a wet sheen, and the game switches on the right one as the piece
    /// spawns. In the prefab they may all be off, or the wrong one on, so trusting the prefab
    /// left walls and floors with nothing to draw, or drew them broken. The new version is
    /// always taken, and the other three never.
    /// </remarks>
    internal static class PieceLooks
    {
        internal struct Part
        {
            internal Mesh Mesh;
            internal Material[] Materials;
            internal Matrix4x4 Matrix;
        }

        internal sealed class Look
        {
            internal readonly List<Part> Parts = new List<Part>();

            /// <summary>Half the piece's size at its normal scale, its longest way, in metres.</summary>
            internal float Extent;
        }

        private static readonly Dictionary<GameObject, Look> Cache = new Dictionary<GameObject, Look>();
        private static readonly Dictionary<GameObject, Look> LowCache = new Dictionary<GameObject, Look>();
        private static readonly Dictionary<(GameObject, int, int, int, bool), Look> BentCache =
            new Dictionary<(GameObject, int, int, int, bool), Look>();
        private static readonly List<Mesh> BentMeshes = new List<Mesh>();

        /// <summary>
        /// A piece's look for an order: bent the way the order is bent, or plain.
        /// </summary>
        internal static Look For(CopyOrder order, bool lowest = false)
        {
            if (order?.Prefab == null)
            {
                return null;
            }

            return Mathf.Abs(order.BendDegrees) < 0.01f
                ? Of(order.Prefab.gameObject, lowest)
                : Bent(order.Prefab.gameObject, order.BendDegrees, order.BendAxis, order.BendRise, lowest);
        }

        /// <summary>
        /// A piece's look with its bend: the same arc, worked out the same way, as a bent piece
        /// standing in the world - so a model, a preview or a ghost shows the curve the built
        /// piece will have.
        /// </summary>
        /// <remarks>
        /// Bent copies of the meshes are made once per kind of piece and bend, to a tenth of a
        /// degree, and shared by every piece drawn that way. Meshes the game will not let a mod
        /// read cannot be curved, and are left out, as the bent piece itself leaves them out.
        /// </remarks>
        internal static Look Bent(GameObject prefab, float degrees, int axis, int rise, bool lowest)
        {
            Look plain = Of(prefab, lowest);
            if (plain == null || plain.Parts.Count == 0 || axis < 0 || axis > 2 || rise < 0 || rise > 2 || axis == rise)
            {
                return plain;
            }

            var key = (prefab, Mathf.RoundToInt(degrees * 10f), axis, rise, lowest);
            if (BentCache.TryGetValue(key, out Look cached))
            {
                return cached;
            }

            // The arc spans the whole piece, as the bent piece measures it: every mesh in it.
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Matrix4x4 toPiece = prefab.transform.worldToLocalMatrix;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                {
                    continue;
                }

                Matrix4x4 matrix = toPiece * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 p = matrix.MultiplyPoint3x4(vertex);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            float length = max[axis] - min[axis];
            float radians = degrees * Mathf.Deg2Rad;
            if (length < 0.001f || Mathf.Abs(radians) < 0.0001f)
            {
                BentCache[key] = plain;
                return plain;
            }

            float radius = length / radians;
            float midAlong = (min[axis] + max[axis]) * 0.5f;
            float midRise = (min[rise] + max[rise]) * 0.5f;
            float segment = Mathf.Max(0.02f, ModConfig.BendSegment.Value);

            Look bent = new Look { Extent = plain.Extent };
            foreach (Part part in plain.Parts)
            {
                if (!part.Mesh.isReadable)
                {
                    continue;
                }

                Mesh dense = MeshSubdivider.Subdivide(part.Mesh, part.Matrix, axis, segment) ?? Object.Instantiate(part.Mesh);
                BentMeshes.Add(dense);

                Matrix4x4 back = part.Matrix.inverse;
                Vector3[] vertices = dense.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = part.Matrix.MultiplyPoint3x4(vertices[i]);

                    float angle = (p[axis] - midAlong) / radius;
                    float arm = radius - (p[rise] - midRise);

                    p[axis] = midAlong + arm * Mathf.Sin(angle);
                    p[rise] = midRise + radius - arm * Mathf.Cos(angle);

                    vertices[i] = back.MultiplyPoint3x4(p);
                }

                dense.vertices = vertices;
                dense.RecalculateNormals();
                dense.RecalculateBounds();

                bent.Parts.Add(new Part { Mesh = dense, Materials = part.Materials, Matrix = part.Matrix });
            }

            BentCache[key] = bent;
            return bent;
        }

        /// <param name="lowest">
        /// The least detailed level instead of the most - for a miniature, where nobody can see
        /// the difference and every vertex saved is memory saved.
        /// </param>
        internal static Look Of(GameObject prefab, bool lowest = false)
        {
            if (prefab == null)
            {
                return null;
            }

            Dictionary<GameObject, Look> cache = lowest ? LowCache : Cache;
            if (cache.TryGetValue(prefab, out Look cached))
            {
                return cached;
            }

            Look look = new Look();
            Transform root = prefab.transform;
            Matrix4x4 toPiece = root.worldToLocalMatrix;
            Bounds bounds = default(Bounds);
            bool any = false;

            foreach (MeshRenderer renderer in Drawn(prefab, lowest, prefab.GetComponent<WearNTear>()))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4 matrix = toPiece * renderer.transform.localToWorldMatrix;
                look.Parts.Add(new Part
                {
                    Mesh = filter.sharedMesh,
                    Materials = renderer.sharedMaterials,
                    Matrix = matrix
                });

                Bounds mesh = filter.sharedMesh.bounds;
                foreach (Vector3 corner in new[] { mesh.min, mesh.max })
                {
                    Vector3 at = matrix.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(at, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(at);
                    }
                }
            }

            look.Extent = any ? bounds.extents.magnitude : 0f;

            // Some pieces' least detailed level draws nothing - empty, or switched off in the
            // prefab. Those fall back to the full detail rather than vanishing from a model.
            if (lowest && look.Parts.Count == 0)
            {
                look = Of(prefab, false);
            }
            else if (!lowest && look.Parts.Count == 0)
            {
                Explain(prefab);
            }

            cache[prefab] = look;
            return look;
        }

        /// <summary>
        /// The renderers that make up the piece's new look, at one level of detail.
        /// </summary>
        /// <remarks>
        /// A piece can have several LOD groups, not one: a worn piece keeps one inside each of its
        /// new, worn and broken versions. Taking the first one found - the first try - often took
        /// the worn or broken one's, whose renderers are rightly passed over, and walls, floors and
        /// roofs came out with nothing to draw. So every LOD group is looked at, each one in a part
        /// of the piece that is drawn gives its chosen level, and renderers belonging to no LOD
        /// group at all are taken as they are.
        /// </remarks>
        private static IEnumerable<MeshRenderer> Drawn(GameObject prefab, bool lowest, WearNTear wear)
        {
            HashSet<Renderer> grouped = new HashSet<Renderer>();
            List<MeshRenderer> chosen = new List<MeshRenderer>();

            foreach (LODGroup lod in prefab.GetComponentsInChildren<LODGroup>(true))
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

                if (levels.Length == 0 || !Shown(lod.transform, prefab.transform, wear))
                {
                    continue;
                }

                // The least detailed level that still draws something, or the most detailed.
                int pick = 0;
                if (lowest)
                {
                    for (int i = levels.Length - 1; i >= 0; i--)
                    {
                        if (levels[i].renderers != null && levels[i].renderers.Length > 0)
                        {
                            pick = i;
                            break;
                        }
                    }
                }

                foreach (Renderer renderer in levels[pick].renderers)
                {
                    if (renderer is MeshRenderer mesh && On(mesh, prefab.transform, wear))
                    {
                        chosen.Add(mesh);
                    }
                }
            }

            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!grouped.Contains(renderer) && On(renderer, prefab.transform, wear))
                {
                    chosen.Add(renderer);
                }
            }

            return chosen;
        }

        /// <summary>Whether this part of the prefab is drawn: switched on, and not a worn, broken or wet look.</summary>
        private static bool Shown(Transform from, Transform root, WearNTear wear)
        {
            for (Transform t = from; t != null && t != root; t = t.parent)
            {
                GameObject go = t.gameObject;
                if (wear != null)
                {
                    if (go == wear.m_new)
                    {
                        continue;
                    }

                    if (go == wear.m_worn || go == wear.m_broken || go == wear.m_wet)
                    {
                        return false;
                    }
                }

                if (!go.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Switched on within the prefab: the renderer, and every object up to the piece - with a
        /// piece's new look counted as on and its worn, broken and wet looks as off, whatever the
        /// prefab has.
        /// </summary>
        private static bool On(MeshRenderer renderer, Transform root, WearNTear wear)
        {
            if (renderer == null || !renderer.enabled)
            {
                return false;
            }

            for (Transform t = renderer.transform; t != null && t != root; t = t.parent)
            {
                GameObject go = t.gameObject;

                if (wear != null)
                {
                    if (go == wear.m_new)
                    {
                        continue;
                    }

                    if (go == wear.m_worn || go == wear.m_broken || go == wear.m_wet)
                    {
                        return false;
                    }
                }

                if (!go.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Diagnostic, debug logging only: what is inside a prefab that ended up with nothing to
        /// draw - every mesh renderer, whether it and each object above it are switched on, and
        /// which parts are a piece's wear looks - so a piece that goes missing can be explained
        /// rather than guessed at.
        /// </summary>
        private static void Explain(GameObject prefab)
        {
            if (!ModConfig.DebugLogging.Value)
            {
                return;
            }

            WearNTear wear = prefab.GetComponent<WearNTear>();
            List<string> lines = new List<string>();
            lines.Add($"'{prefab.name}' has nothing to draw. LOD groups: {prefab.GetComponentsInChildren<LODGroup>(true).Length}; "
                + $"wear looks: new={Name(wear?.m_new)} worn={Name(wear?.m_worn)} broken={Name(wear?.m_broken)} wet={Name(wear?.m_wet)}");

            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                List<string> path = new List<string>();
                for (Transform t = renderer.transform; t != null && t != prefab.transform; t = t.parent)
                {
                    path.Insert(0, t.name + (t.gameObject.activeSelf ? string.Empty : "(off)"));
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                lines.Add($"  {string.Join("/", path)} renderer {(renderer.enabled ? "on" : "off")}, "
                    + $"mesh {(filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : "none")}");
            }

            HammerOfOdenPlugin.Debug(string.Join("\n", lines));
        }

        private static string Name(GameObject go)
        {
            return go == null ? "-" : go.name + (go.activeSelf ? string.Empty : "(off)");
        }

        /// <summary>A copy of a piece's look on an object of its own: no collision, nothing saved.</summary>
        internal static GameObject Draw(Look look, string name)
        {
            if (look == null || look.Parts.Count == 0)
            {
                return null;
            }

            GameObject root = new GameObject(name);
            foreach (Part part in look.Parts)
            {
                GameObject child = new GameObject("part");
                Transform t = child.transform;
                t.SetParent(root.transform, false);
                t.localPosition = part.Matrix.GetColumn(3);
                t.localRotation = part.Matrix.rotation;
                t.localScale = part.Matrix.lossyScale;

                child.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                MeshRenderer renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = part.Materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return root;
        }

        internal static void Clear()
        {
            Cache.Clear();
            LowCache.Clear();
            BentCache.Clear();
            foreach (Mesh mesh in BentMeshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            BentMeshes.Clear();
        }
    }
}
