using System.Collections.Generic;
using System.Reflection;
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

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

            ModConfig.Bind(Config);

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

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            _harmony = null;
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
