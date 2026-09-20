using System.Text;
using HarmonyLib;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// Reports what is actually acting on the mist around the player.
    /// </summary>
    /// <remarks>
    /// Written for one question: why the Mistlands mist churns with most items in hand and
    /// settles with a plain torch. That is a clean, repeatable difference, and a clean
    /// difference has a cause - elimination and a description of "vanilla swirl" do not
    /// account for it, so this goes and looks.
    ///
    /// Mist is a particle system pushed about by ParticleSystemForceField components, and
    /// ParticleMist keeps a list of the ones it is using. So the thing to compare between
    /// one item and another is simply which fields exist near the player and what they are
    /// set to. If a torch brings one that an axe does not, that is the whole answer.
    ///
    /// FindObjectsOfType is exactly the sweep this mod refuses to do anywhere else. It is
    /// acceptable here because it runs only while the setting is on and only when the key is
    /// pressed - never on a timer, and never in a shipped configuration.
    /// </remarks>
    internal static class MistDiagnostics
    {
        internal static void Dump(Player player)
        {
            if (player == null)
            {
                return;
            }

            StringBuilder report = new StringBuilder();

            // The attachment object in the hand is called "attach(Clone)" whatever it
            // holds, so the item itself has to be asked for by name.
            ItemDrop.ItemData item = RightItem != null ? RightItem(player) : null;

            report.AppendLine("[mistdiag] holding: "
                + (item == null ? "nothing" : item.m_shared?.m_name ?? "?")
                + " / prefab " + (item?.m_dropPrefab != null ? item.m_dropPrefab.name : "?"));

            Vector3 where = player.transform.position;

            // Demisters, which are what Valheim itself uses to part the mist.
            var demisters = Demister.GetDemisters();
            report.AppendLine($"[mistdiag] demisters live: {(demisters == null ? -1 : demisters.Count)}");

            if (demisters != null)
            {
                foreach (Demister demister in demisters)
                {
                    if (demister == null)
                    {
                        continue;
                    }

                    float away = Vector3.Distance(demister.transform.position, where);
                    if (away > 60f)
                    {
                        continue;
                    }

                    report.AppendLine($"[mistdiag]   demister '{demister.name}' at {away:0.#}m");
                }
            }

            // Every force field in the scene, which is the set the mist can be pushed by.
            foreach (ParticleSystemForceField field
                in Object.FindObjectsOfType<ParticleSystemForceField>())
            {
                float away = Vector3.Distance(field.transform.position, where);
                if (away > 60f)
                {
                    continue;
                }

                report.AppendLine(
                    $"[mistdiag]   field '{Path(field.transform)}' at {away:0.#}m, "
                    + $"enabled {field.enabled}, shape {field.shape}, "
                    + $"range {field.startRange:0.#}..{field.endRange:0.#}, "
                    + $"gravity {field.gravity.constant:0.##}, "
                    + $"drag {field.drag.constant:0.##}, "
                    + $"vortex {field.rotationSpeed.constant:0.##}");
            }

            HammerOfOdenPlugin.Info(report.ToString().TrimEnd());
        }

        private static readonly AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> RightItem =
            Resolve();

        private static AccessTools.FieldRef<Humanoid, ItemDrop.ItemData> Resolve()
        {
            try
            {
                return AccessTools.FieldRefAccess<Humanoid, ItemDrop.ItemData>("m_rightItem");
            }
            catch (System.Exception ex)
            {
                HammerOfOdenPlugin.Error("[mistdiag] cannot read the held item. " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The whole chain from the root, so it is clear what the field hangs from.
        /// </summary>
        /// <remarks>
        /// Root and leaf alone gave "Player(Clone)/Particle System Force Field", which says
        /// the field is on the player and nothing about which item put it there.
        /// </remarks>
        private static string Path(Transform t)
        {
            string path = t.name;

            for (Transform up = t.parent; up != null; up = up.parent)
            {
                path = up.name + "/" + path;
            }

            return path;
        }
    }
}
