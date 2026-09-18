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
            ref int ___m_manualSnapPoint,
            GameObject ___m_placementGhost)
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
            HandleDerivedModeCycle(__instance, ___m_placementGhost);
            HandleScaling(__instance, ___m_placementGhost);
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

            Notify.Show(player, $"Snap: {updated} per turn ({ModConfig.StepDegrees:0.##} deg)");
        }

        /// <summary>Step through the derived anchor modes, wrapping back to Off.</summary>
        private static void HandleDerivedModeCycle(Player player, GameObject ghost)
        {
            if (!IsDown(ModConfig.CycleDerivedSnapPointsKey))
            {
                return;
            }

            DerivedSnapMode next = ModConfig.DerivedSnaps.Value + 1;
            if (next > DerivedSnapMode.Full)
            {
                next = DerivedSnapMode.Off;
            }

            ModConfig.DerivedSnaps.Value = next;

            // The ghost carries anchors built for the previous mode, and every cached piece
            // was measured against it, so both have to go.
            DerivedSnapPoints.AttachTo(ghost);
            DerivedAnchorCache.Clear();

            Notify.Show(player, "Anchors: " + Describe(next));
        }

        private static string Describe(DerivedSnapMode mode)
        {
            switch (mode)
            {
                case DerivedSnapMode.Centers:
                    return "centres";
                case DerivedSnapMode.CentersAndCorners:
                    return "centres + corners";
                case DerivedSnapMode.CentersCornersAndEdges:
                    return "centres + corners + edges";
                case DerivedSnapMode.Full:
                    return "full";
                default:
                    return "off (piece defaults only)";
            }
        }

        /// <summary>Stretch, compress and reset the piece's size.</summary>
        private static void HandleScaling(Player player, GameObject ghost)
        {
            if (!IsHeld(ModConfig.ScaleModifierKey))
            {
                return;
            }

            if (!Scalable.Allows(ghost))
            {
                // Say so once rather than silently ignoring the keypress, or it reads as the
                // feature being broken.
                if (AnyScaleKeyDown())
                {
                    Notify.Show(player, "This piece cannot be resized");
                }

                return;
            }

            bool changed = false;

            if (ScaleRepeat(ModConfig.ScaleWiderKey)) { ScaleState.Stretch(0, 1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleNarrowerKey)) { ScaleState.Stretch(0, -1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleTallerKey)) { ScaleState.Stretch(1, 1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleShorterKey)) { ScaleState.Stretch(1, -1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleDeeperKey)) { ScaleState.Stretch(2, 1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleShallowerKey)) { ScaleState.Stretch(2, -1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleUpKey)) { ScaleState.Uniform(1); changed = true; }
            else if (ScaleRepeat(ModConfig.ScaleDownKey)) { ScaleState.Uniform(-1); changed = true; }
            else if (IsDown(ModConfig.ScaleResetKey)) { ScaleState.Reset(); changed = true; }

            if (!changed || player == null)
            {
                return;
            }

            Vector3 s = ScaleState.Multiplier;
            Notify.Show(player, $"Scale  {s.x:0.00} x  {s.y:0.00} y  {s.z:0.00} z");
        }

        private static bool AnyScaleKeyDown()
        {
            return IsDown(ModConfig.ScaleWiderKey)
                || IsDown(ModConfig.ScaleNarrowerKey)
                || IsDown(ModConfig.ScaleTallerKey)
                || IsDown(ModConfig.ScaleShorterKey)
                || IsDown(ModConfig.ScaleDeeperKey)
                || IsDown(ModConfig.ScaleShallowerKey)
                || IsDown(ModConfig.ScaleUpKey)
                || IsDown(ModConfig.ScaleDownKey)
                || IsDown(ModConfig.ScaleResetKey);
        }

        private static float _scaleHeldSince;
        private static float _scaleNextRepeat;

        /// <summary>
        /// True on the first press, then again at a steady rate while the key is held.
        /// </summary>
        /// <remarks>
        /// Scaling is inherently repetitive - twenty presses to reach double size at the
        /// default step - so holding the key has to work or the feature is tiring to use.
        /// The initial pause is what keeps a single tap from being read as a hold.
        /// </remarks>
        private static bool ScaleRepeat(ConfigEntry<KeyboardShortcut> key)
        {
            if (IsDown(key))
            {
                _scaleHeldSince = Time.time;
                _scaleNextRepeat = Time.time + ModConfig.ScaleRepeatDelay.Value;
                return true;
            }

            if (!IsHeld(key))
            {
                return false;
            }

            if (Time.time < _scaleNextRepeat)
            {
                return false;
            }

            _scaleNextRepeat = Time.time + ModConfig.ScaleRepeatRate.Value;
            return true;
        }

        private static void HandleClippingToggle(Player player)
        {
            if (!IsDown(ModConfig.ClippingToggleKey))
            {
                return;
            }

            ClippingMode mode = Clipping.Cycle();

            Notify.Show(player, "Clipping: " + Clipping.Describe(mode));
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

            if (Scalable.Allows(___m_placementGhost))
            {
                ScaleState.ApplyTo(___m_placementGhost);
            }
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
            ScaleState.MatchPiece(hovering);
            SnapPointRecall.Record(hovering);
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

    /// <summary>
    /// Carries the placing scale onto the piece that actually gets built.
    /// </summary>
    /// <remarks>
    /// PlacePiece instantiates the real object from the prefab rather than from the ghost,
    /// so the ghost's scale is not inherited and has to be applied to the new instance.
    /// SetCreator is the first thing called on it, which makes this the earliest point the
    /// finished piece can be reached - and it runs before WearNTear.OnPlaced, so support is
    /// evaluated against the size it will actually be.
    /// </remarks>
    [HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
    internal static class PieceSetCreatorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            if (ModConfig.IsEnabled)
            {
                ScaleState.ApplyToPlaced(__instance);
            }
        }
    }

    /// <summary>
    /// Reapplies a stored scale as objects come into the world.
    /// </summary>
    /// <remarks>
    /// Vanilla only reads the stored scale when the prefab opted into scale syncing, which
    /// build pieces do not. Without this a scaled piece is correct until the zone unloads and
    /// then returns at its original size - including on walking away and back, not only on
    /// teleporting.
    /// </remarks>
    [HarmonyPatch(typeof(ZNetView), "Awake")]
    internal static class ZNetViewAwakePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZNetView __instance)
        {
            if (ModConfig.IsEnabled)
            {
                ScalePersistence.Restore(__instance);
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
                ScaleState.Reset();
                PlayerSetupPlacementGhostPatch.ForgetPiece();
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
        private static string _lastGhostPrefab;

        /// <summary>Forget the current piece, so the next one selected starts at normal size.</summary>
        internal static void ForgetPiece()
        {
            _lastGhostPrefab = null;
        }

        [HarmonyPostfix]
        private static void Postfix(GameObject ___m_placementGhost, ref int ___m_manualSnapPoint)
        {
            // Rename first: the ordering below sorts on the new names, and AttachTo
            // measures positions rather than names so it is unaffected either way.
            SnapPointNaming.Apply(___m_placementGhost);

            DerivedSnapPoints.AttachTo(___m_placementGhost);

            // AttachTo orders them itself when it adds anchors; with derived anchors off
            // it returns early, and the piece's own points still deserve sorting.
            SnapPointOrder.Apply(___m_placementGhost);

            // After AttachTo: the anchor we are looking for may be one we just created.
            SnapPointRecall.ApplyTo(___m_placementGhost, ref ___m_manualSnapPoint);

            PlacementOffset.Reset();
            ScaleState.ForgetGhost();
            Scalable.Forget();

            // The ghost is rebuilt after every placement as well as on changing piece, so
            // resetting here unconditionally threw the scale away the moment it was used.
            // Only a genuinely different piece should clear it.
            string prefab = (___m_placementGhost != null)
                ? Utils.GetPrefabName(___m_placementGhost)
                : null;

            if (ModConfig.ResetScaleOnPieceChange.Value && prefab != _lastGhostPrefab)
            {
                ScaleState.Reset();
            }

            _lastGhostPrefab = prefab;

            if (!ModConfig.IsEnabled || !ModConfig.ResetOnPieceChange.Value)
            {
                return;
            }

            RotationState.ResetAll();
        }
    }
}
