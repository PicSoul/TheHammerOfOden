using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Writes a blueprint the way PlanBuild writes one, with what only this mod has carried in the
    /// field Infinity Hammer reads - so PlanBuild and Infinity Hammer can both load it.
    /// </summary>
    /// <remarks>
    /// Each piece line is
    ///
    ///     name ; category ; x ; y ; z ; qx ; qy ; qz ; qw ; info ; sx ; sy ; sz ; data ; 1
    ///
    ///   - positions from the lowest corner of the building, as PlanBuild measures them
    ///   - info: a sign's text, as a JSON string, the way PlanBuild writes it
    ///   - the scale, which PlanBuild and Infinity Hammer both apply
    ///   - data: the piece's bend and a sign's text in World Edit Commands' encoding, which
    ///     Infinity Hammer copies onto what it places - so a bent piece placed by Infinity Hammer
    ///     comes out bent where this mod is installed. PlanBuild does not read past the scale and
    ///     builds it straight
    ///   - 1: Infinity Hammer's chance of placing the piece; always
    ///
    /// A "#HammerOfOden" section at the end says which mod wrote it. Both other mods skip sections
    /// they do not know.
    ///
    /// Nothing a piece holds is written: no chest contents, no items on stands - the same rule as
    /// copies, so a blueprint cannot carry items from one world to another.
    /// </remarks>
    internal static class BlueprintWriter
    {
        internal const int FormatVersion = 1;

        /// <summary>A file name made safe from a blueprint name.</summary>
        internal static string FileNameFor(string name)
        {
            StringBuilder safe = new StringBuilder();
            foreach (char c in name.Trim())
            {
                safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 || c == ';' ? '_' : c);
            }

            string result = safe.ToString().Trim();
            return result.Length == 0 ? "Blueprint" : result;
        }

        internal static string PathFor(string name)
        {
            return Path.Combine(BlueprintFile.SaveFolder, FileNameFor(name) + ".blueprint");
        }

        /// <summary>
        /// Writes the blueprint, keeping whatever it replaces as a backup beside it. Returns false,
        /// having said why, if it could not.
        /// </summary>
        /// <param name="created">When it was first saved: now for a new blueprint, the old file's date when replacing one.</param>
        /// <param name="updated">When it was saved again - null for a new blueprint.</param>
        internal static bool Save(string path, string name, string creator, string description, string category,
            System.DateTime created, System.DateTime? updated, List<CopyOrder> orders, out string error)
        {
            error = null;
            try
            {
                BlueprintFile.EnsureSaveFolder();

                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", true);
                }

                File.WriteAllLines(path, Lines(name, creator, description, category, created, updated, orders).ToArray(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HammerOfOdenPlugin.Error($"Could not save the blueprint '{path}': {ex.Message}");
                return false;
            }
        }

        private static List<string> Lines(string name, string creator, string description, string category,
            System.DateTime created, System.DateTime? updated, List<CopyOrder> orders)
        {
            // The dates go among the headers, where the book reads them without reading the pieces.
            // PlanBuild and Infinity Hammer pass over headers they do not know.
            List<string> lines = new List<string>
            {
                "#Name:" + OneLine(name),
                "#Creator:" + OneLine(creator),
                "#Description:" + Json(description ?? string.Empty),
                "#Category:" + OneLine(category),
                "#Created:" + created.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            };

            if (updated.HasValue)
            {
                lines.Add("#Updated:" + updated.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            }

            lines.Add("#Pieces");

            // From the lowest corner, as PlanBuild measures.
            Vector3 corner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            foreach (CopyOrder order in orders)
            {
                corner = Vector3.Min(corner, order.Position);
            }

            List<CopyOrder> sorted = new List<CopyOrder>(orders);
            sorted.Sort((a, b) =>
            {
                int y = a.Position.y.CompareTo(b.Position.y);
                if (y != 0)
                {
                    return y;
                }

                int x = a.Position.x.CompareTo(b.Position.x);
                return x != 0 ? x : a.Position.z.CompareTo(b.Position.z);
            });

            foreach (CopyOrder order in sorted)
            {
                if (order?.Prefab == null)
                {
                    continue;
                }

                Vector3 at = order.Position - corner;
                Quaternion turn = order.Rotation;
                string sign = order.Prefab.GetComponent<Sign>() != null ? order.SignText : null;

                lines.Add(string.Join(";",
                    Utils.GetPrefabName(order.Prefab.gameObject),
                    order.Prefab.m_category.ToString(),
                    Number(at.x), Number(at.y), Number(at.z),
                    Number(turn.x), Number(turn.y), Number(turn.z), Number(turn.w),
                    Json(sign ?? string.Empty),
                    Number(order.Scale.x), Number(order.Scale.y), Number(order.Scale.z),
                    Data(order, sign),
                    "1"));
            }

            lines.Add("#HammerOfOden:" + FormatVersion.ToString(CultureInfo.InvariantCulture));
            return lines;
        }

        /// <summary>
        /// The bend and a sign's text, in World Edit Commands' encoding - a flags number, then per
        /// kind of value a count and (name hash, value) pairs, floats before whole numbers before
        /// text. Empty when the piece has neither.
        /// </summary>
        private static string Data(CopyOrder order, string sign)
        {
            bool bent = Mathf.Abs(order.BendDegrees) >= 0.01f;
            bool text = !string.IsNullOrEmpty(sign);
            if (!bent && !text)
            {
                return string.Empty;
            }

            ZPackage package = new ZPackage();
            package.Write((bent ? 1 | 8 : 0) | (text ? 16 : 0));

            if (bent)
            {
                package.Write((byte)1);
                package.Write(BentPiece.DegreesKey.GetStableHashCode());
                package.Write(order.BendDegrees);

                package.Write((byte)3);
                package.Write(BentPiece.AxisKey.GetStableHashCode());
                package.Write(order.BendAxis);
                package.Write(BentPiece.RiseKey.GetStableHashCode());
                package.Write(order.BendRise);
                package.Write(BentPiece.ChoiceKey.GetStableHashCode());
                package.Write(order.BendChoice);
            }

            if (text)
            {
                package.Write((byte)1);
                package.Write("text".GetStableHashCode());
                package.Write(sign);
            }

            return package.GetBase64();
        }

        private static string Number(float value)
        {
            return value.ToString(NumberFormatInfo.InvariantInfo);
        }

        /// <summary>A JSON string, as PlanBuild writes text: quoted, escaped, and never a ';', which would split the line.</summary>
        private static string Json(string text)
        {
            StringBuilder json = new StringBuilder("\"");
            foreach (char c in text.Replace(';', ','))
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': break;
                    case '\t': json.Append("\\t"); break;
                    default: json.Append(c); break;
                }
            }

            return json.Append('"').ToString();
        }

        private static string OneLine(string text)
        {
            return (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
        }
    }
}
