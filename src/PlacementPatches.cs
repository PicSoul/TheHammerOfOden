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
        private static void Prefix(
            Player __instance,
            bool takeInput,
            PieceTable ___m_buildPieces,
            ref int ___m_manualSnapPoint)
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

            ActiveSnapPair.Clear();
            FreePlacement.HandleInput(__instance);
            HandleSnapPointReset(__instance, ref ___m_manualSnapPoint);
            HandleClippingToggle(__instance);
            HandleSnapDivisions(__instance);
            HandleResets();
            HandleStandaloneCopyKey(__instance);
            HandleRotation();
        }

        private static float _snapCycleHeldSince;
        private static bool _snapResetFired;

        /// <summary>
        /// Hold either snap-cycle key to jump straight back to automatic snapping.
        /// </summary>
        /// <remarks>
        /// Vanilla only steps one point at a time, so returning to auto from the middle of a
        /// long list means cycling all the way around - and derived anchors make that list
        /// much longer than vanilla ever intended.
        ///
        /// Vanilla cycles on key-down, so the first press still steps once before the hold
        /// registers. Suppressing that would mean knowing a tap from a hold before the key
        /// is released, which is not possible without delaying every tap.
        /// </remarks>
        private static void HandleSnapPointReset(Player player, ref int manualSnapPoint)
        {
            if (!ModConfig.HoldToResetSnapPoint.Value)
            {
                return;
            }

            bool held = ZInput.GetButton("TabLeft") || ZInput.GetButton("TabRight");

            if (!held)
            {
                _snapCycleHeldSince = 0f;
                _snapResetFired = false;
                return;
            }

            if (_snapCycleHeldSince == 0f)
            {
                _snapCycleHeldSince = Time.time;
                return;
            }

            if (_snapResetFired || Time.time - _snapCycleHeldSince < ModConfig.SnapPointResetHold.Value)
            {
                return;
            }

            // Fires once per press, whether or not there was anything to reset, so holding
            // does not repeatedly announce itself.
            _snapResetFired = true;

            if (manualSnapPoint == -1)
            {
                return;
            }

            manualSnapPoint = -1;
            HammerOfOdenPlugin.Debug("Snap point reset to auto by holding the cycle key.");

            if (player != null)
            {
                // Vanilla only announces a change it makes itself, and it samples the value
                // before we get here, so this one is ours to report.
                // Vanilla passes these tokens to Message unlocalised and lets the HUD resolve
                // them, so this reads identically to the game's own snapping message.
                ((Character)player).Message(
                    MessageHud.MessageType.Center,
                    "$msg_snapping $msg_snapping_auto");
            }
        }

        /// <summary>Double or halve the snap angles, so a few presses span the useful range.</summary>
        private static void HandleSnapDivisions(Player player)
        {
            int current = ModConfig.SnapDivisions.Value;
            int updated = current;

            if (IsDown(ModConfig.SnapIncreaseKey))
            {
                updated = Mathf.Min(512, current * 2);
            }
            else if (IsDown(ModConfig.SnapDecreaseKey))
            {
                updated = Mathf.Max(2, current / 2);
            }

            if (updated == current)
            {
                return;
            }

            ModConfig.SnapDivisions.Value = updated;

            if (player != null)
            {
                ((Character)player).Message(
                    MessageHud.MessageType.TopLeft,
                    $"Snap: {updated} per turn ({ModConfig.StepDegrees:0.##} deg)");
            }
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
            float scroll = ZInput.GetMouseScrollWheel();
            if (scroll == 0f)
            {
                return;
            }

            int sign = Mathf.RoundToInt(Mathf.Sign(scroll));

            // Both modifiers together means depth rather than rotation. Each is a single key
            // on its own, so the combination was previously unused.
            if (IsHeld(ModConfig.XAxisKey) && IsHeld(ModConfig.ZAxisKey))
            {
                PlacementOffset.Adjust(sign);
                HammerOfOdenPlugin.Debug($"Placement depth {PlacementOffset.Depth:0.##}m.");
                return;
            }

            RotationState.Rotate(CurrentAxis(), sign, ModConfig.StepDegrees);
        }

        private static void HandleResets()
        {
            if (IsDown(ModConfig.ResetAllKey))
            {
                RotationState.ResetAll();
                PlacementOffset.Reset();
                HammerOfOdenPlugin.Debug("Reset all rotation axes and placement depth.");
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

            PlacementOffset.Apply(___m_placementGhost);

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
    /// Relaxes vanilla's placement verdict while free placement is active.
    /// </summary>
    /// <remarks>
    /// Separate from the visual postfix so its ordering is independent: the verdict must be
    /// settled before anything decides what to draw.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class PlayerUpdatePlacementGhostRulesPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        private static void Postfix(
            Player __instance,
            ref Player.PlacementStatus ___m_placementStatus,
            GameObject ___m_placementGhost)
        {
            PlacementRules.Apply(__instance, ref ___m_placementStatus, ___m_placementGhost);
        }
    }

    /// <summary>
    /// Notes the snap pair vanilla settles on, so the markers can highlight it.
    /// </summary>
    /// <remarks>
    /// Out parameters are declared ref here, which is how Harmony exposes them to a patch.
    /// Read only: nothing is written back, so vanilla's decision stands untouched.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
    internal static class PlayerFindClosestSnapPointsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(bool __result, ref Transform a, ref Transform b)
        {
            if (__result)
            {
                ActiveSnapPair.Record(a, b);
            }
            else
            {
                ActiveSnapPair.Clear();
            }
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
                DerivedAnchorCache.Clear();
            }
        }
    }

    /// <summary>
    /// Rebuilds the derived anchors for a new ghost, and optionally zeroes rotation.
    /// </summary>
    /// <remarks>
    /// The ghost is rebuilt whenever the selected piece changes, which is exactly when the
    /// anchors need recreating for the new shape - and doing it here rather than from a
    /// patch on GetSnapPoints keeps us out of a method vanilla calls dozens of times a frame.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
    internal static class PlayerSetupPlacementGhostPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject ___m_placementGhost)
        {
            DerivedSnapPoints.AttachTo(___m_placementGhost);

            PlacementOffset.Reset();

            if (!ModConfig.IsEnabled || !ModConfig.ResetOnPieceChange.Value)
            {
                return;
            }

            RotationState.ResetAll();
        }
    }
}
