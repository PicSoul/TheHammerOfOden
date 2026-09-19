using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Remembers which anchor you last built each kind of piece by, across sessions.
    /// </summary>
    /// <remarks>
    /// Select a wall, choose to hold it by its bottom centre, and every wall you select after
    /// that comes up held the same way. Pick a different piece and it remembers that piece's
    /// own choice instead.
    ///
    /// This replaces guessing. An earlier attempt inferred the anchor from a placed piece by
    /// searching for one of its anchors sitting on a neighbour's, which works only when the
    /// match is unambiguous - fine for a tilted beam, useless for a wall in a flat grid where
    /// several anchors touch something. Remembering the choice you actually made needs no
    /// inference at all, and none of the per-piece storage that recording it in the world
    /// would have required.
    ///
    /// Stored as a local position rather than an index, because indices move: they depend on
    /// child order and on how many derived anchors the current mode adds, so one recorded
    /// under Centers would mean something else under Full. A position identifies the same
    /// corner of the same piece whatever else changes - including across the config edit that
    /// changed the mode, which matters more now that these outlive the session.
    /// </remarks>
    internal static class SnapPointMemory
    {
        /// <summary>Marks a piece last placed with automatic snapping rather than a chosen anchor.</summary>
        private static readonly Vector3 Automatic = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        private const float Match = 0.01f;
        private const string AutomaticText = "auto";

        private static readonly Dictionary<string, Vector3> Choices = new Dictionary<string, Vector3>();
        private static readonly List<Transform> Scratch = new List<Transform>();

        private static bool _loaded;
        private static bool _writeFailed;

        internal static void Remember(GameObject ghost, int manualSnapPoint)
        {
            if (!ModConfig.IsEnabled || !ModConfig.RememberSnapPoint.Value || ghost == null)
            {
                return;
            }

            string prefab = Utils.GetPrefabName(ghost);
            if (string.IsNullOrEmpty(prefab))
            {
                return;
            }

            Load();

            Vector3 choice;
            string name;

            if (manualSnapPoint < 0)
            {
                choice = Automatic;
                name = AutomaticText;
            }
            else
            {
                Piece piece = ghost.GetComponent<Piece>();
                if (piece == null)
                {
                    return;
                }

                Scratch.Clear();
                piece.GetSnapPoints(Scratch);

                if (manualSnapPoint >= Scratch.Count || Scratch[manualSnapPoint] == null)
                {
                    return;
                }

                choice = Scratch[manualSnapPoint].localPosition;
                name = Scratch[manualSnapPoint].name;
            }

            // Only a genuine change reaches the disk. Building a run of fifty walls by the
            // same corner is one write, not fifty.
            if (Choices.TryGetValue(prefab, out Vector3 existing) && Same(existing, choice))
            {
                return;
            }

            Choices[prefab] = choice;
            HammerOfOdenPlugin.Debug($"Remembered '{name}' as the anchor for '{prefab}'.");

            Save();
        }

        internal static void Restore(GameObject ghost, ref int manualSnapPoint)
        {
            if (!ModConfig.IsEnabled || !ModConfig.RememberSnapPoint.Value || ghost == null)
            {
                return;
            }

            string prefab = Utils.GetPrefabName(ghost);
            if (string.IsNullOrEmpty(prefab))
            {
                return;
            }

            Load();

            if (!Choices.TryGetValue(prefab, out Vector3 wanted))
            {
                return;
            }

            if (wanted == Automatic)
            {
                manualSnapPoint = -1;
                return;
            }

            Piece piece = ghost.GetComponent<Piece>();
            if (piece == null)
            {
                return;
            }

            Scratch.Clear();
            piece.GetSnapPoints(Scratch);

            float best = Match * Match;
            int found = -1;

            for (int i = 0; i < Scratch.Count; i++)
            {
                if (Scratch[i] == null)
                {
                    continue;
                }

                float distance = (Scratch[i].localPosition - wanted).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    found = i;
                }
            }

            if (found < 0)
            {
                // The anchor existed under a denser mode and does not now. Leave the
                // selection alone rather than picking something arbitrary.
                return;
            }

            manualSnapPoint = found;
            HammerOfOdenPlugin.Debug($"Restored '{Scratch[found].name}' as the anchor for '{prefab}'.");
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            if (a == Automatic || b == Automatic)
            {
                return a == b;
            }

            return (a - b).sqrMagnitude < Match * Match;
        }

        /// <summary>
        /// Kept beside the config rather than in it.
        /// </summary>
        /// <remarks>
        /// A BepInEx config file is for settings you choose; this is a record of what you did,
        /// with one entry per kind of piece you have ever built. Putting it in the config would
        /// bury the forty settings that are worth reading under a few hundred lines of
        /// bookkeeping. Its own file also means deleting it starts over with nothing else lost.
        /// </remarks>
        private static string FilePath()
        {
            return Path.Combine(Paths.ConfigPath, HammerOfOdenPlugin.PluginGuid + ".snappoints.cfg");
        }

        private static void Load()
        {
            if (_loaded)
            {
                return;
            }

            // Set before reading, so an unreadable file is not retried on every placement.
            _loaded = true;

            try
            {
                string path = FilePath();
                if (!File.Exists(path))
                {
                    return;
                }

                foreach (string line in File.ReadAllLines(path))
                {
                    string text = line.Trim();
                    if (text.Length == 0 || text[0] == '#')
                    {
                        continue;
                    }

                    int split = text.IndexOf('=');
                    if (split <= 0)
                    {
                        continue;
                    }

                    string prefab = text.Substring(0, split).Trim();
                    string value = text.Substring(split + 1).Trim();

                    if (prefab.Length == 0)
                    {
                        continue;
                    }

                    if (value == AutomaticText)
                    {
                        Choices[prefab] = Automatic;
                    }
                    else if (TryParse(value, out Vector3 position))
                    {
                        Choices[prefab] = position;
                    }
                }

                HammerOfOdenPlugin.Debug($"Loaded {Choices.Count} remembered snap points.");
            }
            catch (Exception ex)
            {
                // A building mod that cannot read a convenience file should still let you
                // build; the session simply starts with nothing remembered.
                HammerOfOdenPlugin.Error(
                    "Could not read the remembered snap points, so this session starts without them. "
                    + ex.Message);
            }
        }

        private static void Save()
        {
            if (_writeFailed)
            {
                return;
            }

            try
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine("# The Hammer of Oden - the anchor you last built each kind of piece by.");
                text.AppendLine("# Written automatically. Delete a line to put that piece back on");
                text.AppendLine("# automatic snapping, or delete the file to start over.");
                text.AppendLine();

                foreach (KeyValuePair<string, Vector3> choice in Choices)
                {
                    text.Append(choice.Key).Append(" = ").AppendLine(Format(choice.Value));
                }

                File.WriteAllText(FilePath(), text.ToString());
            }
            catch (Exception ex)
            {
                _writeFailed = true;
                HammerOfOdenPlugin.Error(
                    "Could not save the remembered snap points, so they will last only this session. "
                    + ex.Message);
            }
        }

        // Invariant culture throughout: a machine whose locale writes decimals with commas
        // would otherwise produce "1,5,0,0,0" for a position and read back nothing.
        private static string Format(Vector3 value)
        {
            if (value == Automatic)
            {
                return AutomaticText;
            }

            return value.x.ToString("0.####", CultureInfo.InvariantCulture)
                + "," + value.y.ToString("0.####", CultureInfo.InvariantCulture)
                + "," + value.z.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static bool TryParse(string text, out Vector3 value)
        {
            value = Vector3.zero;

            string[] parts = text.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }
    }
}
