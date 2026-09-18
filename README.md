# The Hammer of Oden

**Free-axis rotation and precise placement for Valheim building.**

Vanilla lets you turn a piece on the flat and nothing more. This lets you pitch it, roll it, copy the exact angle off something you already built, and keep snapping while you do it.

> **Status: 0.1.0 — early.** Rotation and rotation-copying work. Surface placement and the extended snapping tools are not built yet. See [Roadmap](#roadmap).

## Controls

Everything below is rebindable in the config file.

| Action | Control | Config key |
|---|---|---|
| Rotate **yaw** (turn on the flat) | Scroll wheel | — |
| Rotate **pitch** (tip forward/back) | **Left Shift** + scroll | `XAxisKey` |
| Rotate **roll** (tip left/right) | **Left Alt** + scroll | `ZAxisKey` |
| Reset the axis you're holding | **J** | `ResetAxisKey` |
| Reset **all** axes at once | **U** | `ResetAllKey` |
| Copy a placed piece **with its full rotation** | **Left Shift + middle-click** | `CopyRotationOnPieceCopy` |
| Copy only the rotation, keep your current piece | *unbound* | `CopyRotationKey` |
| Toggle **free placement** | **O** | `FreePlacementKey` |

### Notes on the controls

**Reset is axis-aware.** `J` on its own zeroes the yaw. **Shift + J** zeroes the pitch, **Alt + J** zeroes the roll — the same modifier that selects an axis to rotate also selects which one to reset. `U` flattens everything at once.

If it helps them stick: **J** for *just this axis*, **U** for *undo all*.

**Shift + middle-click is the vanilla copy shortcut**, not something this mod invents. Vanilla already copies the piece and its yaw; this mod extends it to carry the pitch and roll as well. Plain middle-click is still vanilla *remove* and is unaffected.

**Rotation persists between pieces** by default, so you can tilt a wall, then switch to a beam and keep the same angle. Set `ResetOnPieceChange = true` if you would rather start flat each time you pick a new piece.

### Free placement

In vanilla, holding Left Shift ("AltPlace") while building does two things:

1. **Turns off snap attraction.** Snapping only reaches 0.5m in the first place, so this matters only when you are already close enough to snap and do not want to.
2. **Frees terrain pieces from ground height.** For pieces like paths and level-ground, vanilla forces the height to the ground under your character. Holding Shift places at the height you are actually aiming at instead.

The problem is that Left Shift is also the natural pitch modifier, so tilting a piece silently turned snapping off at the same time.

This mod takes that decision over, so the two live on separate keys. **Press `O`** to toggle free placement on and off; a message in the corner tells you which state you are in, and it clears itself when you leave build mode.

| `Mode` | Behaviour |
|---|---|
| `Toggle` *(default)* | Tap `FreePlacementKey` to switch it on and off |
| `Hold` | Hold `FreePlacementKey`, closer to vanilla's feel |
| `Vanilla` | Leave it to Valheim on Left Shift. Use this if you rebind `XAxisKey` off Left Shift |

Vanilla's *other* uses of Left Shift are untouched: **Shift + middle-click** still copies a piece, and **Shift + E** still alt-interacts.

### Keybind conflicts

The defaults follow the convention most Valheim builders already have in their fingers, which means they can collide with other mods that use the same modifiers.

| Symptom | Cause | Fix |
|---|---|---|
| "No Crafting Station Nearby" while rolling | **StationRangePlus** uses Alt + scroll to adjust station range, and answers to *both* Alt keys | Change its `HoldKey`, or change this mod's `ZAxisKey` |
| Character crouches while rolling | `ZAxisKey` set to Left Ctrl, which is vanilla Crouch | Pick a different key |
| Snapping stops while pitching | Left Shift is vanilla AltPlace | Fixed by default — free placement now lives on `O`. Set `Mode = Vanilla` to get the old behaviour back |

Both keys are rebindable, so whichever mod you use more often should keep the key.

## Configuration

`BepInEx/config/com.pics0ul.valheim.thehammerofoden.cfg`

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Master switch. Off returns placement entirely to vanilla. |
| `SnapDivisions` | `16` | Rotation steps per 180°. `16` = 11.25° per notch. Vanilla is `8` (22.5°). Range 2–256. |
| `XAxisKey` | `LeftShift` | Hold to rotate pitch. |
| `ZAxisKey` | `LeftAlt` | Hold to rotate roll. |
| `ResetAxisKey` | `J` | Zero the currently selected axis. |
| `ResetAllKey` | `U` | Zero every axis, returning the piece to flat. |
| `CopyRotationOnPieceCopy` | `true` | Extend vanilla's copy-piece to carry full 3-axis rotation. |
| `CopyRotationKey` | *empty* | Copy rotation from the piece you're looking at, without switching to it. |
| `ResetOnPieceChange` | `false` | Flatten rotation when you select a different build piece. |
| `Mode` (Free Placement) | `Toggle` | Who owns free placement: `Toggle`, `Hold` or `Vanilla`. |
| `FreePlacementKey` | `O` | Key for the Toggle and Hold modes. |
| `DebugLogging` | `false` | Write placement diagnostics to the BepInEx log. |

Finer steps aren't always better: `SnapDivisions = 16` is a good default, but for pieces that are meant to line up flush, `8` matches vanilla's grid and makes structures easier to keep square.

## Requirements and compatibility

Requires BepInEx. Nothing else.

**Client-side only.** No server install, and it doesn't matter what other players have.

### Do not run alongside

This mod owns the placement pipeline. Running another mod that rotates or repositions the build ghost gives unpredictable results, because both are writing the same values every frame.

| Mod | Why |
|---|---|
| **ComfyGizmo** / **Gizmo** | Also rotates the placement ghost |
| **Flip It** | Also rotates the ghost and adjusts snapping |
| **Snapheim** | Also adjusts the ghost's final position |
| **Valheim Plus** (build module) | Overlapping placement features |

The mod checks for ComfyGizmo and Snapheim at startup and logs a warning if either is present. It can't stop them, so disable them yourself.

**Known to be fine:** Jotunn, PlantEverything, AdvancedPortals, XPortal, and other content mods that add pieces without touching how the ghost is positioned.

## Troubleshooting

Set `DebugLogging = true` and look in `BepInEx/LogOutput.log`. A healthy start looks like:

```text
[Info : The Hammer of Oden] The Hammer of Oden 0.1.0 loaded.
```

**"Snapping is off" / pieces land beside where they should.** Almost always another placement mod still installed. Check the log for a conflict warning.

**Nothing rotates.** Look for an error about the vanilla rotation call not being found. That means a Valheim update moved what this mod hooks; it disables its rotation handling rather than guess, so building still works normally. Please report it.

**Scroll changes the build piece instead of rotating.** The build menu is open — close it first.

## Roadmap

- [x] Free 3-axis rotation with configurable snap divisions
- [x] Copy full rotation from a placed piece
- [ ] Surface placement — put pieces on walls, ceilings and slopes
- [ ] Extended snap points — centers, halves, seams, depth
- [ ] Snap point previews
- [ ] Per-piece rotation memory

## Credits

Built from scratch. No code from other mods is included.

Design owes a debt to the mods that solved these problems first: **ComfyGizmo** (ComfyMods), **Snapheim** (Heimlife), and **Flip It** (cdjensen99).

## License

MIT — see `LICENSE`. Fork it, fix it, keep it alive if I go quiet.
