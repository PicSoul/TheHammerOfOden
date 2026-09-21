# The Hammer of Oden

**One mod for Valheim building, in place of several.**

Vanilla lets you turn a piece on the flat and nothing more. This lets you pitch it, roll it, sink it into another piece, stretch it, lay it flat against a wall, pin it in the air and walk around it, lay a whole run of it in one go, take that run back if it was wrong, and see precisely what it is going to snap to.

> **Status: early.** Everything below works and is in daily use, but this has not been released yet.

## Multiplayer

**Install it on the server and on every client.** It is not optional on a shared world, and the
server will refuse entry to a client that does not have it, or that has a different version.

That is deliberate rather than territorial. Most of what this mod does travels the way vanilla
building travels: where a piece sits, which way it faces and what it overlaps are ordinary
networked state, and a player without the mod sees all of it correctly. A piece's **scale** does
not. Valheim stores it on the object, but only reads it back when `m_syncInitialScale` is set on
the instance, and on a building piece that flag comes from the prefab as `false`. This mod reads
the stored value itself and sets the flag afterwards, which makes the size right everywhere the
mod is installed and silently wrong everywhere it is not: a wall you built at twice its height is
an ordinary wall to them, collider and all. They would walk through what you built, or into
nothing at all. A world whose geometry depends on who is looking at it is worse than a world you
have to install a mod to enter.

The server also decides the settings that govern what the world allows - reach, scale limits,
station ranges, how much a single zoop may place, whether undo hands the materials back, how far
the camera may fly and what it may pick up. Keys, colours, marker sizes and camera feel stay
yours; a server has no business choosing those. `Server / LockSettings` decides whether the
server's values are enforced or merely handed out on connect: off suits a server among friends,
on makes them read-only for anyone who is not an admin.

Single player is untouched by all of this. Nothing is pushed, nothing is locked, and every value
stays exactly as your config file has it.

## Controls

Everything is rebindable in the config file.

### Master switch

| Action | Control |
|---|---|
| Turn the whole mod on or off | **Left Shift + H** |

The hammer lights up and gives off a few slow motes while the mod is on, so you can see the state at a glance rather than scrolling to find out. The light reads well at night and washes out at noon; the motes show in any light. Read only while a build tool is in hand, which is the only time any of this applies.

### Mistlands

An upgraded item's glow carries a particle force field reaching five metres, attached to your hand. The Mistlands mist is a particle system, so that field shoves it about wherever you walk — hold an upgraded axe and the fog boils around you; hold a torch, which has no upgrade glow, and it settles. It's Valheim's own effect and almost certainly meant to shape the glow's *own* sparkles.

`QuietUpgradeGlow` switches off just the field, leaving the glow exactly as it was. On by default.

Separately, with the build camera flying you can clear mist around it without a wisplight — `RequiresWisplight = false`. Detection is by status effect rather than by item, so a wisplight, a backpack with one built in, or anything a future mod adds all count.

### Doors

| Action | Control |
|---|---|
| Auto-open doors on/off | **K** |
| Open the door you're looking at, while building | your normal use key |

Vanilla switches interaction off entirely while a build tool is out — right for chests and crafting stations, which you'd trigger by accident lining up a piece, and maddening for the door between you and more wood. So doors, and only doors, stay usable.

Auto-open is off by default and works whatever you're holding. Doors open as you come within 5m and close again once you're 8m away for two seconds.

Both halves live here on purpose. An opener and a closer that each know only distances will fight over any door you stand beside — one sees you near enough to open, the other far enough to close — and making two separate mods agree means tuning thresholds in both until they happen not to overlap. Owning both ends means the doors this opened are *remembered*, so closing them again isn't a guess. A door you opened by hand and left open is never touched.

Neighbouring doors are opened as one, with a single swing direction measured from the middle of the pair. Nothing in the game ties the halves of a double door together — they're just two doors standing next to each other — so `DoorPairDistance` is how they're recognised.

**If you use another mod's auto-close, turn one of them off.**

### Editing a placed piece

| Action | Control |
|---|---|
| Take the piece you're looking at back into the ghost | **Left Alt + E** |
| Cancel and leave it untouched | **Left Alt + E** again |

Its rotation and size come with it, so you start from what is already there rather than from a
fresh piece. Change whatever you like with the usual controls - rotate, scale, nudge, freeze - and
place to apply. The original comes down as the new one goes up, and the materials move across
rather than being charged twice.

The piece is rebuilt rather than altered where it stands, and that is not a shortcut. A built
piece's position and angle are read out of its record once, when it spawns, and never looked at
again - so editing those in place would look right to you and leave the piece exactly where it
was for every other player until their world reloaded. Placing a new piece is something every
client already knows how to draw.

While you are editing, the original stands down: it loses its collision and is drawn faintly, so
you can see where it is without it blocking the space you are trying to move into. Without that, a
small nudge or a slight rescale is the one change you cannot make, because the placement check
sees the original sitting in the way and refuses.

That has a cost worth knowing. Valheim works out what holds a building up from the colliders
actually present, so for the length of an edit the piece supports nothing. Editing a wall that a
roof is resting on can drop the roof, if the game happens to recalculate support in that window.
The window is short and collision returns the instant the edit ends by any route, but
`EditRemovesCollision` turns it off for anyone who would rather not risk it on load-bearing work.

Two further consequences. The piece comes back at full health, so damage and wear are wiped. And a
chest, sign or item stand with something in it is refused rather than quietly emptied, since the
replacement is built from the prefab and the prefab knows nothing about what was inside.

### Build camera

| Action | Control |
|---|---|
| Detach the camera and fly it | **B** |
| Fly | your normal movement keys |
| Up / down | **Space** / **Left Ctrl** |
| Faster | hold **Left Shift** |

Your character stays put and placement follows the camera, so you can put a piece where you could never have stood to aim at it — under a roof, over a cliff, or behind the wall you're building.

It carries a light, and picks up loose items it passes over while respecting your carry weight, which vanilla pickup does not.

The camera is tethered to you, 40m by default. That's not an arbitrary limit: Valheim keeps objects alive around your body, and a camera beyond that either sees a half-built world or forces the game to load a second one around the camera. The second is what makes other build-camera mods expensive, and isn't done here.

### Rotating

| Action | Control |
|---|---|
| Yaw (turn on the flat) | Scroll wheel |
| Pitch (tip forward/back) | **Left Shift** + scroll |
| Roll (tip left/right) | **Left Alt** + scroll |
| Push the piece along your aim | **Left Shift + Left Alt** + scroll |
| Reset the axis you're holding | **J** — *just this axis* |
| Reset everything | **U** — *undo all* |
| Finer / coarser angles | **Page Up** / **Page Down** |

Angles are counted per full turn, defaulting to 32 steps (11.25°). Vanilla uses 16 (22.5°).

### Snapping

| Action | Control |
|---|---|
| Cycle snap points | **Q** / **E** (vanilla) |
| Jump straight back to automatic | **hold Q or E** |
| Cycle anchor density | **Insert** |

### Placement

| Action | Control |
|---|---|
| Free placement on/off | **O** |
| Surface placement on/off | **P** |
| Freeze the piece in place | **Numpad 0** |
| Nudge left / right | **←** / **→** |
| Nudge away / towards you | **↑** / **↓** |
| Nudge up / down | **Home** / **End** |
| Nudge by 1m instead of 0.1m | hold **Left Ctrl** |
| Clear nudging and any run | **Delete** |
| Grid snapping on/off | **G** |
| Lay a run of pieces | **Left Shift** + a nudge direction |
| Undo the last placement | **Left Ctrl + Z** |
| Change a station's build range | **Left Ctrl** + scroll |
| Copy a piece with its full rotation, size and anchor | **Left Shift + middle-click** (vanilla copy) |

### Resizing (hold **Left Shift**)

| Action | Control |
|---|---|
| Narrower / wider | Numpad **4** / **6** |
| Shorter / taller | Numpad **2** / **8** |
| Shallower / deeper | Numpad **7** / **9** |
| Uniform shrink / grow | Numpad **−** / **+** |
| Back to normal size | Numpad **5** |

Hold any of these to repeat. Size is kept as you place a run of pieces and resets when you pick a different one.

## What it does

### Rotation on every axis

Pitch and roll as well as yaw, with snapping intact — tilt a beam and it still snaps to what you put it against.

A gizmo of coloured rings shows each axis, with a bead riding the ring at the current angle. The rings follow the rotation: only yaw is world-aligned, while pitch follows the yaw and roll follows both, because that is what the scroll wheel actually turns the piece about.

### Snap points you can see

Markers show the anchors on the piece you are holding and on the piece you are aiming at, drawn through geometry so a point on the far side is not hidden. The pair currently snapping is highlighted.

Each kind of anchor has its own outline, following AutoCAD's object-snap conventions:

| Anchor | Marker |
|---|---|
| The piece's own snap point | circle |
| Piece centre | circle with a cross, largest |
| Face centre | hexagon |
| Corner | square |
| Edge midpoint | triangle |
| Edge quarter point | diamond |

### Anchors vanilla does not have

Beyond a piece's own snap points, the mod derives more from its shape — **Insert** cycles through how many:

| Mode | Adds |
|---|---|
| `Off` | nothing |
| `Centers` | the piece centre and the centre of each face |
| `CentersAndCorners` | the eight corners |
| `CentersCornersAndEdges` | the midpoint of each of the twelve edges |
| `Full` | quarter points along every edge |

Anchors that land on a snap point the piece already has are dropped, so `CentersAndCorners` adds nothing to a floor whose corners are already snap points — and the debug log says how many it skipped.

With `SnapToDerivedTargets` on, these work on pieces already built too, so you can line up with the centre of a wall.

### Snap points that make sense

Vanilla names snap points with bare ordinals — "Top 1", "Bottom 3" — that say nothing about position and get reused for points in quite different places. The mod renames them after where they actually sit (`Top Front`, `Centre Bottom`) and sorts them into a predictable order: the piece's own points first, then centre, faces, corners, edges.

**Hold Q or E** to jump straight back to automatic snapping instead of cycling all the way around.

Whichever anchor you build a piece by is remembered for that kind of piece. Decide to place walls by their bottom corner and every wall you pick up afterwards is held that way, through switching to a beam and back. Each kind of piece keeps its own choice, and the record survives a restart — it is kept in `com.pics0ul.valheim.thehammerofoden.snappoints.cfg`, next to the config. Delete a line from it to put that piece back on automatic snapping, or delete the file to start over.

### Free placement, on its own key

In vanilla, holding Left Shift turns off snap attraction *and* frees terrain pieces from ground height. That is useful, but Left Shift is also the natural pitch modifier, so tilting a piece silently turned snapping off.

Free placement now lives on **O**. It also relaxes vanilla's placement rules while active — stone on a wood floor, a forge extension crowding its neighbour — and allows pieces to clip into each other.

Wards, no-build zones and occupied ground are never bypassed at any setting.

### Surface placement

Lay a piece flat against whatever you are looking at — a wall, a ceiling, the underside of a roof, the side of a rock — instead of standing it upright on the ground. **P** turns it on.

The piece is positioned by its own geometry rather than its pivot, so a torch meets the wall at its base and a rug lies flat, without you having to know where the artist put the origin. Your own rotation still applies, now measured from the surface rather than from the world, so you can tilt and turn a piece that is already lying against something.

The spin around the surface is taken from the up-slope direction, which depends only on the surface itself. A piece on a wall therefore stays put while you walk past it — deriving that spin from where you are standing is the obvious approach and makes pieces rotate as you move.

By default this applies to everything except the hammer's **Build** and **Heavy Build** tabs. Walls and floors already meet each other through snap points, which is more precise than any surface normal, and aligning them to one fights that. `Applies To` opens it up to structural pieces if you want to build against terrain.

Three modes beyond off: hold the key, toggle it, or `WhenTilted` — active whenever the piece is already pitched or rolled, which is how Flip It does it and costs no key.

Wards, no-build zones and occupied ground are never bypassed, the same as free placement.

### Freezing and nudging

Vanilla ties the piece to wherever your aim meets a surface, so you can only build somewhere you can both see and stand to aim at. That rules out a lot: under a roof you can't back away from, over a cliff edge, deep inside a structure, or anywhere the piece itself is blocking your view of where it should go.

**Numpad 0** pins the piece where it is. Walk around it, look at it from anywhere, judge it properly — it stays put. The arrow keys, **Home** and **End** then move it a step at a time on all three axes, since aiming no longer does anything. **Left Ctrl** makes each step a metre instead of 10cm.

Rotation still works on a frozen piece. If you froze one that was lying against a surface, it keeps that alignment while still answering the rotation keys.

Nudging works unfrozen too. Steps run along the world's own axes: where you're looking picks which axis you mean, but the step follows that axis exactly, so nudges made from anywhere land on the same lattice and pieces line up with each other. `NudgeFrame = Camera` moves along your line of sight instead, at whatever angle you're standing at — useful for pushing a piece away from you, but turning between presses changes what the next one does.

### Grid snapping

**G** restricts placement to a fixed world grid, 1m by default. Snap points handle pieces built to meet each other; this handles the ones that weren't — torches spaced along a wall, fence posts across open ground, chests in a row.

The grid is fixed to the world rather than to where you started building, so it's the same grid everywhere, in every session, for everyone in the world. Height is left alone unless you turn on `GridHeight`, because terrain is rarely level and rounding height on a slope either buries a piece or leaves it hanging.

### Zooping

A wall of ten panels is ten placements, each aimed by hand, and the tenth is never quite in line with the first. **Left Shift + a nudge direction** lays a run instead: press again for one more, press the opposite direction for one fewer. **Delete** cancels it.

Runs compose. Press **Shift + ↑** four times and **Shift + ←** five times and you get a wall five by four rather than two separate runs — four along one axis and five along another is a grid, and asking for it that way is far less work than laying five runs of four. A third direction gives a solid block, which is as far as three dimensions go.

A run lays itself over a moment rather than appearing at once, because every copy is a real placement with its own object and effects and doing sixty in one frame stutters. `PiecesPerFrame` controls that.

Spacing is the piece's own width along the direction you're laying it, so copies sit flush whatever the piece is and however you've turned it. `Spacing = 2` leaves a gap of one piece between each, which suits fence posts and pillars.

The run is previewed before it's built, and it isn't free — each copy is a real placement through Valheim's own code that checks its requirements and pays its materials. If you run out partway, the run stops there rather than leaving a gap in the middle.

### Undo

**Left Ctrl + Z** takes back the last thing you built — the whole run if you zooped, a single piece if you didn't. That's the unit you were thinking in either way.

A piece gives back what it cost and no more — the same as taking it down by hand. An undo that refunded more than that would be a way of manufacturing resources.

*Where* it goes is a different question from how much. Materials are handed straight into your inventory, and only what won't fit is dropped, in one pile at your feet — "won't fit" meaning either out of slots **or** over your carry weight. Valheim only enforces the first; nothing stops a pickup taking you overweight, which is fine when you chose to pick it up and not fine for a refund that arrives unasked. Capacity is read at the moment of the undo, so a belt or a change of gear counts. Vanilla scatters them at each piece instead, which is fine for one piece and a long walk after undoing a run forty long. `RefundToInventory = false` restores the vanilla scatter; the amount is identical either way.

If you build from chests, note the asymmetry: materials can come **out of a chest** and come **back to your pockets**, because the container mods hook spending, not receiving. Undoing a large run built from storage can therefore fill your inventory quickly — which is what the pile at your feet is for.

Ten placements are remembered by default. The limit is about what you can still remember doing rather than memory — a few thousand pieces would cost nothing to keep — so raise `Depth` if you want, knowing that undoing something from twenty minutes ago tends to surprise more than it helps.

Pieces already gone — torn down by hand, or lost to a raid — are skipped, and undo falls through to the placement before rather than doing nothing visible.

### Build range and reach

**Left Ctrl + scroll**, while looking at a crafting station, changes how far that station lets you build. The range is stored on that station, not in the config, so two benches in one base can have different radii and the value travels to other players and survives reloads.

Separately, Valheim limits building twice over: the station's circle says where you *may* build, and an arm's-length limit of about 8m says how far the aiming ray travels. The second has nothing to do with the first, which is why standing in the middle of a 30m workbench still means walking the length of a wall. Reach now rises to match whatever station you're standing in, capped by `ReachLimit`.

This grants nothing that wasn't already permitted — the station still has to cover the spot and every other rule still applies. It saves the walking.

### Resizing

Stretch, compress or uniformly scale a piece before placing it. Size persists through saves, zone reloads and to other players.

**Crafting and production stations are excluded** — workbenches, forges, smelters, kilns, cooking stations, fermenters, beehives — because their behaviour is tied to where parts of the model are. Everything else resizes: chests, doors, gates, portals, torches, beds, item stands, and station add-ons like the forge cooler.

Particle effects are drawn larger to match, though not spread into the space around the piece.

### Copying

Vanilla's copy shortcut already takes a piece's yaw. This extends it to the full 3-axis rotation and the size it was built at.

The snap point comes from your own history rather than from the piece you clicked: copying switches which piece you are holding, and that brings up the anchor you last built that kind of piece by.

## Configuration

`BepInEx/config/com.pics0ul.valheim.thehammerofoden.cfg`

Around a hundred and fifty settings across `General`, `Rotation`, `Snap Points`, `Gizmo`, `Copy`, `Free Placement`, `Surface Placement`, `Freeze`, `Grid`, `Zoop`, `Undo`, `Build Camera`, `Doors`, `Mistlands`, `Station Range`, `Placement Offset`, `Clipping`, `Scale` and `Debug`. Each carries a description in the file explaining what it is for, so the list below is only the handful worth knowing before you start:

| Setting | Default | Why you might change it |
|---|---|---|
| `SnapAnglesPerTurn` | `32` | `16` matches vanilla's 22.5° steps |
| `Display` | `Relevant` | `All` shows every anchor; `ActivePairOnly` shows just the snapping pair |
| `DerivedSnapPoints` | `Centers` | denser modes mean more stops when cycling |
| `SnapToDerivedTargets` | `false` | adds snap targets vanilla does not have |
| `RememberSnapPoint` | `true` | off means every piece starts on automatic snapping |
| `Applies To` | `NonStructural` | `Everything` lets walls and floors lie against surfaces too |
| `AlignToSurface` | `true` | off moves the piece to the surface but keeps your own rotation |
| `GridSize` | `1` | the grid step in metres |
| `NudgeStep` | `0.1` | metres per arrow-key press |
| `Zoop Limit` | `60` | most extra copies one run may place |
| `Zoop PiecesPerFrame` | `8` | raise for instant runs, at the cost of a stutter |
| `Zoop Spacing` | `1` | `2` leaves a piece-sized gap between copies |
| `Undo Depth` | `10` | how many placements back you can go |
| `Restrictions` | `ProductionStations` | what is excluded from resizing |
| `Mode` (Free Placement) | `Toggle` | `Vanilla` hands it back to Left Shift |
| `Freedom` | `SurfacesAndSpacing` | which placement rules free placement sets aside |
| `Tools` | `BuildingOnly` | `AllTools` lets the mod reach the hoe and cultivator |
| `Build Camera Range` | `40` | how far the camera may get from you |
| `Build Camera Pickup` | `true` | sweep up loose items the camera passes |
| `QuietUpgradeGlow` | `true` | stops upgraded gear churning the Mistlands mist |
| `RequiresWisplight` | `true` | off clears mist at the build camera without one |
| `AutoOpenDoors` | `false` | doors open as you approach; **K** toggles it in game |
| `AutoCloseDoors` | `true` | closes only the doors auto-open opened |
| `ShowHammerGlow` | `true` | off if you would rather the tool stayed dark |
| `ShowHammerSparks` | `true` | the motes, independent of the light |
| `SparkRate` | `18` | motes per second; applied live while you watch |
| `GlowHeadOffset` | `0.85` | where along the tool the glow sits, 0 grip to 1 tip |
| `GlowColor` | pale blue | any colour; `GlowIntensity` and `GlowRange` tune it |
| `ExtendReachToStation` | `true` | off keeps vanilla's arm's-length build range |

## Requirements and compatibility

Requires BepInEx. Nothing else. **Client-side only** — no server install, and it does not matter what other players have.

### Do not run alongside

This mod owns the placement pipeline.

| Mod | Why |
|---|---|
| **ComfyGizmo** / **Gizmo** | also rotates the placement ghost |
| **Flip It** | also rotates the ghost and adjusts snapping |
| **Snapheim** | also adjusts the ghost's position |
| **Valheim Plus** (build module) | overlapping placement features |

ComfyGizmo and Snapheim are detected at startup and warned about.

**Known fine:** Jotunn, PlantEverything, AdvancedPortals, XPortal, and other content mods that add pieces without changing how the ghost is positioned.

## Troubleshooting

The startup log states what is actually running:

```text
[Info : The Hammer of Oden] The Hammer of Oden 0.1.0 loaded; 13 patches applied.
  rotation: 32 divisions per turn, pitch=LeftShift, roll=LeftAlt, reset=J/U
  free placement: Toggle on O, freedom=SurfacesAndSpacing
  clipping: WithFreePlacement
  snap points: display=Relevant, derived=Centers, ...
```

Check that first — a feature that appears to do nothing is usually a stale DLL rather than a bug.

Patches are applied one at a time, so a Valheim update that moves something breaks that one feature and names it, rather than silently disabling the whole mod.

**Debug settings.** `DebugLogging` reports placement decisions. `DebugParticles` reports what a scaled piece's effects are doing — visibility, particle counts, renderer bounds — which is how the particle scaling was diagnosed rather than guessed at.

## Building from source

See `BUILDING.md`. Copy `Local.props.example` to `Local.props`, point it at your Valheim install, and run `.\build.ps1`.

## Credits

Built from scratch; no code from other mods is included.

Design owes a debt to the mods that solved these problems first: **ComfyGizmo** (ComfyMods), **Snapheim** (Heimlife) and **Flip It** (cdjensen99). The marker shapes follow AutoCAD's object snap conventions.

## License

MIT — see `LICENSE`. Fork it, fix it, keep it alive if I go quiet.
