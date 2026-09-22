using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Decides which pieces may be bent.
    /// </summary>
    /// <remarks>
    /// Bending deforms a piece's meshes along a curve, which means its collision has to follow.
    /// A BoxCollider cannot curve, so a bent piece needs its box replaced by geometry that can -
    /// and that is only tractable while there is one box to replace. A piece built from nine of
    /// them is nine chains of collision to lay out and keep in step.
    ///
    /// The happy accident is that the collider count turns out to be a good proxy for whether a
    /// piece should bend at all. Measuring six pieces in game: a wall, a log pole, a stone wall
    /// and a roof section carry one collider each and are exactly the plain structure an arch is
    /// made of. A step ladder carries nine and a fence gate four - both things with moving or
    /// functional parts, and both things nobody wants bent. The rule that makes the work
    /// tractable and the rule that keeps doors straight are the same rule.
    ///
    /// So eligibility is read off the piece rather than from a list of names, the way Scalable
    /// and CreatureTiers do it, and a piece another mod adds is judged by the same measure. The
    /// two exception lists are there because a rule this simple will be wrong occasionally, and
    /// being wrong occasionally is fine as long as it can be corrected without a new build.
    /// </remarks>
    internal static class Bendable
    {
        private static GameObject _cachedFor;
        private static bool _cached;
        private static string _cachedReason;

        /// <summary>
        /// Verdicts by object, because the build menu asks about every piece at once.
        /// </summary>
        /// <remarks>
        /// A single last-asked slot is right while the question is only ever about the piece in
        /// hand. Marking the menu asks about a hundred pieces every time it refreshes, which
        /// turned that slot into a miss every time and re-measured the lot on every keystroke.
        /// Prefabs are stable objects, so remembering the answer costs one dictionary and saves
        /// all of it.
        /// </remarks>
        private static readonly Dictionary<GameObject, KeyValuePair<bool, string>> Verdicts =
            new Dictionary<GameObject, KeyValuePair<bool, string>>();

        /// <summary>Prefabs already explained, so a refusal is logged once and not per frame.</summary>
        private static readonly HashSet<string> Explained = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string _listSource;
        private static readonly HashSet<string> Never = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Always = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static bool Allows(GameObject piece)
        {
            Evaluate(piece);
            return _cached;
        }

        /// <summary>Why a piece was refused, for the message and the log.</summary>
        internal static string Reason(GameObject piece)
        {
            Evaluate(piece);
            return _cachedReason;
        }

        internal static void Forget()
        {
            _cachedFor = null;
        }

        private static void Evaluate(GameObject piece)
        {
            if (_cachedFor == piece)
            {
                return;
            }

            _cachedFor = piece;

            if (piece == null)
            {
                _cached = false;
                _cachedReason = "nothing selected";
                return;
            }

            if (Verdicts.TryGetValue(piece, out KeyValuePair<bool, string> known))
            {
                _cached = known.Key;
                _cachedReason = known.Value;
                return;
            }

            _cached = IsAllowed(piece, out _cachedReason);
            Verdicts[piece] = new KeyValuePair<bool, string>(_cached, _cachedReason);

            // A refusal is a fact about a prefab, so it belongs in the log - once. Saying it
            // again every time the build menu redraws buried everything else in the file.
            if (!_cached && Explained.Add(Utils.GetPrefabName(piece)))
            {
                HammerOfOdenPlugin.Debug(
                    $"Bend refused '{Utils.GetPrefabName(piece)}': {_cachedReason}.");
            }
        }

        private static bool IsAllowed(GameObject piece, out string reason)
        {
            RefreshLists();

            string prefab = Utils.GetPrefabName(piece);

            // The lists win over the measurement, in both directions, because the measurement is
            // a good guess rather than a promise.
            if (Never.Contains(prefab))
            {
                reason = "listed as never bendable";
                return false;
            }

            if (Always.Contains(prefab))
            {
                reason = "listed as always bendable";
                return true;
            }

            Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);

            // Distinct volumes, not components. The 4x2 stone wall carries two identical
            // 4x1x1 boxes, one on the root and one on a child called "collider" - a duplicate
            // left in the prefab, not a second working part. Counting components called it
            // complicated; counting the shapes it actually occupies calls it what it is.
            List<Bounds> volumes = new List<Bounds>();

            int solid = 0;
            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                // Triggers are not collision; they are the volumes a piece uses to notice you.
                // Counting them would exclude perfectly plain pieces for having a comfort range.
                if (collider.isTrigger)
                {
                    continue;
                }

                // Nor is a collider that belongs to a damaged version of the piece. Valheim keeps
                // worn and broken states as separate child objects and shows one at a time, so a
                // piece that is one plain slab reports three colliders - one real, two waiting for
                // damage that has not happened.
                if (IsDamagedVariant(piece, collider.transform))
                {
                    continue;
                }

                if (!VolumeOf(collider, piece.transform, out Bounds box))
                {
                    continue;
                }

                bool duplicate = false;

                foreach (Bounds seen in volumes)
                {
                    if ((seen.center - box.center).sqrMagnitude < 0.0004f
                        && (seen.size - box.size).sqrMagnitude < 0.0004f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                volumes.Add(box);
                solid++;
            }

            if (solid == 0)
            {
                reason = "has no collision to rebuild";
                return false;
            }

            if (solid > 1 && !TileOneSolid(volumes))
            {
                reason = "is built from " + solid + " separate parts, so it has moving or working bits";

                foreach (Bounds seen in volumes)
                {
                    HammerOfOdenPlugin.Debug(
                        $"    volume centre {seen.center.ToString("0.###")} size {seen.size.ToString("0.###")}");
                }

                return false;
            }

            // Anything you can use is not plain structure, whatever it is built from. A chest
            // and a chair both carry a single collider and both came back bendable on the first
            // rule, which was wrong in a way that would only have shown up as a bent chest.
            if (piece.GetComponentInChildren<Interactable>(true) != null)
            {
                reason = "is something you interact with";
                return false;
            }

            MeshFilter[] filters = piece.GetComponentsInChildren<MeshFilter>(true);
            int meshes = 0;
            int readable = 0;

            // The biggest mesh the piece shows while undamaged is its body - the thing you are
            // looking at. Whether that one can be read decides the whole question, because
            // anything unreadable is hidden for the duration of a bend: hiding a distant
            // stand-in costs nothing, and hiding the body leaves an invisible piece.
            Mesh body = null;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                meshes++;

                if (!IsDamagedVariant(piece, filter.transform)
                    && (body == null || mesh.vertexCount > body.vertexCount))
                {
                    body = mesh;
                }

                if (mesh.isReadable)
                {
                    readable++;
                }
            }

            if (meshes == 0)
            {
                reason = "has no mesh to bend";
                return false;
            }

            // Some unreadable meshes are survivable, all of them are not. A mesh that cannot be
            // read cannot be curved, so it is hidden for the duration rather than left drawing
            // straight over a bent piece - the same treatment a mesh too coarse to curve gets.
            // The stone fence is the case worth allowing: its detailed mesh reads fine and what
            // cannot be read is its distant stand-in and its broken state, neither of which is
            // what you are looking at while you bend it.
            if (readable == 0)
            {
                reason = "has no mesh that can be read at runtime";
                return false;
            }

            // Judged on the body rather than on a count. Two of the stone fence's six meshes can
            // be read and it bends perfectly, because the two are the fence and the other four
            // are its distant stand-in and its broken state. Three of the darkwood roof's
            // twenty-four can be read and they are the snow lying on it - allowing that one
            // hid the roof and left the snow, which is how this was found.
            if (body != null && !body.isReadable)
            {
                reason = "its main mesh cannot be read at runtime, so bending would hide the piece";
                return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// A collider's extent in the piece's own space, computed rather than queried.
        /// </summary>
        /// <remarks>
        /// Collider.bounds is a world-space box Unity only fills in for a collider that is
        /// actually in the scene. Asking a prefab for it hands back zeros - which is why every
        /// piece in the build menu measured as nothing, filled none of the space it spanned and
        /// was refused. The ghost gave sensible answers throughout because a ghost is a real
        /// object; nothing was wrong with the rule, only with what it was being told.
        ///
        /// So the shape is worked out from what the collider itself is set to, which reads the
        /// same whether the object is standing in the world or sitting in a prefab.
        /// </remarks>
        private static bool VolumeOf(Collider collider, Transform root, out Bounds bounds)
        {
            bounds = new Bounds();

            Vector3 centre;
            Vector3 size;

            switch (collider)
            {
                case BoxCollider box:
                    centre = box.center;
                    size = box.size;
                    break;

                case SphereCollider sphere:
                    centre = sphere.center;
                    size = Vector3.one * sphere.radius * 2f;
                    break;

                case CapsuleCollider capsule:
                    centre = capsule.center;
                    size = Vector3.one * capsule.radius * 2f;
                    size[Mathf.Clamp(capsule.direction, 0, 2)] = capsule.height;
                    break;

                case MeshCollider mesh when mesh.sharedMesh != null:
                    centre = mesh.sharedMesh.bounds.center;
                    size = mesh.sharedMesh.bounds.size;
                    break;

                default:
                    return false;
            }

            Transform local = collider.transform;
            Vector3 half = size * 0.5f;
            bool any = false;

            // Every corner, since a collider on a rotated child does not stay a box once it is
            // expressed in the piece's frame.
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = centre + Vector3.Scale(half, new Vector3(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f));

                Vector3 p = root.InverseTransformPoint(local.TransformPoint(corner));

                if (!any)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }

            return any;
        }

        /// <summary>
        /// Whether several collision boxes are really one solid shape cut into pieces.
        /// </summary>
        /// <remarks>
        /// Counting colliders was a stand-in for "does this have moving or working parts", and it
        /// is wrong in both directions. The 4x2 stone wall carries two boxes, each four by one by
        /// one, stacked a metre apart: together they are the wall, and it is as plain a slab as
        /// the 1x1 that carries a single box. A step ladder also carries several boxes and they
        /// are rungs - small, spread through a tall thin space, most of which is air.
        ///
        /// What separates them is how much of the space they span they actually fill. Boxes that
        /// tile one solid leave nothing over; parts scattered through a volume leave most of it
        /// empty. Rebuilding collision for a slab cut in two is no harder than for a slab, which
        /// is the practical question underneath all of this.
        ///
        /// Approximate on purpose. Overlapping boxes can push the ratio past one and boxes at an
        /// angle to each other will read lower than they deserve; both are safe directions, since
        /// the worst case is a piece that has to be named in AlwaysBendable.
        /// </remarks>
        private static bool TileOneSolid(List<Bounds> volumes)
        {
            if (volumes.Count == 0)
            {
                return false;
            }

            Bounds whole = volumes[0];
            float filled = 0f;

            foreach (Bounds box in volumes)
            {
                whole.Encapsulate(box);
                filled += box.size.x * box.size.y * box.size.z;
            }

            float span = whole.size.x * whole.size.y * whole.size.z;
            if (span <= 0.0001f)
            {
                return false;
            }

            float ratio = filled / span;
            HammerOfOdenPlugin.Debug(
                $"    {volumes.Count} volumes fill {ratio:0.##} of the space they span.");

            return ratio >= Mathf.Clamp01(ModConfig.BendSolidFill.Value);
        }

        /// <summary>
        /// Whether a child belongs to the worn or broken version of a piece.
        /// </summary>
        /// <remarks>
        /// Asked of WearNTear rather than inferred from what happens to be switched on. Which
        /// state is active depends on how damaged the piece is, on whether this is a ghost -
        /// where WearNTear has not run at all - and on whether some other mod has taken wear out
        /// of the game entirely, which plenty of building players do. Judging by activity would
        /// mean a piece that is bendable on one machine and refused on another, for reasons
        /// neither player could see. The prefab's own idea of which children are damage states
        /// does not move.
        /// </remarks>
        private static bool IsDamagedVariant(GameObject piece, Transform child)
        {
            WearNTear wear = piece.GetComponent<WearNTear>();
            if (wear == null)
            {
                return false;
            }

            for (Transform t = child; t != null; t = t.parent)
            {
                if ((wear.m_worn != null && t.gameObject == wear.m_worn)
                    || (wear.m_broken != null && t.gameObject == wear.m_broken))
                {
                    return true;
                }

                if (t.gameObject == piece)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>Splits the exception lists once per change, since this is asked per piece.</summary>
        private static void RefreshLists()
        {
            string raw = (ModConfig.BendNever.Value ?? string.Empty)
                + "\u0000" + (ModConfig.BendAlways.Value ?? string.Empty);

            if (raw == _listSource)
            {
                return;
            }

            _listSource = raw;

            Fill(Never, ModConfig.BendNever.Value);
            Fill(Always, ModConfig.BendAlways.Value);

            // Verdicts already reached were reached under the old lists.
            Verdicts.Clear();
            Explained.Clear();
        }

        private static void Fill(HashSet<string> into, string raw)
        {
            into.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string entry in raw.Split(','))
            {
                string name = entry.Trim();
                if (name.Length > 0)
                {
                    into.Add(name);
                }
            }
        }
    }
}
