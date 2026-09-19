using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    internal enum SurfacePlacementMode
    {
        /// <summary>Never. Pieces stand up the way Valheim intends.</summary>
        Off = 0,

        /// <summary>Hold the key to lay a piece against whatever you are looking at.</summary>
        Hold = 1,

        /// <summary>Tap the key to turn it on and off.</summary>
        Toggle = 2,

        /// <summary>
        /// Whenever the piece is already pitched or rolled, with no key of its own.
        /// </summary>
        /// <remarks>
        /// How Flip It does it. Tilting a piece is taken as the statement of intent, which
        /// costs no key and is one fewer thing to remember, at the price of not being able
        /// to lay an untilted piece flat against a wall.
        /// </remarks>
        WhenTilted = 3
    }

    internal enum SurfaceTargets
    {
        /// <summary>
        /// Everything except the hammer's Build and Heavy Build tabs.
        /// </summary>
        /// <remarks>
        /// Walls, floors and beams already meet each other through snap points, which is
        /// more precise than anything a surface normal can offer, and aligning them to a
        /// surface fights that. Portals, furniture, torches and signs have no such system
        /// and are what this is for.
        /// </remarks>
        NonStructural = 0,

        /// <summary>Structural pieces too, for building against terrain.</summary>
        Everything = 1
    }

    /// <summary>
    /// Lays a piece flat against whatever you are looking at - wall, ceiling or slope.
    /// </summary>
    /// <remarks>
    /// Vanilla places every piece upright and resting on the ground under your cursor. This
    /// takes the surface you are aiming at, turns the piece so its base sits against that
    /// surface, and slides it back until it touches.
    ///
    /// Two details carry most of the behaviour:
    ///
    /// The twist around the surface normal is derived from the up-slope direction, not from
    /// where you are standing. Quaternion.FromToRotation(Vector3.up, normal) is the obvious
    /// way to align to a surface and is the wrong one: it gives the minimal arc between the
    /// two vectors, which leaves the rotation about the normal unspecified, so a piece on a
    /// wall visibly spins as you strafe along it. Building the basis from the normal and a
    /// second, independent direction pins it down. The same reasoning is in RotationGizmo.
    ///
    /// The piece is positioned by its collider rather than its pivot. Valheim's pivots sit
    /// wherever the artist left them - a torch's at its base, a rug's in the middle - so
    /// putting the pivot on the surface buries half of some pieces and floats others. The
    /// ghost is pushed well clear along the normal, its nearest collider point to the target
    /// is measured, and it is then brought back by that difference, which lands the piece's
    /// own surface on the target surface whatever its pivot is doing. That trick is Flip
    /// It's, and it is the right one.
    /// </remarks>
    internal static class SurfacePlacement
    {
        /// <summary>Far enough that the whole ghost clears any surface before measuring.</summary>
        private const float MeasureLift = 50f;

        private delegate bool PieceRayTestCall(
            Player player,
            out Vector3 point,
            out Vector3 normal,
            out Piece piece,
            out Heightmap heightmap,
            out Collider waterSurface,
            bool water);

        private static readonly PieceRayTestCall RayTest = ResolveRayTest();

        /// <summary>
        /// Binds Valheim's own ray test, which knows the layers, the reach and the water
        /// rules that a hand-rolled raycast would have to guess at.
        /// </summary>
        /// <remarks>
        /// A delegate rather than MethodInfo.Invoke: this runs every frame while a ghost
        /// exists, and Invoke would box six arguments into a fresh object[] each time.
        ///
        /// Resolved defensively because the signature is private and could change. A
        /// failure here disables surface placement and says so, in keeping with patches
        /// being applied one class at a time so that one break costs one feature.
        /// </remarks>
        private static PieceRayTestCall ResolveRayTest()
        {
            try
            {
                MethodInfo method = AccessTools.Method(typeof(Player), "PieceRayTest");
                if (method == null)
                {
                    HammerOfOdenPlugin.Error(
                        "Player.PieceRayTest was not found, so surface placement is disabled. "
                        + "Valheim has probably changed. Everything else is unaffected.");
                    return null;
                }

                return AccessTools.MethodDelegate<PieceRayTestCall>(method);
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error(
                    "Could not bind Player.PieceRayTest, so surface placement is disabled. "
                    + ex.Message);
                return null;
            }
        }

        private static bool _toggledOn;

        // Colliders are cached per ghost: the ghost is rebuilt whenever the piece changes,
        // so the array is a constant for as long as one is held, and GetComponentsInChildren
        // allocates every time it is called.
        private static GameObject _cachedGhost;
        private static Collider[] _cachedColliders;

        private static Vector3 _lastNormal = Vector3.up;
        private static bool _appliedThisFrame;

        /// <summary>The surface normal used on the most recent frame.</summary>
        internal static Vector3 LastNormal => _lastNormal;

        /// <summary>
        /// Whether the ghost was laid against a surface on the most recent update.
        /// </summary>
        /// <remarks>
        /// Read by the visual postfix, which runs later in the same frame and would
        /// otherwise move the ghost again through snapping or the depth offset.
        /// </remarks>
        internal static bool AppliedThisFrame => _appliedThisFrame;

        internal static bool IsActiveNow()
        {
            if (!ModConfig.IsEnabled)
            {
                return false;
            }

            switch (ModConfig.SurfaceMode.Value)
            {
                case SurfacePlacementMode.Hold:
                    return IsKeyHeld();
                case SurfacePlacementMode.Toggle:
                    return _toggledOn;
                case SurfacePlacementMode.WhenTilted:
                    return RotationState.IsTilted;
                default:
                    return false;
            }
        }

        /// <summary>Called once per placement update to service the toggle.</summary>
        internal static void HandleInput(Player player)
        {
            if (ModConfig.SurfaceMode.Value != SurfacePlacementMode.Toggle || !IsKeyDown())
            {
                return;
            }

            _toggledOn = !_toggledOn;
            HammerOfOdenPlugin.Debug($"Surface placement toggled {(_toggledOn ? "on" : "off")}.");

            Notify.Show(player, "Surface placement: " + (_toggledOn ? "on" : "off"));
        }

        /// <summary>Drop the toggle when leaving build mode, so it never surprises you later.</summary>
        internal static void Reset()
        {
            _toggledOn = false;
            _cachedGhost = null;
            _cachedColliders = null;
            _lastNormal = Vector3.up;
            _appliedThisFrame = false;
        }

        /// <summary>Whether this piece may be laid against a surface at all.</summary>
        internal static bool Allows(GameObject ghost)
        {
            if (ghost == null)
            {
                return false;
            }

            Piece piece = ghost.GetComponent<Piece>();
            if (piece == null)
            {
                return false;
            }

            // Water pieces carry their own height rules and read as floating nonsense when
            // laid against anything; terrain pieces reshape the ground rather than sit on it.
            if (piece.m_waterPiece || piece.m_groundPiece)
            {
                return false;
            }

            if (ModConfig.SurfaceTarget.Value == SurfaceTargets.Everything)
            {
                return true;
            }

            return piece.m_category != Piece.PieceCategory.BuildingWorkbench
                && piece.m_category != Piece.PieceCategory.BuildingStonecutter;
        }

        /// <summary>
        /// Moves and turns the ghost to sit against the aimed-at surface.
        /// </summary>
        /// <returns>True if the ghost was placed on a surface, so callers can skip snapping.</returns>
        internal static bool Apply(Player player, GameObject ghost)
        {
            _appliedThisFrame = false;

            if (player == null || ghost == null || RayTest == null)
            {
                return false;
            }

            if (!IsActiveNow() || !Allows(ghost))
            {
                return false;
            }

            if (!RayTest(player, out Vector3 point, out Vector3 normal,
                    out Piece _, out Heightmap _, out Collider _, false))
            {
                return false;
            }

            normal = normal.normalized;
            if (normal.sqrMagnitude < 0.001f)
            {
                return false;
            }

            _lastNormal = normal;

            // One rule hides the ghost outright before bailing out; bring it back.
            if (!ghost.activeSelf)
            {
                ghost.SetActive(true);
            }

            // Before measuring, not after. Colliders are queried in world space, so a scale
            // applied later in the frame would move the piece's surface after we had already
            // worked out where to put it, and a stretched piece would hover or sink.
            if (Scalable.Allows(ghost))
            {
                ScaleState.ApplyTo(ghost);
            }

            ghost.transform.rotation = OrientationFor(normal);
            PositionAgainst(ghost, point, normal);

            _appliedThisFrame = true;
            return true;
        }

        /// <summary>
        /// The ghost's rotation: the surface's own frame, with your rotation applied within it.
        /// </summary>
        /// <remarks>
        /// LookRotation's second argument becomes the piece's up, so passing the surface
        /// normal stands the piece off the surface. Its first argument fixes the twist, and
        /// the up-slope direction is used because it depends only on the surface - a piece on
        /// a wall then keeps still while you walk past it.
        ///
        /// On level ground and on a flat ceiling there is no up-slope direction, and world
        /// forward is used instead. That is deliberately the same frame vanilla uses, so a
        /// piece laid on flat ground faces exactly where it would have anyway.
        /// </remarks>
        private static Quaternion OrientationFor(Vector3 normal)
        {
            // Off means the piece still moves to the surface but keeps the rotation you
            // set by hand, which is what you want when aiming a piece yourself.
            if (!ModConfig.AlignToSurface.Value)
            {
                return RotationState.Current;
            }

            Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (reference.sqrMagnitude < 0.0001f)
            {
                reference = Vector3.forward;
            }

            return Quaternion.LookRotation(reference.normalized, normal) * RotationState.Current;
        }

        /// <summary>
        /// Slides the ghost along the normal until its own geometry touches the surface.
        /// </summary>
        private static void PositionAgainst(GameObject ghost, Vector3 point, Vector3 normal)
        {
            Collider[] colliders = CollidersOf(ghost);
            float offset = ModConfig.SurfaceGap.Value;

            if (colliders.Length == 0)
            {
                ghost.transform.position = point + normal * offset;
                return;
            }

            // Measure from well clear of the surface: ClosestPoint on a collider that is
            // already intersecting the target returns the query point itself, which would
            // measure a gap of zero and leave the piece exactly where vanilla had it.
            ghost.transform.position = point + normal * MeasureLift;

            Vector3 nearest = Vector3.zero;
            float best = float.MaxValue;
            bool found = false;

            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                // A non-convex mesh collider cannot answer ClosestPoint and throws.
                if (collider is MeshCollider mesh && !mesh.convex)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(point);
                float distance = (closest - point).sqrMagnitude;

                if (distance < best)
                {
                    best = distance;
                    nearest = closest;
                    found = true;
                }
            }

            if (!found)
            {
                ghost.transform.position = point + normal * offset;
                return;
            }

            ghost.transform.position = point + (ghost.transform.position - nearest) + normal * offset;
        }

        private static Collider[] CollidersOf(GameObject ghost)
        {
            if (_cachedGhost == ghost && _cachedColliders != null)
            {
                return _cachedColliders;
            }

            _cachedGhost = ghost;
            _cachedColliders = ghost.GetComponentsInChildren<Collider>(true);
            return _cachedColliders;
        }

        private static bool IsKeyHeld()
        {
            KeyboardShortcut shortcut = ModConfig.SurfacePlacementKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
        }

        private static bool IsKeyDown()
        {
            KeyboardShortcut shortcut = ModConfig.SurfacePlacementKey.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }
    }
}
