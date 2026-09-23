using System;
using System.Collections.Generic;
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
    /// Asked the right way round. This used to rule a tool out by finding a TerrainOp on the
    /// selected piece, and anything it could not rule out was treated as a hammer. That fails
    /// open: a tool it has never heard of gets the whole mod by default, so MyDirtyHoe - which
    /// brings its own piece table and its own pieces - arrived wearing a rotation gizmo. Every
    /// future terrain mod would have done the same, each one needing a new release here.
    ///
    /// So the question is now "is this a building tool?" rather than "is this not a hoe?", and
    /// the signal is the one vanilla itself uses to mean it: PieceTable.m_canRemovePieces, which
    /// is what Player.Update tests before letting the tool in hand take a built piece down. A
    /// hammer builds and unbuilds; a hoe and a cultivator do neither, and a modded terrain tool
    /// that copies the hoe's table copies that with it. A modded hammer worth the name sets it,
    /// because without it the tool could not remove what it places.
    ///
    /// The TerrainOp check is kept as a second line, for a table that claims to build but whose
    /// selected piece is plainly terrain work. Both are measured rather than named, so no list
    /// of mods has to be maintained - and the two config lists are there for the case where a
    /// mod is stranger than either test expects.
    ///
    /// The build camera deliberately consults none of this. Flying out to look at what you are
    /// about to flatten is as useful with a hoe as with a hammer, so it asks IsPlacementTool
    /// instead, which is true for anything holding a piece table at all.
    ///
    /// Evaluated once per frame and cached, because the answer is read from the rotation
    /// transpiler, which runs inside Valheim's own placement code.
    /// </remarks>
    internal static class BuildTool
    {
        private static PieceTable _lastTable;
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

        /// <summary>
        /// Whether any placement tool is in hand at all, terrain tools included.
        /// </summary>
        /// <remarks>
        /// A third question, because the build camera answers it differently from everything
        /// else. Rotation, snapping and scaling mean nothing for a levelling operation, but
        /// flying the camera to look at what you are about to flatten is just as useful with
        /// a hoe as with a hammer - so the camera covers every placement tool, vanilla or
        /// modded, while the rest of the mod stays on the ones that build.
        /// </remarks>
        internal static bool IsPlacementTool { get; private set; }

        /// <summary>Whether the mod should act on the tool currently in hand.</summary>
        internal static bool AppliesNow { get; private set; } = true;

        /// <summary>
        /// Called at the top of the placement update, before anything reads the verdict.
        /// </summary>
        internal static void Evaluate(PieceTable table)
        {
            IsPlacementTool = table != null;
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

            if (table == _lastTable && selected == _lastPiece)
            {
                return _lastVerdict;
            }

            _lastTable = table;
            _lastPiece = selected;
            _lastVerdict = Decide(table, selected);

            if (_lastVerdict)
            {
                // Logged on the way in as well as the way out. A tool that is wrongly refused
                // names itself in the refusal, but one that is wrongly accepted - which is how
                // this was noticed in the first place - would otherwise say nothing at all,
                // and the table name is what the override lists need.
                HammerOfOdenPlugin.Debug(
                    "'" + Clean(table.name) + "' is a building tool; the mod applies.");
            }

            return _lastVerdict;
        }

        private static bool Decide(PieceTable table, GameObject selected)
        {
            string tableName = Clean(table.name);

            Refresh();

            if (Forced.Contains(tableName))
            {
                return true;
            }

            if (Excluded.Contains(tableName))
            {
                HammerOfOdenPlugin.Debug(
                    "'" + tableName + "' is listed in TerrainToolTables; leaving it to vanilla.");
                return false;
            }

            // What vanilla means by a building tool: one that can take a built piece down.
            if (!table.m_canRemovePieces)
            {
                HammerOfOdenPlugin.Debug(
                    "'" + tableName + "' cannot remove built pieces, so it is not a building "
                    + "tool; leaving it to vanilla. Only the build camera applies.");
                return false;
            }

            // Nothing chosen yet. The table has already answered, so say yes rather than
            // blinking the gizmo out between selecting a tool and selecting a piece.
            if (selected == null)
            {
                return true;
            }

            if (selected.GetComponentInChildren<TerrainOp>(true) != null)
            {
                HammerOfOdenPlugin.Debug(
                    "'" + selected.name + "' is a terrain operation; leaving it to vanilla.");
                return false;
            }

            return true;
        }

        /// <summary>Unity hands back "_HammerPieceTable(Clone)" as readily as the original.</summary>
        private static string Clean(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int clone = name.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            return (clone < 0 ? name : name.Substring(0, clone)).Trim();
        }

        private static readonly HashSet<string> Forced =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> Excluded =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string _listed;

        /// <summary>Splits the override lists once per change, not once per lookup.</summary>
        private static void Refresh()
        {
            string raw = (ModConfig.BuildToolTables.Value ?? string.Empty)
                + "\u0000" + (ModConfig.TerrainToolTables.Value ?? string.Empty);

            if (raw == _listed)
            {
                return;
            }

            _listed = raw;

            Fill(Forced, ModConfig.BuildToolTables.Value);
            Fill(Excluded, ModConfig.TerrainToolTables.Value);
        }

        private static void Fill(HashSet<string> into, string raw)
        {
            into.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string entry in raw.Split(','))
            {
                string trimmed = Clean(entry);
                if (trimmed.Length > 0)
                {
                    into.Add(trimmed);
                }
            }
        }
    }
}
