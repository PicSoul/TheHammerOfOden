using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Lays a run of identical pieces in one action.
    /// </summary>
    /// <remarks>
    /// A wall of ten panels is ten placements, each aimed by hand, and the tenth is never
    /// quite in line with the first. Choosing a direction and a count instead makes the run
    /// exact by construction: every copy is one piece-width from the last, measured along
    /// the direction it is being laid in, so they sit flush whatever the piece is and
    /// however it has been turned.
    ///
    /// The copies are shown before they are built. A count with no preview is a guess, and
    /// the whole point is to see the run before paying for it.
    ///
    /// It is deliberately not free. Each copy is a real placement that checks its own
    /// requirements and pays its own materials, and the run stops at the first one that
    /// cannot be afforded rather than placing a partial ghost run or going into debt. That
    /// keeps this a convenience for building rather than a way around building.
    /// </remarks>
    internal static class Zooping
    {
        /// <summary>One direction of a run, and how many pieces have been asked for along it.</summary>
        private struct Leg
        {
            internal Vector3 Direction;
            internal int Count;
        }

        /// <summary>
        /// Up to three perpendicular directions, multiplied together.
        /// </summary>
        /// <remarks>
        /// Runs compose. Four along one axis and five along another is not two runs, it is a
        /// wall four by five, and asking for it that way is far less work than laying five
        /// runs of four. Three legs gives a solid block; there is no fourth direction in a
        /// three-dimensional world, so that is the natural ceiling rather than an arbitrary
        /// one.
        /// </remarks>
        private static readonly List<Leg> Legs = new List<Leg>(3);

        private static readonly List<GameObject> Previews = new List<GameObject>();
        private static GameObject _previewsFor;

        /// <summary>Whether a run is being built right now, so its copies are not new actions.</summary>
        internal static bool IsPlacing => _placing;

        /// <summary>How many extra copies accompany the piece you are placing.</summary>
        internal static int Count => TotalCopies();

        /// <summary>
        /// Extra space between copies, in metres, on top of the piece's own size and the spacing
        /// multiplier. Negative overlaps them.
        /// </summary>
        /// <remarks>
        /// Metres rather than another multiplier, because a gap is something you measure by eye -
        /// a hand's width between fence posts - not a fraction of whichever piece you happen to
        /// be holding. Kept across runs, so a gap set once lays a whole fence line.
        /// </remarks>
        internal static float Gap { get; private set; }

        internal static void AdjustGap(Player player, float delta)
        {
            Gap = Mathf.Clamp(Mathf.Round((Gap + delta) * 100f) / 100f, -5f, 20f);

            string text = Mathf.Approximately(Gap, 0f)
                ? "Zoop gap: none, copies touch"
                : Gap > 0f ? $"Zoop gap: {Gap:0.##} m apart" : $"Zoop gap: {-Gap:0.##} m overlap";

            Notify.Show(player, IsActive ? text : text + " - applies to your next run");
        }

        internal static void ResetGap()
        {
            Gap = 0f;
        }

        internal static bool IsActive => Legs.Count > 0;

        /// <summary>Every combination of the legs, less the piece itself.</summary>
        private static int TotalCopies()
        {
            if (Legs.Count == 0)
            {
                return 0;
            }

            int total = 1;
            foreach (Leg leg in Legs)
            {
                total *= leg.Count + 1;
            }

            return total - 1;
        }

        /// <summary>
        /// Extends the run along a direction, or shortens it when pushed back the other way.
        /// </summary>
        internal static void Extend(Player player, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.5f)
            {
                return;
            }

            for (int i = 0; i < Legs.Count; i++)
            {
                float alignment = Vector3.Dot(direction, Legs[i].Direction);

                if (alignment > 0.5f)
                {
                    Grow(player, i, 1);
                    return;
                }

                if (alignment < -0.5f)
                {
                    Grow(player, i, -1);
                    return;
                }
            }

            if (Legs.Count >= 3)
            {
                Notify.Show(player, "Zoop: already using three directions");
                return;
            }

            Legs.Add(new Leg { Direction = direction, Count = 1 });

            if (TotalCopies() > ModConfig.ZoopLimit.Value)
            {
                Legs.RemoveAt(Legs.Count - 1);
                Notify.Show(player, $"Zoop limit is {ModConfig.ZoopLimit.Value}");
                return;
            }

            Announce(player);
        }

        private static void Grow(Player player, int index, int by)
        {
            Leg leg = Legs[index];
            int wanted = leg.Count + by;

            if (wanted <= 0)
            {
                Legs.RemoveAt(index);
                DestroyPreviews();
                Announce(player);
                return;
            }

            leg.Count = wanted;
            Leg previous = Legs[index];
            Legs[index] = leg;

            if (TotalCopies() > ModConfig.ZoopLimit.Value)
            {
                Legs[index] = previous;
                Notify.Show(player, $"Zoop limit is {ModConfig.ZoopLimit.Value}");
                return;
            }

            Announce(player);
        }

        private static void Announce(Player player)
        {
            if (Legs.Count == 0)
            {
                Notify.Show(player, "Zoop: off");
                return;
            }

            string shape = string.Empty;
            foreach (Leg leg in Legs)
            {
                shape += (shape.Length == 0 ? string.Empty : " x ") + (leg.Count + 1);
            }

            Notify.Show(player, $"Zoop: {shape} ({TotalCopies() + 1} pieces)");
        }

        /// <summary>Forget the run, and take down its preview.</summary>
        internal static void Clear()
        {
            Legs.Clear();
            DestroyPreviews();
        }

        /// <summary>
        /// Positions of the extra copies, given where the piece itself is going.
        /// </summary>
        /// <remarks>
        /// Every combination of the legs, which for one leg is a line, for two a grid and for
        /// three a block. The origin combination is skipped because that is the piece you are
        /// actually placing.
        ///
        /// Steps are recomputed from the ghost each time rather than stored, because the
        /// spacing depends on the piece's current rotation - turning a beam ninety degrees
        /// changes how far apart its copies belong.
        /// </remarks>
        internal static void PositionsFor(GameObject ghost, Vector3 origin, List<Vector3> into)
        {
            into.Clear();

            if (!IsActive || ghost == null)
            {
                return;
            }

            Vector3 a = Vector3.zero, b = Vector3.zero, c = Vector3.zero;
            int ca = 0, cb = 0, cc = 0;

            for (int i = 0; i < Legs.Count; i++)
            {
                // Never less than a few centimetres: a gap wide enough to overlap a copy
                // entirely would stack the whole run in one place.
                float size = GhostBounds.SizeAlong(ghost, Legs[i].Direction);
                float length = Mathf.Max(size * ModConfig.ZoopSpacing.Value + Gap, Mathf.Max(0.05f, size * 0.05f));
                Vector3 step = Legs[i].Direction * length;

                if (i == 0) { a = step; ca = Legs[i].Count; }
                else if (i == 1) { b = step; cb = Legs[i].Count; }
                else { c = step; cc = Legs[i].Count; }
            }

            for (int i = 0; i <= ca; i++)
            {
                for (int j = 0; j <= cb; j++)
                {
                    for (int k = 0; k <= cc; k++)
                    {
                        if (i == 0 && j == 0 && k == 0)
                        {
                            continue;
                        }

                        into.Add(origin + a * i + b * j + c * k);
                    }
                }
            }
        }

        private static readonly List<Vector3> Scratch = new List<Vector3>();
        private static readonly List<Vector3> Pending = new List<Vector3>();
        private static readonly List<Vector3> Run = new List<Vector3>();
        private static bool _placing;

        private delegate void PlacePieceCall(
            Player player, Piece piece, Vector3 pos, Quaternion rot, bool doAttack, bool cheated);

        private static readonly PlacePieceCall Place = ResolvePlace();

        private static PlacePieceCall ResolvePlace()
        {
            try
            {
                MethodInfo method = AccessTools.Method(typeof(Player), "PlacePiece", new[]
                {
                    typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
                });

                if (method == null)
                {
                    HammerOfOdenPlugin.Error(
                        "Player.PlacePiece was not found, so zooping is disabled. "
                        + "Valheim has probably changed.");
                    return null;
                }

                return AccessTools.MethodDelegate<PlacePieceCall>(method);
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error("Could not bind Player.PlacePiece, so zooping is disabled. "
                    + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Works out where the run goes, while the ghost is still there to measure.
        /// </summary>
        /// <remarks>
        /// Called from the prefix rather than the postfix because the spacing comes from the
        /// ghost's size at its current rotation, and by the time the placement has finished
        /// the ghost may already have been torn down and rebuilt.
        /// </remarks>
        internal static void Remember(GameObject ghost, Vector3 origin)
        {
            // The guard comes first. Each copy goes through PlacePiece, which re-enters this
            // via our own prefix, and clearing before the check emptied the list that
            // PlaceRun was in the middle of walking - one copy placed, then the enumeration
            // threw. That is why a run of seven placed two.
            if (_placing)
            {
                return;
            }

            Pending.Clear();

            if (!ModConfig.IsEnabled)
            {
                return;
            }

            PositionsFor(ghost, origin, Pending);
        }

        /// <summary>
        /// Builds the rest of the run, one real placement at a time.
        /// </summary>
        /// <remarks>
        /// Each copy goes through Valheim's own PlacePiece, so it costs materials, raises the
        /// same effects and is the same object as one placed by hand - there is no second,
        /// cheaper path that could drift from the real one.
        ///
        /// The run stops at the first copy that cannot be afforded rather than placing what
        /// it can and leaving a gap, because a wall with a hole in the middle is worse than a
        /// shorter wall. Only the piece you actually swung at does the attack animation.
        /// </remarks>
        private static Piece _duePiece;
        private static Quaternion _dueRotation;
        private static bool _dueCheated;

        /// <summary>
        /// Notes that a run is due, without building it here.
        /// </summary>
        /// <remarks>
        /// Placing the copies from inside the PlacePiece postfix means calling PlacePiece
        /// again while the original call is still on the stack, and every other mod patching
        /// it gets re-entered halfway through its own work - StoreAndCraft was iterating a
        /// collection and threw when ours modified it underneath.
        ///
        /// Waiting a frame costs nothing visible and means each copy is an ordinary, separate
        /// placement as far as everything else is concerned.
        /// </remarks>
        internal static void QueueRun(Piece piece, Quaternion rotation, bool cheated)
        {
            if (Pending.Count == 0)
            {
                return;
            }

            _duePiece = piece;
            _dueRotation = rotation;
            _dueCheated = cheated;
        }

        /// <summary>Builds any run left over from the previous frame.</summary>
        private static int _built;

        /// <summary>
        /// Builds part of the outstanding run, a few pieces at a time.
        /// </summary>
        /// <remarks>
        /// Spread across frames rather than done in one go. Each copy is a real placement -
        /// an instantiation, a ZDO, an effect - and a hundred of those in a single frame is
        /// a visible stutter at exactly the moment you are looking at what you just built.
        /// A budget per frame turns that into a run that lays itself over a second or so,
        /// which also reads better than a wall appearing from nowhere.
        ///
        /// The positions were settled when the run was queued, so moving, turning or
        /// changing piece partway through cannot bend a run that is already being laid.
        /// </remarks>
        internal static void PlaceDue(Player player, bool free)
        {
            if (_duePiece == null || Place == null || player == null)
            {
                return;
            }

            if (Run.Count == 0)
            {
                // Taken as a copy: PlaceRun must be able to finish whatever any patch
                // reached through PlacePiece decides to do to Pending.
                Run.AddRange(Pending);
                Pending.Clear();
                _built = 0;

                if (Run.Count == 0)
                {
                    _duePiece = null;
                    return;
                }
            }

            Piece piece = _duePiece;
            int budget = Mathf.Max(1, ModConfig.ZoopPerFrame.Value);

            _placing = true;

            try
            {
                while (Run.Count > 0 && budget > 0)
                {
                    if (!free && !player.HaveRequirements(piece, Player.RequirementMode.CanBuild))
                    {
                        Notify.Show(player, $"Zoop stopped: out of materials after {_built}");
                        Finish();
                        return;
                    }

                    Place(player, piece, Run[0], _dueRotation, false, _dueCheated);
                    Run.RemoveAt(0);

                    _built++;
                    budget--;
                }
            }
            finally
            {
                _placing = false;
            }

            if (Run.Count == 0)
            {
                HammerOfOdenPlugin.Debug($"Zoop placed {_built} extra pieces.");
                Finish();
            }
        }

        private static void Finish()
        {
            _duePiece = null;
            Pending.Clear();
            Run.Clear();
        }

        /// <summary>Shows the run where it will actually be built.</summary>
        internal static void UpdatePreview(GameObject ghost)
        {
            if (!ModConfig.IsEnabled || !IsActive || ghost == null || !ghost.activeSelf)
            {
                DestroyPreviews();
                return;
            }

            PositionsFor(ghost, ghost.transform.position, Scratch);

            // Rebuilt only when the run changes or the ghost is replaced; moving them is
            // free, cloning them is not.
            if (_previewsFor != ghost || Previews.Count != Scratch.Count || AnyMissing())
            {
                BuildPreviews(ghost, Scratch.Count);
            }

            for (int i = 0; i < Previews.Count && i < Scratch.Count; i++)
            {
                if (Previews[i] == null)
                {
                    continue;
                }

                Previews[i].transform.SetPositionAndRotation(Scratch[i], ghost.transform.rotation);
                Previews[i].transform.localScale = ghost.transform.localScale;
            }
        }

        private static bool AnyMissing()
        {
            foreach (GameObject preview in Previews)
            {
                if (preview == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds a preview out of nothing but meshes and materials.
        /// </summary>
        /// <remarks>
        /// Cloning the ghost is the obvious approach and is wrong. Disabling the clone's
        /// components does not stop them running: Unity calls Awake on activation whatever
        /// enabled is set to, so every copy ran Valheim's own piece logic, and WearNTear.Awake
        /// threw on each one for want of the things a real piece has. Destroying the
        /// components instead is a race, because Destroy is deferred to the end of the frame
        /// and the object is activated before then.
        ///
        /// So nothing is cloned. Each preview is built from scratch as a mesh renderer per
        /// mesh in the ghost, which cannot run any game logic because there is none on it to
        /// run. The materials come from the ghost, so the copies are already the translucent
        /// blue that Valheim gives a piece being placed.
        /// </remarks>
        private static void BuildPreviews(GameObject ghost, int wanted)
        {
            DestroyPreviews();
            _previewsFor = ghost;

            Transform origin = ghost.transform;
            Vector3 originScale = origin.lossyScale;

            for (int i = 0; i < wanted; i++)
            {
                GameObject root = new GameObject("HoO_ZoopPreview");

                foreach (MeshRenderer source in ghost.GetComponentsInChildren<MeshRenderer>(false))
                {
                    MeshFilter mesh = source.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null)
                    {
                        continue;
                    }

                    GameObject part = new GameObject(source.name);
                    Transform t = part.transform;
                    t.SetParent(root.transform, false);

                    // Held relative to the ghost, so moving the root moves the whole piece.
                    t.localPosition = origin.InverseTransformPoint(source.transform.position);
                    t.localRotation = Quaternion.Inverse(origin.rotation) * source.transform.rotation;

                    Vector3 scale = source.transform.lossyScale;
                    t.localScale = new Vector3(
                        Safe(scale.x, originScale.x),
                        Safe(scale.y, originScale.y),
                        Safe(scale.z, originScale.z));

                    part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;

                    MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = source.sharedMaterials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                Previews.Add(root);
            }
        }

        private static float Safe(float value, float divisor)
        {
            return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
        }

        private static void DestroyPreviews()
        {
            foreach (GameObject preview in Previews)
            {
                if (preview != null)
                {
                    UnityEngine.Object.Destroy(preview);
                }
            }

            Previews.Clear();
            _previewsFor = null;
        }
    }
}
