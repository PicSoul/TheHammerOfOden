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
| Snapping | `DerivedSnapPoints`, `DerivedAnchorCache`, `TargetSnapping`, `SnapPointMarkers`, `SnapPointOrder`, `SnapPointNaming`, `ActiveSnapPair`, `SnapPointRecall` |
| Placement | `FreePlacement`, `PlacementRules`, `PlacementOffset`, `Clipping` |
| Scale | `ScaleState`, `Scalable`, `ScalePersistence` |
| Shared | `ModConfig`, `GhostBounds`, `MainCamera`, `GizmoMaterial`, `LineStyle`, `Notify` |

### Things that are easy to get wrong

Several decisions here look arbitrary and are not. Each is explained where it lives, but the ones most likely to be "simplified" back into bugs:

**Rotation must be substituted before vanilla's snap search, so it has to be a transpiler.** Vanilla locates the ghost's snap points in world space from its current rotation and then slides the ghost until a pair meets. Rotating afterwards in a postfix swings those points away from the alignment just solved for, which reads in game as snapping being broken. Position can safely be adjusted in a postfix, because nothing downstream derives anything from it.

**Anchors are tagged children of the ghost, not appended from a patch.** Tagging means vanilla's own `GetSnapPoints` collects them with no patch at all.

**Anchor kinds live in a component, not in the name.** Vanilla puts a snap point's name on screen when cycling, so anything encoded there is user-visible.

**Marker shapes differ by vertex count, never by rotation.** A square and a diamond are the same outline turned 45°, and that difference disappears at small scale.

**Particle scaling raises size only, never `scalingMode = Hierarchy`.** Hierarchy multiplies velocity and emission volume too, which throws a scaled piece's effects metres past it — they then appear to vanish when you stand near them. `DebugParticles` exists because that took two wrong guesses to find.

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
