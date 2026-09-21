using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Adds vertices along one axis so that a mesh has something to curve with.
    /// </summary>
    /// <remarks>
    /// A deformer can only move vertices that exist. Hiding the meshes that had too few was a
    /// way of not drawing the problem, and it works for a coarse stand-in nobody looks at, but
    /// it is no answer for a piece's real geometry: hiding the rails of a wood wall leaves a
    /// wall with no rails.
    ///
    /// What matters is not how many vertices a mesh has but how far apart they are along the
    /// bend. That is why the wall's uprights looked right and its rails did not, though both
    /// were plain boxes - an upright is short along the bend, so it is carried round the arc
    /// almost as a rigid thing, while a rail spans the whole piece and has to curve across its
    /// entire length. Four rings of vertices over two and a third metres is a segment every
    /// sixty centimetres, which no amount of correct arithmetic makes look curved.
    ///
    /// So triangles are cut, repeatedly and only along the bend axis, until no piece of one
    /// spans more than a set distance. Splitting at the midpoint of a triangle's own extent
    /// halves the worst case each time and terminates quickly; the alternative, slicing against
    /// a fixed grid of planes, does the same work and leaves slivers wherever a triangle happens
    /// to straddle one.
    /// </remarks>
    internal static class MeshSubdivider
    {
        /// <summary>Guards against a pathological mesh turning one bend into a memory problem.</summary>
        private const int MaximumTriangles = 60000;

        private sealed class Vertex
        {
            public Vector3 Position;
            public Vector2 Uv;
            public float Along;
        }

        /// <summary>
        /// Rebuilds a mesh with enough subdivision along <paramref name="axis"/> to curve.
        /// </summary>
        /// <param name="source">The mesh to copy from. Never modified.</param>
        /// <param name="toPiece">Mesh-local to piece-local, for measuring spans the piece's way.</param>
        /// <param name="maxSegment">Longest a triangle may span along the axis, in piece units.</param>
        /// <returns>A new mesh, or null when the source already has enough detail.</returns>
        internal static Mesh Subdivide(Mesh source, Matrix4x4 toPiece, int axis, float maxSegment)
        {
            Vector3[] positions = source.vertices;
            Vector2[] uvs = source.uv;
            bool hasUv = uvs != null && uvs.Length == positions.Length;

            float[] along = new float[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                along[i] = toPiece.MultiplyPoint3x4(positions[i])[axis];
            }

            List<Vertex> vertices = new List<Vertex>(positions.Length * 4);
            for (int i = 0; i < positions.Length; i++)
            {
                vertices.Add(new Vertex
                {
                    Position = positions[i],
                    Uv = hasUv ? uvs[i] : Vector2.zero,
                    Along = along[i]
                });
            }

            List<int[]> perSubmesh = new List<int[]>();
            bool changed = false;

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                List<int> output = new List<int>();
                int[] input = source.GetTriangles(sub);

                for (int t = 0; t < input.Length; t += 3)
                {
                    changed |= Split(vertices, output, input[t], input[t + 1], input[t + 2], maxSegment, 0);

                    if (vertices.Count > MaximumTriangles)
                    {
                        // Give up rather than grow without bound; the caller keeps the original.
                        return null;
                    }
                }

                perSubmesh.Add(output.ToArray());
            }

            if (!changed)
            {
                return null;
            }

            Mesh built = new Mesh { name = source.name + "_dense" };

            if (vertices.Count > 65534)
            {
                built.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            Vector3[] outPositions = new Vector3[vertices.Count];
            Vector2[] outUvs = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                outPositions[i] = vertices[i].Position;
                outUvs[i] = vertices[i].Uv;
            }

            built.vertices = outPositions;
            if (hasUv)
            {
                built.uv = outUvs;
            }

            built.subMeshCount = perSubmesh.Count;
            for (int sub = 0; sub < perSubmesh.Count; sub++)
            {
                built.SetTriangles(perSubmesh[sub], sub);
            }

            built.RecalculateNormals();
            built.RecalculateBounds();

            return built;
        }

        /// <summary>
        /// Emits one triangle, cutting it first if it reaches too far along the axis.
        /// </summary>
        /// <returns>True if anything was cut.</returns>
        private static bool Split(
            List<Vertex> vertices, List<int> output, int a, int b, int c, float maxSegment, int depth)
        {
            float lo = Mathf.Min(vertices[a].Along, Mathf.Min(vertices[b].Along, vertices[c].Along));
            float hi = Mathf.Max(vertices[a].Along, Mathf.Max(vertices[b].Along, vertices[c].Along));

            // Depth is bounded as well as size: a degenerate triangle can otherwise be halved
            // forever without either end moving.
            if (hi - lo <= maxSegment || depth >= 8)
            {
                output.Add(a);
                output.Add(b);
                output.Add(c);
                return depth > 0;
            }

            // The longest edge along the axis is the one worth cutting; halving anything else
            // leaves the reach unchanged and recurses without progress.
            int p = a, q = b, r = c;
            float ab = Mathf.Abs(vertices[a].Along - vertices[b].Along);
            float bc = Mathf.Abs(vertices[b].Along - vertices[c].Along);
            float ca = Mathf.Abs(vertices[c].Along - vertices[a].Along);

            if (bc >= ab && bc >= ca) { p = b; q = c; r = a; }
            else if (ca >= ab && ca >= bc) { p = c; q = a; r = b; }

            int mid = Midpoint(vertices, p, q);

            Split(vertices, output, p, mid, r, maxSegment, depth + 1);
            Split(vertices, output, mid, q, r, maxSegment, depth + 1);
            return true;
        }

        private static int Midpoint(List<Vertex> vertices, int i, int j)
        {
            Vertex u = vertices[i];
            Vertex v = vertices[j];

            vertices.Add(new Vertex
            {
                Position = (u.Position + v.Position) * 0.5f,
                Uv = (u.Uv + v.Uv) * 0.5f,
                Along = (u.Along + v.Along) * 0.5f
            });

            return vertices.Count - 1;
        }
    }
}
