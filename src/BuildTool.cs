using UnityEngine;

namespace TheHammerOfOden
{
    internal enum ToolScope
    {
        /// <summary>
        /// Building tools only. Terrain tools are left entirely alone.
        /// </summary>
        /// <remarks>
        /// The hoe and the cultivator share Valheim's placement code with the hammer, which
        /// is why a mod that patches that code reaches them by accident. Almost nothing here
        /// means anything for them: a levelling operation has no rotation worth showing, no
        /// snap points to cycle, and no size to change.
        /// </remarks>
        BuildingOnly = 0,

        /// <summary>Every tool that uses the placement ghost, terrain tools included.</summary>
        AllTools = 1
    }

    /// <summary>
    /// Decides whether the tool in hand is one this mod should touch.
    /// </summary>
    /// <remarks>
    /// Told apart by what the selected piece does rather than by what the tool is called.
    /// Matching on the piece table's name would work for the vanilla three and break on the
    /// first mod that ships its own hammer, while a terrain operation carries a TerrainOp
    /// component whoever authored it - so the hoe, the cultivator and any modded equivalent
    /// are recognised without a list to maintain.
    ///
    /// Evaluated once per frame and cached, because the answer is read from the rotation
    /// transpiler, which runs inside Valheim's own placement code.
    /// </remarks>
    internal static class BuildTool
    {
        private static GameObject _lastPiece;
        private static bool _lastVerdict = true;

        /// <summary>
        /// Whether the tool in hand is one this mod covers, regardless of the master switch.
        /// </summary>
        /// <remarks>
        /// Separate from AppliesNow because two things need different answers. Features ask
        /// "should I act?", which means the mod is on and the tool is right. The master
        /// toggle and the glow that reports it ask "is this my tool?" - they have to keep
        /// working while the mod is off, or there would be no way to switch it back on, and
        /// they must still keep away from the hoe.
        /// </remarks>
        internal static bool IsBuildingTool { get; private set; } = true;

        /// <summary>Whether the mod should act on the tool currently in hand.</summary>
        internal static bool AppliesNow { get; private set; } = true;

        /// <summary>
        /// Called at the top of the placement update, before anything reads the verdict.
        /// </summary>
        internal static void Evaluate(PieceTable table)
        {
            IsBuildingTool = Applies(table);
            AppliesNow = ModConfig.IsEnabled && IsBuildingTool;
        }

        private static bool Applies(PieceTable table)
        {
            if (ModConfig.Tools.Value == ToolScope.AllTools)
            {
                return true;
            }

            if (table == null)
            {
                return false;
            }

            GameObject selected = table.GetSelectedPrefab();
            if (selected == null)
            {
                // Nothing chosen yet; assume the mod applies so the gizmo does not blink out
                // between selecting a tool and selecting a piece.
                return true;
            }

            if (selected == _lastPiece)
            {
                return _lastVerdict;
            }

            _lastPiece = selected;
            _lastVerdict = selected.GetComponentInChildren<TerrainOp>(true) == null;

            if (!_lastVerdict)
            {
                HammerOfOdenPlugin.Debug(
                    $"'{selected.name}' is a terrain operation; leaving it to vanilla.");
            }

            return _lastVerdict;
        }
    }
}
