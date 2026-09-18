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

        private void Awake()
        {
            _logger = Logger;

            ModConfig.Bind(Config);

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
                WarnAboutKnownConflicts();
            }
            catch (Exception ex)
            {
                try { _harmony?.UnpatchSelf(); } catch { }
                _harmony = null;
                Logger.LogError("Failed to apply Harmony patches; placement is unchanged.");
                Logger.LogError(ex);
            }
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
