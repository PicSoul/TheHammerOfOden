using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TheHammerOfOden
{
    /// <summary>
    /// A small window, pinned beside the other game displays, listing what your construction
    /// sites still need - for as long as any of them is unfinished.
    /// </summary>
    /// <remarks>
    /// The materials a site is waiting for used to be a message, and a message goes: put the
    /// hammer away, or wait a few seconds, and the list was gone while the building was not.
    /// This stays until the site is finished or stopped.
    ///
    /// It shows with the rest of the game's own displays and hides when they do: the HUD hidden,
    /// the pause menu, the full map, a cutscene.
    ///
    /// A site's first piece - and the plan on it - is only loaded while you are near it, so the
    /// panel keeps a record of its own: each of your sites, what it still needs, and where it is.
    /// The record is saved with your character, per world, so walking away or logging out does
    /// not lose track of a building you have not finished. A site found gone - its first piece
    /// taken down while you were away - is dropped once you have stood near where it was.
    /// </remarks>
    internal static class SitePanel
    {
        private sealed class Entry
        {
            internal long Id;
            internal Vector3 Position;
            internal int Built;
            internal int Total;
            internal bool Waiting;
            internal readonly List<(string item, int amount)> Needs = new List<(string, int)>();
            internal float NearSince = -1f;
        }

        private const string SaveKey = "HoO_sites";

        private static readonly Dictionary<long, Entry> Entries = new Dictionary<long, Entry>();
        private static long _loadedFor;
        private static bool _dirty;
        private static float _nextSave;
        private static float _nextCheck;

        private static GUIStyle _box;
        private static GUIStyle _title;
        private static GUIStyle _line;
        private static Texture2D _backdrop;
        private static Texture2D _stripe;
        private static int _styledFontSize;

        // ------------------------------------------------------------------ keeping the record

        /// <summary>Brings a site's entry up to date: what is up, what it waits for, what it needs.</summary>
        internal static void Update(ConstructionSite site, bool waiting)
        {
            Player player = Player.m_localPlayer;
            if (site == null || player == null || site.Owner != player.GetPlayerID() || site.Plan == null || site.Done == null)
            {
                return;
            }

            Load();

            if (!Entries.TryGetValue(site.Id, out Entry entry))
            {
                entry = new Entry { Id = site.Id };
                Entries[site.Id] = entry;
            }

            entry.Position = site.View != null ? site.View.transform.position : entry.Position;
            entry.Built = site.BuiltCount + 1;
            entry.Total = site.Plan.Count + 1;
            entry.Waiting = waiting;
            entry.Needs.Clear();
            entry.Needs.AddRange(GroupBuilder.Needs(site));
            _dirty = true;
        }

        /// <summary>The site is finished or stopped: off the panel.</summary>
        internal static void Forget(long id)
        {
            if (Entries.Remove(id))
            {
                _dirty = true;
            }
        }

        /// <summary>
        /// Loads this character's record for this world, once per world. Kept in the character's
        /// own saved data, which is where the game keeps a mod's per-character notes.
        /// </summary>
        private static void Load()
        {
            Player player = Player.m_localPlayer;
            long world = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L;
            if (player == null || world == 0L || _loadedFor == world)
            {
                return;
            }

            _loadedFor = world;
            Entries.Clear();

            if (!player.m_customData.TryGetValue(SaveKey, out string saved) || string.IsNullOrEmpty(saved))
            {
                return;
            }

            foreach (string line in saved.Split('\n'))
            {
                // world|id|x,y,z|built|total|item:amount;item:amount
                string[] parts = line.Split('|');
                if (parts.Length < 6 || parts[0] != world.ToString(CultureInfo.InvariantCulture))
                {
                    continue;
                }

                try
                {
                    string[] at = parts[2].Split(',');
                    Entry entry = new Entry
                    {
                        Id = long.Parse(parts[1], CultureInfo.InvariantCulture),
                        Position = new Vector3(
                            float.Parse(at[0], CultureInfo.InvariantCulture),
                            float.Parse(at[1], CultureInfo.InvariantCulture),
                            float.Parse(at[2], CultureInfo.InvariantCulture)),
                        Built = int.Parse(parts[3], CultureInfo.InvariantCulture),
                        Total = int.Parse(parts[4], CultureInfo.InvariantCulture),
                        Waiting = true
                    };

                    foreach (string need in parts[5].Split(';'))
                    {
                        string[] pair = need.Split(':');
                        if (pair.Length == 2)
                        {
                            entry.Needs.Add((pair[0], int.Parse(pair[1], CultureInfo.InvariantCulture)));
                        }
                    }

                    Entries[entry.Id] = entry;
                }
                catch
                {
                    // A line from an older or damaged record is simply skipped.
                }
            }
        }

        private static void Save()
        {
            Player player = Player.m_localPlayer;
            long world = ZNet.instance != null ? ZNet.instance.GetWorldUID() : 0L;
            if (player == null || world == 0L)
            {
                return;
            }

            // Keep other worlds' lines; replace this world's.
            List<string> lines = new List<string>();
            string worldKey = world.ToString(CultureInfo.InvariantCulture);
            if (player.m_customData.TryGetValue(SaveKey, out string saved) && !string.IsNullOrEmpty(saved))
            {
                foreach (string line in saved.Split('\n'))
                {
                    if (line.Length > 0 && !line.StartsWith(worldKey + "|"))
                    {
                        lines.Add(line);
                    }
                }
            }

            foreach (Entry entry in Entries.Values)
            {
                StringBuilder needs = new StringBuilder();
                foreach ((string item, int amount) in entry.Needs)
                {
                    if (needs.Length > 0)
                    {
                        needs.Append(';');
                    }

                    needs.Append(item).Append(':').Append(amount.ToString(CultureInfo.InvariantCulture));
                }

                lines.Add(string.Join("|",
                    worldKey,
                    entry.Id.ToString(CultureInfo.InvariantCulture),
                    string.Join(",",
                        entry.Position.x.ToString("0.##", CultureInfo.InvariantCulture),
                        entry.Position.y.ToString("0.##", CultureInfo.InvariantCulture),
                        entry.Position.z.ToString("0.##", CultureInfo.InvariantCulture)),
                    entry.Built.ToString(CultureInfo.InvariantCulture),
                    entry.Total.ToString(CultureInfo.InvariantCulture),
                    needs.ToString()));
            }

            if (lines.Count == 0)
            {
                player.m_customData.Remove(SaveKey);
            }
            else
            {
                player.m_customData[SaveKey] = string.Join("\n", lines);
            }

            _dirty = false;
        }

        /// <summary>
        /// Now and then: saves the record if it changed, and drops a site found gone - you have
        /// stood near where it was for a while and it never loaded.
        /// </summary>
        private static void Tend(Player player)
        {
            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + 2f;

                List<long> gone = null;
                foreach (Entry entry in Entries.Values)
                {
                    bool live = false;
                    foreach (ConstructionSite site in ConstructionSite.All)
                    {
                        if (site != null && site.Id == entry.Id)
                        {
                            live = true;
                            break;
                        }
                    }

                    bool near = Vector3.Distance(player.transform.position, entry.Position) < 40f;
                    if (live || !near)
                    {
                        entry.NearSince = -1f;
                        continue;
                    }

                    if (entry.NearSince < 0f)
                    {
                        entry.NearSince = Time.time;
                    }
                    else if (Time.time - entry.NearSince > 10f)
                    {
                        (gone ??= new List<long>()).Add(entry.Id);
                    }
                }

                if (gone != null)
                {
                    foreach (long id in gone)
                    {
                        Forget(id);
                    }
                }
            }

            if (_dirty && Time.time >= _nextSave)
            {
                _nextSave = Time.time + 5f;
                Save();
            }
        }

        // ------------------------------------------------------------------ drawing

        internal static void Draw()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !ModConfig.SitePanelShown.Value)
            {
                return;
            }

            Load();
            Tend(player);

            if (Entries.Count == 0 || !GameDisplaysShowing())
            {
                return;
            }

            float ui = Mathf.Max(0.5f, Screen.height / 1080f);
            EnsureStyles(Mathf.RoundToInt(ModConfig.SitePanelFontSize.Value * ui));

            float width = ModConfig.SitePanelWidth.Value * ui;
            float icon = _line.fontSize * 1.4f;
            float rowHeight = icon + 2f * ui;
            float pad = _line.fontSize * 0.7f;

            // Nearest site first.
            List<Entry> shown = new List<Entry>(Entries.Values);
            shown.Sort((a, b) => (a.Position - player.transform.position).sqrMagnitude
                .CompareTo((b.Position - player.transform.position).sqrMagnitude));
            if (shown.Count > 3)
            {
                shown.RemoveRange(3, shown.Count - 3);
            }

            int mostNeeds = ModConfig.SitePanelMostNeeds.Value;

            float height = pad;
            foreach (Entry entry in shown)
            {
                height += _title.fontSize * 2.8f + Mathf.Min(entry.Needs.Count, mostNeeds) * rowHeight
                    + (entry.Needs.Count > mostNeeds ? rowHeight : 0f) + pad;
            }

            MessagePosition where = ModConfig.SitePanelPosition.Value;
            float offsetX = ModConfig.SitePanelOffsetX.Value * ui;
            float offsetY = ModConfig.SitePanelOffsetY.Value * ui;

            float x = where == MessagePosition.TopLeft || where == MessagePosition.MiddleLeft || where == MessagePosition.BottomLeft
                ? offsetX
                : where == MessagePosition.TopCentre || where == MessagePosition.BottomCentre
                    ? (Screen.width - width) / 2f
                    : Screen.width - width - offsetX;
            float y = where == MessagePosition.TopLeft || where == MessagePosition.TopRight || where == MessagePosition.TopCentre
                ? offsetY
                : where == MessagePosition.MiddleLeft || where == MessagePosition.MiddleRight
                    ? (Screen.height - height) / 2f + offsetY
                    : Screen.height - offsetY - height;

            GUI.Box(new Rect(x, y, width, height), GUIContent.none, _box);
            GUI.DrawTexture(new Rect(x, y, 3f * ui, height), _stripe);

            float cy = y + pad;
            foreach (Entry entry in shown)
            {
                float distance = Vector3.Distance(player.transform.position, entry.Position);
                string state = distance > ModConfig.SiteRange.Value
                    ? $"away - {distance:0} m off, carries on within {ModConfig.SiteRange.Value:0} m"
                    : entry.Waiting ? "waiting for materials" : "building";

                GUI.Label(new Rect(x + pad, cy, width - 2f * pad, _title.fontSize * 1.4f),
                    $"<b>Building - {entry.Built:N0} of {entry.Total:N0} up</b>", _title);
                cy += _title.fontSize * 1.4f;
                GUI.Label(new Rect(x + pad, cy, width - 2f * pad, _title.fontSize * 1.4f),
                    $"<color=#94A3B8>{state}</color>", _line);
                cy += _title.fontSize * 1.4f;

                for (int i = 0; i < entry.Needs.Count && i < mostNeeds; i++)
                {
                    (string item, int amount) = entry.Needs[i];
                    ItemDrop drop = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(item)?.GetComponent<ItemDrop>() : null;

                    Sprite sprite = drop != null ? drop.m_itemData.GetIcon() : null;
                    if (sprite != null && sprite.texture != null)
                    {
                        Rect tex = sprite.textureRect;
                        Texture2D texture = sprite.texture;
                        GUI.DrawTextureWithTexCoords(new Rect(x + pad, cy, icon, icon), texture,
                            new Rect(tex.x / texture.width, tex.y / texture.height, tex.width / texture.width, tex.height / texture.height));
                    }

                    string name = drop != null && Localization.instance != null
                        ? Localization.instance.Localize(drop.m_itemData.m_shared.m_name)
                        : item;
                    GUI.Label(new Rect(x + pad + icon + 6f * ui, cy, width - 2f * pad - icon, rowHeight), $"{amount:N0}  {name}", _line);
                    cy += rowHeight;
                }

                if (entry.Needs.Count > mostNeeds)
                {
                    GUI.Label(new Rect(x + pad, cy, width - 2f * pad, rowHeight),
                        $"<color=#94A3B8>and {entry.Needs.Count - mostNeeds} more</color>", _line);
                    cy += rowHeight;
                }

                cy += pad;
            }
        }

        /// <summary>
        /// Whether the game's own displays - the minimap and the rest - are showing: not hidden by
        /// the player, not under the pause menu or the full map, not in a cutscene.
        /// </summary>
        internal static bool GameDisplaysShowing()
        {
            return Hud.instance != null && Hud.instance.IsVisible() && !Hud.IsUserHidden()
                && !Menu.IsVisible() && !Minimap.IsOpen()
                && (Game.instance == null || !Game.IsPaused());
        }

        private static void EnsureStyles(int fontSize)
        {
            if (_box != null && _styledFontSize == fontSize && _backdrop != null)
            {
                return;
            }

            _styledFontSize = fontSize;
            if (_backdrop == null)
            {
                _backdrop = Solid(new Color(0.065f, 0.075f, 0.095f, 0.85f));
                _stripe = Solid(new Color(0.85f, 0.68f, 0.28f, 1f));
            }

            _box = new GUIStyle(GUI.skin.box) { border = new RectOffset(0, 0, 0, 0) };
            _box.normal.background = _backdrop;

            _title = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 1, richText = true, wordWrap = false };
            _title.normal.textColor = new Color(0.96f, 0.82f, 0.47f);

            _line = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                richText = true,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };
            _line.normal.textColor = new Color(0.88f, 0.91f, 0.95f);
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            return tex;
        }

        /// <summary>Saves and forgets, for leaving a world.</summary>
        internal static void Reset()
        {
            if (_dirty)
            {
                Save();
            }

            Entries.Clear();
            _loadedFor = 0L;
        }
    }
}
