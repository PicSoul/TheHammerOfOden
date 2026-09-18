using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Reads rotation input. Runs before the vanilla placement update so the ghost is
    /// built this frame with whatever the player just asked for.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class PlayerUpdatePlacementPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player __instance, bool takeInput, PieceTable ___m_buildPieces)
        {
            if (!ModConfig.IsEnabled || !takeInput || ___m_buildPieces == null)
            {
                return;
            }

            // The build menu eats the scroll wheel; do not fight it.
            if (Hud.IsPieceSelectionVisible())
            {
                return;
            }

            FreePlacement.HandleInput(__instance);
            HandleClippingToggle(__instance);
            HandleResets();
            HandleStandaloneCopyKey(__instance);
            HandleRotation();
        }

        private static void HandleClippingToggle(Player player)
        {
            if (!IsDown(ModConfig.ClippingToggleKey))
            {
                return;
            }

            ClippingMode mode = Clipping.Cycle();

            if (player != null)
            {
                ((Character)player).Message(
                    MessageHud.MessageType.TopLeft,
                    "Clipping: " + Clipping.Describe(mode));
            }
        }

        private static void HandleRotation()
        {
            int sign = Mathf.RoundToInt(Mathf.Sign(ZInput.GetMouseScrollWheel()));
            if (ZInput.GetMouseScrollWheel() == 0f)
            {
                return;
            }

            RotationState.Rotate(CurrentAxis(), sign, ModConfig.StepDegrees);
        }

        private static void HandleResets()
        {
            if (IsDown(ModConfig.ResetAllKey))
            {
                RotationState.ResetAll();
                HammerOfOdenPlugin.Debug("Reset all rotation axes.");
                return;
            }

            if (IsDown(ModConfig.ResetAxisKey))
            {
                RotationAxis axis = CurrentAxis();
                RotationState.ResetAxis(axis);
                HammerOfOdenPlugin.Debug($"Reset {axis} axis.");
            }
        }

        private static void HandleStandaloneCopyKey(Player player)
        {
            if (!IsDown(ModConfig.CopyRotationKey))
            {
                return;
            }

            Piece hovering = player.GetHoveringPiece();
            if (hovering == null)
            {
                return;
            }

            RotationState.MatchPiece(hovering);
            HammerOfOdenPlugin.Debug($"Copied rotation from '{hovering.name}' via CopyRotationKey.");
        }

        /// <summary>Which axis the held modifier keys select. Yaw when nothing is held.</summary>
        internal static RotationAxis CurrentAxis()
        {
            if (IsHeld(ModConfig.XAxisKey))
            {
                return RotationAxis.X;
            }

            if (IsHeld(ModConfig.ZAxisKey))
            {
                return RotationAxis.Z;
            }

            return RotationAxis.Y;
        }

        // Only the main key is consulted for held modifiers: these are axis selectors,
        // not shortcuts, and a KeyboardShortcut's own modifier list would be redundant.
        private static bool IsHeld(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
        }

        private static bool IsDown(ConfigEntry<KeyboardShortcut> entry)
        {
            KeyboardShortcut shortcut = entry.Value;
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }
    }

    /// <summary>
    /// Substitutes our rotation for the yaw-only one vanilla computes.
    /// </summary>
    /// <remarks>
    /// This has to be a transpiler; a postfix genuinely cannot do the job.
    ///
    /// Vanilla computes the ghost rotation near the top of the method and then uses it
    /// for everything downstream: the manual snap point offset
    /// (position = point + rotation * -snapPoint.localPosition), and more importantly
    /// FindClosestSnapPoints, which locates the ghost's snap points in world space from
    /// its current rotation and then translates the ghost so a pair meets.
    ///
    /// Rotating in a postfix therefore swings the snap points away from the alignment
    /// vanilla just solved for, which reads in game as snapping being broken.
    ///
    /// The substitution is deliberately minimal. Vanilla contains exactly one
    /// Quaternion.Euler call in this method, so we insert a single call immediately
    /// after it taking a Quaternion and returning a Quaternion. The stack shape is
    /// unchanged and no branch targets or locals move, which keeps this about as
    /// conflict-tolerant as a transpiler can be.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class PlayerUpdatePlacementGhostPatch
    {
        private static readonly MethodInfo VanillaEuler = AccessTools.Method(
            typeof(Quaternion), nameof(Quaternion.Euler),
            new[] { typeof(float), typeof(float), typeof(float) });

        private static readonly MethodInfo Substitute = AccessTools.Method(
            typeof(PlayerUpdatePlacementGhostPatch), nameof(SubstituteRotation));

        private static readonly MethodInfo VanillaGetButton = AccessTools.Method(
            typeof(ZInput), nameof(ZInput.GetButton), new[] { typeof(string) });

        private static readonly MethodInfo SubstituteAltPlace = AccessTools.Method(
            typeof(PlayerUpdatePlacementGhostPatch), nameof(SubstituteFreePlacement));

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            PatchRotation(codes);
            PatchFreePlacement(codes);

            return codes;
        }

        private static void PatchRotation(List<CodeInstruction> codes)
        {
            int index = codes.FindIndex(code =>
                (code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt)
                && code.operand is MethodInfo called
                && called == VanillaEuler);

            if (index < 0)
            {
                HammerOfOdenPlugin.Error(
                    "Could not find the vanilla rotation call in Player.UpdatePlacementGhost. "
                    + "Valheim has probably changed. Rotation is left untouched so building still works.");
                return;
            }

            codes.Insert(index + 1, new CodeInstruction(OpCodes.Call, Substitute));
        }

        /// <summary>
        /// Routes every ZInput.GetButton("AltPlace") read inside this method through our own
        /// decision, so free placement can live on a key of its own instead of sharing Left
        /// Shift with the pitch modifier. Reads of AltPlace in other methods (copy piece,
        /// alt-interact) are untouched.
        /// </summary>
        private static void PatchFreePlacement(List<CodeInstruction> codes)
        {
            int patched = 0;

            // Walk backwards: inserting shifts every later index.
            for (int i = codes.Count - 1; i > 0; i--)
            {
                CodeInstruction code = codes[i];

                bool isGetButton = (code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt)
                    && code.operand is MethodInfo called
                    && called == VanillaGetButton;

                if (!isGetButton)
                {
                    continue;
                }

                if (codes[i - 1].opcode != OpCodes.Ldstr || (codes[i - 1].operand as string) != "AltPlace")
                {
                    continue;
                }

                codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, SubstituteAltPlace));
                patched++;
            }

            if (patched == 0)
            {
                HammerOfOdenPlugin.Error(
                    "Could not find the vanilla AltPlace checks in Player.UpdatePlacementGhost. "
                    + "Free placement falls back to Valheim's own Left Shift behaviour.");
                return;
            }

            HammerOfOdenPlugin.Debug($"Free placement: intercepted {patched} AltPlace check(s).");
        }

        /// <summary>Called from patched IL, receiving vanilla's yaw-only rotation.</summary>
        internal static Quaternion SubstituteRotation(Quaternion vanilla)
        {
            return ModConfig.IsEnabled ? RotationState.Current : vanilla;
        }

        /// <summary>Called from patched IL, receiving whether AltPlace is physically held.</summary>
        internal static bool SubstituteFreePlacement(bool vanillaHeld)
        {
            return FreePlacement.IsActive(vanillaHeld);
        }
    }

    /// <summary>
    /// Draws the rotation rings. Visual only: it reads the ghost and writes nothing vanilla
    /// looks at, so unlike the rotation itself this genuinely can be a postfix.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class PlayerUpdatePlacementGhostGizmoPatch
    {
        [HarmonyPostfix]
        private static void Postfix(
            Player __instance,
            GameObject ___m_placementGhost,
            int ___m_manualSnapPoint,
            List<Transform> ___m_tempSnapPoints1)
        {
            // Before the markers, so they are drawn where the piece actually ends up.
            TargetSnapping.Apply(
                __instance,
                ___m_placementGhost,
                ___m_manualSnapPoint,
                FreePlacement.IsActiveNow());

            RotationGizmo.Update(___m_placementGhost, PlayerUpdatePlacementPatch.CurrentAxis());
            SnapPointMarkers.Update(
                ___m_placementGhost,
                ___m_manualSnapPoint,
                ___m_tempSnapPoints1,
                __instance.GetHoveringPiece());
        }
    }

    /// <summary>
    /// Vanilla's copy-piece already runs only while AltPlace (Left Shift) is held, so
    /// hooking it gives shift + middle-click full-rotation copying for free, with no
    /// effect on a plain middle-click remove.
    /// </summary>
    [HarmonyPatch(typeof(Player), "CopyPiece")]
    internal static class PlayerCopyPiecePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, bool __result)
        {
            if (!ModConfig.IsEnabled || !ModConfig.CopyRotationOnPieceCopy.Value || !__result)
            {
                return;
            }

            Piece hovering = __instance.GetHoveringPiece();
            if (hovering == null)
            {
                return;
            }

            RotationState.MatchPiece(hovering);
            HammerOfOdenPlugin.Debug($"Copied full rotation from '{hovering.name}' on piece copy.");
        }
    }

    /// <summary>
    /// Appends our derived anchors to the placement ghost's snap points, so vanilla's own
    /// cycling and snapping pick them up without us reimplementing either.
    /// </summary>
    // Piece has two GetSnapPoints overloads - an instance one and a static radius search -
    // so the argument types are required or Harmony cannot tell them apart.
    [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), new[] { typeof(List<Transform>) })]
    internal static class PieceGetSnapPointsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance, List<Transform> points)
        {
            DerivedSnapPoints.Append(__instance, points);
        }
    }

    /// <summary>
    /// Lets pieces be placed intersecting other objects.
    /// </summary>
    /// <remarks>
    /// Vanilla marks a placement invalid when the ghost penetrates another collider by more
    /// than 0.2m, but only for pieces whose prefab sets m_noClipping. Because the test
    /// itself lives on Player rather than on the piece, overriding its result covers every
    /// piece in the game, including ones added by other mods and ones that do not exist yet.
    ///
    /// Reporting "not clipping" rather than skipping the caller keeps the change to exactly
    /// one decision: nothing else vanilla does with the result is affected.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "TestGhostClipping")]
    internal static class PlayerTestGhostClippingPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!Clipping.IsAllowed())
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    /// <summary>Clear the free-placement toggle when leaving build mode.</summary>
    [HarmonyPatch(typeof(Player), "SetPlaceMode")]
    internal static class PlayerSetPlaceModePatch
    {
        [HarmonyPostfix]
        private static void Postfix(PieceTable buildPieces)
        {
            if (buildPieces == null)
            {
                FreePlacement.Reset();
                RotationGizmo.Hide();
                SnapPointMarkers.Hide();
            }
        }
    }

    /// <summary>Optionally zero rotation when the selected build piece changes.</summary>
    [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
    internal static class PlayerSetupPlacementGhostPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            DerivedSnapPoints.Invalidate();

            if (!ModConfig.IsEnabled || !ModConfig.ResetOnPieceChange.Value)
            {
                return;
            }

            RotationState.ResetAll();
        }
    }
}
