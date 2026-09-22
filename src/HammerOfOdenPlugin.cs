using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using System.Reflection;
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;

namespace TheHammerOfOden
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class HammerOfOdenPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.pics0ul.valheim.thehammerofoden";
        public const string PluginName = "The Hammer of Oden";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource _logger;
        private Harmony _harmony;

        /// <summary>
        /// Lets a server decide the settings that govern what the world allows, and leaves the
        /// rest to each player.
        /// </summary>
        /// <remarks>
        /// ModRequired is true because a client without this mod does not see the same world.
        /// Rotation, position and clipping all travel as ordinary networked state, so those are
        /// safe - but a piece's scale does not. ZNetView.Awake only reads the stored scale when
        /// m_syncInitialScale is set, that flag comes from the prefab, and on a building piece
        /// it is false. This mod sets it after reading the value back itself, so the size is
        /// correct wherever the mod is installed and silently wrong where it is not: a wall you
        /// see as twice its height is a normal wall to them, collider included. They would walk
        /// through what you built, or into nothing at all.
        ///
        /// Refusing entry is the blunt answer and the honest one. The alternative is a world
        /// whose geometry depends on who is looking at it.
        ///
        /// It also means a version mismatch is a kick, so a new build has to reach the server
        /// and the players together.
        ///
        /// On a server without the mod, and in single player, nothing is pushed and every value
        /// stays exactly as the config file has it.
        /// </remarks>
        private readonly ConfigSync _configSync = new ConfigSync(PluginGuid)
        {
            DisplayName = PluginName,
            CurrentVersion = PluginVersion,
            MinimumRequiredVersion = PluginVersion,
            ModRequired = true
        };

        /// <summary>
        /// Patches that did not apply, so the player can be told rather than left guessing.
        /// </summary>
        /// <remarks>
        /// Patching class by class means a bad target costs one feature instead of the whole
        /// mod, which is the right trade - but it makes the failure quiet, and quiet is how a
        /// wrong method name survives. Twice now a feature has been written, shipped and
        /// tested while its patch was never applied at all: once naming a PlacePiece overload
        /// that does not exist, once patching CraftingStation.Awake, which does not exist
        /// either. Both times the log said so plainly and neither of us read it.
        ///
        /// So it is said on screen instead, the first time you pick up a hammer.
        /// </remarks>
        internal static readonly List<string> FailedPatches = new List<string>();

        private void Awake()
        {
            _logger = Logger;

            ModConfig.Bind(Config, _configSync);

            _harmony = new Harmony(PluginGuid);
            ApplyPatches();
            LogActiveFeatures();
            WarnAboutKnownConflicts();
        }

        /// <summary>
        /// Apply each patch class on its own, rather than with PatchAll.
        /// </summary>
        /// <remarks>
        /// PatchAll is all-or-nothing: one bad patch target throws and nothing gets applied,
        /// so a mistake in a minor feature silently disables rotation, snapping and
        /// everything else. Patching class by class costs a few lines and means a failure
        /// takes out only the feature it belongs to, and says which one.
        /// </remarks>
        private void ApplyPatches()
        {
            int applied = 0;
            int failed = 0;

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: true).Length == 0)
                {
                    continue;
                }

                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    FailedPatches.Add(type.Name);
                    Logger.LogError(
                        $"Patch '{type.Name}' could not be applied, so that feature is disabled. "
                        + "The rest of the mod is unaffected.");
                    Logger.LogError(ex.Message);
                }
            }

            if (failed == 0)
            {
                Logger.LogInfo($"{PluginName} {PluginVersion} loaded; {applied} patches applied.");
            }
            else
            {
                Logger.LogWarning(
                    $"{PluginName} {PluginVersion} loaded with {applied} of {applied + failed} patches applied. "
                    + "See the errors above for what is missing.");
            }
        }

        /// <summary>
        /// Print what is actually switched on, at Info level.
        /// </summary>
        /// <remarks>
        /// A failed install leaves an older DLL in place, and the symptom is a feature that
        /// "does not work" rather than anything obviously wrong. Stating the running
        /// configuration outright turns that into a five second check, and gives anyone
        /// reporting a bug something worth pasting.
        /// </remarks>
        private void LogActiveFeatures()
        {
            Logger.LogInfo(
                $"  rotation: {ModConfig.SnapDivisions.Value} divisions per 180 deg, "
                + $"pitch={ModConfig.XAxisKey.Value.MainKey}, roll={ModConfig.ZAxisKey.Value.MainKey}, "
                + $"reset={ModConfig.ResetAxisKey.Value.MainKey}/{ModConfig.ResetAllKey.Value.MainKey}");

            Logger.LogInfo(
                $"  free placement: {ModConfig.FreePlacement.Value} on "
                + $"{ModConfig.FreePlacementKey.Value.MainKey}, freedom={ModConfig.Freedom.Value}");

            Logger.LogInfo(
                $"  surface placement: {ModConfig.SurfaceMode.Value} on "
                + $"{ModConfig.SurfacePlacementKey.Value.MainKey}, applies to "
                + $"{ModConfig.SurfaceTarget.Value}, align={ModConfig.AlignToSurface.Value}");

            Logger.LogInfo(
                $"  freeze: {ModConfig.FreezeKey.Value.MainKey}, "
                + $"nudge={ModConfig.NudgeStep.Value}m/{ModConfig.NudgeStepLarge.Value}m, "
                + $"grid={ModConfig.GridKey.Value.MainKey} at {ModConfig.GridSize.Value}m");

            Logger.LogInfo(
                $"  station range: {ModConfig.StationRangeKey.Value.MainKey}+wheel, "
                + $"step={ModConfig.StationRangeStep.Value}m, "
                + $"extendReach={ModConfig.ExtendReachToStation.Value} "
                + $"capped at {ModConfig.ReachLimit.Value}m");

            Logger.LogInfo(
                $"  zoop: {ModConfig.ZoopModifierKey.Value.MainKey}+direction, "
                + $"limit={ModConfig.ZoopLimit.Value}, spacing={ModConfig.ZoopSpacing.Value}x");

            Logger.LogInfo(
                $"  undo: {ModConfig.UndoKey.Value}, depth={ModConfig.UndoDepth.Value}");

            Logger.LogInfo(
                $"  master toggle: {ModConfig.MasterToggleKey.Value.MainKey}, "
                + $"glow={ModConfig.ShowHammerGlow.Value}");

            Logger.LogInfo(
                $"  build camera: {ModConfig.BuildCameraKey.Value.MainKey}, "
                + $"speed={ModConfig.CameraSpeed.Value}m/s, range={ModConfig.CameraRange.Value}m, "
                + $"light={ModConfig.CameraLight.Value}");

            Logger.LogInfo($"  clipping: {ModConfig.Clipping.Value}");

            Logger.LogInfo(
                $"  snap points: display={ModConfig.SnapDisplay.Value}, "
                + $"derived={ModConfig.DerivedSnaps.Value}, "
                + $"snapToDerivedTargets={ModConfig.SnapToDerivedTargets.Value}, "
                + $"seeThrough={ModConfig.SnapPointsSeeThrough.Value}");

            Logger.LogInfo(
                $"  gizmo: {(ModConfig.ShowGizmo.Value ? "on" : "off")}, "
                + $"placement offset step={ModConfig.OffsetStep.Value}m");
        }

        /// <summary>
        /// Watches for the mist diagnostic key.
        /// </summary>
        /// <remarks>
        /// On the plugin rather than in the placement patches because the question it answers
        /// is about holding an axe or a torch, neither of which is a build tool.
        /// </remarks>
        private void Update()
        {
            if (Player.m_localPlayer != null)
            {
                UpgradeGlowFix.Apply(Player.m_localPlayer);
                DoorAccess.HandleToggle(Player.m_localPlayer);
                DoorAccess.AutoOpen(Player.m_localPlayer);
                DoorAccess.AutoClose(Player.m_localPlayer);
            }

            KeyboardShortcut help = ModConfig.HelpKey?.Value ?? default(KeyboardShortcut);

            // Ctrl held means the game, not us. Valheim hides the HUD on Ctrl+F3, and a bare
            // key that also fires as part of somebody else's combination is the kind of conflict
            // that gets blamed on whichever mod the player installed most recently.
            bool claimedByGame = ZInput.instance != null
                && (ZInput.GetKey(KeyCode.LeftControl, true) || ZInput.GetKey(KeyCode.RightControl, true))
                && help.Modifiers != null
                && !new List<KeyCode>(help.Modifiers).Contains(KeyCode.LeftControl);

            if (help.MainKey != KeyCode.None
                && !claimedByGame
                && Player.m_localPlayer != null
                && ZInput.instance != null
                && ZInput.GetKeyDown(help.MainKey, true))
            {
                HelpPanel.Toggle();
            }

            // The three below are tools for working out why something is not doing what it
            // should, and every one of them holds a function key hostage. Kept, because each has
            // ended an investigation that guessing had prolonged - but switched off with the
            // rest of the debugging, so a player who never turns that on never loses F9 to F11
            // and never wonders why a screenshot key stopped working.
            if (!ModConfig.DebugEnabled)
            {
                return;
            }

            KeyboardShortcut key = ModConfig.DebugMistKey?.Value ?? default(KeyboardShortcut);

            if (key.MainKey != KeyCode.None
                && Player.m_localPlayer != null
                && ZInput.instance != null
                && ZInput.GetKeyDown(key.MainKey, true))
            {
                MistDiagnostics.Dump(Player.m_localPlayer);
            }

            KeyboardShortcut patches = ModConfig.DebugPatchesKey?.Value ?? default(KeyboardShortcut);

            if (patches.MainKey != KeyCode.None
                && ZInput.instance != null
                && ZInput.GetKeyDown(patches.MainKey, true))
            {
                PatchInspector.Dump();
            }

            KeyboardShortcut mesh = ModConfig.DebugMeshKey?.Value ?? default(KeyboardShortcut);

            if (mesh.MainKey != KeyCode.None
                && Player.m_localPlayer != null
                && ZInput.instance != null
                && ZInput.GetKeyDown(mesh.MainKey, true))
            {
                MeshProbe.Dump(Player.m_localPlayer);
            }
        }

        private void OnGUI()
        {
            HelpPanel.Draw();
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            _harmony = null;
            PlacementUndo.Clear();
            PlacementEdit.Clear();
            BendDeformer.Release();
            HammerGlow.Forget();
            RotationGizmo.Destroy();
            SnapPointMarkers.Destroy();
            GizmoMaterial.Destroy();
        }

        /// <summary>
        /// This mod owns the whole placement pipeline. Another mod driving ghost rotation
        /// at the same time produces confusing results, so say so plainly at startup
        /// rather than leaving the player to work it out mid-build.
        /// </summary>
        private void WarnAboutKnownConflicts()
        {
            CheckConflict("bruce.valheim.comfymods.gizmo", "ComfyGizmo",
                "both rotate the placement ghost");
            CheckConflict("Snapheim.Valheim", "Snapheim",
                "both adjust the placement ghost");
        }

        private void CheckConflict(string guid, string displayName, string reason)
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid))
            {
                return;
            }

            Logger.LogWarning(
                $"{displayName} is installed and {reason}. Running both is not supported and "
                + $"will give unpredictable placement. Disable one of them.");
        }

        /// <summary>Unconditional info, for diagnostics that have their own switch.</summary>
        internal static void Info(string message)
        {
            _logger.LogInfo(message);
        }

        internal static void Error(string message)
        {
            _logger.LogError(message);
        }

        internal static void Debug(string message)
        {
            if (ModConfig.DebugEnabled)
            {
                _logger.LogInfo(message);
            }
        }
    }
}
