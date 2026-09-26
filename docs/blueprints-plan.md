# Blueprints for The Hammer of Oden: research and plan

*Researched 26 September 2026 from the current source of PlanBuild (commit 84b66c0, 23 Sep 2026),
Infinity Hammer (c3e1e89, 15 Sep 2026) and World Edit Commands.*

## The short answer

**Full two-way compatibility is possible, and you do not have to give anything up.**

- Other mods' blueprints (PlanBuild `.blueprint`, Infinity Hammer's variant of it, and BuildShare
  `.vbuild`) can be read by The Hammer of Oden and built with everything we already have:
  construction sites, the flying bottom-up build, materials from chests and backpacks, undo/redo,
  the building panel, and models.
- Blueprints saved by The Hammer of Oden can be written in the same file format, so PlanBuild and
  Infinity Hammer can load them.
- **Scaling survives everywhere.** The shared format already has a scale per piece, and both PlanBuild
  and Infinity Hammer apply it when they place.
- **Bending survives in Infinity Hammer, and degrades gracefully in PlanBuild.** Infinity Hammer
  copies a piece's saved data onto what it places; our bend lives in that saved data, so a bent piece
  placed by Infinity Hammer comes out bent whenever our mod is installed too. PlanBuild ignores the
  data, so it builds the piece straight but otherwise correct. Nothing breaks either way.

The one-way fallback you mentioned (we read theirs, they can't read ours) is not needed.

## Who uses which format

| Mod | Reads | Writes | Looks for files in |
|---|---|---|---|
| **PlanBuild** | `.blueprint`, `.vbuild` | `.blueprint` | Everything under the game folder (searched recursively). Saves to `BepInEx/config/PlanBuild/blueprints` |
| **Infinity Hammer** | `.blueprint`, `.vbuild` | `.blueprint` (PlanBuild layout plus two extra fields) | `BepInEx/config/PlanBuild`, in both the game folder and the mod profile |
| **BuildShare** | `.vbuild` | `.vbuild` | Old mod; the format lives on because both of the above still read it |

World Edit Commands does not read blueprint files itself; it supplies the "object data" format that
Infinity Hammer uses (below).

**The practical upshot:** a file in `BepInEx/config/PlanBuild/` is found by both PlanBuild and
Infinity Hammer. That is where ours should go by default.

## The file format, field by field

A `.blueprint` file is plain text: a few header lines, then one line per piece.

```
#Name:Longhouse
#Creator:PicSoul
#Description:"A cosy longhouse"
#Category:Buildings
#SnapPoints
0;0;0
#Pieces
woodwall;BuildingWorkbench;1.5;0;0;0;0.7071;0;0.7071;"";1;1;1;<data>;1
```

The piece line, split on `;`:

| # | Field | PlanBuild | Infinity Hammer | Notes |
|---|---|---|---|---|
| 0 | Prefab name | ✓ | ✓ | e.g. `woodwall` |
| 1 | Category | ✓ (written, mostly ignored) | ignored | |
| 2–4 | Position x, y, z | ✓ | ✓ | Relative to the blueprint's origin |
| 5–8 | Rotation x, y, z, w | ✓ | ✓ | A world-space quaternion |
| 9 | "Additional info" | ✓ | ✓ | Sign text; see below for what else goes here |
| 10–12 | **Scale x, y, z** | ✓ applied on placing | ✓ applied on placing | Absolute scale, as we store it |
| 13 | Object data | ignored | ✓ | A copy of the piece's saved values (see below) |
| 14 | Chance | ignored | ✓ | Chance of placing the piece; `1` = always |

`.vbuild` (BuildShare) lines are simpler, space-separated:
`name rotX rotY rotZ rotW posX posY posZ`. There's no scale and no extra data.

**Headers:** both mods skip any `#Section` they don't recognise until the next section they do.
PlanBuild's code even comments on this with Infinity Hammer's `#TerrainHeight` section as the
example. So we can add our own section at the end of the file without upsetting either.

### Field 9, "additional info": what other mods put in it

PlanBuild stores more than sign text here, depending on the piece:

| Piece | PlanBuild stores |
|---|---|
| Sign | The text |
| Item stand | The item on it |
| Armour stand | The pose and every item on it |
| Chest | **The full contents**, encoded |
| Door | Open/closed |
| Ward | On/off |

**Our rule: when we read a blueprint, we take the sign text and nothing else.** Chest contents,
item-stand items and armour are ignored, the same anti-duplication rules as our copies. A blueprint
downloaded from the internet cannot hand anyone free items through our mod.

### Field 13, object data (Infinity Hammer's)

A compact encoding of named values: a flags number, then lists of floats, integers, text and so on,
each keyed by the hash of its name. It is exactly how Valheim stores a piece's own values. Our bend is
four such values (`HoO_bendDegrees`, `HoO_bendAxis`, `HoO_bendRise`, `HoO_bendChoice`), and sign text
is one more (`text`).

When we **write**, we put the bend values and the sign text here, nothing else. Infinity Hammer
places the piece with those values, and if our mod is installed, the piece bends as it loads, the
same way a bent piece reloads today.

When we **read**, we take only our bend values and the sign text from it, and ignore everything else
(Infinity Hammer can save chest contents here too).

## How our features map

| Feature | Saved as | In PlanBuild | In Infinity Hammer | In our mod |
|---|---|---|---|---|
| Pieces, positions, rotations | Standard fields | ✓ | ✓ | ✓ |
| **Scale** (incl. uneven) | Fields 10–12 | ✓ | ✓ | ✓ (within the server's scale limits) |
| **Bend** | Field 13 data | Built straight | ✓ bent, if our mod is installed | ✓ |
| Sign text | Field 9 and field 13 | ✓ | ✓ | ✓ |
| Chest contents, item-stand items | Never written by us | n/a | n/a | Ignored when reading others' files |
| Plants | Left out when saving, like copies | n/a | n/a | Skipped when reading |
| Terrain (Infinity Hammer's `#TerrainHeight` / `#TerrainPaint`) | Not written | n/a | n/a | Skipped for now |
| Snap points (`#SnapPoints`) | Written | ✓ | ✓ | Used later for snapping the ghost (phase 3) |
| Model scale and similar extras | Our own `#HammerOfOden` section at the end | Skipped | Skipped | ✓ |

## How blueprints plug into what we already have

Almost nothing new has to be invented. A loaded blueprint is a list of pieces with positions,
rotations, scales, bends and sign text. That is exactly the "copy order" list that copies,
construction sites, redo and models already run on.

- **Placing a blueprint = placing a copy.** It is held the same way: preview, turning, uniform
  scaling, nudging, freeze. Placing it creates a **construction site**. What you can afford flies in
  bottom-up, the rest waits as a ghost, and the building panel lists what's missing. Materials come
  from your inventory, your backpack (AdventureBackpacks) and nearby chests (AzuCraftyBoxes), as now.
- **Undo/redo** work unchanged; a placed blueprint is one undo step.
- **Models from blueprints:** pressing the model key while holding a blueprint turns it into a
  miniature on a table, without ever building it. It's a nice way to show off a blueprint collection.
- **Saving a blueprint = a selection written to a file.** It uses the same saved-record reading that
  copies already use, so unloaded pieces and bends are included.
- **Rules carried over:** unlearned pieces are left out and listed (unless `CopyAllowUnlearned` is
  on), plants are skipped, no crafting station is needed, and the whole bill is shown before you
  place.

The one piece of real new work in holding a blueprint: today a copy is held by an actual piece you
grabbed. A blueprint has no piece standing in the world, so the held piece has to come from the build
menu instead. It would be the lowest piece in the blueprint that you know how to build. That's a
contained change to how a copy is picked up.

## What you would use

1. **Save:** with a selection made, press a key (proposed: **Numpad Enter while nothing is held**,
   or a new key, your choice). Type a name, and it's saved to the blueprint folder. Optionally,
   a thumbnail picture is saved beside it. PlanBuild shows `.png` thumbnails next to its blueprints,
   and we can render one from the model code.
2. **Browse:** a **blueprint book** window (proposed key: **K**), in the same style as the F3 guide.
   It lists every blueprint in the shared folders, with search, categories, piece count, cost and
   which mod made each one.
3. **Pick one** and it's in your hand like a copy. Place it for a construction site, or press the
   model key for a miniature.

## Changing a saved blueprint

**When its pieces still stand in this world:** adjust the selection and save again.
- The save box offers the blueprint's name, because the mod remembers which blueprint a selection was
  last saved as.
- Saving under an existing name asks first: *"Replace 'Longhouse' (212 pieces) with this selection
  (240 pieces)?"* The blueprint's description and creator are kept.
- The previous version is kept as a backup file next to it, so a mistake can be put right by hand.

**When it came from another world or server:** place it first, then change it.
1. Place it from the book. It becomes a construction site: what you can afford goes up, and the rest
   waits as a ghost.
2. Add pieces, take pieces down, then select the result.
3. Save it over the original. **A selection that includes a construction site also saves that site's
   unbuilt ghost pieces**, so you don't have to afford the whole blueprint to edit it.
4. If you only placed it to edit it, undo afterwards. The pieces come down and the materials come
   back.

A site also remembers which blueprint it came from, so re-saving offers the original name even days
later.

**Not yet possible:** removing a single unbuilt ghost piece. Ghosts have no solid body to aim at, so
for now a piece has to be built before it can be taken down. A later **draft mode** could fix this:
a blueprint placed as a free ghost that never builds, purely for editing, whose ghost pieces can be
picked out and removed. Blueprint files are plain text, one line per piece, so deleting a piece's
line by hand also works, as PlanBuild users already do.

## Plan, in phases

**Status (26 September 2026):** phase 1 is built and working in the DEV copy, with ground shaping as
a book option. Phase 2 is built and awaiting its first in-game test. Loading our files in PlanBuild
and Infinity Hammer is still untested.

| Phase | What | Effort |
|---|---|---|
| **1. Read** | Load `.blueprint` and `.vbuild` from the shared folders; turn them into copy orders; the blueprint book window with its turning model preview; hold, place (as a site) or model it | Largest: about 3 evenings |
| **2. Write** | Save a selection as a `.blueprint` (field 13 for bends and sign text, our end section for extras); a thumbnail from the preview camera; overwrite with confirmation and a backup; remember which blueprint a selection or site came from; save a site's unbuilt ghost pieces with it | About 1–2 evenings |
| **3. Polish** | Snap the held blueprint using its `#SnapPoints`; show a blueprint's cost and missing pieces in the book before picking it | About 1 evening |
| Later, maybe | Draft mode (a free, never-building ghost for editing a blueprint piece by piece) |  |
| **Later, wanted** | **Server blueprint library:** a blueprint folder on the server (filled by FTP on GPortal), a book tab to browse it with each blueprint downloaded only when picked, and optionally players or admins uploading. Blueprints are hand-shared files until then |  |
| Later, maybe | Building Infinity Hammer's ground-shaping sections (skipped for now; hoe steps in blueprints are already a book option) | Not planned |

Phase 1 first, because reading gives you all the blueprints already out there on day one. Each phase
is testable on its own.

## Previewing blueprints with the model

The book shows the selected blueprint as a turning 3D miniature, drawn by the same code as table
models:

- When a blueprint is picked in the book, it is built once as a model and kept while it stays
  selected.
- The model sits hidden, far out of sight, on a layer only a small preview camera sees. That camera
  films it into a picture shown in the book, with its own light so it looks the same by day or night.
- It turns slowly on its own. Dragging with the mouse turns it by hand, and scrolling zooms.
- It gets every fix the models already have: no warp, textures sized for a model, every structural
  piece drawn, bends included.
- It only costs anything while the book is open. A big blueprint builds its preview in about the
  20–30 ms a table model takes.

The same camera also takes a **thumbnail** when a blueprint is saved. PlanBuild shows `.png` pictures
next to blueprints, so ours get pictures there too, and the book can show blueprints as a grid of
small pictures.

## Decisions (26 September 2026)

1. **Folder:** the shared `BepInEx/config/PlanBuild/blueprints` in the game folder.
2. **Keys:** **Numpad Enter** with a selection made and nothing held saves a blueprint; **K** opens
   the blueprint book. Neither clashes with the mod's keys, and K isn't bound by vanilla Valheim.
   Numpad Enter keeps its current job, turning a held copy into a model, whenever a copy is in hand.
   Both keys are settings, in case another mod in someone's profile uses them.
3. **Scale:** a blueprint's scaled pieces are kept within the server's scale limits, the same rule
   as scaling by hand. Any piece that had to be clamped is mentioned.
4. **Terrain:** Infinity Hammer's ground-shaping sections are skipped for now.
5. **Previews:** the blueprint book previews with the model, as above, and saving writes a thumbnail.

## Risks and open questions

- **Not tested yet:** I've read all three mods' code, but haven't yet loaded a file written by us in
  PlanBuild or Infinity Hammer. The first test in phase 2 should be exactly that, using both mods
  in a test profile.
- **Infinity Hammer's "save data" option** can put chest contents into its files. We ignore them, but
  someone using Infinity Hammer to place a blueprint gets whatever Infinity Hammer does. That's its
  behaviour, not ours.
- **Gale and the game folder:** PlanBuild searches relative to the game folder, while Infinity Hammer
  also searches the mod profile. Writing into the game folder's `BepInEx/config/PlanBuild` reaches
  both. That folder is outside Gale's managed files, so it doesn't clash with the dev-copy rules.
- **Formats can change:** PlanBuild and Infinity Hammer are both actively maintained (both updated
  this month). The format has stayed stable for years, and both mods deliberately keep old files
  loading, so this is low risk.
- **Licences:** PlanBuild is WTFPL, and Infinity Hammer is public domain. We read and write their file
  format without copying their code, so there's no concern either way.
