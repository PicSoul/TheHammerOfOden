using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Reports what a placed piece is actually made of, so the bending question can be settled
    /// by looking rather than by reasoning about it.
    /// </summary>
    /// <remarks>
    /// Bending a beam into an arch means deforming its mesh, and whether that is possible at all
    /// comes down to one flag. Unity meshes imported with Read/Write disabled cannot have their
    /// vertices read at runtime: <c>mesh.vertices</c> logs an error and hands back an empty
    /// array. Games often ship that way, because the flag doubles a mesh's memory - the CPU copy
    /// is kept alongside the GPU one - and nothing in a normal build needs it.
    ///
    /// If these meshes are readable, bending is a hard job with a known shape. If they are not,
    /// the generic approach is dead on arrival and no amount of care rescues it, because there
    /// is nothing to read. That is worth ten minutes of measuring rather than a week of
    /// discovering.
    ///
    /// Three other things are reported because they decide how much work the answer implies:
    /// the collider type, since a BoxCollider cannot bend and would have to become a generated
    /// mesh or a row of boxes; the snap points, which are child transforms that would have to be
    /// carried along the same curve or the arch would not connect to anything; and the shader,
    /// because the fallback for unreadable meshes is to bend in a vertex shader, which means
    /// replacing whatever Valheim uses and losing what it does for wetness, snow and damage.
    /// </remarks>
    internal static class MeshProbe
    {
        internal static void Dump(Player player)
        {
            Piece piece = SurfacePlacement.LookingAt(player);
            if (piece == null)
            {
                HammerOfOdenPlugin.Info("Mesh probe: not looking at a piece.");
                Notify.Show(player, "Mesh probe: look at a piece first");
                return;
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("---- mesh probe: " + piece.name + " ----");
            report.AppendLine("  piece name  : " + piece.m_name);
            report.AppendLine("  comfort/wear: "
                + (piece.GetComponent<WearNTear>() != null ? "has WearNTear" : "no WearNTear"));

            DescribeMeshes(piece, report);
            DescribeColliders(piece, report);
            DescribeSnapPoints(piece, report);

            report.AppendLine("---- end ----");

            HammerOfOdenPlugin.Info(report.ToString());
            Notify.Show(player, "Mesh probe written to the log");
        }

        private static void DescribeMeshes(Piece piece, StringBuilder report)
        {
            MeshFilter[] filters = piece.GetComponentsInChildren<MeshFilter>(true);
            report.AppendLine("  mesh filters: " + filters.Length);

            int readable = 0;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    report.AppendLine("    - " + filter.name + ": no mesh");
                    continue;
                }

                // The whole question, in one property. Asking for vertexCount is safe either
                // way; asking for vertices would log a Unity error when it is false, which is
                // exactly the noise a probe should not make.
                if (mesh.isReadable)
                {
                    readable++;
                }

                report.AppendLine(
                    "    - " + filter.name
                    + ": mesh '" + mesh.name + "'"
                    + ", readable=" + mesh.isReadable
                    + ", verts=" + mesh.vertexCount
                    + ", submeshes=" + mesh.subMeshCount);

                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    report.AppendLine(
                        "      shader: " + (renderer.sharedMaterial.shader != null
                            ? renderer.sharedMaterial.shader.name
                            : "none"));
                }
            }

            SkinnedMeshRenderer[] skinned = piece.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length > 0)
            {
                report.AppendLine("  skinned renderers: " + skinned.Length + " (these bend by rig, not by vertex)");
            }

            report.AppendLine(readable == filters.Length && filters.Length > 0
                ? "  VERDICT: every mesh is readable - vertex deformation is on the table."
                : "  VERDICT: " + readable + " of " + filters.Length
                  + " readable - anything not readable cannot be deformed vertex by vertex.");
        }

        private static void DescribeColliders(Piece piece, StringBuilder report)
        {
            Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
            report.AppendLine("  colliders   : " + colliders.Length);

            Dictionary<string, int> kinds = new Dictionary<string, int>();
            foreach (Collider collider in colliders)
            {
                string kind = collider.GetType().Name;
                if (collider is MeshCollider mesh)
                {
                    kind += mesh.convex ? " (convex)" : " (concave)";
                    if (mesh.sharedMesh != null)
                    {
                        kind += " readable=" + mesh.sharedMesh.isReadable;
                    }
                }

                kinds.TryGetValue(kind, out int count);
                kinds[kind] = count + 1;
            }

            foreach (KeyValuePair<string, int> kind in kinds)
            {
                report.AppendLine("    - " + kind.Key + " x" + kind.Value);
            }
        }

        private static void DescribeSnapPoints(Piece piece, StringBuilder report)
        {
            int snaps = 0;
            foreach (Transform child in piece.GetComponentsInChildren<Transform>(true))
            {
                if (child.CompareTag("snappoint"))
                {
                    snaps++;
                }
            }

            report.AppendLine("  snap points : " + snaps + " (each one would have to follow the bend)");
        }
    }
}
