using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;

namespace TheHammerOfOden
{
    /// <summary>
    /// Names every mod that has patched a given method.
    /// </summary>
    /// <remarks>
    /// A stack trace from inside a patched method is close to useless for finding out whose
    /// patch threw. Harmony compiles all of a method's patches into one dynamic method, so
    /// the trace shows a single frame - DMD&lt;Character::CheckDeath&gt; - whoever was
    /// actually at fault, and an exception raised in a prefix from one mod looks exactly like
    /// one raised in a finalizer from another.
    ///
    /// Harmony keeps the answer, though. GetPatchInfo lists every patch on a method with the
    /// plugin id that applied it, in the order they run. Printing that turns "which mod is
    /// doing this?" from an afternoon of rolling versions back into one key press.
    ///
    /// Nothing here is specific to this mod - it will report on any method, including methods
    /// this mod has never touched.
    /// </remarks>
    internal static class PatchInspector
    {
        /// <summary>Methods worth asking about when a death goes wrong.</summary>
        private static readonly (Type type, string method)[] Watched =
        {
            (typeof(Character), "CheckDeath"),
            (typeof(Character), "OnDeath"),
            (typeof(Player), "OnDeath"),
            (typeof(Player), "CreateTombStone"),
            (typeof(Humanoid), "UnequipAllItems"),
            (typeof(Inventory), "RemoveItem"),
            (typeof(Inventory), "AddItem"),
            (typeof(Inventory), "MoveAll"),
            (typeof(Inventory), "Changed"),
        };

        internal static void Dump()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("[patches] who has patched what:");

            foreach ((Type type, string name) in Watched)
            {
                foreach (MethodBase method in Methods(type, name))
                {
                    Describe(report, type, name, method);
                }
            }

            HammerOfOdenPlugin.Info(report.ToString().TrimEnd());
        }

        /// <summary>Every overload, since the interesting one may not be the first.</summary>
        private static IEnumerable<MethodBase> Methods(Type type, string name)
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (MethodInfo method in type.GetMethods(All))
            {
                if (method.Name == name)
                {
                    yield return method;
                }
            }
        }

        private static void Describe(StringBuilder report, Type type, string name, MethodBase method)
        {
            Patches info;

            try
            {
                info = Harmony.GetPatchInfo(method);
            }
            catch (Exception ex)
            {
                report.AppendLine($"  {type.Name}.{name} - could not be read: {ex.Message}");
                return;
            }

            if (info == null)
            {
                return;
            }

            int count = info.Prefixes.Count + info.Postfixes.Count
                + info.Finalizers.Count + info.Transpilers.Count;

            if (count == 0)
            {
                return;
            }

            report.AppendLine($"  {type.Name}.{name}({Parameters(method)})");

            List(report, "prefix", info.Prefixes);
            List(report, "postfix", info.Postfixes);
            List(report, "finalizer", info.Finalizers);
            List(report, "transpiler", info.Transpilers);
        }

        private static string Parameters(MethodBase method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string[] names = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                names[i] = parameters[i].ParameterType.Name;
            }

            return string.Join(", ", names);
        }

        private static void List(StringBuilder report, string kind, IList<Patch> patches)
        {
            foreach (Patch patch in patches)
            {
                report.AppendLine(
                    $"      {kind,-11} {patch.owner}"
                    + $"  ->  {patch.PatchMethod.DeclaringType?.FullName}.{patch.PatchMethod.Name}"
                    + $"  (priority {patch.priority})");
            }
        }
    }
}
