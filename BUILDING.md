# Building from source

## What you need

- [.NET SDK](https://dotnet.microsoft.com/download) 6.0 or newer — the project targets `netstandard2.1`, which any modern SDK can build
- A Valheim install
- BepInEx installed for Valheim

No game assemblies are included here. Valheim's DLLs are not redistributable, so the build references them from your own install.

## Setup

```sh
git clone https://github.com/PicSoul/TheHammerOfOden.git
cd TheHammerOfOden
cp Local.props.example Local.props
```

Edit `Local.props` and point `ValheimInstall` at the folder containing `valheim.exe`.

If you use **r2modman** or **Thunderstore Mod Manager**, BepInEx lives inside the mod profile rather than the game folder, so set `BepInExCore` as well:

```xml
<BepInExCore>$(AppData)\r2modmanPlus-local\Valheim\profiles\YourProfile\BepInEx\core</BepInExCore>
```

`Local.props` is gitignored, so your paths never reach a commit. The `VALHEIM_INSTALL` and `BEPINEX_CORE` environment variables work as an alternative.

A wrong path stops the build with a message naming which one, rather than a few hundred "type not found" errors.

## Build

```sh
dotnet build -c Release
```

Output is `bin/Release/TheHammerOfOden.dll`. Copy it into `BepInEx/plugins/` to test, or:

```powershell
.\build.ps1 -Install     # build and copy into a local r2modman profile
```

`-Install` refuses to run while Valheim is open, since the DLL would be locked and you would be testing a stale build without knowing it.

## How it is put together

Everything hooks `Player`, once per frame, plus one hook on `ZNetView` for restoring scale. Nothing patches `Piece.GetSnapPoints`, deliberately — vanilla calls it on every piece within 10m every frame, and an earlier version that patched it put a Harmony detour in one of the game's hottest loops.

Patch classes are applied individually rather than with `PatchAll`, so one bad target disables one feature and names it instead of silently disabling the mod.

### The layers

| Area | Files |
|---|---|
| Rotation | `RotationState`, `RotationGizmo`, `AngleBeads`, `MarkerShapes` |
| Snapping | `DerivedSnapPoints`, `DerivedAnchorCache`, `TargetSnapping`, `SnapPointMarkers`, `SnapPointOrder`, `SnapPointNaming`, `ActiveSnapPair`, `SnapPointMemory` |
| Placement | `FreePlacement`, `SurfacePlacement`, `PlacementFreeze`, `PlacementGrid`, `PlacementRules`, `PlacementOffset`, `Clipping` |
| Scale | `ScaleState`, `Scalable`, `ScalePersistence`, `ScaledRanges` |
| Shared | `ModConfig`, `GhostBounds`, `MainCamera`, `GizmoMaterial`, `LineStyle`, `Notify` |

### Things that are easy to get wrong

Several decisions here look arbitrary and are not. Each is explained where it lives, but the ones most likely to be "simplified" back into bugs:

**Rotation must be substituted before vanilla's snap search, so it has to be a transpiler.** Vanilla locates the ghost's snap points in world space from its current rotation and then slides the ghost until a pair meets. Rotating afterwards in a postfix swings those points away from the alignment just solved for, which reads in game as snapping being broken. Position can safely be adjusted in a postfix, because nothing downstream derives anything from it.

**Anchors are tagged children of the ghost, not appended from a patch.** Tagging means vanilla's own `GetSnapPoints` collects them with no patch at all.

**Anchor kinds live in a component, not in the name.** Vanilla puts a snap point's name on screen when cycling, so anything encoded there is user-visible.

**Marker shapes differ by vertex count, never by rotation.** A square and a diamond are the same outline turned 45°, and that difference disappears at small scale.

**Particle scaling raises size only, never `scalingMode = Hierarchy`.** Hierarchy multiplies velocity and emission volume too, which throws a scaled piece's effects metres past it.

**A shape's extent is `radius × scale`, so only one of them may be scaled.** Scaling both squares the effect — 9× at 3×, 25× at 5×. Box shapes ignore `radius` entirely, which is why a shrunk hearth looked correct while every portal did not, and why the bug survived a round of testing.

**An effect that vanishes up close is probably not a particle problem.** A portal gates its effect on `TeleportWorld.m_activationRange`, a plain float that scaling does not touch, so on a large portal the trigger no longer has anything to do with where the portal is. `ScaledRanges` grows such ranges by how far the surface moved outward, not by the scale factor. Anything with a proximity trigger is a candidate for the same treatment.

`DebugParticles` exists because the three above cost several wrong guesses between them, and dumping the instance's configuration next to the prefab's is what finally separated them.

**Snap point choices are stored as local positions, never indices.** An index depends on child order and on how many anchors the current `DerivedSnapPoints` mode adds, so one recorded under `Centers` points somewhere else under `Full`. `SnapPointMemory` carries a position and matches within a centimetre.

**Aligning to a surface needs two vectors, not one.** `Quaternion.FromToRotation(Vector3.up, normal)` is the obvious way and gives the minimal arc, which leaves the twist about the normal unspecified — a piece on a wall then spins as the player strafes. `SurfacePlacement` and `RotationGizmo` both build the basis from a second, independent direction for this reason.

**Private signatures are checked against the assembly, not guessed.** `Player.PlacePiece` takes five arguments, not one, and a patch naming a signature that does not exist throws — which costs that feature silently, since a feature that never runs does not announce itself. `MetadataLoadContext` over `assembly_valheim.dll` will print the truth in a few lines.

**Four things decide the ghost's position, in a fixed order.** Surface alignment, then the grid, then freezing, then the nudge — set in `PlayerUpdatePlacementGhostRulesPatch` and `PlayerUpdatePlacementGhostGizmoPatch`. Each later one may override an earlier one, which is why the grid skips a frozen piece and why snapping is skipped for both. `PlacementSource` carries which of them won through to `PlacementRules`, because the answer changes what may be overridden: `NoRayHits` is bypassable only for a frozen piece, whose position was settled before you looked away.

**Freezing records a rotation *difference*, not a rotation.** Storing the absolute rotation would leave a pinned piece unturnable. Storing `ghost.rotation * Quaternion.Inverse(RotationState.Current)` gives identity in the ordinary case and the surface's frame when frozen against something, and re-applying it each frame keeps the alignment while the rotation keys stay connected.

**Bounds ignore particle renderers.** A charcoal kiln was otherwise measured against its smoke plume.

### Performance

Building is expensive in vanilla — two 10m sphere queries and an O(n·m) penetration test per frame while a ghost exists — so the aim is to add as little as possible on top:

- anchors for built pieces are cached; a placed piece does not move, so it is a constant
- the piece search takes a layer mask and sizes its radius from the piece's own anchor spread
- `Camera.main` is cached, since Unity implements it as a scene-wide tag search
- ghost bounds are measured once per piece, in local space
- `LineRenderer` colour and width are written only when they change, because those setters dirty the mesh whether or not the value differs

## License

MIT. Fork it, ship it, take it over if this repo goes quiet.
