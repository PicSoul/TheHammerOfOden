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
            Piece piece = Aiming(player);
            if (piece == null)
            {
                HammerOfOdenPlugin.Info("Mesh probe: nothing under the crosshair.");
                Notify.Show(player, "Mesh probe: aim at a piece first");
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

            report.AppendLine(Bendable.Allows(piece.gameObject)
                ? "  BENDABLE    : yes - one collider and a mesh, which is what plain structure looks like"
                : "  BENDABLE    : no - " + Bendable.Reason(piece.gameObject));

            report.AppendLine("---- end ----");

            HammerOfOdenPlugin.Info(report.ToString());
            Notify.Show(player, "Mesh probe written to the log");
        }

        /// <summary>
        /// The piece the player means.
        /// </summary>
        /// <remarks>
        /// With a build tool out, this is the piece Valheim is already highlighting - the one a
        /// hammer would repair or remove. It is set by UpdateWearNTearHover, which raycasts from
        /// the camera on the removal mask and stops at the placement distance, so there is no
        /// ambiguity about which piece is meant: the game has drawn an outline round it.
        ///
        /// Without a build tool there is no highlight and no hovering piece, so the same ray is
        /// cast directly. The probe is worth being able to use with empty hands, since the
        /// question it answers is about a piece's geometry and has nothing to do with building.
        /// </remarks>
        private static Piece Aiming(Player player)
        {
            return player.GetHoveringPiece() ?? SurfacePlacement.LookingAt(player);
        }

        private static void DescribeMeshes(Piece piece, StringBuilder report)
        {
            MeshFilter[] filters = piece.GetComponentsInChildren<MeshFilter>(true);
            report.AppendLine("  mesh filters: " + filters.Length);

            int readable = 0;
            int withMesh = 0;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    // A MeshFilter with nothing in it. Counting this against readability once
                    // reported a wall as "26 of 27" and made a clean result look like a
                    // problem, which is the opposite of what a probe is for.
                    report.AppendLine("    - " + filter.name + ": no mesh (empty filter)");
                    continue;
                }

                withMesh++;

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

                // Why a part might not follow a deformation even though its mesh was bent.
                // Static batching bakes geometry into a shared buffer and draws from that, so
                // replacing the filter's mesh changes nothing on screen. An inactive object or
                // a disabled renderer is not drawn at all, so bending it is wasted work rather
                // than a fault. Both are invisible from the outside and both look identical to
                // "the maths is wrong", which is the trap this is here to spring.
                bool batched = renderer != null && renderer.isPartOfStaticBatch;
                bool drawn = filter.gameObject.activeInHierarchy && renderer != null && renderer.enabled;

                if (batched || !drawn)
                {
                    report.AppendLine(
                        "      NOTE: " + (batched ? "static-batched" : "")
                        + (batched && !drawn ? ", " : "")
                        + (!drawn ? "not drawn right now" : ""));
                }

                LODGroup lod = filter.GetComponentInParent<LODGroup>();
                if (lod != null)
                {
                    report.AppendLine("      part of LODGroup '" + lod.name + "'");
                }
            }

            SkinnedMeshRenderer[] skinned = piece.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length > 0)
            {
                report.AppendLine("  skinned renderers: " + skinned.Length + " (these bend by rig, not by vertex)");
            }

            report.AppendLine(readable == withMesh && withMesh > 0
                ? "  VERDICT: all " + withMesh + " meshes readable - vertex deformation is on the table."
                : "  VERDICT: " + readable + " of " + withMesh
                  + " readable - anything not readable cannot be deformed vertex by vertex.");
        }

        private static void DescribeColliders(Piece piece, StringBuilder report)
        {
            Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
            report.AppendLine("  colliders   : " + colliders.Length);

            // Listed rather than counted. A piece that reports three colliders may be three
            // working parts, or one part and two copies of it sitting inside switched-off worn
            // and broken variants - and those are opposite answers to whether it may bend.
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

                bool live = collider.gameObject.activeInHierarchy && collider.enabled;

                report.AppendLine(
                    "    - " + kind
                    + (collider.isTrigger ? " [trigger]" : string.Empty)
                    + (live ? " [live]" : " [inactive]")
                    + "  size " + collider.bounds.size.ToString("0.##")
                    + "  at " + Path(collider.transform, piece.transform));
            }
        }

        /// <summary>Where a child sits in the piece, so a duplicate can be told from a part.</summary>
        private static string Path(Transform child, Transform root)
        {
            string path = child.name;

            for (Transform t = child.parent; t != null && t != root; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
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
