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
            float dt,
            PieceTable ___m_buildPieces,
            ref int ___m_manualSnapPoint,
            ref float ___m_maxPlaceDistance,
            bool ___m_noPlacementCost,
            GameObject ___m_placementGhost)
        {
            // Before any early return: the rotation transpiler reads this verdict from
            // inside Valheim's own code, on frames where input is not being taken.
            BuildTool.Evaluate(___m_buildPieces);

            // The camera covers the hoe and cultivator as well as the hammer, so it sits
            // above the hammer-only gate below. Losing the tool entirely puts it away.
            if (ModConfig.IsEnabled && BuildTool.IsPlacementTool && takeInput
                && !Hud.IsPieceSelectionVisible())
            {
                BuildCamera.HandleInput(__instance);
                BuildCamera.Move(__instance, dt);
            }
            else if (!BuildTool.IsPlacementTool)
            {
                BuildCamera.Deactivate(__instance);
            }

            // Read every frame, on any tool, so putting the camera away or swapping to an axe
            // takes the lent effect back rather than leaving it running.
            Wisplight.Ensure(__instance);

            if (BuildTool.IsPlacementTool && takeInput && !Hud.IsPieceSelectionVisible())
            {
                DoorAccess.HandleUse(__instance);
            }

            // Both of these run whether the mod is on or off - the toggle has to, or there
            // would be no way back once it was off, and the glow has to so it can take itself
            // down. Neither belongs on a terrain tool, so both ask about the tool rather than
            // about the master switch.
            if (BuildTool.IsBuildingTool && takeInput
                && ___m_buildPieces != null && !Hud.IsPieceSelectionVisible())
            {
                HandleMasterToggle(__instance);
            }

            HammerGlow.Apply(__instance, ModConfig.IsEnabled && BuildTool.IsBuildingTool);

            if (!ModConfig.IsEnabled || !takeInput || ___m_buildPieces == null)
            {
                return;
            }

            // Before anything asks a station how far it reaches. Not inside the reach
            // extension, which a player may have switched off: the range another player set
            // is vanilla's own build permission, so it has to be right either way.
            StationRange.RefreshAll();

            PlacementEdit.Tick();

            if (PressedWithModifiers(ModConfig.EditKey.Value))
            {
                PlacementEdit.Toggle(__instance);
            }

            // Every frame rather than on a change: the covering station can change by
            // walking, and its range by another player adjusting it.
            PlacementReach.Apply(__instance, ref ___m_maxPlaceDistance);

            // Last frame's run, built here rather than inside PlacePiece so that other mods
            // patching it are not re-entered mid-call.
            Zooping.PlaceDue(__instance, ___m_noPlacementCost);

            if (!BuildTool.AppliesNow)
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
            SurfacePlacement.HandleInput(__instance);

            PlacementFreeze.HandleInput(__instance);
            PlacementGrid.HandleInput(__instance);
            HandleNudge(__instance);
            HandleUndo(__instance);
            HandleSnapPointReset(__instance, ref ___m_manualSnapPoint);
            HandleClippingToggle(__instance);
            HandleSnapDivisions(__instance);
            HandleDerivedModeCycle(__instance, ___m_placementGhost);
            HandleScaling(__instance, ___m_placementGhost);
            HandleResets();
            HandleStandaloneCopyKey(__instance);

            HandleBendKeys(__instance, ___m_placementGhost);

            // Before rotation, which otherwise consumes the wheel for yaw.
            if (!HandleStationRange(__instance) && !HandleBend(__instance, ___m_placementGhost))
            {
                HandleRotation();
            }
        }

        /// <summary>
        /// Moves the piece a step at a time, on all three axes.
        /// </summary>
        /// <remarks>
        /// Key repeat is left to the operating system rather than timed here, the way the
        /// scaling keys do it. Nudging is a deliberate, one-step-at-a-time action - you are
        /// lining something up by eye - so the usual hold-to-repeat would overshoot more
        /// often than it would help.
        /// </remarks>
        private static void HandleNudge(Player player)
        {
            if (Pressed(ModConfig.ResetOffsetKey.Value))
            {
                bool hadZoop = Zooping.IsActive;

                PlacementOffset.Reset();
                Zooping.Clear();

                Notify.Show(player, hadZoop ? "Zoop and offset cleared" : "Placement offset cleared");
                return;
            }

            // Checked first: holding the zoop modifier means these keys are laying a run,
            // not moving the piece.
            if (Held(ModConfig.ZoopModifierKey.Value))
            {
                HandleZoop(player);
                return;
            }

            bool large = Held(ModConfig.NudgeLargeModifierKey.Value);

            if (Pressed(ModConfig.NudgeForwardKey.Value))  PlacementOffset.NudgeBy(NudgeAxis.Forward, 1, large);
            if (Pressed(ModConfig.NudgeBackwardKey.Value)) PlacementOffset.NudgeBy(NudgeAxis.Forward, -1, large);
            if (Pressed(ModConfig.NudgeRightKey.Value))    PlacementOffset.NudgeBy(NudgeAxis.Lateral, 1, large);
            if (Pressed(ModConfig.NudgeLeftKey.Value))     PlacementOffset.NudgeBy(NudgeAxis.Lateral, -1, large);
            if (Pressed(ModConfig.NudgeUpKey.Value))       PlacementOffset.NudgeBy(NudgeAxis.Vertical, 1, large);
            if (Pressed(ModConfig.NudgeDownKey.Value))     PlacementOffset.NudgeBy(NudgeAxis.Vertical, -1, large);
        }

        /// <summary>Builds up a run of pieces along one of the nudge directions.</summary>
        private static void HandleZoop(Player player)
        {
            Camera camera = MainCamera.Get();
            if (camera == null)
            {
                return;
            }

            Vector3 forward = camera.transform.forward;
            forward.y = 0f;
            forward = forward.normalized;

            // Quantised the same way the nudge is, so a run laid from any angle follows a
            // world axis and lines up with everything else built that way.
            forward = Mathf.Abs(forward.x) > Mathf.Abs(forward.z)
                ? new Vector3(Mathf.Sign(forward.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(forward.z));

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            if (Pressed(ModConfig.NudgeForwardKey.Value))  Zooping.Extend(player, forward);
            if (Pressed(ModConfig.NudgeBackwardKey.Value)) Zooping.Extend(player, -forward);
            if (Pressed(ModConfig.NudgeRightKey.Value))    Zooping.Extend(player, right);
            if (Pressed(ModConfig.NudgeLeftKey.Value))     Zooping.Extend(player, -right);
            if (Pressed(ModConfig.NudgeUpKey.Value))       Zooping.Extend(player, Vector3.up);
            if (Pressed(ModConfig.NudgeDownKey.Value))     Zooping.Extend(player, Vector3.down);
        }

        /// <summary>Turns the whole mod on or off, and says which.</summary>
        private static void HandleMasterToggle(Player player)
        {
            if (!PressedWithModifiers(ModConfig.MasterToggleKey.Value))
            {
                return;
            }

            // Written to the setting rather than held in a field, so the choice survives a
            // restart and reads correctly in the config file.
            ModConfig.Enabled.Value = !ModConfig.Enabled.Value;

            HammerOfOdenPlugin.Info(
                $"Master toggle: the mod is now {(ModConfig.Enabled.Value ? "on" : "off")}.");

            Notify.Show(player, ModConfig.Enabled.Value
                ? "The Hammer of Oden: on"
                : "The Hammer of Oden: off");
        }

        private static void HandleUndo(Player player)
        {
            if (PressedWithModifiers(ModConfig.UndoKey.Value))
            {
                PlacementUndo.UndoLast(player);
            }
        }

        /// <summary>
        /// A shortcut including its modifiers, unlike the rest of the bindings here.
        /// </summary>
        /// <remarks>
        /// Everything else in this mod uses a bare key or a key plus a modifier it owns
        /// outright, so reading MainKey alone is enough. Undo is the one binding where the
        /// modifier is the whole point: Z on its own would fire while walking.
        /// </remarks>
        private static bool PressedWithModifiers(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey, true))
            {
                return false;
            }

            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier, true))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Pressed(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && ZInput.GetKeyDown(shortcut.MainKey, true);
        }

        private static bool Held(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
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

        /// <summary>
        /// Changes a nearby station's build range with the wheel.
        /// </summary>
        /// <returns>True if the wheel was used for this, so rotation should leave it alone.</returns>
        private static void HandleBendKeys(Player player, GameObject ghost)
        {
            if (Pressed(ModConfig.BendAxisKey.Value))
            {
                BendState.CycleRise(player, ghost);
            }

            if (Pressed(ModConfig.BendResetKey.Value))
            {
                BendState.Reset(player, true);
            }
        }

        /// <summary>
        /// Bends the piece in hand while the modifier is held.
        /// </summary>
        /// <returns>True if the wheel was used for this, so rotation should leave it alone.</returns>
        private static bool HandleBend(Player player, GameObject ghost)
        {
            if (!IsHeld(ModConfig.BendModifierKey))
            {
                return false;
            }

            // Held means the wheel is spoken for, movement or not, so a held modifier cannot
            // rotate the piece on a still frame.
            BendState.HandleScroll(player, ghost, ZInput.GetMouseScrollWheel());
            return true;
        }

        private static bool HandleStationRange(Player player)
        {
            if (!IsHeld(ModConfig.StationRangeKey))
            {
                return false;
            }

            float scroll = ZInput.GetMouseScrollWheel();
            if (scroll == 0f)
            {
                // The modifier is down, so the wheel is spoken for even on a frame with no
                // movement. Returning true keeps a held modifier from rotating the piece.
                return true;
            }

            StationRange.Adjust(player, Mathf.RoundToInt(Mathf.Sign(scroll)));
            return true;
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
            return ModConfig.IsEnabled && BuildTool.AppliesNow ? RotationState.Current : vanilla;
        }

        /// <summary>Called from patched IL, receiving whether AltPlace is physically held.</summary>
        internal static bool SubstituteFreePlacement(bool vanillaHeld)
        {
            return BuildTool.AppliesNow ? FreePlacement.IsActive(vanillaHeld) : vanillaHeld;
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
            if (!BuildTool.AppliesNow)
            {
                return;
            }

            // Snapping and the depth offset both move the ghost, which would undo the
            // surface placement decided moments ago in the rules postfix. On a surface the
            // aimed-at point is the answer, so neither gets a say.
            bool onSurface = SurfacePlacement.AppliedThisFrame || PlacementFreeze.IsFrozen;

            if (!onSurface)
            {
                // Before the markers, so they are drawn where the piece actually ends up.
                TargetSnapping.Apply(
                    __instance,
                    ___m_placementGhost,
                    ___m_manualSnapPoint,
                    FreePlacement.IsActiveNow());
            }

            if (Scalable.Allows(___m_placementGhost))
            {
                ScaleState.ApplyTo(___m_placementGhost);
                BendState.ApplyTo(___m_placementGhost);
            }

            // Deliberately not gated on the above: the nudge is the only way to move a
            // frozen piece, and on a surface it is how you lift a piece clear of it.
            PlacementOffset.Apply(___m_placementGhost);

            // After every other positioning step, so the run follows the piece wherever it
            // has ended up rather than where vanilla first put it.
            Zooping.UpdatePreview(___m_placementGhost);

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
            HammerOfOdenPlugin.Debug($"Copied full rotation from '{hovering.name}' on piece copy.");
        }
    }

    /// <summary>
    /// Lays the piece against a surface, then settles what the placement verdict should be.
    /// </summary>
    /// <remarks>
    /// Separate from the visual postfix so its ordering is independent: the verdict must be
    /// settled before anything decides what to draw.
    ///
    /// Both live here rather than in two patches because their order matters and Harmony
    /// does not promise one between patches of equal priority. Surface placement moves the
    /// ghost; the verdict then has to account for it.
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
            if (!BuildTool.AppliesNow)
            {
                return;
            }

            // Order matters. The surface decides a position from what you are aiming at;
            // the grid rounds that off; freezing then overrides both with the position you
            // pinned, which is why it comes last and why the grid skips a frozen piece.
            bool onSurface = SurfacePlacement.Apply(__instance, ___m_placementGhost);

            if (!onSurface && !PlacementFreeze.IsFrozen)
            {
                PlacementGrid.Apply(___m_placementGhost);
            }

            bool frozen = PlacementFreeze.Apply(___m_placementGhost);

            PlacementSource source = frozen
                ? PlacementSource.Frozen
                : onSurface ? PlacementSource.Surface : PlacementSource.Aim;

            PlacementRules.Apply(__instance, ref ___m_placementStatus, ___m_placementGhost, source);
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
    /// Flies the camera instead of following the player.
    /// </summary>
    /// <remarks>
    /// A prefix that skips vanilla outright rather than a postfix that moves the camera
    /// afterwards: UpdateCamera does collision and smoothing work on its way to a position
    /// that is then thrown away, and the smoothing fights anything written after it.
    /// </remarks>
    [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
    internal static class GameCameraUpdatePatch
    {
        private static readonly MethodInfo VanillaScroll = AccessTools.Method(
            typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));

        private static readonly MethodInfo Substitute = AccessTools.Method(
            typeof(GameCameraUpdatePatch), nameof(SubstituteScroll));

        [HarmonyPrefix]
        private static bool Prefix(GameCamera __instance)
        {
            if (!ModConfig.IsEnabled || !BuildCamera.IsActive)
            {
                return true;
            }

            BuildCamera.ApplyTo(__instance);
            return false;
        }

        /// <summary>
        /// Hides the wheel from the camera while it is being used for something else.
        /// </summary>
        /// <remarks>
        /// With a piece selected the camera already ignores the wheel, so adjusting a
        /// station's range looks right. With only the hammer in hand and nothing selected
        /// there is no ghost, vanilla stops suppressing the zoom, and the same gesture walks
        /// the camera in and out while it changes the range.
        ///
        /// Intercepting the read rather than putting the distance back afterwards: the
        /// camera's position is computed from the distance inside this very method, so a
        /// restore after the fact still shows one frame of the zoom before snapping back.
        /// </remarks>
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
            int patched = 0;

            // Backwards: inserting shifts every later index.
            for (int i = codes.Count - 1; i >= 0; i--)
            {
                bool isScroll = (codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt)
                    && codes[i].operand is MethodInfo called
                    && called == VanillaScroll;

                if (!isScroll)
                {
                    continue;
                }

                codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, Substitute));
                patched++;
            }

            if (patched == 0)
            {
                HammerOfOdenPlugin.Error(
                    "Could not find the camera's scroll wheel read, so the camera will zoom "
                    + "while changing a station's build range. Nothing else is affected.");
            }
            else
            {
                HammerOfOdenPlugin.Debug($"Camera zoom: intercepted {patched} wheel read(s).");
            }

            return codes;
        }

        /// <summary>Called from patched IL, receiving the wheel movement vanilla read.</summary>
        internal static float SubstituteScroll(float vanilla)
        {
            if (HelpPanel.IsOpen)
            {
                return 0f;
            }

            if (!ModConfig.IsEnabled || !BuildTool.IsBuildingTool)
            {
                return vanilla;
            }

            return ClaimsWheel(ModConfig.StationRangeKey.Value)
                || ClaimsWheel(ModConfig.BendModifierKey.Value)
                ? 0f
                : vanilla;
        }

        private static bool ClaimsWheel(KeyboardShortcut shortcut)
        {
            return shortcut.MainKey != KeyCode.None && ZInput.GetKey(shortcut.MainKey, true);
        }
    }

    /// <summary>
    /// Holds the character still while the camera is away or the help panel is open.
    /// </summary>
    /// <remarks>
    /// This gates movement and looking only. Placement input is read elsewhere, so you can
    /// still build - which is the point of flying the camera somewhere in the first place.
    /// </remarks>
    [HarmonyPatch(typeof(PlayerController), "TakeInput")]
    internal static class PlayerControllerTakeInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (HelpPanel.IsOpen || (ModConfig.IsEnabled && BuildCamera.IsActive))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Keeps the cursor unlocked and visible while the help panel is open.
    /// </summary>
    [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
    internal static class GameCameraUpdateMouseCapturePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (HelpPanel.IsOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ZCursor.LockState = CursorLockMode.None;
                ZCursor.Show();
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Prevents player actions (attacks, building, interactions) while navigating the help panel.
    /// </summary>
    [HarmonyPatch(typeof(Player), "TakeInput")]
    internal static class PlayerTakeInputPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (HelpPanel.IsOpen)
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// Closes the help panel on Escape without popping open the Valheim pause menu.
    /// </summary>
    [HarmonyPatch(typeof(Menu), "Update")]
    internal static class MenuUpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix()
        {
            if (HelpPanel.IsOpen || HelpPanel.ClosedThisFrame)
            {
                if (HelpPanel.IsOpen && ZInput.GetKeyDown(KeyCode.Escape, true))
                {
                    HelpPanel.Close();
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Aims placement from the camera rather than from the player's head.
    /// </summary>
    /// <remarks>
    /// Valheim aims from Character.m_eye, so lending the placement update the camera's eye
    /// is the whole of it - no placement code needs to know the camera exists.
    ///
    /// A finalizer returns it, not a postfix, because a finalizer runs even when something
    /// inside the method throws. Leaving the eye behind the camera after an exception would
    /// put the player's attacks, interaction and hover text somewhere out in the air, and it
    /// would be a puzzle to trace back to here.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    internal static class PlayerUpdatePlacementEyePatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Transform ___m_eye)
        {
            if (ModConfig.IsEnabled)
            {
                BuildCamera.BorrowEye(___m_eye);
            }
        }

        [HarmonyFinalizer]
        private static void Finalizer(Transform ___m_eye)
        {
            BuildCamera.ReturnEye(___m_eye);
        }
    }

    /// <summary>
    /// Notes which anchor a piece was built by, so the next one of its kind matches.
    /// </summary>
    /// <remarks>
    /// Placing is the moment worth recording. Cycling with Q and E happens while looking for
    /// the right anchor and changes several times on the way; building with one is the point
    /// at which the choice was meant.
    ///
    /// A prefix rather than a postfix because the return value is not interesting here. A
    /// refused placement - no room, no materials - still tells us how you intended to hold
    /// the piece, and the ghost is rebuilt either way.
    ///
    /// The argument types are spelled out because Player has one PlacePiece and it is not
    /// the one you would guess: it takes the position and rotation as well as the piece.
    /// Naming a signature that does not exist makes Harmony throw, which costs this feature
    /// silently - it does not announce itself when it never runs.
    /// </remarks>
    [HarmonyPatch(typeof(Player), "PlacePiece", new[]
    {
        typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool)
    })]
    internal static class PlayerPlacePiecePatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            Vector3 pos,
            GameObject ___m_placementGhost,
            int ___m_manualSnapPoint)
        {
            if (!ModConfig.IsEnabled)
            {
                return;
            }

            SnapPointMemory.Remember(___m_placementGhost, ___m_manualSnapPoint);

            // A new group, unless this is one of the copies of a run already under way -
            // those belong to the group the run itself opened.
            if (!Zooping.IsPlacing)
            {
                PlacementUndo.BeginAction();
            }

            // Measured now, while the ghost still exists to measure.
            Zooping.Remember(___m_placementGhost, pos);
            BendState.Remember(___m_placementGhost);
        }

        [HarmonyPostfix]
        private static void Postfix(Player __instance, Piece piece, Quaternion rot, bool cheated)
        {
            if (!ModConfig.IsEnabled)
            {
                return;
            }

            // After the new piece exists, so whatever the old one was supporting has something
            // to hold on to before it goes. A zoop run is not an edit - only the first
            // placement replaces anything, and Zooping is what places the rest.
            if (!Zooping.IsPlacing)
            {
                PlacementEdit.CommitAfterPlacement(__instance);
            }

            Zooping.QueueRun(piece, rot, cheated);
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
                BendState.ApplyToPlaced(__instance);
                PlacementUndo.Record(__instance);
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
                BentPiece.Restore(__instance);
            }
        }
    }

    /// <summary>
    /// Puts a station's stored build range back as it comes into the world.
    /// </summary>
    /// <remarks>
    /// m_rangeBuild is a plain serialized field that vanilla never networks or saves, so a
    /// station always wakes up with its prefab's range and has to be corrected afterwards.
    ///
    /// Priority.Last because this is contested ground. Any mod offering a global build
    /// radius does it the same way - ValheimQoL postfixes this very method and writes its
    /// WorkBenchRange setting - and Harmony gives no order between two postfixes that both
    /// take the default priority, so whichever ran second won and it was not us. Running
    /// last settles it: a global setting is the base for stations nobody has adjusted, and
    /// a range stored on a particular station is a deliberate choice about that station, so
    /// it should be the one that survives.
    ///
    /// Stations with nothing stored are left alone, so another mod's default still applies
    /// everywhere it has not been overruled.
    /// </remarks>
    [HarmonyPatch(typeof(CraftingStation), "Start")]
    internal static class CraftingStationStartPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CraftingStation __instance)
        {
            if (ModConfig.IsEnabled)
            {
                StationRange.Restore(__instance);
            }
        }
    }

    /// <summary>Clear the free-placement toggle when leaving build mode.</summary>
    [HarmonyPatch(typeof(Player), "SetPlaceMode")]
    internal static class PlayerSetPlaceModePatch
    {
        private static bool _warnedAboutPatches;

        [HarmonyPostfix]
        private static void Postfix(
            Player __instance,
            PieceTable buildPieces,
            ref float ___m_maxPlaceDistance)
        {
            // On entering build mode, not at startup: a message during the loading screen is
            // a message nobody sees.
            if (buildPieces != null && !_warnedAboutPatches
                && HammerOfOdenPlugin.FailedPatches.Count > 0)
            {
                _warnedAboutPatches = true;
                Notify.Show(__instance,
                    $"Hammer of Oden: {HammerOfOdenPlugin.FailedPatches.Count} feature(s) "
                    + "disabled - see the log");
            }

            if (buildPieces == null)
            {
                PlacementReach.Restore(ref ___m_maxPlaceDistance);

                // Leaving build mode abandons the edit rather than finishing it. The ghost is
                // gone, so there is nothing left to place, and a remembered id would fire
                // against whatever was next placed instead.
                PlacementEdit.Cancel(null, null);
                FreePlacement.Reset();
                SurfacePlacement.Reset();
                PlacementFreeze.Reset();
                PlacementGrid.Reset();
                BuildCamera.Deactivate(Player.m_localPlayer);
                Wisplight.Remove();
                Zooping.Clear();
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
            if (!BuildTool.AppliesNow)
            {
                return;
            }

            // Rename first: the ordering below sorts on the new names, and AttachTo
            // measures positions rather than names so it is unaffected either way.
            SnapPointNaming.Apply(___m_placementGhost);

            DerivedSnapPoints.AttachTo(___m_placementGhost);

            // AttachTo orders them itself when it adds anchors; with derived anchors off
            // it returns early, and the piece's own points still deserve sorting.
            SnapPointOrder.Apply(___m_placementGhost);

            // After AttachTo, because the remembered anchor may be one we just created.
            //
            // Not while editing. Remembering an anchor is for laying a run of the same piece,
            // where the last one you used is almost certainly the one you want again. Editing
            // is the opposite: the piece already stands somewhere, you are adjusting it where
            // it is, and an anchor recalled from whatever you were building an hour ago drags
            // it somewhere else entirely. Automatic picks the nearest, which is at worst a
            // guess about a piece you are looking at rather than a guess about your history.
            if (PlacementEdit.IsEditing)
            {
                ___m_manualSnapPoint = -1;
                HammerOfOdenPlugin.Debug("Ghost rebuilt during an edit; snap anchor left automatic.");
            }
            else
            {
                SnapPointMemory.Restore(___m_placementGhost, ref ___m_manualSnapPoint);
                HammerOfOdenPlugin.Debug($"Ghost rebuilt; snap anchor recalled as {___m_manualSnapPoint}.");
            }

            PlacementOffset.Reset();
            PlacementFreeze.Reset();
            ScaleState.ForgetGhost();
            Scalable.Forget();
            BendState.ForgetGhost();
            Bendable.Forget();

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
