# The Hammer of Oden

**Free-axis rotation, precise snapping and resizable pieces for Valheim building.**

Vanilla lets you turn a piece on the flat and nothing more. This lets you pitch it, roll it, sink it into another piece, stretch it, copy the exact angle off something you already built, and see precisely what it is going to snap to.

> **Status: early.** Everything below works and is in daily use, but this has not been released yet.

## Controls

Everything is rebindable in the config file.

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
| Clear all nudging | **Delete** |
| Grid snapping on/off | **G** |
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

Around forty settings across `Rotation`, `Snap Points`, `Gizmo`, `Free Placement`, `Clipping`, `Scale`, `Placement Offset` and `Debug`. Each carries a description in the file. The ones worth knowing:

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
| `Restrictions` | `ProductionStations` | what is excluded from resizing |
| `Mode` (Free Placement) | `Toggle` | `Vanilla` hands it back to Left Shift |
| `Freedom` | `SurfacesAndSpacing` | which placement rules free placement sets aside |

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
