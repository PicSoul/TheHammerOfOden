using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A blueprint on disk: PlanBuild's .blueprint, Infinity Hammer's variant of it, or BuildShare's
    /// .vbuild - read the way those mods read them, so a file made by any of them works here.
    /// </summary>
    /// <remarks>
    /// See docs/blueprints-plan.md for the formats. In short, a .blueprint is a few "#Header:"
    /// lines and then one line per piece, split on ';':
    ///
    ///     name ; category ; x ; y ; z ; qx ; qy ; qz ; qw ; info ; sx ; sy ; sz ; data ; chance
    ///
    /// Fields 10-12, the scale, are PlanBuild's and both it and Infinity Hammer apply them.
    /// Field 13 is Infinity Hammer's copy of the piece's saved values and 14 its placing chance;
    /// PlanBuild ignores both. A .vbuild line is "name qx qy qz qw x y z", split on spaces.
    ///
    /// Sections this mod does not know - Infinity Hammer's terrain, anything newer - are skipped
    /// up to the next one it does, exactly as both of those mods skip each other's.
    ///
    /// Listing reads only the headers, so opening the book with a few hundred blueprints is quick;
    /// the pieces are read when one is picked.
    /// </remarks>
    internal sealed class BlueprintFile
    {
        internal sealed class Entry
        {
            internal string Prefab;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal Vector3 Scale = Vector3.one;
            internal bool HasScale;
            internal string Info;
            internal string Data;
        }

        internal string Path;
        internal string Name;
        internal string Creator;
        internal string Description;
        internal string Category;
        internal string Source;

        /// <summary>When it was first saved and last saved again - this mod writes both; other mods' files have neither.</summary>
        internal System.DateTime? Created;
        internal System.DateTime? Updated;
        internal readonly List<Entry> Entries = new List<Entry>();
        internal bool Loaded;

        // ------------------------------------------------------------------ where they are

        /// <summary>
        /// The folders searched: the shared PlanBuild folder in the game's own folder - where
        /// PlanBuild saves and where both it and Infinity Hammer look - and the same folder in the
        /// mod profile, which Infinity Hammer also searches.
        /// </summary>
        internal static IEnumerable<string> Folders()
        {
            yield return System.IO.Path.Combine(Paths.GameRootPath, "BepInEx", "config", "PlanBuild");
            yield return System.IO.Path.Combine(Paths.ConfigPath, "PlanBuild");
        }

        /// <summary>
        /// Where blueprints are saved, and the first place looked: PlanBuild's own save folder,
        /// which Infinity Hammer searches too.
        /// </summary>
        internal static string SaveFolder => System.IO.Path.Combine(Paths.GameRootPath, "BepInEx", "config", "PlanBuild", "blueprints");

        /// <summary>
        /// Makes the save folder if it is not there yet, so a player always has an obvious place
        /// to drop blueprint files - and PlanBuild and Infinity Hammer find it too.
        /// </summary>
        internal static void EnsureSaveFolder()
        {
            try
            {
                if (!Directory.Exists(SaveFolder))
                {
                    Directory.CreateDirectory(SaveFolder);
                    HammerOfOdenPlugin.Info($"Made the blueprint folder: {SaveFolder}");
                }
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error($"Could not make the blueprint folder '{SaveFolder}': {ex.Message}");
            }
        }

        /// <summary>Every blueprint in the searched folders, headers only.</summary>
        internal static List<BlueprintFile> List()
        {
            EnsureSaveFolder();

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<BlueprintFile> found = new List<BlueprintFile>();

            foreach (string folder in Folders())
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    HammerOfOdenPlugin.Error($"Could not look through '{folder}' for blueprints: {ex.Message}");
                    continue;
                }

                foreach (string file in files)
                {
                    string extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if ((extension != ".blueprint" && extension != ".vbuild") || !seen.Add(System.IO.Path.GetFullPath(file)))
                    {
                        continue;
                    }

                    BlueprintFile blueprint = ReadHeaders(file);
                    if (blueprint != null)
                    {
                        found.Add(blueprint);
                    }
                }
            }

            found.Sort((a, b) =>
            {
                int byCategory = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
                return byCategory != 0 ? byCategory : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return found;
        }

        internal static BlueprintFile ReadHeaders(string file)
        {
            BlueprintFile blueprint = new BlueprintFile
            {
                Path = file,
                Name = System.IO.Path.GetFileNameWithoutExtension(file),
                Category = "Blueprints",
                Source = "BuildShare"
            };

            if (System.IO.Path.GetExtension(file).ToLowerInvariant() == ".vbuild")
            {
                return blueprint;
            }

            blueprint.Source = "PlanBuild";

            try
            {
                using (StreamReader reader = new StreamReader(file))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("#Pieces", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        blueprint.Header(line);
                    }
                }
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error($"Could not read the blueprint '{file}': {ex.Message}");
                return null;
            }

            return blueprint;
        }

        private void Header(string line)
        {
            string value = line.IndexOf(':') >= 0 ? line.Substring(line.IndexOf(':') + 1) : string.Empty;

            if (line.StartsWith("#Name:", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
            {
                Name = value;
            }
            else if (line.StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase))
            {
                Creator = value;
            }
            else if (line.StartsWith("#Description:", StringComparison.OrdinalIgnoreCase))
            {
                Description = Unquote(value);
            }
            else if (line.StartsWith("#Category:", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Length > 0)
                {
                    Category = value;
                }

                if (value.Equals("InfinityHammer", StringComparison.OrdinalIgnoreCase))
                {
                    Source = "Infinity Hammer";
                }
            }
            else if (line.StartsWith("#Created:", StringComparison.OrdinalIgnoreCase))
            {
                Created = Date(value);
            }
            else if (line.StartsWith("#Updated:", StringComparison.OrdinalIgnoreCase))
            {
                Updated = Date(value);
            }
            else if (line.StartsWith("#HammerOfOden", StringComparison.OrdinalIgnoreCase))
            {
                Source = "The Hammer of Oden";
            }
        }

        // ------------------------------------------------------------------ reading the pieces

        /// <summary>Reads every piece. Returns false, having said why in the log, if the file cannot be read.</summary>
        internal bool Load()
        {
            if (Loaded)
            {
                return true;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(Path);
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Error($"Could not read the blueprint '{Path}': {ex.Message}");
                return false;
            }

            Entries.Clear();
            bool vbuild = System.IO.Path.GetExtension(Path).ToLowerInvariant() == ".vbuild";
            bool pieces = true;

            foreach (string raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string line = raw.Trim();

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    // Pieces follow #Pieces; everything else under a header is someone's own
                    // section - snap points, terrain - and is passed over until the next header.
                    Header(line);
                    pieces = line.StartsWith("#Pieces", StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!pieces && !vbuild)
                {
                    continue;
                }

                try
                {
                    Entry entry = vbuild ? FromVBuild(line) : FromBlueprint(line);
                    if (entry != null)
                    {
                        Entries.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    HammerOfOdenPlugin.Debug($"Skipped an unreadable line in '{Name}': {ex.Message}");
                }
            }

            Loaded = true;
            return true;
        }

        private static Entry FromBlueprint(string line)
        {
            // Old PlanBuild files were written with the local decimal comma.
            if (line.IndexOf(',') > -1)
            {
                line = line.Replace(',', '.');
            }

            string[] parts = line.Split(';');
            if (parts.Length < 9)
            {
                return null;
            }

            Entry entry = new Entry
            {
                Prefab = parts[0].Split('(')[0].Trim(),
                Position = new Vector3(Float(parts, 2), Float(parts, 3), Float(parts, 4)),
                Rotation = Normalised(new Quaternion(Float(parts, 5), Float(parts, 6), Float(parts, 7), Float(parts, 8))),
                Info = parts.Length > 9 ? Unquote(parts[9]) : null,
                Data = parts.Length > 13 ? parts[13] : null
            };

            if (parts.Length > 12)
            {
                entry.Scale = new Vector3(Float(parts, 10, 1f), Float(parts, 11, 1f), Float(parts, 12, 1f));
                entry.HasScale = true;
            }

            return entry;
        }

        private static Entry FromVBuild(string line)
        {
            if (line.IndexOf(',') > -1)
            {
                line = line.Replace(',', '.');
            }

            string[] parts = line.Split(' ');
            if (parts.Length < 8)
            {
                return null;
            }

            return new Entry
            {
                Prefab = parts[0].Split('(')[0].Trim(),
                Rotation = Normalised(new Quaternion(Float(parts, 1), Float(parts, 2), Float(parts, 3), Float(parts, 4))),
                Position = new Vector3(Float(parts, 5), Float(parts, 6), Float(parts, 7))
            };
        }

        /// <summary>A date as written: ISO 8601, in UTC, so it reads the same wherever the file travels.</summary>
        private static System.DateTime? Date(string text)
        {
            return System.DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out System.DateTime date)
                ? date
                : (System.DateTime?)null;
        }

        private static float Float(string[] parts, int index, float fallback = 0f)
        {
            return index < parts.Length && float.TryParse(parts[index], NumberStyles.Any, NumberFormatInfo.InvariantInfo, out float value)
                ? value
                : fallback;
        }

        private static Quaternion Normalised(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return length < 0.0001f ? Quaternion.identity : new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
        }

        /// <summary>PlanBuild writes text fields as JSON strings: "\"Hello\"". Older files and Infinity Hammer write them bare.</summary>
        private static string Unquote(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "\"\"")
            {
                return null;
            }

            if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
            {
                text = text.Substring(1, text.Length - 2)
                    .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            return text;
        }

        // ------------------------------------------------------------------ Infinity Hammer's data

        private static readonly int TextHash = "text".GetStableHashCode();
        private static readonly int BendDegreesHash = BentPiece.DegreesKey.GetStableHashCode();
        private static readonly int BendAxisHash = BentPiece.AxisKey.GetStableHashCode();
        private static readonly int BendRiseHash = BentPiece.RiseKey.GetStableHashCode();
        private static readonly int BendChoiceHash = BentPiece.ChoiceKey.GetStableHashCode();

        /// <summary>
        /// The few values this mod takes from a piece's saved data: its bend and a sign's text.
        /// Everything else in it - a chest's contents above all - is passed over.
        /// </summary>
        /// <remarks>
        /// The data is World Edit Commands' encoding, which Infinity Hammer writes: a flags number,
        /// then for each kind of value present a count and that many (name hash, value) pairs, in
        /// a fixed order - floats, vectors, rotations, whole numbers, longs, text. Reading stops
        /// after the text; nothing past it is wanted.
        /// </remarks>
        internal static void ReadData(string base64, CopyOrder order)
        {
            if (string.IsNullOrEmpty(base64))
            {
                return;
            }

            try
            {
                ZPackage package = new ZPackage(base64);
                int flags = package.ReadInt();

                if ((flags & 1) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int key = package.ReadInt();
                        float value = package.ReadSingle();
                        if (key == BendDegreesHash)
                        {
                            order.BendDegrees = value;
                        }
                    }
                }

                if ((flags & 2) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        package.ReadInt();
                        package.ReadVector3();
                    }
                }

                if ((flags & 4) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        package.ReadInt();
                        package.ReadQuaternion();
                    }
                }

                if ((flags & 8) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int key = package.ReadInt();
                        int value = package.ReadInt();
                        if (key == BendAxisHash)
                        {
                            order.BendAxis = value;
                        }
                        else if (key == BendRiseHash)
                        {
                            order.BendRise = value;
                        }
                        else if (key == BendChoiceHash)
                        {
                            order.BendChoice = value;
                        }
                    }
                }

                if ((flags & 64) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        package.ReadInt();
                        package.ReadLong();
                    }
                }

                if ((flags & 16) != 0)
                {
                    int count = package.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int key = package.ReadInt();
                        string value = package.ReadString();
                        if (key == TextHash && string.IsNullOrEmpty(order.SignText))
                        {
                            order.SignText = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HammerOfOdenPlugin.Debug($"A piece's saved data could not be read and was left out: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// A blueprint turned into the pieces this mod builds from - copy orders, the same list copies,
    /// construction sites and models all use - with the rules copies follow.
    /// </summary>
    internal sealed class BlueprintPlan
    {
        internal readonly List<CopyOrder> Orders = new List<CopyOrder>();

        /// <summary>Piece names this game does not have - from a mod not installed here.</summary>
        internal readonly Dictionary<string, int> Unknown = new Dictionary<string, int>();

        /// <summary>Pieces this character has not learned, by their display name.</summary>
        internal readonly Dictionary<string, int> Unlearned = new Dictionary<string, int>();

        internal int Plants;
        internal int Ground;
        internal int Clamped;

        /// <summary>
        /// Reads a blueprint into orders: unknown pieces, plants and - unless the server allows
        /// otherwise - unlearned pieces left out and counted; scale kept within the server's
        /// limits; a sign's text and a piece's bend taken, and nothing else.
        /// </summary>
        internal static BlueprintPlan From(BlueprintFile file, Player player)
        {
            BlueprintPlan plan = new BlueprintPlan();
            if (file == null || !file.Load() || ZNetScene.instance == null)
            {
                return plan;
            }

            foreach (BlueprintFile.Entry entry in file.Entries)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(entry.Prefab);
                Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (piece == null)
                {
                    Tally(plan.Unknown, entry.Prefab);
                    continue;
                }

                if (GroupHold.IsPlantedPrefab(prefab, piece))
                {
                    plan.Plants++;
                    continue;
                }

                // The hoe's tools - raise, level, paths - are pieces as far as the game is
                // concerned, and BuildShare blueprints often carry them. They reshape the ground
                // for good, so they are only built when the book's option says so.
                bool ground = prefab.GetComponent<TerrainModifier>() != null || prefab.GetComponent<TerrainOp>() != null;
                if (ground)
                {
                    plan.Ground++;
                    if (!ModConfig.BlueprintShapeGround.Value)
                    {
                        continue;
                    }
                }

                if (player != null && !GroupHold.KnowsForCopy(player, piece))
                {
                    Tally(plan.Unlearned, piece.m_name);
                    continue;
                }

                CopyOrder order = new CopyOrder
                {
                    Prefab = piece,
                    Position = entry.Position,
                    Rotation = entry.Rotation,
                    Scale = ground ? prefab.transform.localScale : plan.Limit(prefab, entry.HasScale ? entry.Scale : prefab.transform.localScale),

                    // The values a piece has before it is ever bent - what the data overwrites
                    // if the piece was.
                    BendRise = 1,
                    BendChoice = 1
                };

                if (piece.GetComponent<Sign>() != null && !string.IsNullOrEmpty(entry.Info))
                {
                    order.SignText = entry.Info;
                }

                BlueprintFile.ReadData(entry.Data, order);

                // A sign's text from the data alone belongs to a sign.
                if (piece.GetComponent<Sign>() == null)
                {
                    order.SignText = null;
                }

                plan.Orders.Add(order);
            }

            return plan;
        }

        /// <summary>A piece's scale, held within the server's scale limits and to pieces allowed to scale at all.</summary>
        private Vector3 Limit(GameObject prefab, Vector3 scale)
        {
            Vector3 own = prefab.transform.localScale;
            if (!Scalable.Allows(prefab))
            {
                if ((scale - own).sqrMagnitude > 0.000001f)
                {
                    Clamped++;
                }

                return own;
            }

            float min = ModConfig.ScaleMin.Value;
            float max = ModConfig.ScaleMax.Value;
            Vector3 ratio = new Vector3(
                own.x != 0f ? scale.x / own.x : 1f,
                own.y != 0f ? scale.y / own.y : 1f,
                own.z != 0f ? scale.z / own.z : 1f);
            Vector3 held = new Vector3(Mathf.Clamp(ratio.x, min, max), Mathf.Clamp(ratio.y, min, max), Mathf.Clamp(ratio.z, min, max));

            if ((held - ratio).sqrMagnitude > 0.000001f)
            {
                Clamped++;
            }

            return Vector3.Scale(held, own);
        }

        private static void Tally(Dictionary<string, int> counts, string name)
        {
            counts.TryGetValue(name, out int n);
            counts[name] = n + 1;
        }

        internal static string Describe(Dictionary<string, int> counts, int most = 6)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            List<string> parts = new List<string>();
            for (int i = 0; i < list.Count && i < most; i++)
            {
                parts.Add($"{list[i].Value} x {list[i].Key}");
            }

            if (list.Count > most)
            {
                parts.Add($"{list.Count - most} more kinds");
            }

            return string.Join(", ", parts);
        }
    }
}
